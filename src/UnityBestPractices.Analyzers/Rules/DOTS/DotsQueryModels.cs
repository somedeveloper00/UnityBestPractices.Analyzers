// Semantic models shared by the DOTS rule family.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityBestPractices.Analyzers;

internal enum DotsParameterAccess
{
    ReadOnly,
    ReadWrite,
    Entity,
}

internal sealed class DotsQueryParameter
{
    internal DotsQueryParameter(
        string name,
        string typeName,
        DotsParameterAccess access,
        ISymbol symbol)
    {
        Name = name;
        TypeName = typeName;
        Access = access;
        Symbol = symbol;
    }

    internal string Name { get; }

    internal string TypeName { get; }

    internal DotsParameterAccess Access { get; }

    internal ISymbol Symbol { get; }

    internal string JobParameter => Access switch
    {
        DotsParameterAccess.ReadWrite => "ref " + TypeName + " " + Name,
        DotsParameterAccess.ReadOnly => "in " + TypeName + " " + Name,
        _ => TypeName + " " + Name,
    };

    internal string SystemApiType => Access switch
    {
        DotsParameterAccess.ReadWrite => "Unity.Entities.RefRW<" + TypeName + ">",
        DotsParameterAccess.ReadOnly => "Unity.Entities.RefRO<" + TypeName + ">",
        _ => string.Empty,
    };
}

internal sealed class DotsQueryFilter
{
    internal DotsQueryFilter(string name, ImmutableArray<string> typeNames, string? argument)
    {
        Name = name;
        TypeNames = typeNames;
        Argument = argument;
    }

    internal string Name { get; }

    internal ImmutableArray<string> TypeNames { get; }

    internal string? Argument { get; }

    internal string ToSystemApiSuffix()
    {
        if (Name == "WithEntityQueryOptions")
        {
            return ".WithOptions(" + Argument + ")";
        }

        return "." + Name + "<" + string.Join(", ", TypeNames) + ">()";
    }

    internal string ToJobAttribute()
    {
        if (Name == "WithEntityQueryOptions")
        {
            return "[Unity.Entities.WithOptions(" + Argument + ")]\n";
        }

        return
            "[Unity.Entities." + Name +
            "(" + string.Join(", ", TypeNames.Select(type => "typeof(" + type + ")")) + ")]\n";
    }
}

internal sealed class EntitiesForEachQuery
{
    private EntitiesForEachQuery(
        ExpressionStatementSyntax statement,
        TypeDeclarationSyntax containingType,
        AnonymousFunctionExpressionSyntax lambda,
        ImmutableArray<DotsQueryParameter> parameters,
        ImmutableArray<DotsQueryFilter> filters,
        string executionMode,
        string jobName)
    {
        Statement = statement;
        ContainingType = containingType;
        Lambda = lambda;
        Parameters = parameters;
        Filters = filters;
        ExecutionMode = executionMode;
        JobName = jobName;
    }

    internal ExpressionStatementSyntax Statement { get; }

    internal TypeDeclarationSyntax ContainingType { get; }

    internal AnonymousFunctionExpressionSyntax Lambda { get; }

    internal ImmutableArray<DotsQueryParameter> Parameters { get; }

    internal ImmutableArray<DotsQueryFilter> Filters { get; }

    internal string ExecutionMode { get; }

    internal string JobName { get; }

