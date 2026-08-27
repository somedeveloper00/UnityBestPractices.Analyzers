using UnityBestPractices.Analyzers;
using UnityBestPractices.Analyzers.Infrastructure;
// Entities.ForEach query model and migration helpers.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityBestPractices.Analyzers.Rules.Dots;

internal sealed class EntitiesForEachQuery
{
    private EntitiesForEachQuery(
        ExpressionStatementSyntax statement,
        TypeDeclarationSyntax containingType,
        AnonymousFunctionExpressionSyntax lambda,
        ImmutableArray<DotsQueryParameter> parameters,
        ImmutableArray<DotsQueryFilter> filters,
        ImmutableArray<DotsDisposalCapture> disposalCaptures,
        string? explicitDependency,
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
        DisposalCaptures = disposalCaptures;
        ExplicitDependency = explicitDependency;
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

    internal ImmutableArray<DotsDisposalCapture> DisposalCaptures { get; }

    internal string? ExplicitDependency { get; }

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
            terminalInvocation.ArgumentList.Arguments.Count > 1 ||
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
                out var disposalCaptureSymbols,
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
            parameters.Count(item => item.Access == DotsParameterAccess.EntityIndexInQuery) > 1)
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
            if (field.SourceSymbol is not null)
            {
                field.IsReadOnly = readOnlyCaptures.Contains(field.SourceSymbol);
            }
        }
        var disposalCaptures = ImmutableArray.CreateBuilder<DotsDisposalCapture>();
        foreach (var capture in disposalCaptureSymbols)
        {
            var symbol = capture.Symbol;
            foreach (var field in jobFields.Where(field =>
                         field.SourceSymbol is not null &&
                         SymbolEqualityComparer.Default.Equals(field.SourceSymbol, symbol)))
            {
                field.IsReadOnly = false;
            }

            var captureType = symbol switch
            {
                ILocalSymbol local => local.Type,
                IParameterSymbol parameter => parameter.Type,
                IFieldSymbol field => field.Type,
                IPropertySymbol property => property.Type,
                _ => null,
            };
            var matchingFields = jobFields.Where(field =>
                field.SourceSymbol is not null &&
                SymbolEqualityComparer.Default.Equals(field.SourceSymbol, symbol)).ToImmutableArray();
            if (captureType is null || !captureType.IsValueType || !captureType.IsUnmanagedType ||
                matchingFields.Length != 1)
            {
                hasUnsupportedCaptures = true;
            }

            disposalCaptures.Add(new DotsDisposalCapture(
                symbol,
                matchingFields.Length == 1 ? matchingFields[0] : null,
                capture.Expression));
        }
        query = new EntitiesForEachQuery(
            statement,
            containingType,
            lambda,
            parameters.ToImmutable(),
            filters,
            disposalCaptures.ToImmutable(),
            terminalInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression.WithoutTrivia().ToFullString(),
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
        if (!DisposalCaptures.IsEmpty || UnitySymbolCache.GetTypeByMetadataName(
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

            if (TryCreateStructuralCommandBufferLoop(
                    semanticModel,
                    cancellationToken,
                    componentParameters,
                    entityParameter,
                    out statement))
            {
                return true;
            }

            return TryCreateStructuralComponentLoop(
                semanticModel,
                cancellationToken,
                componentParameters,
                entityParameter,
                out statement);
        }

        // An `in` component which is never referenced by the lambda is a query
        // constraint, rather than data that the generated loop needs to retrieve.
        // Keep writable parameters: dropping a RefRW would change the dependency
        // and write-access semantics even when the value is not read by the body.
        var referencedParameterSymbols = Lambda.Body.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Select(identifier => semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol)
            .OfType<ISymbol>()
            .ToImmutableHashSet(SymbolEqualityComparer.Default);
        var constraintParameters = componentParameters
            .Where(parameter =>
                parameter.Access == DotsParameterAccess.ReadOnly &&
                !referencedParameterSymbols.Contains(parameter.Symbol))
            .ToImmutableArray();
        componentParameters = componentParameters
            .Except(constraintParameters)
            .ToImmutableArray();

        // SystemAPI.Query has overloads for at most seven query elements. Apply
        // that limit after constraint-only parameters have been removed.
        if (componentParameters.Length > 7)
        {
            return false;
        }

        var existingWithAllTypes = Filters
            .Where(filter => filter.Name == "WithAll")
            .SelectMany(filter => filter.TypeNames)
            .ToImmutableHashSet(StringComparer.Ordinal);
        var additionalWithAllTypes = constraintParameters
            .Select(parameter => parameter.TypeName)
            .Where(typeName => !existingWithAllTypes.Contains(typeName))
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();
        var queryFilters = Filters;
        if (!additionalWithAllTypes.IsEmpty)
        {
            queryFilters = queryFilters.Add(new DotsQueryFilter(
                "WithAll",
                additionalWithAllTypes,
                argument: null));
        }

        var entityOnlyQueryType = componentParameters.Length == 0
            ? queryFilters
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
            string.Concat(queryFilters.Select(filter => filter.ToSystemApiSuffix())) +
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

    private bool TryCreateStructuralCommandBufferLoop(
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
                "Unity.Entities.EntityCommandBuffer") is null ||
            UnitySymbolCache.GetTypeByMetadataName(
                compilation,
                "Unity.Collections.Allocator") is null)
        {
            return false;
        }

        var destroyCalls = Lambda.Body.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation =>
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax access ||
                    access.Name.Identifier.ValueText != "DestroyEntity")
                {
                    return false;
                }

                return semanticModel.GetTypeInfo(access.Expression, cancellationToken)
                    .Type?.ToDisplayString() == "Unity.Entities.EntityManager";
            })
            .ToImmutableArray();
        if (destroyCalls.IsEmpty)
        {
            return false;
        }

        var commandBufferName = CreateUniqueLocalName("ecb");
        var identifiers = Lambda.Body.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier => componentParameters.Any(parameter =>
                parameter.Access != DotsParameterAccess.BufferReadOnly &&
                parameter.Access != DotsParameterAccess.BufferReadWrite &&
                SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                    parameter.Symbol)))
            .Cast<SyntaxNode>();
        var nodesToRewrite = identifiers.Concat(destroyCalls).ToImmutableArray();
        var rewrittenBody = DotsQuerySemanticHelpers.CreateBlock(
            Lambda.Body.ReplaceNodes(nodesToRewrite, (original, rewritten) =>
            {
                if (original is InvocationExpressionSyntax)
                {
                    var invocation = (InvocationExpressionSyntax)rewritten;
                    var access = (MemberAccessExpressionSyntax)invocation.Expression;
                    return invocation.WithExpression(access.WithExpression(
                        SyntaxFactory.IdentifierName(commandBufferName)));
                }

                var identifier = (IdentifierNameSyntax)original;
                var parameter = componentParameters.First(item =>
                    SymbolEqualityComparer.Default.Equals(
                        semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                        item.Symbol));
                var valueProperty = parameter.Access == DotsParameterAccess.ReadWrite
                    ? "ValueRW"
                    : "ValueRO";
                return SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(identifier.Identifier.WithoutTrivia()),
                        SyntaxFactory.IdentifierName(valueProperty))
                    .WithTriviaFrom(identifier);
            }));
        rewrittenBody = RewriteLambdaReturns(rewrittenBody);

        var variableNames = componentParameters.Select(parameter => parameter.Name).ToList();
        if (entityParameter is not null)
        {
            variableNames.Add(entityParameter.Name);
        }

        var iterationVariable = variableNames.Count == 1
            ? "var " + variableNames[0]
            : "var (" + string.Join(", ", variableNames) + ")";
        var query =
            "Unity.Entities.SystemAPI.Query<" +
            string.Join(", ", componentParameters.Select(parameter => parameter.SystemApiType)) +
            ">()" +
            string.Concat(Filters.Select(filter => filter.ToSystemApiSuffix())) +
            (entityParameter is null ? string.Empty : ".WithEntityAccess()");
        statement = SyntaxFactory.ParseStatement(
            "{\n" +
            "using var " + commandBufferName +
            " = new Unity.Entities.EntityCommandBuffer(Unity.Collections.Allocator.Temp);\n" +
            "foreach (" + iterationVariable + " in " + query + ")\n" +
            rewrittenBody.ToFullString() + "\n" +
            commandBufferName + ".Playback(EntityManager);\n" +
            "}");
        return !statement.ContainsDiagnostics;
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
        out ImmutableArray<(ISymbol Symbol, string Expression)> disposalCaptures,
        out ExpressionSyntax entitiesExpression)
    {
        var builder = ImmutableArray.CreateBuilder<DotsQueryFilter>();
        hasStructuralChanges = false;
        hasWithoutBurst = false;
        var readOnlyBuilder = ImmutableHashSet.CreateBuilder<ISymbol>(SymbolEqualityComparer.Default);
        var disposalBuilder = ImmutableArray.CreateBuilder<(ISymbol Symbol, string Expression)>();
        var current = expression;
        while (current is InvocationExpressionSyntax invocation &&
               invocation.Expression is MemberAccessExpressionSyntax access)
        {
            var methodName = access.Name.Identifier.ValueText;
            if (methodName == "WithDisposeOnCompletion")
            {
                var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
                var symbol = invocation.ArgumentList.Arguments.Count == 1
                    ? semanticModel.GetSymbolInfo(
                        invocation.ArgumentList.Arguments[0].Expression,
                        cancellationToken).Symbol
                    : null;
                if (method is null ||
                    !DotsQuerySemanticHelpers.IsUnityEntitiesMethod(method, methodName) ||
                    symbol is not ILocalSymbol and not IParameterSymbol and not IFieldSymbol and not IPropertySymbol ||
                    disposalBuilder.Any(item => SymbolEqualityComparer.Default.Equals(item.Symbol, symbol)))
                {
                    filters = default;
                    readOnlyCaptures = ImmutableHashSet<ISymbol>.Empty;
                    disposalCaptures = default;
                    entitiesExpression = null!;
                    return false;
                }

                disposalBuilder.Insert(0, (
                    symbol,
                    invocation.ArgumentList.Arguments[0].Expression.WithoutTrivia().ToFullString()));
                current = access.Expression;
                continue;
            }

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
                    disposalCaptures = default;
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
                disposalCaptures = default;
                entitiesExpression = null!;
                return false;
            }

            builder.Insert(0, filter);
            current = access.Expression;
        }

        filters = builder.ToImmutable();
        readOnlyCaptures = readOnlyBuilder.ToImmutable();
        disposalCaptures = disposalBuilder.ToImmutable();
        entitiesExpression = current;
        return true;
    }
}

