using System;
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

namespace UnityBestPractices.Analyzers;

[ExportCodeRefactoringProvider(
    LanguageNames.CSharp,
    Name = nameof(ConvertStringLiteralToNameofCodeRefactoringProvider)),
 Shared]
public sealed class ConvertStringLiteralToNameofCodeRefactoringProvider : CodeRefactoringProvider
{
    public const string Title = "Replace string literal with nameof";

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var literal = FindStringLiteral(root, context.Span);
        if (literal is null ||
            semanticModel is null ||
            literal.Token.Value is not string symbolName ||
            symbolName.Length == 0 ||
            !CanReplaceWithNameof(
                literal,
                symbolName,
                root!,
                semanticModel,
                context.CancellationToken,
                out var replacement))
        {
            return;
        }

        context.RegisterRefactoring(
            CodeAction.Create(
                Title,
                cancellationToken => ReplaceAsync(
                    context.Document,
                    literal,
                    replacement,
                    cancellationToken),
                Title));
    }

    private static LiteralExpressionSyntax? FindStringLiteral(SyntaxNode? root, Microsoft.CodeAnalysis.Text.TextSpan span)
    {
        if (root is null || root.FullSpan.IsEmpty || span.Start >= root.FullSpan.End)
        {
            return null;
        }

        var literal = root.FindToken(span.Start)
            .Parent?
            .FirstAncestorOrSelf<LiteralExpressionSyntax>();
        return literal is not null &&
               literal.IsKind(SyntaxKind.StringLiteralExpression) &&
               literal.Span.Contains(span)
            ? literal
            : null;
    }

    private static bool CanReplaceWithNameof(
        LiteralExpressionSyntax literal,
        string symbolName,
        SyntaxNode root,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out InvocationExpressionSyntax replacement)
    {
        replacement = CreateNameofExpression(symbolName)
            .WithTriviaFrom(literal)
            .WithAdditionalAnnotations(Formatter.Annotation);

        if (!semanticModel.LookupSymbols(literal.SpanStart, name: symbolName)
                .Any(IsNameofSymbol))
        {
            return false;
        }

        // LookupSymbols establishes that the name is in scope. Bind the proposed
        // expression as well so a user-defined method called "nameof", or another
        // unusual binding context, cannot turn this constant-preserving edit into
        // a method call or a compiler error.
        var annotation = new SyntaxAnnotation();
        var annotatedReplacement = replacement.WithAdditionalAnnotations(annotation);
        var updatedRoot = root.ReplaceNode(literal, annotatedReplacement);
        var updatedTree = literal.SyntaxTree.WithRootAndOptions(
            updatedRoot,
            literal.SyntaxTree.Options);
        var updatedCompilation = semanticModel.Compilation.ReplaceSyntaxTree(
            literal.SyntaxTree,
            updatedTree);
        var updatedModel = updatedCompilation.GetSemanticModel(updatedTree);
        var reboundReplacement = updatedTree.GetRoot(cancellationToken)
            .GetAnnotatedNodes(annotation)
            .OfType<InvocationExpressionSyntax>()
            .Single();
        var constant = updatedModel.GetConstantValue(reboundReplacement, cancellationToken);
        return constant.HasValue &&
               constant.Value is string value &&
               string.Equals(value, symbolName, StringComparison.Ordinal);
    }

    private static bool IsNameofSymbol(ISymbol symbol) =>
        symbol.Kind == SymbolKind.Alias ||
        symbol.Kind == SymbolKind.Event ||
        symbol.Kind == SymbolKind.Field ||
        symbol.Kind == SymbolKind.Local ||
        symbol.Kind == SymbolKind.Method ||
        symbol.Kind == SymbolKind.NamedType ||
        symbol.Kind == SymbolKind.Namespace ||
        symbol.Kind == SymbolKind.Parameter ||
        symbol.Kind == SymbolKind.Property ||
        symbol.Kind == SymbolKind.RangeVariable ||
        symbol.Kind == SymbolKind.TypeParameter;

    private static InvocationExpressionSyntax CreateNameofExpression(string symbolName)
    {
        var escapedName = SyntaxFacts.GetKeywordKind(symbolName) == SyntaxKind.None
            ? symbolName
            : "@" + symbolName;
        return (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(
            "nameof(" + escapedName + ")");
    }

    private static async Task<Document> ReplaceAsync(
        Document document,
        LiteralExpressionSyntax literal,
        InvocationExpressionSyntax replacement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return root is null
            ? document
            : document.WithSyntaxRoot(root.ReplaceNode(literal, replacement));
    }
}