    internal static bool TryCreate(
        ExpressionStatementSyntax statement,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out EntitiesForEachQuery query)
    {
        query = null!;
        if (statement.Expression is not InvocationExpressionSyntax terminalInvocation ||
            terminalInvocation.ArgumentList.Arguments.Count != 0 ||
            terminalInvocation.Expression is not MemberAccessExpressionSyntax terminalAccess)
        {
            return false;
        }

        var executionMode = terminalAccess.Name.Identifier.ValueText;
        if (executionMode != "Run" && executionMode != "Schedule" && executionMode != "ScheduleParallel")
        {
            return false;
        }

        if (terminalAccess.Expression is not InvocationExpressionSyntax forEachInvocation ||
            forEachInvocation.ArgumentList.Arguments.Count != 1 ||
            forEachInvocation.Expression is not MemberAccessExpressionSyntax forEachAccess ||
            forEachAccess.Name.Identifier.ValueText != "ForEach" ||
            forEachInvocation.ArgumentList.Arguments[0].Expression is not AnonymousFunctionExpressionSyntax lambda ||
            lambda.AsyncKeyword != default)
        {
            return false;
        }

        var forEachMethod = semanticModel.GetSymbolInfo(forEachInvocation, cancellationToken).Symbol as IMethodSymbol;
        var terminalMethod = semanticModel.GetSymbolInfo(terminalInvocation, cancellationToken).Symbol as IMethodSymbol;
        if (!DotsQuerySemanticHelpers.IsUnityEntitiesMethod(forEachMethod, "ForEach") ||
            !DotsQuerySemanticHelpers.IsUnityEntitiesMethod(terminalMethod, executionMode))
        {
            return false;
        }

        if (!TryReadFilters(
                forEachAccess.Expression,
                semanticModel,
                cancellationToken,
                out var filters,
                out var entitiesExpression) ||
            !DotsQuerySemanticHelpers.IsEntitiesBuilderExpression(
                entitiesExpression,
                semanticModel,
                cancellationToken))
        {
            return false;
        }

        var syntaxParameters = lambda switch
        {
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters,
            SimpleLambdaExpressionSyntax simple => SyntaxFactory.SingletonSeparatedList(simple.Parameter),
            _ => default,
        };
        if (syntaxParameters.Count == 0 || syntaxParameters.Count > 8)
        {
            return false;
        }

        var parameters = ImmutableArray.CreateBuilder<DotsQueryParameter>(syntaxParameters.Count);
        var componentData = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "Unity.Entities.IComponentData");
        var entityType = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "Unity.Entities.Entity");
        if (componentData is null || entityType is null)
        {
            return false;
        }

        foreach (var parameter in syntaxParameters)
        {
            var symbol = semanticModel.GetDeclaredSymbol(parameter, cancellationToken) as IParameterSymbol;
            if (symbol is null || parameter.Type is null || symbol.RefKind == RefKind.Out)
            {
                return false;
            }

            DotsParameterAccess access;
            if (SymbolEqualityComparer.Default.Equals(symbol.Type, entityType))
            {
                if (symbol.RefKind != RefKind.None)
                {
                    return false;
                }

                access = DotsParameterAccess.Entity;
            }
            else if (symbol.Type is INamedTypeSymbol namedType &&
                     namedType.AllInterfaces.Any(@interface =>
                         SymbolEqualityComparer.Default.Equals(@interface, componentData)))
            {
                access = symbol.RefKind == RefKind.Ref
                    ? DotsParameterAccess.ReadWrite
                    : DotsParameterAccess.ReadOnly;
            }
            else
            {
                return false;
            }

            parameters.Add(new DotsQueryParameter(
                parameter.Identifier.ValueText,
                parameter.Type.WithoutTrivia().ToFullString(),
                access,
                symbol));
        }

        if (parameters.Count(item => item.Access == DotsParameterAccess.Entity) > 1 ||
            parameters.Count(item => item.Access != DotsParameterAccess.Entity) == 0 ||
            parameters.Count(item => item.Access != DotsParameterAccess.Entity) > 7 ||
            DotsQuerySemanticHelpers.HasUnsupportedCaptures(
                lambda.Body,
                parameters.Select(item => item.Symbol).ToImmutableArray(),
                semanticModel,
                cancellationToken))
        {
            return false;
        }

        var containingType = statement.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (containingType is null)
        {
            return false;
        }

        query = new EntitiesForEachQuery(
            statement,
            containingType,
            lambda,
            parameters.ToImmutable(),
            filters,
            executionMode,
            DotsQuerySemanticHelpers.CreateUniqueNestedTypeName(containingType, "EntitiesForEachJob"));
        return true;
    }

    internal bool TryCreateSystemApiLoop(
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out StatementSyntax statement)
    {
        statement = null!;
        if (UnitySymbolCache.GetTypeByMetadataName(
                semanticModel.Compilation,
                "Unity.Entities.SystemAPI") is null ||
            UnitySymbolCache.GetTypeByMetadataName(
                semanticModel.Compilation,
                "Unity.Entities.RefRO`1") is null ||
            UnitySymbolCache.GetTypeByMetadataName(
                semanticModel.Compilation,
                "Unity.Entities.RefRW`1") is null)
        {
            return false;
        }

        var componentParameters = Parameters
            .Where(parameter => parameter.Access != DotsParameterAccess.Entity)
            .ToImmutableArray();
        var entityParameter = Parameters.FirstOrDefault(parameter => parameter.Access == DotsParameterAccess.Entity);
        var queryText =
            "Unity.Entities.SystemAPI.Query<" +
            string.Join(", ", componentParameters.Select(parameter => parameter.SystemApiType)) +
            ">()" +
            string.Concat(Filters.Select(filter => filter.ToSystemApiSuffix())) +
            (entityParameter is null ? string.Empty : ".WithEntityAccess()");

        var rewrittenBody = CreateJobBody();
        var identifiers = rewrittenBody.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier => componentParameters.Any(parameter =>
                SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                    parameter.Symbol)))
            .ToImmutableArray();
        rewrittenBody = rewrittenBody.ReplaceNodes(identifiers, (identifier, _) =>
        {
            var parameter = componentParameters.First(item =>
                SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                    item.Symbol));
            var valueProperty = parameter.Access == DotsParameterAccess.ReadWrite ? "ValueRW" : "ValueRO";
            return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(identifier.Identifier.WithoutTrivia()),
                    SyntaxFactory.IdentifierName(valueProperty))
                .WithTriviaFrom(identifier);
        });

        var variableNames = componentParameters.Select(parameter => parameter.Name).ToList();
        if (entityParameter is not null)
        {
            variableNames.Add(entityParameter.Name);
        }

        var iterationVariable = variableNames.Count == 1
            ? "var " + variableNames[0]
            : "var (" + string.Join(", ", variableNames) + ")";
        statement = SyntaxFactory.ParseStatement(
            "foreach (" + iterationVariable + " in " + queryText + ")\n" +
            rewrittenBody.ToFullString());
        return !statement.ContainsDiagnostics;
    }

    internal BlockSyntax CreateJobBody() =>
        DotsQuerySemanticHelpers.CreateBlock(Lambda.Body);

    internal string CreateJobParameters() =>
        string.Join(", ", Parameters.Select(parameter => parameter.JobParameter));

    internal string CreateJobAttributes() =>
        string.Concat(Filters.Select(filter => filter.ToJobAttribute()));

    private static bool TryReadFilters(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ImmutableArray<DotsQueryFilter> filters,
        out ExpressionSyntax entitiesExpression)
    {
        var builder = ImmutableArray.CreateBuilder<DotsQueryFilter>();
        var current = expression;
        while (current is InvocationExpressionSyntax invocation &&
               invocation.Expression is MemberAccessExpressionSyntax access)
        {
            var methodName = access.Name.Identifier.ValueText;
            if (!DotsQuerySemanticHelpers.IsSupportedFilterName(methodName) ||
                !DotsQuerySemanticHelpers.IsUnityEntitiesMethod(
                    semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol,
                    methodName) ||
                !DotsQuerySemanticHelpers.TryCreateFilter(invocation, access, out var filter))
            {
                filters = default;
                entitiesExpression = null!;
                return false;
            }

            builder.Insert(0, filter);
            current = access.Expression;
        }

        filters = builder.ToImmutable();
        entitiesExpression = current;
        return true;
    }
}

