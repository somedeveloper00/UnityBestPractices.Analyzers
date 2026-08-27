using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

internal sealed partial class AnalyzerTests
{
    private async Task RunDeclarativeCasesAsync()
    {
        var cases = CoreRuleCases.Cases
            .AddRange(CorrectnessRuleCases.Cases)
            .AddRange(ExpressionRuleCases.Cases)
            .AddRange(DotsRuleCases.Cases);

        foreach (var testCase in cases)
        {
            await RunCaseAsync(testCase);
        }
    }

    private async Task RunCaseAsync(RuleTestCase testCase)
    {
        Document? document = null;
        ImmutableArray<Diagnostic> diagnostics = ImmutableArray<Diagnostic>.Empty;
        string transformedSource = testCase.Source;
        try
        {
            document = CreateDocument(testCase.Source, testCase.Configuration);
            var compilation = await document.Project.GetCompilationAsync()
                ?? throw new InvalidOperationException("Could not create a compilation.");
            var compilerErrors = compilation.GetDiagnostics()
                .Where(item => item.Severity == DiagnosticSeverity.Error)
                .ToImmutableArray();
            if ((compilerErrors.Length == 0) != testCase.ExpectCompilerSuccess)
            {
                throw CreateCaseFailure(testCase, diagnostics, compilerErrors, transformedSource,
                    "Compiler status did not match the expectation.");
            }

            if (!testCase.ExpectCompilerSuccess)
            {
                return;
            }

            diagnostics = await GetDiagnosticsAsync(document);
            var actualIds = diagnostics.Select(item => item.Id).OrderBy(item => item).ToImmutableArray();
            var expectedIds = testCase.ExpectedDiagnosticIds.OrderBy(item => item).ToImmutableArray();
            if (!actualIds.SequenceEqual(expectedIds))
            {
                throw CreateCaseFailure(testCase, diagnostics, compilerErrors, transformedSource,
                    "Diagnostic IDs did not match the expectation.");
            }

            if (testCase.ExpectedOutput is not null)
            {
                await VerifyFixAsync(testCase.Source, expectedIds.Single(), testCase.ExpectedOutput);
                transformedSource = testCase.ExpectedOutput;
            }
        }
        catch (RegressionCaseException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var compilerErrors = document is null
                ? Array.Empty<Diagnostic>()
                : (await document.Project.GetCompilationAsync())?.GetDiagnostics()
                    .Where(item => item.Severity == DiagnosticSeverity.Error).ToArray() ?? Array.Empty<Diagnostic>();
            throw CreateCaseFailure(testCase, diagnostics, compilerErrors, transformedSource, exception.Message, exception);
        }
    }

    private static RegressionCaseException CreateCaseFailure(
        RuleTestCase testCase,
        IEnumerable<Diagnostic> diagnostics,
        IEnumerable<Diagnostic> compilerErrors,
        string transformedSource,
        string reason,
        Exception? inner = null) => new(
            $"Suite: {testCase.Suite}\nDiagnostic ID: {string.Join(", ", testCase.ExpectedDiagnosticIds.DefaultIfEmpty("<none>"))}\n" +
            $"Case: {testCase.Name}\nReason: {reason}\nUnexpected diagnostics:\n{FormatDiagnostics(diagnostics)}\n" +
            $"Compiler errors:\n{FormatDiagnostics(compilerErrors)}\nTransformed source:\n{transformedSource}", inner);
}

internal sealed class RegressionCaseException : Exception
{
    internal RegressionCaseException(string message, Exception? innerException) : base(message, innerException) { }
}
