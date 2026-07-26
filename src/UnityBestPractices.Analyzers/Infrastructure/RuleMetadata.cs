using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace UnityBestPractices.Analyzers;

public enum RuleSafety
{
    Safe,
    ReviewRequired,
    Experimental,
}

public static class RuleCategories
{
    public const string UnityPerformanceSafe = "Unity.Performance.Safe";
    public const string UnityPerformanceReview = "Unity.Performance.Review";
    public const string UnityCorrectness = "Unity.Correctness";
    public const string UnityDotsMigration = "Unity.DOTS.Migration";
    public const string UnityApiDesign = "Unity.API.Design";
    public const string CSharpPerformance = "CSharp.Performance";
    public const string CSharpCodeStyle = "CSharp.CodeStyle";
}

public sealed class RuleMetadata
{
    internal RuleMetadata(
        string diagnosticId,
        string title,
        string messageFormat,
        string description,
        string fixTitle,
        string category,
        DiagnosticSeverity defaultSeverity,
        RuleSafety safety,
        bool hasCodeFix,
        bool supportsFixAll,
        ImmutableArray<string> requiredSymbols,
        string minimumUnityVersion)
    {
        if (supportsFixAll && (safety != RuleSafety.Safe || !hasCodeFix))
        {
            throw new ArgumentException(
                "Only rules classified as Safe may support Fix All.",
                nameof(supportsFixAll));
        }

        DiagnosticId = diagnosticId;
        Title = title;
        MessageFormat = messageFormat;
        Description = description;
        FixTitle = fixTitle;
        Category = category;
        DefaultSeverity = defaultSeverity;
        Safety = safety;
        HasCodeFix = hasCodeFix;
        SupportsFixAll = supportsFixAll;
        RequiredSymbols = requiredSymbols;
        MinimumUnityVersion = minimumUnityVersion;
        DocumentationUrl = RuleDocumentation.GetUrl(diagnosticId);
        Descriptor = new DiagnosticDescriptor(
            diagnosticId,
            title,
            messageFormat,
            category,
            defaultSeverity,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: DocumentationUrl);
    }

    public string DiagnosticId { get; }

    public string Title { get; }

    public string MessageFormat { get; }

    public string Description { get; }

    public string FixTitle { get; }

    public string Category { get; }

    public DiagnosticSeverity DefaultSeverity { get; }

    public RuleSafety Safety { get; }

    public bool SupportsFixAll { get; }

    public bool HasCodeFix { get; }

    public string DocumentationUrl { get; }

    public ImmutableArray<string> RequiredSymbols { get; }

    public string MinimumUnityVersion { get; }

    public bool IsExperimental => Safety == RuleSafety.Experimental;

    internal DiagnosticDescriptor Descriptor { get; }
}

internal static class RuleDocumentation
{
    private const string BaseUrl =
        "https://github.com/somedeveloper00/UnityBestPractices.Analyzers/blob/master/docs/rules/";

    internal static string GetUrl(string diagnosticId) => BaseUrl + diagnosticId + ".md";
}

internal static class RuleMetadataFactory
{
    internal static RuleMetadata Create(
        string diagnosticId,
        string title,
        string messageFormat,
        string description,
        string fixTitle,
        params string[] requiredSymbols)
    {
        var safety = GetSafety(diagnosticId);
        return new RuleMetadata(
            diagnosticId,
            title,
            messageFormat,
            description,
            fixTitle,
            GetCategory(diagnosticId, safety),
            DiagnosticSeverity.Info,
            safety,
            hasCodeFix: true,
            supportsFixAll: safety == RuleSafety.Safe,
            requiredSymbols.ToImmutableArray(),
            GetMinimumUnityVersion(diagnosticId));
    }

