using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace UnityBestPractices.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnityBestPracticesCodeFixProvider)), Shared]
public sealed class UnityBestPracticesCodeFixProvider : CodeFixProvider
{
    // Derived from the central catalog so HasCodeFix and IDE registration cannot drift.
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        DiagnosticCatalog.All
            .Where(metadata => metadata.HasCodeFix)
            .Select(metadata => metadata.DiagnosticId)
            .ToImmutableArray();

    public override FixAllProvider GetFixAllProvider() => RuleAwareFixAllProvider.Instance;

    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        CodeFixDispatcher.RegisterAsync(context);

    internal static async Task<Document> ApplyExpressionQuickFixAsync(
        Document document,
        Diagnostic diagnostic,
        ExpressionQuickFixRule rule,
        System.Threading.CancellationToken cancellationToken)
    {
        var (root, semanticModel) = await CodeFixDocument.TryLoadAsync(document, cancellationToken)
            .ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var expression = CodeFixDocument.FindExpression(root, diagnostic);
        while (expression is not null)
        {
            if (expression.Kind() == rule.SyntaxKind &&
                rule.TryGetReplacement(
                    expression,
                    semanticModel,
                    cancellationToken,
                    out var replacement))
            {
                replacement = replacement
                    .WithTriviaFrom(expression)
                    .WithAdditionalAnnotations(Formatter.Annotation);
                return document.WithSyntaxRoot(root.ReplaceNode(expression, replacement));
            }

            expression = expression.Parent?.FirstAncestorOrSelf<ExpressionSyntax>();
        }

        return document;
    }
}
