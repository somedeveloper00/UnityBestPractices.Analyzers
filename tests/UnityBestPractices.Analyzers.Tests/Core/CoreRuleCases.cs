using System.Collections.Immutable;
using UnityBestPractices.Analyzers;

internal static class CoreRuleCases
{
    internal static ImmutableArray<RuleTestCase> Cases { get; } = ImmutableArray.Create(
        RuleTestCase.QuickFix(
            "Core", "yield integer becomes null",
            "using System.Collections; using UnityEngine; class Waiter : MonoBehaviour { IEnumerator Wait() { yield return 0; } }",
            DiagnosticIds.YieldNull,
            "using System.Collections; using UnityEngine; class Waiter : MonoBehaviour { IEnumerator Wait() { yield return null; } }"),
        RuleTestCase.NoDiagnostic(
            "Core", "custom yield instruction is retained",
            "using System.Collections; using UnityEngine; class Waiter : MonoBehaviour { IEnumerator Wait() { yield return \"wait\"; } }"));
}
