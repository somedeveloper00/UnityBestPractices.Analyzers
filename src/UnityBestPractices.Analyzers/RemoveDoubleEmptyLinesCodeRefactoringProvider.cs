using UnityBestPractices.Analyzers.Infrastructure;
using System.Collections.Generic;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace UnityBestPractices.Analyzers;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(RemoveDoubleEmptyLinesCodeRefactoringProvider)), Shared]
public sealed class RemoveDoubleEmptyLinesCodeRefactoringProvider : CodeRefactoringProvider
{
    public const string Title = "Remove double empty lines";

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        if (!HasDoubleEmptyLines(text))
        {
            return;
        }

        context.RegisterRefactoring(CodeAction.Create(
            FixTitleLocalizer.Get(FixTitleLocalizer.RemoveDoubleEmptyLines, Title),
            cancellationToken => RemoveAsync(context.Document, cancellationToken),
            Title));
    }

    private static bool HasDoubleEmptyLines(SourceText text)
    {
        var previousLineWasEmpty = false;
        foreach (var line in text.Lines)
        {
            var lineIsEmpty = IsEmpty(line, text);
            if (lineIsEmpty && previousLineWasEmpty && line.SpanIncludingLineBreak.Length > 0)
            {
                return true;
            }

            previousLineWasEmpty = lineIsEmpty;
        }

        return false;
    }

    private static async Task<Document> RemoveAsync(Document document, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var changes = new List<TextChange>();
        var previousLineWasEmpty = false;

        foreach (var line in text.Lines)
        {
            var lineIsEmpty = IsEmpty(line, text);
            if (lineIsEmpty && previousLineWasEmpty && line.SpanIncludingLineBreak.Length > 0)
            {
                changes.Add(new TextChange(line.SpanIncludingLineBreak, string.Empty));
            }

            previousLineWasEmpty = lineIsEmpty;
        }

        return document.WithText(text.WithChanges(changes));
    }

    private static bool IsEmpty(TextLine line, SourceText text)
    {
        for (var position = line.Start; position < line.End; position++)
        {
            if (text[position] != ' ' && text[position] != '\t')
            {
                return false;
            }
        }

        return true;
    }
}
