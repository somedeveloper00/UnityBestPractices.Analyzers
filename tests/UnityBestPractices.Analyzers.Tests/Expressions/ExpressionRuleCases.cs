using System.Collections.Immutable;
using UnityBestPractices.Analyzers;

internal static class ExpressionRuleCases
{
    internal static ImmutableArray<RuleTestCase> Cases { get; } = ImmutableArray.Create(
        RuleTestCase.QuickFix(
            "Expressions", "square uses multiplication",
            "using UnityEngine; class MathCode { float Square(float value) => Mathf.Pow(value, 2f); }",
            DiagnosticIds.UseMultiplicationForSquare,
            "using UnityEngine; class MathCode { float Square(float value) => (value * value); }"));
}