internal sealed class JobEntityExecution
{
    private JobEntityExecution(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        string mode)
    {
        Invocation = invocation;
        MemberAccess = memberAccess;
        Mode = mode;
    }

    internal InvocationExpressionSyntax Invocation { get; }

    internal MemberAccessExpressionSyntax MemberAccess { get; }

    internal string Mode { get; }

    internal static bool TryCreate(
        ExpressionStatementSyntax statement,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out JobEntityExecution execution)
    {
        execution = null!;
        if (statement.Expression is not InvocationExpressionSyntax invocation ||
            invocation.ArgumentList.Arguments.Count != 0 ||
            invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return false;
        }

        var mode = access.Name.Identifier.ValueText;
        if (mode != "Run" && mode != "Schedule" && mode != "ScheduleParallel")
        {
            return false;
        }

        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        var receiverType = semanticModel.GetTypeInfo(access.Expression, cancellationToken).Type;
        var jobEntity = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "Unity.Entities.IJobEntity");
        if (!DotsQuerySemanticHelpers.IsUnityEntitiesMethod(method, mode) ||
            receiverType is not INamedTypeSymbol namedReceiver ||
            jobEntity is null ||
            !namedReceiver.AllInterfaces.Any(@interface =>
                SymbolEqualityComparer.Default.Equals(@interface, jobEntity)))
        {
            return false;
        }

