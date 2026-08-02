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
    BufferReadOnly,
    BufferReadWrite,
    Entity,
    EntityIndexInQuery,
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
        DotsParameterAccess.ReadWrite or DotsParameterAccess.BufferReadWrite =>
            "ref " + TypeName + " " + Name,
        DotsParameterAccess.ReadOnly or DotsParameterAccess.BufferReadOnly =>
            "in " + TypeName + " " + Name,
        DotsParameterAccess.EntityIndexInQuery =>
            "[Unity.Entities.EntityIndexInQuery] int " + Name,
        _ => TypeName + " " + Name,
    };

    internal string SystemApiType => Access switch
    {
        DotsParameterAccess.ReadWrite => "Unity.Entities.RefRW<" + TypeName + ">",
        DotsParameterAccess.ReadOnly => "Unity.Entities.RefRO<" + TypeName + ">",
        DotsParameterAccess.BufferReadOnly or DotsParameterAccess.BufferReadWrite => TypeName,
        _ => string.Empty,
    };
}

internal sealed class DotsJobField
{
    internal DotsJobField(string name, string typeName, string initializer, ISymbol? sourceSymbol = null)
    {
        Name = name;
        TypeName = typeName;
        Initializer = initializer;
        SourceSymbol = sourceSymbol;
    }

    internal string Name { get; }

    internal string TypeName { get; }

    internal string Initializer { get; }

    internal ISymbol? SourceSymbol { get; }