    private static RuleSafety GetSafety(string diagnosticId) => diagnosticId switch
    {
        DiagnosticIds.EncapsulateSerializedField or
        DiagnosticIds.UseSquaredMagnitude or
        DiagnosticIds.AddBurstCompile or
        DiagnosticIds.MarkNativeArrayReadOnly or
        DiagnosticIds.UseStackalloc or
        DiagnosticIds.UseRefLocal or
        DiagnosticIds.CacheCameraMain or
        DiagnosticIds.UseMultiplicationForSquare or
        DiagnosticIds.UseUninitializedNativeArray or
        DiagnosticIds.UseQuaternionIdentityForEulerZero or
        DiagnosticIds.UseMathfSqrt or
        DiagnosticIds.UseMathfFloorToInt or
        DiagnosticIds.UseMathfCeilToInt or
        DiagnosticIds.UseMathfRoundToInt or
        DiagnosticIds.UseArrayEmpty or
        DiagnosticIds.UseArrayEmptyForEnumerableEmpty or
        DiagnosticIds.EntitiesForEachToSystemApiQuery or
        DiagnosticIds.EntitiesForEachToJobEntityRun or
        DiagnosticIds.EntitiesForEachToJobEntitySchedule or
        DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel or
        DiagnosticIds.SystemApiQueryToJobEntityRun or
        DiagnosticIds.SystemApiQueryToJobEntitySchedule or
        DiagnosticIds.SystemApiQueryToJobEntityScheduleParallel or
        DiagnosticIds.JobEntityRunToSchedule or
        DiagnosticIds.JobEntityRunToScheduleParallel or
        DiagnosticIds.JobEntityScheduleToRun or
        DiagnosticIds.JobEntityScheduleToScheduleParallel or
        DiagnosticIds.JobEntityScheduleParallelToRun or
        DiagnosticIds.JobEntityScheduleParallelToSchedule or
        DiagnosticIds.MatchFolderNamespace => RuleSafety.ReviewRequired,
        _ => RuleSafety.Safe,
    };

    private static string GetCategory(string diagnosticId, RuleSafety safety)
    {
        if (string.CompareOrdinal(diagnosticId, DiagnosticIds.EntitiesForEachToSystemApiQuery) >= 0 &&
            string.CompareOrdinal(diagnosticId, DiagnosticIds.JobEntityScheduleParallelToSchedule) <= 0)
        {
            return RuleCategories.UnityDotsMigration;
        }

        if (diagnosticId == DiagnosticIds.EncapsulateSerializedField)
        {
            return RuleCategories.UnityApiDesign;
        }

        if (diagnosticId == DiagnosticIds.MatchFolderNamespace)
        {
            return RuleCategories.CSharpCodeStyle;
        }

        if (diagnosticId is DiagnosticIds.YieldNull or
            DiagnosticIds.UseSquaredMagnitude or
            DiagnosticIds.AddBurstCompile or
            DiagnosticIds.MarkNativeArrayReadOnly or
            DiagnosticIds.CacheCameraMain or
            DiagnosticIds.UseMultiplicationForSquare or
            DiagnosticIds.UseUninitializedNativeArray ||
            string.CompareOrdinal(diagnosticId, DiagnosticIds.UseVector2Zero) >= 0 &&
            string.CompareOrdinal(diagnosticId, DiagnosticIds.UseMathfRoundToInt) <= 0)
        {
            return safety == RuleSafety.Safe
                ? RuleCategories.UnityPerformanceSafe
                : RuleCategories.UnityPerformanceReview;
        }

        return RuleCategories.CSharpPerformance;
    }

    private static string GetMinimumUnityVersion(string diagnosticId) =>
        string.CompareOrdinal(diagnosticId, DiagnosticIds.EntitiesForEachToSystemApiQuery) >= 0 &&
        string.CompareOrdinal(diagnosticId, DiagnosticIds.JobEntityScheduleParallelToSchedule) <= 0
            ? "Unity 2022.3 with Entities 1.0"
            : "Unity 2021.3";
}
