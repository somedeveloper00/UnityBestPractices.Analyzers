using System.Collections.Immutable;

/// <summary>Immutable description of an analyzer regression, independent of its execution.</summary>
internal sealed record RuleTestCase(
    string Suite,
    string Name,
    string Source,
    ImmutableArray<string> ExpectedDiagnosticIds,
    string? ExpectedOutput = null,
    string? Configuration = null,
    bool ExpectCompilerSuccess = true)
{
    internal static RuleTestCase NoDiagnostic(string suite, string name, string source) =>
        new(suite, name, source, ImmutableArray<string>.Empty);

    internal static RuleTestCase QuickFix(
        string suite,
        string name,
        string source,
        string diagnosticId,
        string expectedOutput,
        string? configuration = null) =>
        new(suite, name, source, ImmutableArray.Create(diagnosticId), expectedOutput, configuration);
}