    internal bool IsReadOnly { get; set; }
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
        bool hasStructuralChanges,
        bool hasWithoutBurst,
        bool hasUnsupportedCaptures,
        BlockSyntax jobBody,
        ImmutableArray<DotsJobField> jobFields,
        string executionMode,
        string jobName)
    {
        Statement = statement;
        ContainingType = containingType;
        Lambda = lambda;
        Parameters = parameters;
        Filters = filters;
        HasStructuralChanges = hasStructuralChanges;
        HasWithoutBurst = hasWithoutBurst;
        HasUnsupportedCaptures = hasUnsupportedCaptures;
        JobBody = jobBody;
        JobFields = jobFields;
        ExecutionMode = executionMode;
        JobName = jobName;
    }

    internal ExpressionStatementSyntax Statement { get; }

    internal TypeDeclarationSyntax ContainingType { get; }

    internal AnonymousFunctionExpressionSyntax Lambda { get; }

    internal ImmutableArray<DotsQueryParameter> Parameters { get; }

    internal ImmutableArray<DotsQueryFilter> Filters { get; }

    internal bool HasStructuralChanges { get; }

    internal bool HasWithoutBurst { get; }

    internal bool HasUnsupportedCaptures { get; }

    internal BlockSyntax JobBody { get; }

    internal ImmutableArray<DotsJobField> JobFields { get; }

    internal bool SupportsJobConversion =>
        !HasStructuralChanges && !HasWithoutBurst && !HasUnsupportedCaptures;

    internal string ExecutionMode { get; }

    internal string JobName { get; }

    internal bool InlineSystemApiReplacementBlock =>
        HasStructuralChanges &&
        Parameters.Any(parameter =>
            parameter.Access != DotsParameterAccess.Entity &&
            parameter.Access != DotsParameterAccess.EntityIndexInQuery);

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
                out var hasStructuralChanges,
                out var hasWithoutBurst,
                out var readOnlyCaptures,
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
        var dynamicBufferType = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "Unity.Entities.DynamicBuffer`1");
        var intType = semanticModel.Compilation.GetSpecialType(SpecialType.System_Int32);
        if (componentData is null || entityType is null)
        {
            return false;
        }

        foreach (var parameter in syntaxParameters)
        {
            var symbol = semanticModel.GetDeclaredSymbol(parameter, cancellationToken) as IParameterSymbol;
            if (symbol is null || parameter.Type is null)
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
            else if (SymbolEqualityComparer.Default.Equals(symbol.Type, intType) &&
                     symbol.RefKind == RefKind.None &&
                     parameter.Identifier.ValueText == "entityInQueryIndex")
            {
                access = DotsParameterAccess.EntityIndexInQuery;
            }
            else if (symbol.Type is INamedTypeSymbol namedType &&
                     dynamicBufferType is not null &&
                     SymbolEqualityComparer.Default.Equals(
                         namedType.OriginalDefinition,
                         dynamicBufferType))
            {
                if (symbol.RefKind != RefKind.Ref &&
                    symbol.RefKind != RefKind.In)
                {
                    return false;
                }

                access = symbol.RefKind == RefKind.Ref
                    ? DotsParameterAccess.BufferReadWrite
                    : DotsParameterAccess.BufferReadOnly;
            }
            else if (symbol.Type is INamedTypeSymbol componentNamedType &&
                     componentNamedType.AllInterfaces.Any(@interface =>
                         SymbolEqualityComparer.Default.Equals(@interface, componentData)))
            {
                // Neither SystemAPI.Query nor IJobEntity has a write-only component
                // parameter. RefRW/ref is the closest equivalent for an `out`
                // Entities.ForEach parameter; the original lambda's definite-
                // assignment rules ensure its body does not depend on the incoming
                // component value.
                access = symbol.RefKind is RefKind.Ref or RefKind.Out
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
            parameters.Count(item => item.Access == DotsParameterAccess.EntityIndexInQuery) > 1 ||
            parameters.Count(item =>
                item.Access != DotsParameterAccess.Entity &&
                item.Access != DotsParameterAccess.EntityIndexInQuery) > 7)
        {
            return false;
        }

        var containingType = statement.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (containingType is null)
        {
            return false;
        }

        var hasUnsupportedCaptures = !DotsQuerySemanticHelpers.TryCreateJobData(
            lambda.Body,
            parameters.Select(item => item.Symbol).ToImmutableArray(),
            semanticModel,
            cancellationToken,
            out var jobBody,
            out var jobFields);
        foreach (var field in jobFields)
        {
            field.IsReadOnly = field.SourceSymbol is not null && readOnlyCaptures.Contains(field.SourceSymbol);
        }
        query = new EntitiesForEachQuery(
            statement,
            containingType,
            lambda,
            parameters.ToImmutable(),
            filters,
            hasStructuralChanges,
            hasWithoutBurst,
            hasUnsupportedCaptures,
            jobBody,
            jobFields,
            executionMode,
            DotsQuerySemanticHelpers.CreateUniqueNestedTypeName(containingType, "EntitiesForEachJob"));
        return true;
    }

    internal bool TryCreateSystemApiLoop(
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out StatementSyntax statement,
        out ParallelEcbConversion? parallelEcbConversion)
    {
        statement = null!;
        parallelEcbConversion = null;
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
            .Where(parameter =>
                parameter.Access != DotsParameterAccess.Entity &&
                parameter.Access != DotsParameterAccess.EntityIndexInQuery)
            .ToImmutableArray();
        var entityParameter = Parameters.FirstOrDefault(parameter => parameter.Access == DotsParameterAccess.Entity);
        var entityIndexParameter = Parameters.FirstOrDefault(
            parameter => parameter.Access == DotsParameterAccess.EntityIndexInQuery);
        if (entityIndexParameter is not null)
        {
            TryCreateParallelEcbConversion(
                semanticModel,
                cancellationToken,
                entityIndexParameter,
                out parallelEcbConversion);
        }
        if (componentParameters.Any(
                parameter => parameter.Access == DotsParameterAccess.BufferReadOnly))
        {
            // SystemAPI.Query<DynamicBuffer<T>> always requests read-write access.
            // Converting an `in DynamicBuffer<T>` parameter would silently broaden
            // the query's dependency and write-access semantics.
            return false;
        }

        if (HasStructuralChanges)
        {
            if (entityIndexParameter is not null)
            {
                return false;
            }

            if (componentParameters.Length == 0)
            {
                return entityParameter is not null &&
                       TryCreateStructuralEntitySnapshot(semanticModel, out statement);
            }

            return TryCreateStructuralComponentLoop(
                semanticModel,
                cancellationToken,
                componentParameters,
                entityParameter,
                out statement);
        }

        var entityOnlyQueryType = componentParameters.Length == 0
            ? Filters
                .Where(filter => filter.Name == "WithAll")
                .SelectMany(filter => filter.TypeNames)
                .FirstOrDefault()
            : null;
        if (componentParameters.Length == 0 && string.IsNullOrEmpty(entityOnlyQueryType))
        {
            return false;
        }

        var queryText =
            "Unity.Entities.SystemAPI.Query<" +
            (entityOnlyQueryType is null
                ? string.Join(", ", componentParameters.Select(parameter => parameter.SystemApiType))
                : "Unity.Entities.RefRO<" + entityOnlyQueryType + ">") +
            ">()" +
            string.Concat(Filters.Select(filter => filter.ToSystemApiSuffix())) +
            (entityParameter is null ? string.Empty : ".WithEntityAccess()");

        // Query and rewrite the original in-tree lambda body; wrapping an expression
        // body through CreateBlock first would detach the nodes from the semantic
        // model's tree and make GetSymbolInfo throw.
        var identifiers = Lambda.Body.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier => componentParameters.Any(parameter =>
                parameter.Access != DotsParameterAccess.BufferReadOnly &&
                parameter.Access != DotsParameterAccess.BufferReadWrite &&
                SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                    parameter.Symbol)))
            .ToImmutableArray();
        var writableBufferElements = Lambda.Body.DescendantNodesAndSelf()
            .OfType<ElementAccessExpressionSyntax>()
            .Where(element =>
                IsWrittenBufferElement(element) &&
                componentParameters.Any(parameter =>
                    parameter.Access == DotsParameterAccess.BufferReadWrite &&
                    SymbolEqualityComparer.Default.Equals(
                        semanticModel.GetSymbolInfo(element.Expression, cancellationToken).Symbol,
                        parameter.Symbol)))
            .ToImmutableArray();
        var nodesToRewrite = identifiers
            .Cast<SyntaxNode>()
            .Concat(writableBufferElements)
            .ToImmutableArray();
        var rewrittenBody = DotsQuerySemanticHelpers.CreateBlock(
            Lambda.Body.ReplaceNodes(nodesToRewrite, (original, rewritten) =>
            {
                if (original is ElementAccessExpressionSyntax)
                {
                    var element = (ElementAccessExpressionSyntax)rewritten;
                    return SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                element.Expression.WithoutTrailingTrivia(),
                                SyntaxFactory.IdentifierName("ElementAt")),
                            SyntaxFactory.ArgumentList(element.ArgumentList.Arguments))
                        .WithTriviaFrom(element);
                }

                var identifier = (IdentifierNameSyntax)original;
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
            }));
        rewrittenBody = RewriteLambdaReturns(rewrittenBody);
        if (parallelEcbConversion is not null)
        {
            rewrittenBody = (BlockSyntax)new ParallelEcbBodyRewriter(
                parallelEcbConversion.OldName,
                parallelEcbConversion.NewName,
                entityIndexParameter!.Name).Visit(rewrittenBody)!;
        }

        var variableNames = componentParameters.Select(parameter => parameter.Name).ToList();
        if (componentParameters.Length == 0)
        {
            variableNames.Add("_");
        }

        if (entityParameter is not null)
        {
            variableNames.Add(entityParameter.Name);
        }

        var iterationVariable = variableNames.Count == 1
            ? "var " + variableNames[0]
            : "var (" + string.Join(", ", variableNames) + ")";
        var loopText =
            "foreach (" + iterationVariable + " in " + queryText + ")\n" +
            rewrittenBody.ToFullString();
        if (entityIndexParameter is null || parallelEcbConversion is not null)
        {
            statement = SyntaxFactory.ParseStatement(loopText);
            return !statement.ContainsDiagnostics;
        }

        var counterName = CreateUniqueLocalName(entityIndexParameter.Name + "Counter");
        rewrittenBody = rewrittenBody.WithStatements(
            rewrittenBody.Statements.Insert(
                0,
                SyntaxFactory.ParseStatement(
                    "var " + entityIndexParameter.Name + " = " + counterName + "++;")));
        statement = SyntaxFactory.ParseStatement(
            "{\n" +
            "var " + counterName + " = 0;\n" +
            "foreach (" + iterationVariable + " in " + queryText + ")\n" +
            rewrittenBody.ToFullString() +
            "\n}");
        return !statement.ContainsDiagnostics;
    }

    private bool TryCreateParallelEcbConversion(
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        DotsQueryParameter entityIndexParameter,
        out ParallelEcbConversion? conversion)
    {
        conversion = null;
        var indexReferences = Lambda.Body.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier => SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                entityIndexParameter.Symbol))
            .ToImmutableArray();
        if (indexReferences.IsEmpty)
        {
            return false;
        }

        ILocalSymbol? writerSymbol = null;
        foreach (var reference in indexReferences)
        {
            if (reference.Parent is not ArgumentSyntax argument ||
                argument.Parent is not ArgumentListSyntax arguments ||
                arguments.Arguments.FirstOrDefault() != argument ||
                arguments.Parent is not InvocationExpressionSyntax invocation ||
                invocation.Expression is not MemberAccessExpressionSyntax access ||
                access.Expression is not IdentifierNameSyntax receiver ||
                semanticModel.GetSymbolInfo(receiver, cancellationToken).Symbol is not ILocalSymbol local ||
                (writerSymbol is not null && !SymbolEqualityComparer.Default.Equals(writerSymbol, local)))
            {
                return false;
            }

            writerSymbol = local;
        }

        if (writerSymbol?.DeclaringSyntaxReferences.SingleOrDefault()?.GetSyntax(cancellationToken)
                is not VariableDeclaratorSyntax declarator ||
            declarator.Parent is not VariableDeclarationSyntax declaration ||
            declaration.Variables.Count != 1 ||
            declaration.Parent is not LocalDeclarationStatementSyntax declarationStatement ||
            declarator.Initializer?.Value is not InvocationExpressionSyntax asParallelWriter ||
            asParallelWriter.ArgumentList.Arguments.Count != 0 ||
            asParallelWriter.Expression is not MemberAccessExpressionSyntax parallelAccess ||
            parallelAccess.Name.Identifier.ValueText != "AsParallelWriter")
        {
            return false;
        }

        var localReferences = declarationStatement.Parent!.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier => SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                writerSymbol))
            .ToImmutableArray();
        if (localReferences.Any(reference => !Lambda.Body.Span.Contains(reference.Span)))
        {
            return false;
        }

        var oldName = writerSymbol.Name;
        var newName = oldName.EndsWith("ParallelWriter", StringComparison.Ordinal)
            ? oldName.Substring(0, oldName.Length - "ParallelWriter".Length)
            : oldName + "CommandBuffer";
        conversion = new ParallelEcbConversion(
            declarationStatement,
            oldName,
            CreateUniqueLocalName(newName),
            parallelAccess.Expression);
        return true;
    }

    private static bool IsWrittenBufferElement(ElementAccessExpressionSyntax element)
    {
        if (element.Parent is AssignmentExpressionSyntax assignment &&
            assignment.Left == element)
        {
            return true;
        }

        if (element.Parent is PrefixUnaryExpressionSyntax prefix &&
            prefix.Operand == element &&
            (prefix.IsKind(SyntaxKind.PreIncrementExpression) ||
             prefix.IsKind(SyntaxKind.PreDecrementExpression)))
        {
            return true;
        }

        if (element.Parent is PostfixUnaryExpressionSyntax postfix &&
            postfix.Operand == element &&
            (postfix.IsKind(SyntaxKind.PostIncrementExpression) ||
             postfix.IsKind(SyntaxKind.PostDecrementExpression)))
        {
            return true;
        }

        return element.Parent is ArgumentSyntax argument &&
               argument.Expression == element &&
               (argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) ||
                argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword));
    }

    private bool TryCreateStructuralComponentLoop(
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ImmutableArray<DotsQueryParameter> componentParameters,
        DotsQueryParameter? entityParameter,
        out StatementSyntax statement)
    {
        statement = null!;
        var compilation = semanticModel.Compilation;
        if (UnitySymbolCache.GetTypeByMetadataName(
                compilation,
                "Unity.Collections.Allocator") is null ||
            UnitySymbolCache.GetTypeByMetadataName(
                compilation,
                "Unity.Collections.NativeList`1") is null)
        {
            return false;
        }

        var replacements = new Dictionary<SyntaxNode, SyntaxNode>();

        foreach (var access in Lambda.Body.DescendantNodesAndSelf()
                     .OfType<MemberAccessExpressionSyntax>()
                     .Where(access => IsLegacyWorldTimeAccess(
                         access,
                         semanticModel,
                         cancellationToken)))
        {
            replacements.Add(
                access,
                SyntaxFactory.ParseExpression(
                        "Unity.Entities.SystemAPI.Time." + access.Name.Identifier.ValueText)
                    .WithTriviaFrom(access));
        }

        var rewrittenBodyNode = Lambda.Body.ReplaceNodes(
            replacements.Keys,
            (original, _) => replacements[original]);
        var rewrittenBody = DotsQuerySemanticHelpers.CreateBlock(rewrittenBodyNode);
        rewrittenBody = RewriteLambdaReturns(rewrittenBody);
        var entityName = entityParameter?.Name ?? CreateUniqueLocalName("entity");
        var aliasStatements = componentParameters
            .Select(parameter => SyntaxFactory.ParseStatement(parameter.Access switch
            {
                DotsParameterAccess.ReadWrite =>
                    "ref " + parameter.TypeName + " " + parameter.Name +
                    " = ref Unity.Entities.SystemAPI.GetComponentRW<" + parameter.TypeName +
                    ">(" + entityName + ").ValueRW;",
                DotsParameterAccess.ReadOnly =>
                    "ref readonly " + parameter.TypeName + " " + parameter.Name +
                    " = ref Unity.Entities.SystemAPI.GetComponentRO<" + parameter.TypeName +
                    ">(" + entityName + ").ValueRO;",
                _ => "var " + parameter.Name + " = Unity.Entities.SystemAPI.GetBuffer<" +
                     GetDynamicBufferElementType(parameter.TypeName) + ">(" + entityName + ");",
            }))
            .ToImmutableArray();
        rewrittenBody = rewrittenBody.WithStatements(
            rewrittenBody.Statements.InsertRange(0, aliasStatements));

        var query =
            "Unity.Entities.SystemAPI.Query<" +
            string.Join(", ", componentParameters.Select(parameter => parameter.SystemApiType)) +
            ">()" +
            string.Concat(Filters.Select(filter => filter.ToSystemApiSuffix())) +
            ".WithEntityAccess()";
        var snapshotName = CreateUniqueLocalName("entitiesSnapshot");
        var discardNames = string.Join(", ", componentParameters.Select(_ => "_"));
        statement = SyntaxFactory.ParseStatement(
            "{\n" +
            "using (var " + snapshotName +
            " = new Unity.Collections.NativeList<Unity.Entities.Entity>(Unity.Collections.Allocator.Temp))\n" +
            "{\n" +
            "foreach (var (" + discardNames + ", " + entityName + ") in " + query + ")\n" +
            "{ " + snapshotName + ".Add(" + entityName + "); }\n" +
            "foreach (var " + entityName + " in " + snapshotName + ")\n" +
            rewrittenBody.ToFullString() + "\n" +
            "}\n" +
            "}");
        return !statement.ContainsDiagnostics;
    }

    private static string GetDynamicBufferElementType(string typeName)
    {
        var start = typeName.IndexOf('<') + 1;
        return start == 0 ? typeName : typeName.Substring(start, typeName.Length - start - 1);
    }

    private static bool IsLegacyWorldTimeAccess(
        MemberAccessExpressionSyntax access,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var member = access.Name.Identifier.ValueText;
        if ((member != "ElapsedTime" && member != "DeltaTime") ||
            access.Expression is not MemberAccessExpressionSyntax timeAccess ||
            timeAccess.Name.Identifier.ValueText != "Time")
        {
            return false;
        }

        var worldType = semanticModel.GetTypeInfo(
            timeAccess.Expression,
            cancellationToken).Type;
        return worldType?.ToDisplayString() == "Unity.Entities.World";
    }

    private bool TryCreateStructuralEntitySnapshot(
        SemanticModel semanticModel,
        out StatementSyntax statement)
    {
        statement = null!;
        if (UnitySymbolCache.GetTypeByMetadataName(
                semanticModel.Compilation,
                "Unity.Collections.Allocator") is null ||
            UnitySymbolCache.GetTypeByMetadataName(
                semanticModel.Compilation,
                "Unity.Collections.NativeList`1") is null)
        {
            return false;
        }

        var entityParameter = Parameters.Single(parameter => parameter.Access == DotsParameterAccess.Entity);
        var queryComponent = Filters
            .Where(filter => filter.Name == "WithAll")
            .SelectMany(filter => filter.TypeNames)
            .FirstOrDefault();
        if (string.IsNullOrEmpty(queryComponent))
        {
            return false;
        }

        var query =
            "Unity.Entities.SystemAPI.Query<Unity.Entities.RefRO<" + queryComponent + ">>()" +
            string.Concat(Filters.Select(filter => filter.ToSystemApiSuffix())) +
            ".WithEntityAccess()";
        var snapshotName = CreateUniqueLocalName("entitiesSnapshot");
        var body = RewriteLambdaReturns(DotsQuerySemanticHelpers.CreateBlock(Lambda.Body));
        statement = SyntaxFactory.ParseStatement(
            "{\n" +
            "using (var " + snapshotName +
            " = new Unity.Collections.NativeList<Unity.Entities.Entity>(Unity.Collections.Allocator.Temp))\n" +
            "{\n" +
            "foreach (var (_, " + entityParameter.Name + ") in " + query + ")\n" +
            "{\n" +
            snapshotName + ".Add(" + entityParameter.Name + ");\n" +
            "}\n" +
            "foreach (var " + entityParameter.Name + " in " + snapshotName + ")\n" +
            body.WithoutTrivia().ToFullString() + "\n" +
            "}\n" +
            "}");
        return !statement.ContainsDiagnostics;
    }

    private BlockSyntax RewriteLambdaReturns(BlockSyntax body)
    {
        var returns = body.DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .Where(statement => statement.Expression is null &&
                !statement.Ancestors()
                    .TakeWhile(ancestor => ancestor != body)
                    .Any(ancestor => ancestor is AnonymousFunctionExpressionSyntax ||
                                     ancestor is LocalFunctionStatementSyntax))
            .ToImmutableArray();
        if (returns.IsEmpty)
        {
            return body;
        }

        var returnsInsideNestedLoops = returns
            .Where(statement => statement.Ancestors()
                .TakeWhile(ancestor => ancestor != body)
                .Any(ancestor => ancestor is ForStatementSyntax ||
                                 ancestor is ForEachStatementSyntax ||
                                 ancestor is ForEachVariableStatementSyntax ||
                                 ancestor is WhileStatementSyntax ||
                                 ancestor is DoStatementSyntax))
            .ToImmutableHashSet();
        var continueLabel = returnsInsideNestedLoops.IsEmpty
            ? null
            : CreateUniqueLocalName("systemApiQueryContinue");
        var rewritten = body.ReplaceNodes(returns, (original, _) =>
        {
            StatementSyntax replacement = returnsInsideNestedLoops.Contains(original)
                ? SyntaxFactory.ParseStatement("goto " + continueLabel + ";")
                : SyntaxFactory.ContinueStatement();
            return replacement.WithTriviaFrom(original);
        });

        if (continueLabel is null)
        {
            return rewritten;
        }

        return rewritten.AddStatements(
            SyntaxFactory.LabeledStatement(
                continueLabel,
                SyntaxFactory.EmptyStatement()));
    }

    private string CreateUniqueLocalName(string baseName)
    {
        var names = ContainingType.DescendantTokens()
            .Where(token => token.IsKind(SyntaxKind.IdentifierToken))
            .Select(token => token.ValueText)
            .ToImmutableHashSet(StringComparer.Ordinal);
        var candidate = baseName;
        for (var suffix = 2; names.Contains(candidate); suffix++)
        {
            candidate = baseName + suffix;
        }

        return candidate;
    }

    internal BlockSyntax CreateJobBody() => JobBody;

    internal string CreateJobParameters() =>
        string.Join(", ", Parameters.Select(parameter => parameter.JobParameter));

    internal string CreateJobAttributes() =>
        string.Concat(Filters.Select(filter => filter.ToJobAttribute()));

    private static bool TryReadFilters(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ImmutableArray<DotsQueryFilter> filters,
        out bool hasStructuralChanges,
        out bool hasWithoutBurst,
        out ImmutableHashSet<ISymbol> readOnlyCaptures,
        out ExpressionSyntax entitiesExpression)
    {
        var builder = ImmutableArray.CreateBuilder<DotsQueryFilter>();
        hasStructuralChanges = false;
        hasWithoutBurst = false;
        var readOnlyBuilder = ImmutableHashSet.CreateBuilder<ISymbol>(SymbolEqualityComparer.Default);
        var current = expression;
        while (current is InvocationExpressionSyntax invocation &&
               invocation.Expression is MemberAccessExpressionSyntax access)
        {
            var methodName = access.Name.Identifier.ValueText;
            if (methodName == "WithReadOnly" &&
                invocation.ArgumentList.Arguments.Count == 1 &&
                DotsQuerySemanticHelpers.IsUnityEntitiesMethod(
                    semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol,
                    methodName))
            {
                var symbol = semanticModel.GetSymbolInfo(
                    invocation.ArgumentList.Arguments[0].Expression,
                    cancellationToken).Symbol;
                if (symbol is not ILocalSymbol and not IParameterSymbol and not IFieldSymbol and not IPropertySymbol)
                {
                    filters = default;
                    readOnlyCaptures = ImmutableHashSet<ISymbol>.Empty;
                    entitiesExpression = null!;
                    return false;
                }

                readOnlyBuilder.Add(symbol);
                current = access.Expression;
                continue;
            }

            if ((methodName == "WithStructuralChanges" || methodName == "WithoutBurst") &&
                invocation.ArgumentList.Arguments.Count == 0 &&
                DotsQuerySemanticHelpers.IsUnityEntitiesMethod(
                    semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol,
                    methodName))
            {
                hasStructuralChanges |= methodName == "WithStructuralChanges";
                hasWithoutBurst |= methodName == "WithoutBurst";
                current = access.Expression;
                continue;
            }

            if (!DotsQuerySemanticHelpers.IsSupportedFilterName(methodName) ||
                !DotsQuerySemanticHelpers.IsUnityEntitiesMethod(
                    semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol,
                    methodName) ||
                !DotsQuerySemanticHelpers.TryCreateFilter(invocation, access, out var filter))
            {
                filters = default;
                hasStructuralChanges = false;
                hasWithoutBurst = false;
                readOnlyCaptures = ImmutableHashSet<ISymbol>.Empty;
                entitiesExpression = null!;
                return false;
            }

            builder.Insert(0, filter);
            current = access.Expression;
        }

        filters = builder.ToImmutable();
        readOnlyCaptures = readOnlyBuilder.ToImmutable();
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

        if (!TryRewriteSystemApiBody(
                statement.Statement,
                parameters.ToImmutable(),
                semanticModel,
                cancellationToken,
                out var rewrittenBody) ||
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
        StatementSyntax body,
        ImmutableArray<DotsQueryParameter> parameters,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out BlockSyntax rewrittenBody)
    {
        // Query and rewrite the original in-tree loop body; wrapping a brace-less
        // body through CreateBlock first would detach the nodes from the semantic
        // model's tree and make GetSymbolInfo throw.
        var replacements = new Dictionary<MemberAccessExpressionSyntax, IdentifierNameSyntax>();
        foreach (var parameter in parameters.Where(item => item.Access != DotsParameterAccess.Entity))
        {
            var identifiers = body.DescendantNodesAndSelf()
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

        rewrittenBody = DotsQuerySemanticHelpers.CreateBlock(
            body.ReplaceNodes(
                replacements.Keys,
                (original, _) => replacements[original]));
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

    internal static bool TryCreateJobData(
        CSharpSyntaxNode body,
        ImmutableArray<ISymbol> allowedParameters,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out BlockSyntax jobBody,
        out ImmutableArray<DotsJobField> jobFields)
    {
        jobBody = CreateBlock(body);
        jobFields = ImmutableArray<DotsJobField>.Empty;
        if (body.DescendantNodesAndSelf().Any(node =>
                node is AnonymousFunctionExpressionSyntax ||
                node is LocalFunctionStatementSyntax ||
                node is ThisExpressionSyntax ||
                node is BaseExpressionSyntax))
        {
            return false;
        }

        var systemTimeAccesses = body.DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(access => TryGetSystemTimeMember(
                access,
                semanticModel,
                cancellationToken,
                out _))
            .ToImmutableArray();
        var replacedTimeMemberTokens = systemTimeAccesses
            .Select(access => access.Name.Identifier)
            .ToImmutableHashSet();
        var usedNames = body.DescendantTokens()
            .Where(token => token.IsKind(SyntaxKind.IdentifierToken))
            .Where(token => !replacedTimeMemberTokens.Contains(token))
            .Select(token => token.ValueText)
            .Concat(allowedParameters.Select(parameter => parameter.Name))
            .ToImmutableHashSet(StringComparer.Ordinal);
        var fields = ImmutableArray.CreateBuilder<DotsJobField>();
        var capturedFieldNames = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
        var replacements = new Dictionary<SyntaxNode, string>();
        var bodySpan = body.Span;

        foreach (var identifier in body.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
            if (symbol is ILocalSymbol or IParameterSymbol)
            {
                if (allowedParameters.Any(parameter =>
                        SymbolEqualityComparer.Default.Equals(parameter, symbol)) ||
                    symbol.Locations.Any(location =>
                        location.IsInSource && bodySpan.Contains(location.SourceSpan)))
                {
                    continue;
                }

                var captureType = symbol switch
                {
                    ILocalSymbol local => local.Type,
                    IParameterSymbol parameter => parameter.Type,
                    _ => null,
                };
                if (captureType is null ||
                    !captureType.IsUnmanagedType ||
                    symbol is IParameterSymbol { RefKind: not RefKind.None } ||
                    IsWrittenByReference(identifier))
                {
                    return false;
                }

                if (!capturedFieldNames.TryGetValue(symbol, out var fieldName))
                {
                    fieldName = CreateUniqueJobFieldName(symbol.Name, usedNames);
                    usedNames = usedNames.Add(fieldName);
                    capturedFieldNames.Add(symbol, fieldName);
                    fields.Add(new DotsJobField(
                        fieldName,
                        captureType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        EscapeIdentifier(symbol.Name),
                        symbol));
                }

                replacements.Add(identifier, fieldName);
                continue;
            }

            if (symbol is IFieldSymbol { IsStatic: false } or
                IPropertySymbol { IsStatic: false })
            {
                var isMemberAccessName =
                    identifier.Parent is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name == identifier;
                var isObjectInitializerMember =
                    identifier.Parent is AssignmentExpressionSyntax assignment &&
                    assignment.Left == identifier &&
                    assignment.Parent is InitializerExpressionSyntax;
                if (!isMemberAccessName && !isObjectInitializerMember)
                {
                    var captureType = symbol switch
                    {
                        IFieldSymbol capturedField => capturedField.Type,
                        IPropertySymbol capturedProperty => capturedProperty.Type,
                        _ => throw new InvalidOperationException(),
                    };
                    if (!captureType.IsUnmanagedType || IsWrittenByReference(identifier))
                    {
                        return false;
                    }

                    if (!capturedFieldNames.TryGetValue(symbol, out var fieldName))
                    {
                        fieldName = CreateUniqueJobFieldName(symbol.Name, usedNames);
                        usedNames = usedNames.Add(fieldName);
                        capturedFieldNames.Add(symbol, fieldName);
                        fields.Add(new DotsJobField(
                            fieldName,
                            captureType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            "this." + EscapeIdentifier(symbol.Name),
                            symbol));
                    }

                    replacements.Add(identifier, fieldName);
                }

                continue;
            }

            if (symbol is IMethodSymbol { IsStatic: false })
            {
                var isMemberAccessName =
                    identifier.Parent is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name == identifier;
                if (!isMemberAccessName)
                {
                    return false;
                }
            }
        }

        foreach (var access in systemTimeAccesses)
        {
            TryGetSystemTimeMember(
                access,
                semanticModel,
                cancellationToken,
                out var timeMember);
            var existingField = fields.FirstOrDefault(field =>
                field.Initializer == "Unity.Entities.SystemAPI.Time." + timeMember);
            var fieldName = existingField?.Name;
            if (fieldName is null)
            {
                fieldName = CreateUniqueJobFieldName(timeMember, usedNames);
                usedNames = usedNames.Add(fieldName);
                fields.Add(new DotsJobField(
                    fieldName,
                    timeMember == "ElapsedTime" ? "double" : "float",
                    "Unity.Entities.SystemAPI.Time." + timeMember));
            }

            replacements.Add(access, fieldName);
        }

        var unsupportedSystemApiAccess = body.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier =>
            {
                var symbol = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
                if (symbol?.ContainingType?.ToDisplayString() != "Unity.Entities.SystemAPI")
                {
                    return false;
                }

                return !identifier.AncestorsAndSelf()
                    .OfType<MemberAccessExpressionSyntax>()
                    .Any(access => replacements.ContainsKey(access));
            });
        if (unsupportedSystemApiAccess)
        {
            return false;
        }

        jobBody = CreateBlock(
            body.ReplaceNodes(
                replacements.Keys,
                (original, _) => SyntaxFactory.IdentifierName(replacements[original])
                    .WithTriviaFrom(original)));
        jobFields = fields.ToImmutable();
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
                var isMemberAccessName =
                    identifier.Parent is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name == identifier;
                var isObjectInitializerMember =
                    identifier.Parent is AssignmentExpressionSyntax assignment &&
                    assignment.Left == identifier &&
                    assignment.Parent is InitializerExpressionSyntax;
                if (!isMemberAccessName && !isObjectInitializerMember)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetSystemTimeMember(
        MemberAccessExpressionSyntax access,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out string member)
    {
        member = access.Name.Identifier.ValueText;
        if ((member != "ElapsedTime" && member != "DeltaTime") ||
            access.Expression is not MemberAccessExpressionSyntax timeAccess ||
            timeAccess.Name.Identifier.ValueText != "Time")
        {
            return false;
        }

        var receiver = semanticModel.GetSymbolInfo(
            timeAccess.Expression,
            cancellationToken).Symbol as INamedTypeSymbol;
        return receiver?.ToDisplayString() == "Unity.Entities.SystemAPI";
    }

    private static bool IsWrittenByReference(IdentifierNameSyntax identifier)
    {
        for (SyntaxNode? current = identifier; current is ExpressionSyntax; current = current.Parent)
        {
            if (current.Parent is AssignmentExpressionSyntax assignment &&
                assignment.Left == current)
            {
                return true;
            }

            if (current.Parent is PrefixUnaryExpressionSyntax prefix &&
                (prefix.IsKind(SyntaxKind.PreIncrementExpression) ||
                 prefix.IsKind(SyntaxKind.PreDecrementExpression)))
            {
                return true;
            }

            if (current.Parent is PostfixUnaryExpressionSyntax postfix &&
                (postfix.IsKind(SyntaxKind.PostIncrementExpression) ||
                 postfix.IsKind(SyntaxKind.PostDecrementExpression)))
            {
                return true;
            }

            if (current.Parent is ArgumentSyntax argument &&
                !argument.RefKindKeyword.IsKind(SyntaxKind.None))
            {
                return true;
            }

            if (current.Parent is RefExpressionSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static string CreateUniqueJobFieldName(
        string sourceName,
        ImmutableHashSet<string> usedNames)
    {
        var baseName = sourceName.Length == 0
            ? "Value"
            : char.ToUpperInvariant(sourceName[0]) + sourceName.Substring(1);
        var candidate = baseName;
        for (var suffix = 2; usedNames.Contains(candidate); suffix++)
        {
            candidate = baseName + suffix;
        }

        return candidate;
    }

    private static string EscapeIdentifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;

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
