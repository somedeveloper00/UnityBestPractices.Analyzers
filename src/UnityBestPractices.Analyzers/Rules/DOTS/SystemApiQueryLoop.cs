// SystemAPI.Query foreach loop model and job extraction helpers.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityBestPractices.Analyzers;

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