        execution = new JobEntityExecution(invocation, access, mode);
        return true;
    }
}

internal sealed class SystemApiQueryLoop
{
    private SystemApiQueryLoop(
        CommonForEachStatementSyntax statement,
        TypeDeclarationSyntax containingType,
        ImmutableArray<DotsQueryParameter> parameters,
        ImmutableArray<DotsQueryFilter> filters,
        BlockSyntax body,
        string jobName)
    {
        Statement = statement;
        ContainingType = containingType;
        Parameters = parameters;
        Filters = filters;
        Body = body;
        JobName = jobName;
    }

    internal CommonForEachStatementSyntax Statement { get; }

    internal TypeDeclarationSyntax ContainingType { get; }

    internal ImmutableArray<DotsQueryParameter> Parameters { get; }

    internal ImmutableArray<DotsQueryFilter> Filters { get; }

    internal BlockSyntax Body { get; }

    internal string JobName { get; }

    internal static bool TryCreate(
        CommonForEachStatementSyntax statement,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out SystemApiQueryLoop query)
    {
        query = null!;
        if (!TryReadQuery(
                statement.Expression,
                semanticModel,
                cancellationToken,
                out var queryInvocation,
                out var filters,
                out var withEntityAccess) ||
            queryInvocation.Expression is not MemberAccessExpressionSyntax queryAccess ||
            queryAccess.Name is not GenericNameSyntax queryName)
        {
            return false;
        }

        var queryMethod = semanticModel.GetSymbolInfo(queryInvocation, cancellationToken).Symbol as IMethodSymbol;
        if (!DotsQuerySemanticHelpers.IsUnityEntitiesSystemApiMethod(queryMethod, "Query"))
        {
            return false;
        }

        var designations = statement switch
        {
            ForEachStatementSyntax simple => ImmutableArray.Create<SyntaxNode>(simple),
            ForEachVariableStatementSyntax deconstruction => deconstruction.Variable
                .DescendantNodesAndSelf()
                .OfType<SingleVariableDesignationSyntax>()
                .Cast<SyntaxNode>()
                .ToImmutableArray(),
            _ => ImmutableArray<SyntaxNode>.Empty,
        };
        if (queryName.TypeArgumentList.Arguments.Count == 0 ||
            designations.Length != queryName.TypeArgumentList.Arguments.Count + (withEntityAccess ? 1 : 0))
        {
            return false;
        }

        var parameters = ImmutableArray.CreateBuilder<DotsQueryParameter>();
        for (var index = 0; index < queryName.TypeArgumentList.Arguments.Count; index++)
        {
            var wrapperSyntax = queryName.TypeArgumentList.Arguments[index];
            var wrapperType = semanticModel.GetTypeInfo(wrapperSyntax, cancellationToken).Type as INamedTypeSymbol;
            if (wrapperType?.TypeArguments.Length != 1)
            {
                return false;
            }

            var definitionName = wrapperType.OriginalDefinition.ToDisplayString();
            var access = definitionName switch
            {
                "Unity.Entities.RefRW<T>" => DotsParameterAccess.ReadWrite,
                "Unity.Entities.RefRO<T>" => DotsParameterAccess.ReadOnly,
                _ => (DotsParameterAccess?)null,
            };
            if (access is null)
            {
                return false;
            }

            var symbol = GetIterationSymbol(designations[index], semanticModel, cancellationToken);
            var name = GetIterationName(designations[index]);
            if (symbol is null || string.IsNullOrEmpty(name))
            {
                return false;
            }

            parameters.Add(new DotsQueryParameter(
                name,
                wrapperType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                access.Value,
                symbol));
        }

        if (withEntityAccess)
        {
            var entityDesignation = designations[designations.Length - 1];
            var symbol = GetIterationSymbol(entityDesignation, semanticModel, cancellationToken);
            var name = GetIterationName(entityDesignation);
            if (symbol is null || string.IsNullOrEmpty(name))
            {
                return false;
            }

            parameters.Add(new DotsQueryParameter(name, "Unity.Entities.Entity", DotsParameterAccess.Entity, symbol));
        }

        var body = DotsQuerySemanticHelpers.CreateBlock(statement.Statement);
        if (!TryRewriteSystemApiBody(body, parameters.ToImmutable(), semanticModel, cancellationToken, out var rewrittenBody) ||
            DotsQuerySemanticHelpers.HasUnsupportedCaptures(
                statement.Statement,
                parameters.Select(item => item.Symbol).ToImmutableArray(),
                semanticModel,
                cancellationToken))
        {
            return false;
        }

        var containingType = statement.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (containingType is null)
        {
            return false;
        }

        query = new SystemApiQueryLoop(
            statement,
            containingType,
            parameters.ToImmutable(),
            filters,
            rewrittenBody,
            DotsQuerySemanticHelpers.CreateUniqueNestedTypeName(containingType, "SystemApiQueryJob"));
        return true;
    }

