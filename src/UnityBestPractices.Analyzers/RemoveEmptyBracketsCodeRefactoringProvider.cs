using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace UnityBestPractices.Analyzers;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(RemoveEmptyBracketsCodeRefactoringProvider)), Shared]
public sealed class RemoveEmptyBracketsCodeRefactoringProvider : CodeRefactoringProvider
{
    public const string Title = "Remove empty brackets";

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || !TryFindEmptyBracketSpan(root, text, context.Span, out var bracketSpan))
        {
            return;
        }

        context.RegisterRefactoring(CodeAction.Create(
            FixTitleLocalizer.Get(FixTitleLocalizer.RemoveEmptyBrackets, Title),
            cancellationToken => RemoveAsync(context.Document, bracketSpan, cancellationToken),
            Title));
    }

    private static bool TryFindEmptyBracketSpan(
        SyntaxNode root,
        SourceText text,
        TextSpan requestedSpan,
        out TextSpan bracketSpan)
    {
        foreach (var node in root.DescendantNodesAndSelf())
        {
            SyntaxToken opening = default;
            foreach (var child in node.ChildNodesAndTokens())
            {
                if (!child.IsToken)
                {
                    // Empty array rank specifiers contain an omitted-size node between
                    // their brackets. Roslyn represents it as a zero-width node, so it
                    // must not make an otherwise empty pair appear to contain content.
                    if (!child.Span.IsEmpty)
                    {
                        opening = default;
                    }

                    continue;
                }

                var token = child.AsToken();
                if (opening.RawKind != 0 &&
                    !opening.IsMissing &&
                    !token.IsMissing &&
                    IsMatchingPair(opening.Kind(), token.Kind()))
                {
                    var candidate = TextSpan.FromBounds(opening.SpanStart, token.Span.End);
                    if (Touches(candidate, requestedSpan) &&
                        ContainsOnlyWhitespace(text, opening.Span.End, token.SpanStart))
                    {
                        bracketSpan = candidate;
                        return true;
                    }
                }

                opening = token;
            }
        }

        bracketSpan = default;
        return false;
    }

    private static bool Touches(TextSpan candidate, TextSpan requested)
    {
        if (requested.IsEmpty)
        {
            return requested.Start >= candidate.Start && requested.Start <= candidate.End;
        }

        return candidate.IntersectsWith(requested);
    }

    private static bool ContainsOnlyWhitespace(SourceText text, int start, int end)
    {
        for (var position = start; position < end; position++)
        {
            if (!char.IsWhiteSpace(text[position]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsMatchingPair(SyntaxKind opening, SyntaxKind closing) =>
        (opening == SyntaxKind.OpenParenToken && closing == SyntaxKind.CloseParenToken) ||
        (opening == SyntaxKind.OpenBracketToken && closing == SyntaxKind.CloseBracketToken) ||
        (opening == SyntaxKind.OpenBraceToken && closing == SyntaxKind.CloseBraceToken);

    private static async Task<Document> RemoveAsync(
        Document document,
        TextSpan bracketSpan,
        CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return document.WithText(text.WithChanges(new TextChange(bracketSpan, string.Empty)));
    }
}
