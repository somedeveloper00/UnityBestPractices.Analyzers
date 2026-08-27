using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace UnityBestPractices.Analyzers.Testing;

public sealed record TestDocument(AdhocWorkspace Workspace, Document Document);
public sealed record CompilationValidation(ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.IsEmpty;
}
public sealed record AnalyzerRunResult(
    ImmutableArray<Diagnostic> CompilerDiagnostics,
    ImmutableArray<Diagnostic> AnalyzerDiagnostics)
{
    public bool InputCompiled => CompilerDiagnostics.IsEmpty;
}
public sealed record CodeActionResult(
    ImmutableArray<CodeAction> Actions,
    CodeAction? SelectedAction,
    Solution? ChangedSolution,
    ImmutableArray<Diagnostic> CompilerDiagnostics);
public sealed record SolutionComparison(bool AreEqual, string Expected, string Actual);

/// <summary>Assertion-framework-neutral Roslyn test infrastructure shared by all test hosts.</summary>
public static class AnalyzerTestHost
{
    public static ImmutableArray<MetadataReference> PlatformReferences { get; } = CreatePlatformReferences();

    public static TestDocument CreateDocument(
        string source,
        string? editorConfig = null,
        IEnumerable<(string Name, string Source)>? additionalSources = null,
        bool includeUnityStubs = true)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var directory = Path.Combine(Path.GetTempPath(), "UnityBestPracticesAnalyzerTests");
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "AnalyzerTest", "AnalyzerTest", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.CSharp9));

        foreach (var reference in PlatformReferences)
            solution = solution.AddMetadataReference(projectId, reference);
        if (includeUnityStubs)
            solution = solution.AddDocument(DocumentId.CreateNewId(projectId), "UnityStubs.cs", SourceText.From(FixtureSources.Unity));
        solution = solution.AddDocument(documentId, "Test.cs", SourceText.From(source), filePath: Path.Combine(directory, "Test.cs"));
        if (additionalSources != null)
            foreach (var item in additionalSources)
                solution = solution.AddDocument(DocumentId.CreateNewId(projectId), item.Name, SourceText.From(item.Source), filePath: Path.Combine(directory, item.Name));
        if (editorConfig != null)
            solution = solution.AddAnalyzerConfigDocument(DocumentId.CreateNewId(projectId), ".editorconfig", SourceText.From(editorConfig), filePath: Path.Combine(directory, ".editorconfig"));

        if (!workspace.TryApplyChanges(solution))
            throw new InvalidOperationException("The workspace rejected the test solution.");
        return new TestDocument(workspace, workspace.CurrentSolution.GetDocument(documentId)!);
    }

    public static async Task<CompilationValidation> ValidateCompilationAsync(Project project, CancellationToken cancellationToken = default)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Could not create a compilation.");
        return new CompilationValidation(compilation.GetDiagnostics(cancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToImmutableArray());
    }

    public static Task<CompilationValidation> ValidateUnityFixturesAsync(CancellationToken cancellationToken = default) =>
        ValidateCompilationAsync(CreateDocument(string.Empty).Document.Project, cancellationToken);

    public static async Task<AnalyzerRunResult> RunAnalyzerAsync(Document document, DiagnosticAnalyzer analyzer, CancellationToken cancellationToken = default)
    {
        var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Could not create a compilation.");
        var compilerDiagnostics = compilation.GetDiagnostics(cancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToImmutableArray();
        var analyzerDiagnostics = compilerDiagnostics.IsEmpty
            ? await compilation.WithAnalyzers(ImmutableArray.Create(analyzer), document.Project.AnalyzerOptions, cancellationToken)
                .GetAnalyzerDiagnosticsAsync(cancellationToken).ConfigureAwait(false)
            : ImmutableArray<Diagnostic>.Empty;
        return new AnalyzerRunResult(compilerDiagnostics, analyzerDiagnostics);
    }

    public static async Task<CodeActionResult> ApplyCodeFixAsync(Document document, Diagnostic diagnostic, CodeFixProvider provider, string? equivalenceKey = null, CancellationToken cancellationToken = default)
    {
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), cancellationToken)).ConfigureAwait(false);
        var selected = equivalenceKey == null ? actions.FirstOrDefault() : actions.FirstOrDefault(action => action.EquivalenceKey == equivalenceKey);
        if (selected == null)
            return new CodeActionResult(actions.ToImmutableArray(), null, null, ImmutableArray<Diagnostic>.Empty);
        var operation = (await selected.GetOperationsAsync(cancellationToken).ConfigureAwait(false)).OfType<ApplyChangesOperation>().SingleOrDefault();
        var validation = operation == null
            ? new CompilationValidation(ImmutableArray<Diagnostic>.Empty)
            : await ValidateCompilationAsync(operation.ChangedSolution.GetProject(document.Project.Id)!, cancellationToken).ConfigureAwait(false);
        return new CodeActionResult(actions.ToImmutableArray(), selected, operation?.ChangedSolution, validation.Diagnostics);
    }

    public static SolutionComparison CompareSolutions(string expected, string actual)
    {
        var expectedText = CSharpSyntaxTree.ParseText(expected).GetRoot().NormalizeWhitespace().ToFullString();
        var actualText = CSharpSyntaxTree.ParseText(actual).GetRoot().NormalizeWhitespace().ToFullString();
        return new SolutionComparison(string.Equals(expectedText, actualText, StringComparison.Ordinal), expectedText, actualText);
    }

    private static ImmutableArray<MetadataReference> CreatePlatformReferences()
    {
        var paths = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("The runtime did not provide platform assemblies.");
        return paths.Split(Path.PathSeparator).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToImmutableArray();
    }
}