    internal BlockSyntax CreateJobBody() => Body;

    internal string CreateJobParameters() =>
        string.Join(", ", Parameters.Select(parameter => parameter.JobParameter));

    internal string CreateJobAttributes() =>
        string.Concat(Filters.Select(filter => filter.ToJobAttribute()));

    private static bool TryReadQuery(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out InvocationExpressionSyntax queryInvocation,
        out ImmutableArray<DotsQueryFilter> filters,
        out bool withEntityAccess)
    {
        queryInvocation = null!;
        var builder = ImmutableArray.CreateBuilder<DotsQueryFilter>();
        withEntityAccess = false;
        var current = expression;
        while (current is InvocationExpressionSyntax invocation &&
               invocation.Expression is MemberAccessExpressionSyntax access)
        {
            var methodName = access.Name.Identifier.ValueText;
            if (methodName == "Query")
            {
                queryInvocation = invocation;
                filters = builder.ToImmutable();
                return true;
            }

            if (methodName == "WithEntityAccess" && invocation.ArgumentList.Arguments.Count == 0)
            {
                withEntityAccess = true;
                current = access.Expression;
                continue;
            }

            var normalizedName = methodName == "WithOptions" ? "WithEntityQueryOptions" : methodName;
            if (!DotsQuerySemanticHelpers.IsSupportedFilterName(normalizedName) ||
                !DotsQuerySemanticHelpers.TryCreateFilter(invocation, access, out var filter, normalizedName))
            {
                filters = default;
                return false;
            }

            builder.Insert(0, filter);
            current = access.Expression;
        }

        filters = default;
        return false;
    }

    private static ISymbol? GetIterationSymbol(
        SyntaxNode designation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken) => designation switch
    {
        ForEachStatementSyntax simple => semanticModel.GetDeclaredSymbol(simple, cancellationToken),
        SingleVariableDesignationSyntax variable => semanticModel.GetDeclaredSymbol(variable, cancellationToken),
        _ => null,
    };

    private static string GetIterationName(SyntaxNode designation) => designation switch
    {
        ForEachStatementSyntax simple => simple.Identifier.ValueText,
        SingleVariableDesignationSyntax variable => variable.Identifier.ValueText,
        _ => string.Empty,
    };

    private static bool TryRewriteSystemApiBody(
        BlockSyntax body,
        ImmutableArray<DotsQueryParameter> parameters,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out BlockSyntax rewrittenBody)
    {
        var replacements = new Dictionary<MemberAccessExpressionSyntax, IdentifierNameSyntax>();
        foreach (var parameter in parameters.Where(item => item.Access != DotsParameterAccess.Entity))
        {
            var identifiers = body.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Where(identifier => SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                    parameter.Symbol))
                .ToImmutableArray();
            var expectedProperty = parameter.Access == DotsParameterAccess.ReadWrite ? "ValueRW" : "ValueRO";
            foreach (var identifier in identifiers)
            {
                if (identifier.Parent is not MemberAccessExpressionSyntax access ||
                    access.Expression != identifier ||
                    access.Name.Identifier.ValueText != expectedProperty)
                {
                    rewrittenBody = null!;
                    return false;
                }

                replacements[access] = SyntaxFactory.IdentifierName(identifier.Identifier).WithTriviaFrom(access);
            }
        }

        rewrittenBody = body.ReplaceNodes(
            replacements.Keys,
            (original, _) => replacements[original]);
        return true;
    }
}

