using UnityBestPractices.Analyzers;
using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UnityBestPractices.Analyzers.Infrastructure;

internal readonly struct AnalyzerConfiguration
{
    internal const int DefaultMaxStackallocBytes = 1024;
    internal const int DefaultMinimumListAdds = 5;

    private const string MaxStackallocBytesKey = "ubp_max_stackalloc_bytes";
    private const string MinimumListAddsKey = "ubp_minimum_list_adds";
    private const string EnableDotsMigrationKey = "ubp_enable_dots_migration";
    private const string EnableReviewRequiredKey = "ubp_enable_review_required";

    private AnalyzerConfiguration(
        int maxStackallocBytes,
        int minimumListAdds,
        bool enableDotsMigration,
        bool enableReviewRequired)
    {
        MaxStackallocBytes = maxStackallocBytes;
        MinimumListAdds = minimumListAdds;
        EnableDotsMigration = enableDotsMigration;
        EnableReviewRequired = enableReviewRequired;
    }

    internal int MaxStackallocBytes { get; }

    internal int MinimumListAdds { get; }

    internal bool EnableDotsMigration { get; }

    internal bool EnableReviewRequired { get; }

    internal static AnalyzerConfiguration For(SyntaxNodeAnalysisContext context) =>
        For(context.Options.AnalyzerConfigOptionsProvider, context.Node.SyntaxTree);

    internal static AnalyzerConfiguration For(
        AnalyzerConfigOptionsProvider provider,
        SyntaxTree syntaxTree)
    {
        var treeOptions = provider.GetOptions(syntaxTree);
        var globalOptions = provider.GlobalOptions;
        return new AnalyzerConfiguration(
            GetInt(
                treeOptions,
                globalOptions,
                MaxStackallocBytesKey,
                DefaultMaxStackallocBytes,
                minimum: 16,
                maximum: 1024 * 1024),
            GetInt(
                treeOptions,
                globalOptions,
                MinimumListAddsKey,
                DefaultMinimumListAdds,
                minimum: 2,
                maximum: 1000),
            GetBool(treeOptions, globalOptions, EnableDotsMigrationKey, defaultValue: true),
            GetBool(treeOptions, globalOptions, EnableReviewRequiredKey, defaultValue: true));
    }

    internal bool IsRuleEnabled(string diagnosticId)
    {
        if (!DiagnosticCatalog.TryGet(diagnosticId, out var metadata))
        {
            return false;
        }

        if (metadata.Category == RuleCategories.UnityDotsMigration && !EnableDotsMigration)
        {
            return false;
        }

        return metadata.Safety != RuleSafety.ReviewRequired || EnableReviewRequired;
    }

    private static int GetInt(
        AnalyzerConfigOptions treeOptions,
        AnalyzerConfigOptions globalOptions,
        string key,
        int defaultValue,
        int minimum,
        int maximum)
    {
        var value = GetValue(treeOptions, globalOptions, key);
        return int.TryParse(value, out var parsed) && parsed >= minimum && parsed <= maximum
            ? parsed
            : defaultValue;
    }

    private static bool GetBool(
        AnalyzerConfigOptions treeOptions,
        AnalyzerConfigOptions globalOptions,
        string key,
        bool defaultValue)
    {
        var value = GetValue(treeOptions, globalOptions, key);
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static string? GetValue(
        AnalyzerConfigOptions treeOptions,
        AnalyzerConfigOptions globalOptions,
        string key)
    {
        if (treeOptions.TryGetValue(key, out var treeValue))
        {
            return treeValue;
        }

        return globalOptions.TryGetValue(key, out var globalValue)
            ? globalValue
            : null;
    }
}
