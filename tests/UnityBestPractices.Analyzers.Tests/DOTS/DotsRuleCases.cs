using System.Collections.Immutable;
using UnityBestPractices.Analyzers;

internal static class DotsRuleCases
{
    internal static ImmutableArray<RuleTestCase> Cases { get; } = ImmutableArray.Create(
        RuleTestCase.QuickFix(
            "DOTS", "job receives BurstCompile",
            "using Unity.Jobs; struct MovementJob : IJob { public void Execute() { } }",
            DiagnosticIds.AddBurstCompile,
            "using Unity.Jobs; [Unity.Burst.BurstCompile] struct MovementJob : IJob { public void Execute() { } }"));
}