internal static class DotsQuerySemanticHelpers
{
    private static readonly ImmutableHashSet<string> SupportedFilters = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "WithAll",
        "WithAny",
        "WithNone",
        "WithChangeFilter",
        "WithEntityQueryOptions");

    internal static bool IsSupportedFilterName(string name) => SupportedFilters.Contains(name);

    internal static bool IsUnityEntitiesMethod(IMethodSymbol? method, string name) =>
        method is not null &&
        method.Name == name &&
        IsUnityEntitiesNamespace(method.ContainingNamespace);

    internal static bool IsUnityEntitiesSystemApiMethod(IMethodSymbol? method, string name) =>
        IsUnityEntitiesMethod(method, name) && method!.ContainingType.Name == "SystemAPI";

    internal static bool IsEntitiesBuilderExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var symbol = semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol;
        var type = semanticModel.GetTypeInfo(expression, cancellationToken).Type;
        return symbol?.Name == "Entities" &&
               type is not null &&
               IsUnityEntitiesNamespace(type.ContainingNamespace);
    }

    internal static bool TryCreateFilter(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax access,
        out DotsQueryFilter filter,
        string? normalizedName = null)
    {
        filter = null!;
        var name = normalizedName ?? access.Name.Identifier.ValueText;
        if (name == "WithEntityQueryOptions")
        {
            if (invocation.ArgumentList.Arguments.Count != 1)
            {
                return false;
            }

            filter = new DotsQueryFilter(
                name,
                ImmutableArray<string>.Empty,
                invocation.ArgumentList.Arguments[0].Expression.WithoutTrivia().ToFullString());
            return true;
        }

        if (invocation.ArgumentList.Arguments.Count != 0 ||
            access.Name is not GenericNameSyntax genericName ||
            genericName.TypeArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        filter = new DotsQueryFilter(
            name,
            genericName.TypeArgumentList.Arguments
                .Select(type => type.WithoutTrivia().ToFullString())
                .ToImmutableArray(),
            argument: null);
        return true;
    }

    internal static bool HasUnsupportedCaptures(
        CSharpSyntaxNode body,
        ImmutableArray<ISymbol> allowedParameters,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (body.DescendantNodesAndSelf().Any(node =>
                node is AnonymousFunctionExpressionSyntax ||
                node is LocalFunctionStatementSyntax ||
                node is ThisExpressionSyntax ||
                node is BaseExpressionSyntax))
        {
            return true;
        }

        var bodySpan = body.Span;
        foreach (var identifier in body.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
            if (symbol is ILocalSymbol or IParameterSymbol)
            {
                if (allowedParameters.Any(parameter => SymbolEqualityComparer.Default.Equals(parameter, symbol)))
                {
                    continue;
                }

                if (!symbol.Locations.Any(location =>
                        location.IsInSource && bodySpan.Contains(location.SourceSpan)))
                {
                    return true;
                }
            }

            if (symbol is IFieldSymbol { IsStatic: false } or IPropertySymbol { IsStatic: false } or IMethodSymbol { IsStatic: false })
            {
                if (identifier.Parent is not MemberAccessExpressionSyntax memberAccess ||
                    memberAccess.Name != identifier)
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static BlockSyntax CreateBlock(CSharpSyntaxNode body) => body switch
    {
        BlockSyntax block => block,
        ExpressionSyntax expression => SyntaxFactory.Block(SyntaxFactory.ExpressionStatement(expression)),
        StatementSyntax statement => statement as BlockSyntax ?? SyntaxFactory.Block(statement),
        _ => SyntaxFactory.Block(),
    };

    internal static string CreateUniqueNestedTypeName(TypeDeclarationSyntax containingType, string baseName)
    {
        var existingNames = containingType.Members
            .SelectMany(member => member switch
            {
                BaseTypeDeclarationSyntax type => new[] { type.Identifier.ValueText },
                _ => Array.Empty<string>(),
            })
            .ToImmutableHashSet(StringComparer.Ordinal);
        var candidate = baseName;
        for (var suffix = 2; existingNames.Contains(candidate); suffix++)
        {
            candidate = baseName + suffix;
        }

        return candidate;
    }

    private static bool IsUnityEntitiesNamespace(INamespaceSymbol? namespaceSymbol)
    {
        var name = namespaceSymbol?.ToDisplayString();
        return name == "Unity.Entities" || name?.StartsWith("Unity.Entities.", StringComparison.Ordinal) == true;
    }
}
