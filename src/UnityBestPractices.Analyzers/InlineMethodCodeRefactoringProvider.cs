using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Operations;

namespace UnityBestPractices.Analyzers;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(InlineMethodCodeRefactoringProvider)), Shared]
public sealed class InlineMethodCodeRefactoringProvider : CodeRefactoringProvider
{
    public const string Title = "Inline method";

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        if (!context.Span.IsEmpty)
        {
            return;
        }

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var invocation = root?.FindToken(context.Span.Start).Parent?
            .FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is null || semanticModel is null ||
            !TryCreateReplacement(invocation, semanticModel, context.CancellationToken, out var replacement))
        {
            return;
        }

        context.RegisterRefactoring(CodeAction.Create(
            Title,
            cancellationToken => InlineAsync(context.Document, invocation, replacement, cancellationToken),
            Title));
    }

    private static bool TryCreateReplacement(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExpressionSyntax replacement)
    {
        replacement = null!;
        if (semanticModel.GetOperation(invocation, cancellationToken) is not IInvocationOperation operation)
        {
            return false;
        }

        var method = operation.TargetMethod;
        if (!method.IsStatic || method.MethodKind != MethodKind.Ordinary || method.IsAsync ||
            method.IsGenericMethod || method.Parameters.Any(parameter => parameter.RefKind != RefKind.None || parameter.IsParams) ||
            method.DeclaringSyntaxReferences.Length != 1)
        {
            return false;
        }

        var declaration = method.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) as MethodDeclarationSyntax;
        var bodyExpression = declaration?.ExpressionBody?.Expression ??
            (declaration?.Body?.Statements.Count == 1 && declaration.Body.Statements[0] is ReturnStatementSyntax returnStatement
                ? returnStatement.Expression
                : null);
        if (bodyExpression is null)
        {
            return false;
        }

        var declarationModel = semanticModel.Compilation.GetSemanticModel(declaration!.SyntaxTree);
        var identifiers = bodyExpression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().ToImmutableArray();
        var parameterUses = new List<(IdentifierNameSyntax Syntax, IParameterSymbol Symbol)>();
        foreach (var identifier in identifiers)
        {
            var symbol = declarationModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
            if (symbol is IParameterSymbol parameter && SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, method))
            {
                parameterUses.Add((identifier, parameter));
            }
            else if (symbol is not null)
            {
                // Unqualified members could bind differently at the call site. Keep this
                // first version intentionally conservative rather than changing behavior.
                return false;
            }
        }

        if (method.Parameters.Any(parameter => parameterUses.Count(use =>
                SymbolEqualityComparer.Default.Equals(use.Symbol, parameter)) != 1))
        {
            return false;
        }

        var arguments = operation.Arguments
            .Where(argument => !argument.IsImplicit && argument.Parameter is not null)
            .ToImmutableArray();
        if (arguments.Length != method.Parameters.Length)
        {
            return false;
        }

        var argumentByParameter = arguments.ToDictionary(
            argument => argument.Parameter!,
            argument => ((ArgumentSyntax)argument.Syntax).Expression,
            SymbolEqualityComparer.Default);

        // Substitution must not reorder evaluation of argument expressions.
        var useOrdinals = parameterUses.Select(use => use.Symbol.Ordinal).ToImmutableArray();
        var argumentOrdinals = arguments.Select(argument => argument.Parameter!.Ordinal).ToImmutableArray();
        if (!useOrdinals.SequenceEqual(argumentOrdinals))
        {
            return false;
        }

        var substitutedExpression = bodyExpression.ReplaceNodes(
                parameterUses.Select(use => use.Syntax),
                (original, _) => SyntaxFactory.ParenthesizedExpression(argumentByParameter[
                    (IParameterSymbol)declarationModel.GetSymbolInfo(original, cancellationToken).Symbol!]
                    .WithoutTrivia()));
        replacement = SyntaxFactory.ParenthesizedExpression(substitutedExpression.WithoutTrivia())
            .WithTriviaFrom(invocation)
            .WithAdditionalAnnotations(Formatter.Annotation);
        return true;
    }

    private static async Task<Document> InlineAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax replacement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return root is null
            ? document
            : document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement));
    }
}
