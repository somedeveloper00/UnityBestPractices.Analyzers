using UnityBestPractices.Analyzers;
using UnityBestPractices.Analyzers.Infrastructure;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;

namespace UnityBestPractices.Analyzers.Rules.Dots;

internal static class UnusedEntityAccessRule
{
    internal static ImmutableArray<CodeFixHandler> Handlers => ImmutableArray.Create(
        new CodeFixHandler(
            DiagnosticCatalog.Get(DiagnosticIds.RemoveUnusedEntityAccess),
            RemoveAsync));

    private static readonly DiagnosticDescriptor Descriptor =
        DiagnosticCatalog.Get(DiagnosticIds.RemoveUnusedEntityAccess).Descriptor;

    internal static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var statement = (ForEachVariableStatementSyntax)context.Node;
        if (!TryGetParts(statement, context.SemanticModel, context.CancellationToken,
                out _, out var entityDesignation))
        {
            return;
        }

        var symbol = entityDesignation is SingleVariableDesignationSyntax variable
            ? context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken)
            : null;
        var isDiscard = entityDesignation is DiscardDesignationSyntax;
        var isUsed = symbol is not null && statement.Statement.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => SymbolEqualityComparer.Default.Equals(
                context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol,
                symbol));
        if (isDiscard || !isUsed)
        {
            context.ReportDiagnostic(Diagnostic.Create(Descriptor, entityDesignation.GetLocation()));
        }
    }

    internal static async Task<Document> RemoveAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var statement = root?.FindNode(diagnostic.Location.SourceSpan)
            .FirstAncestorOrSelf<ForEachVariableStatementSyntax>();
        if (root is null || semanticModel is null || statement is null ||
            !TryGetParts(statement, semanticModel, cancellationToken,
                out var entityAccess, out var entityDesignation) ||
            entityDesignation.Parent is not ParenthesizedVariableDesignationSyntax tuple)
        {
            return document;
        }

        var expression = statement.Expression.ReplaceNode(entityAccess, GetReceiver(entityAccess));
        StatementSyntax replacement;
        if (tuple.Variables.Count == 2)
        {
            if (tuple.Variables[0] is not SingleVariableDesignationSyntax remaining)
            {
                return document;
            }

            replacement = SyntaxFactory.ForEachStatement(
                    SyntaxFactory.IdentifierName("var"),
                    remaining.Identifier,
                    expression,
                    statement.Statement)
                .WithAwaitKeyword(statement.AwaitKeyword)
                .WithForEachKeyword(statement.ForEachKeyword)
                .WithOpenParenToken(statement.OpenParenToken)
                .WithInKeyword(statement.InKeyword)
                .WithCloseParenToken(statement.CloseParenToken);
        }
        else
        {
            replacement = statement
                .WithVariable(statement.Variable.ReplaceNode(tuple, tuple.WithVariables(tuple.Variables.RemoveAt(tuple.Variables.Count - 1))))
                .WithExpression(expression);
        }

        replacement = replacement.WithTriviaFrom(statement).WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(statement, replacement));
    }

    private static bool TryGetParts(
        ForEachVariableStatementSyntax statement,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out InvocationExpressionSyntax entityAccess,
        out VariableDesignationSyntax entityDesignation)
    {
        entityAccess = statement.Expression.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax access &&
                access.Name.Identifier.ValueText == "WithEntityAccess" &&
                invocation.ArgumentList.Arguments.Count == 0)!;
        entityDesignation = null!;
        if (entityAccess is null ||
            statement.Variable is not DeclarationExpressionSyntax declaration ||
            declaration.Designation is not ParenthesizedVariableDesignationSyntax tuple ||
            tuple.Variables.LastOrDefault() is not VariableDesignationSyntax last ||
            entityAccess.Expression is not MemberAccessExpressionSyntax entityAccessMember)
        {
            return false;
        }

        var queryInvocation = entityAccessMember.Expression.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax access &&
                access.Name is GenericNameSyntax name && name.Identifier.ValueText == "Query");
        var queryMethod = queryInvocation is null
            ? null
            : semanticModel.GetSymbolInfo(queryInvocation, cancellationToken).Symbol as IMethodSymbol;
        if (queryInvocation?.Expression is not MemberAccessExpressionSyntax queryAccess ||
            queryAccess.Name is not GenericNameSyntax queryName ||
            !DotsQuerySemanticHelpers.IsUnityEntitiesSystemApiMethod(queryMethod, "Query") ||
            tuple.Variables.Count != queryName.TypeArgumentList.Arguments.Count + 1)
        {
            return false;
        }

        entityDesignation = last;
        return true;
    }

    private static ExpressionSyntax GetReceiver(InvocationExpressionSyntax invocation) =>
        ((MemberAccessExpressionSyntax)invocation.Expression).Expression;
}
