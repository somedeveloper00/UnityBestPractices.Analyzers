using System;

namespace UnityBestPractices.Analyzers;

internal static class OmniSharpRefactoringTitle
{
    internal const string InlinePrefix = "Inline ";
    internal const string ExtractPrefix = "Extract ";

    internal static string Inline(string localizedTitle) => AddPrefix(localizedTitle, InlinePrefix);

    internal static string Extract(string localizedTitle) => AddPrefix(localizedTitle, ExtractPrefix);

    private static string AddPrefix(string localizedTitle, string prefix) =>
        localizedTitle.StartsWith(prefix, StringComparison.Ordinal)
            ? localizedTitle
            : prefix + localizedTitle;
}
