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
using Microsoft.CodeAnalysis.Text;

namespace UnityBestPractices.Analyzers;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(InlineMethodCodeRefactoringProvider)), Shared]
public sealed class InlineMethodCodeRefactoringProvider : CodeRefactoringProvider
{
    public const string Title = "Inline method";

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var invocation = FindInvocation(root, context.Span);
        if (invocation is null || semanticModel is null ||
            !TryCreateReplacement(invocation, semanticModel, context.CancellationToken, out var nodeToReplace, out var replacement))
        {
            return;
        }

        context.RegisterRefactoring(CodeAction.Create(
            Title,
            cancellationToken => InlineAsync(context.Document, nodeToReplace, replacement, cancellationToken),
            Title));
    }

    private static InvocationExpressionSyntax? FindInvocation(SyntaxNode? root, TextSpan span)
    {
        if (root is null || root.FullSpan.IsEmpty || span.Start >= root.FullSpan.End)
        {
            return null;
        }

        var position = System.Math.Min(span.Start, root.FullSpan.End - 1);
        return root.FindToken(position).Parent?
            .AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(invocation =>
                span.IsEmpty
                    ? invocation.Expression.FullSpan.Contains(position) ||
                      position == invocation.Expression.Span.End
                    : invocation.Expression.FullSpan.IntersectsWith(span));
    }

    private static bool TryCreateReplacement(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out SyntaxNode nodeToReplace,
        out SyntaxNode replacement)
    {
        nodeToReplace = null!;
        replacement = null!;
        if (semanticModel.GetOperation(invocation, cancellationToken) is not IInvocationOperation operation)
        {
            return false;
        }

        var method = operation.TargetMethod;
        if (method.MethodKind != MethodKind.Ordinary || method.IsAsync ||
            method.ReturnsByRef || method.ReturnsByRefReadonly ||
            method.IsGenericMethod || method.Parameters.Any(parameter => parameter.RefKind != RefKind.None || parameter.IsParams) ||
            method.DeclaringSyntaxReferences.Length != 1)
        {
            return false;
        }

        var declaration = method.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) as MethodDeclarationSyntax;
        if (TryCreateVoidStatementReplacement(
                invocation,
                operation,
                method,
                declaration,
                semanticModel,
                cancellationToken,
                out nodeToReplace,
                out replacement))
        {
            return true;
        }

        if (!method.IsStatic)
        {
            return false;
        }

        var bodyExpression = declaration?.ExpressionBody?.Expression ??
            (declaration?.Body?.Statements.Count == 1 && declaration.Body.Statements[0] is ReturnStatementSyntax returnStatement
                ? returnStatement.Expression
                : null);
        if (bodyExpression is null)
        {
            return false;
        }

        var declarationModel = semanticModel.Compilation.GetSemanticModel(declaration!.SyntaxTree);
        var bodyType = declarationModel.GetTypeInfo(bodyExpression, cancellationToken).Type;
        if (!SymbolEqualityComparer.Default.Equals(bodyType, method.ReturnType))
        {
            // Preserve the implicit conversion performed at the return boundary.
            return false;
        }

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

        if (arguments.Any(argument => !argument.InConversion.IsIdentity))
        {
            // Inlining would otherwise remove the conversion performed when the
            // value crosses the method parameter boundary.
            return false;
        }

        var argumentByParameter = new Dictionary<IParameterSymbol, ExpressionSyntax>(
            SymbolEqualityComparer.Default);
        foreach (var argument in arguments)
        {
            var argumentSyntax = (ArgumentSyntax)argument.Syntax;
            var sourceType = semanticModel.GetTypeInfo(
                argumentSyntax.Expression,
                cancellationToken).Type;
            if (!SymbolEqualityComparer.Default.Equals(sourceType, argument.Parameter!.Type))
            {
                return false;
            }

            argumentByParameter.Add(
                argument.Parameter!,
                argumentSyntax.Expression);
        }

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
                    (IParameterSymbol)declarationModel.GetSymbolInfo(original, cancellationToken).Symbol!]));
        replacement = SyntaxFactory.ParenthesizedExpression(substitutedExpression.WithoutTrivia())
            .WithTriviaFrom(invocation)
            .WithAdditionalAnnotations(Formatter.Annotation);

        var argumentExpressions = argumentByParameter.Values.ToImmutableArray();
        var orphanedTrivia = invocation.DescendantTrivia(descendIntoTrivia: true)
            .Where(trivia =>
                invocation.Span.Contains(trivia.Span) &&
                !argumentExpressions.Any(expression => expression.FullSpan.Contains(trivia.Span)))
            .ToImmutableArray();
        if (orphanedTrivia.Any(IsComment))
        {
            replacement = replacement.WithLeadingTrivia(
                invocation.GetLeadingTrivia().AddRange(orphanedTrivia));
        }

        nodeToReplace = invocation;
        return true;
    }

    private static bool TryCreateVoidStatementReplacement(
        InvocationExpressionSyntax invocation,
        IInvocationOperation operation,
        IMethodSymbol method,
        MethodDeclarationSyntax? declaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out SyntaxNode nodeToReplace,
        out SyntaxNode replacement)
    {
        nodeToReplace = null!;
        replacement = null!;
        if (!method.ReturnsVoid || method.Parameters.Length != 0 ||
            invocation.Parent is not ExpressionStatementSyntax invocationStatement ||
            declaration?.Body?.Statements.Count != 1 ||
            declaration.Body.Statements[0] is not ExpressionStatementSyntax bodyStatement ||
            semanticModel.GetEnclosingSymbol(invocation.SpanStart, cancellationToken)?.ContainingType is not INamedTypeSymbol containingType ||
            !SymbolEqualityComparer.Default.Equals(containingType, method.ContainingType))
        {
            return false;
        }

        if (!method.IsStatic)
        {
            // An explicitly supplied receiver may be evaluated or may differ from
            // the current instance. Only inline an implicit call on this instance,
            // and only while still inside the method's declaring type.
            if (operation.Instance is not IInstanceReferenceOperation { IsImplicit: true })
            {
                return false;
            }
        }

        nodeToReplace = invocationStatement;
        replacement = bodyStatement
            .WithLeadingTrivia(default(SyntaxTriviaList))
            .WithTrailingTrivia(default(SyntaxTriviaList))
            .WithTriviaFrom(invocationStatement)
            .WithAdditionalAnnotations(Formatter.Annotation);
        return true;
    }

    private static bool IsComment(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
        trivia.IsKind(SyntaxKind.MultiLineCommentTrivia);

    private static async Task<Document> InlineAsync(
        Document document,
        SyntaxNode nodeToReplace,
        SyntaxNode replacement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return root is null
            ? document
            : document.WithSyntaxRoot(root.ReplaceNode(nodeToReplace, replacement));
    }
}
