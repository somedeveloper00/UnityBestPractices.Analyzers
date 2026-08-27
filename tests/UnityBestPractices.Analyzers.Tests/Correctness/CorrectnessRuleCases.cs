using System.Collections.Immutable;
using UnityBestPractices.Analyzers;

internal static class CorrectnessRuleCases
{
    internal static ImmutableArray<RuleTestCase> Cases { get; } = ImmutableArray.Create(
        RuleTestCase.QuickFix(
            "Correctness", "public Unity field is serialized privately",
            "using UnityEngine; class Settings : MonoBehaviour { public float Speed = 1f; }",
            DiagnosticIds.EncapsulateSerializedField,
            "using UnityEngine; class Settings : MonoBehaviour { [UnityEngine.SerializeField] private float Speed = 1f; }"));
}
