using UnityBestPractices.Analyzers;
using System;

namespace UnityBestPractices.Analyzers.Infrastructure;

internal static class OmniSharpRefactoringTitle
{
    internal const string InlinePrefix = "Inline ";
    internal const string ExtractPrefix = "Extract ";

    internal static string Inline(string displayTitle, string englishTitle) =>
        AddPrefix(displayTitle, englishTitle, InlinePrefix);

    internal static string Extract(string displayTitle, string englishTitle) =>
        AddPrefix(displayTitle, englishTitle, ExtractPrefix);

    private static string AddPrefix(string displayTitle, string englishTitle, string prefix) =>
        displayTitle == englishTitle && !displayTitle.StartsWith(prefix, StringComparison.Ordinal)
            ? prefix + displayTitle
            : displayTitle;
}
