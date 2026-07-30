using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using UnityBestPractices.Analyzers;
using Xunit;

public sealed class RiskyRuleSafetyTests
{
    private static readonly string[] ExpectedReviewRequiredIds =
    {
        "UBP0001", "UBP0003", "UBP0004", "UBP0005", "UBP0006", "UBP0007", "UBP0008",
        "UBP0010", "UBP0011", "UBP0027", "UBP0038", "UBP0039", "UBP0040", "UBP0041",
        "UBP0042", "UBP0057", "UBP0058", "UBP0059", "UBP0060", "UBP0061", "UBP0062",
        "UBP0063", "UBP0064", "UBP0065", "UBP0066", "UBP0067", "UBP0068", "UBP0069",
        "UBP0070", "UBP0075",
    };

    public static IEnumerable<object[]> ReviewRequiredIds =>
        ExpectedReviewRequiredIds.Select(id => new object[] { id });

    [Fact]
    public void CatalogKeepsTheCompleteRiskyRuleSetReviewRequired()
    {
        var actual = DiagnosticCatalog.All
            .Where(rule => rule.Safety == RuleSafety.ReviewRequired)
            .Select(rule => rule.DiagnosticId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedReviewRequiredIds, actual);
    }

    [Theory]
    [MemberData(nameof(ReviewRequiredIds))]
    public void RiskyRuleOffersOnlyAnIndividualReviewedFix(string diagnosticId)
    {
        var rule = DiagnosticCatalog.Get(diagnosticId);

        Assert.Equal(RuleSafety.ReviewRequired, rule.Safety);
        Assert.True(rule.HasCodeFix);
        Assert.False(rule.SupportsFixAll);
    }

    [Theory]
    [MemberData(nameof(ReviewRequiredIds))]
    public void FixAllProviderNeverAdvertisesRiskyRule(string diagnosticId)
    {
        var provider = new UnityBestPracticesCodeFixProvider();
        var supportedIds = provider.GetFixAllProvider()
            .GetSupportedFixAllDiagnosticIds(provider);

        Assert.DoesNotContain(diagnosticId, supportedIds);
    }

    [Theory]
    [InlineData(RuleSafety.ReviewRequired)]
    [InlineData(RuleSafety.Experimental)]
    public void MetadataRejectsFixAllForAnyUnsafeClassification(RuleSafety safety)
    {
        Assert.Throws<ArgumentException>(() => CreateMetadata(safety, hasCodeFix: true, supportsFixAll: true));
    }

    [Fact]
    public void MetadataRejectsFixAllWhenNoCodeFixExists()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateMetadata(RuleSafety.Safe, hasCodeFix: false, supportsFixAll: true));
    }

    [Fact]
    public void DefaultConfigurationKeepsReviewedRulesVisible()
    {
        var configuration = Configuration();

        Assert.True(configuration.IsRuleEnabled(DiagnosticIds.UseSquaredMagnitude));
        Assert.True(configuration.IsRuleEnabled(DiagnosticIds.EntitiesForEachToSystemApiQuery));
    }

    [Theory]
    [MemberData(nameof(ReviewRequiredIds))]
    public void ReviewRequiredKillSwitchDisablesEveryRiskyRule(string diagnosticId)
    {
        var configuration = Configuration(global: new Dictionary<string, string>
        {
            ["ubp_enable_review_required"] = "false",
        });

        Assert.False(configuration.IsRuleEnabled(diagnosticId));
    }

    [Fact]
    public void ReviewRequiredKillSwitchDoesNotDisableSafeRules()
    {
        var configuration = Configuration(global: new Dictionary<string, string>
        {
            ["ubp_enable_review_required"] = "false",
        });

        Assert.True(configuration.IsRuleEnabled(DiagnosticIds.YieldNull));
    }

    [Theory]
    [InlineData("not-a-boolean")]
    [InlineData("")]
    [InlineData("0")]
    public void InvalidRiskSwitchFailsOpenToTheDocumentedDefault(string value)
    {
        var configuration = Configuration(global: new Dictionary<string, string>
        {
            ["ubp_enable_review_required"] = value,
        });

        Assert.True(configuration.IsRuleEnabled(DiagnosticIds.UseSquaredMagnitude));
    }

    [Fact]
    public void PerFileRiskSwitchOverridesTheGlobalSetting()
    {
        var configuration = Configuration(
            global: new Dictionary<string, string> { ["ubp_enable_review_required"] = "true" },
            tree: new Dictionary<string, string> { ["ubp_enable_review_required"] = "false" });

        Assert.False(configuration.IsRuleEnabled(DiagnosticIds.UseSquaredMagnitude));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void DotsMigrationRequiresBothRiskAndMigrationSwitches(
        bool enableReviewRequired,
        bool enableDotsMigration,
        bool expected)
    {
        var configuration = Configuration(global: new Dictionary<string, string>
        {
            ["ubp_enable_review_required"] = enableReviewRequired.ToString(),
            ["ubp_enable_dots_migration"] = enableDotsMigration.ToString(),
        });

        Assert.Equal(expected, configuration.IsRuleEnabled(DiagnosticIds.EntitiesForEachToSystemApiQuery));
    }

    [Fact]
    public void UnknownRuleIsNeverEnabledEvenWhenRiskyRulesAreEnabled()
    {
        Assert.False(Configuration().IsRuleEnabled("UBP9999"));
    }

    private static RuleMetadata CreateMetadata(RuleSafety safety, bool hasCodeFix, bool supportsFixAll) =>
        new(
            "TEST0001",
            "Test title",
            "Test message",
            "Test description",
            "Test fix",
            RuleCategories.CSharpPerformance,
            DiagnosticSeverity.Info,
            safety,
            hasCodeFix,
            supportsFixAll,
            ImmutableArray<string>.Empty,
            "Unity 2021.3");

    private static AnalyzerConfiguration Configuration(
        IReadOnlyDictionary<string, string>? global = null,
        IReadOnlyDictionary<string, string>? tree = null)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText("class Test { }");
        var provider = new DictionaryOptionsProvider(global, tree);
        return AnalyzerConfiguration.For(provider, syntaxTree);
    }

    private sealed class DictionaryOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _treeOptions;

        internal DictionaryOptionsProvider(
            IReadOnlyDictionary<string, string>? global,
            IReadOnlyDictionary<string, string>? tree)
        {
            GlobalOptions = new DictionaryOptions(global);
            _treeOptions = new DictionaryOptions(tree);
        }

        public override AnalyzerConfigOptions GlobalOptions { get; }

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _treeOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
            new DictionaryOptions(values: null);
    }

    private sealed class DictionaryOptions : AnalyzerConfigOptions
    {
        private readonly IReadOnlyDictionary<string, string> _values;

        internal DictionaryOptions(IReadOnlyDictionary<string, string>? values)
        {
            _values = values ?? new Dictionary<string, string>();
        }

        public override bool TryGetValue(string key, out string value) =>
            _values.TryGetValue(key, out value!);
    }
}
