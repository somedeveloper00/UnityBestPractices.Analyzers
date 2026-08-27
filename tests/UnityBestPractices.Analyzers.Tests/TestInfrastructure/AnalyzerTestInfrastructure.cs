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
using UnityBestPractices.Analyzers;

internal sealed partial class AnalyzerTests
{
    private async Task VerifyFixAllScopesAsync()
    {
        await VerifyFixAllScopeAsync(FixAllScope.Document, expectedFixedDocuments: 1);
        await VerifyFixAllScopeAsync(FixAllScope.Project, expectedFixedDocuments: 2);
        await VerifyFixAllScopeAsync(FixAllScope.Solution, expectedFixedDocuments: 3);
    }

    private async Task VerifyFixAllScopeAsync(FixAllScope scope, int expectedFixedDocuments)
    {
        var (solution, startingDocumentId) = CreateFixAllSolution();
        var startingDocument = solution.GetDocument(startingDocumentId)
            ?? throw new InvalidOperationException("Could not locate the Fix All starting document.");
        var startingDiagnostic = (await GetDiagnosticsAsync(startingDocument))
            .First(diagnostic => diagnostic.Id == DiagnosticIds.YieldNull);
        var registeredActions = new List<CodeAction>();
        await _codeFix.RegisterCodeFixesAsync(
            new CodeFixContext(
                startingDocument,
                startingDiagnostic,
                (action, _) => registeredActions.Add(action),
                CancellationToken.None));
        var equivalenceKey = registeredActions.Single().EquivalenceKey
            ?? throw new InvalidOperationException("The representative safe fix has no equivalence key.");
        var context = new FixAllContext(
            startingDocument,
            _codeFix,
            scope,
            equivalenceKey,
            new[] { DiagnosticIds.YieldNull },
            new AnalyzerDiagnosticProvider(_analyzer, DiagnosticIds.YieldNull),
            CancellationToken.None);
        var action = await _codeFix.GetFixAllProvider().GetFixAsync(context)
            ?? throw new InvalidOperationException($"No {scope} Fix All action was created.");
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;

        var fixedDocuments = 0;
        var unchangedDocuments = 0;
        foreach (var project in changedSolution.Projects)
        {
            foreach (var document in project.Documents.Where(document => document.Name != "UnityStubs.cs"))
            {
                var text = (await document.GetTextAsync()).ToString();
                if (text.Contains("yield return null;", StringComparison.Ordinal) &&
                    !text.Contains("yield return 0;", StringComparison.Ordinal))
                {
                    fixedDocuments++;
                }
                else if (text.Contains("yield return 0;", StringComparison.Ordinal))
                {
                    unchangedDocuments++;
                }
            }
        }

        if (fixedDocuments != expectedFixedDocuments || unchangedDocuments != 3 - expectedFixedDocuments)
        {
            throw new InvalidOperationException(
                $"{scope} Fix All fixed {fixedDocuments} documents and left {unchangedDocuments}; expected {expectedFixedDocuments} and {3 - expectedFixedDocuments}.");
        }
    }

    private static (Solution Solution, DocumentId StartingDocumentId) CreateFixAllSolution()
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        DocumentId? startingDocumentId = null;
        for (var projectIndex = 0; projectIndex < 2; projectIndex++)
        {
            var projectId = ProjectId.CreateNewId();
            solution = solution
                .AddProject(projectId, $"FixAllProject{projectIndex}", $"FixAllProject{projectIndex}", LanguageNames.CSharp)
                .WithProjectCompilationOptions(
                    projectId,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.CSharp9));
            foreach (var reference in PlatformReferenceFixture.Discover())
            {
                solution = solution.AddMetadataReference(projectId, reference);
            }

            solution = solution.AddDocument(
                DocumentId.CreateNewId(projectId),
                "UnityStubs.cs",
                SourceText.From(FixtureSources.Unity));
            var documentCount = projectIndex == 0 ? 2 : 1;
            for (var documentIndex = 0; documentIndex < documentCount; documentIndex++)
            {
                var documentId = DocumentId.CreateNewId(projectId);
                startingDocumentId ??= documentId;
                var typeSuffix = projectIndex * 2 + documentIndex;
                solution = solution.AddDocument(
                    documentId,
                    $"Coroutine{typeSuffix}.cs",
                    SourceText.From(
                        $$"""
                        using System.Collections;
                        using UnityEngine;
                        class Coroutine{{typeSuffix}} : MonoBehaviour
                        {
                            IEnumerator Run()
                            {
                                yield return 0;
                                yield return 0;
                            }
                        }
                        """));
            }
        }

        return (
            solution,
            startingDocumentId ?? throw new InvalidOperationException("No Fix All document was created."));
    }

    private sealed class AnalyzerDiagnosticProvider : FixAllContext.DiagnosticProvider
    {
        private readonly DiagnosticAnalyzer _analyzer;
        private readonly string _diagnosticId;

        internal AnalyzerDiagnosticProvider(DiagnosticAnalyzer analyzer, string diagnosticId)
        {
            _analyzer = analyzer;
            _diagnosticId = diagnosticId;
        }

        public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(
            Document document,
            CancellationToken cancellationToken)
        {
            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
            return (await GetAllDiagnosticsAsync(document.Project, cancellationToken))
                .Where(diagnostic => diagnostic.Location.SourceTree == syntaxTree);
        }

        public override async Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken) =>
            (await GetAllDiagnosticsAsync(project, cancellationToken))
                .Where(diagnostic => diagnostic.Location == Location.None);

        public override async Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken)
        {
            var compilation = await project.GetCompilationAsync(cancellationToken)
                ?? throw new InvalidOperationException("Could not compile the Fix All project.");
            return (await compilation
                    .WithAnalyzers(
                        ImmutableArray.Create(_analyzer),
                        project.AnalyzerOptions,
                        cancellationToken)
                    .GetAnalyzerDiagnosticsAsync(cancellationToken))
                .Where(diagnostic => diagnostic.Id == _diagnosticId);
        }
    }

    private async Task VerifyDiagnosticPresenceAsync(
        string source,
        string diagnosticId,
        bool expected,
        string? editorConfig = null)
    {
        var diagnostics = await GetDiagnosticsAsync(CreateDocument(source, editorConfig));
        var present = diagnostics.Any(diagnostic => diagnostic.Id == diagnosticId);
        if (present != expected)
        {
            throw new InvalidOperationException(
                $"Expected {diagnosticId} presence to be {expected}, got: {FormatDiagnostics(diagnostics)}");
        }
    }

    private async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(Document document)
    {
        var compilation = await document.Project.GetCompilationAsync()
            ?? throw new InvalidOperationException("Could not create a compilation.");
        var compilerErrors = compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        if (compilerErrors.Length != 0)
        {
            throw new InvalidOperationException($"Test input did not compile: {FormatDiagnostics(compilerErrors)}");
        }

        var diagnostics = await compilation
            .WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(_analyzer),
                document.Project.AnalyzerOptions)
            .GetAnalyzerDiagnosticsAsync();
        foreach (var diagnostic in diagnostics)
        {
            AssertSuggestion(diagnostic);
        }

        return diagnostics;
    }

    private static Document CreateDocument(
        string source,
        string? editorConfig = null,
        string? additionalSource = null,
        string? secondAdditionalSource = null)
    {
        var workspace = new AdhocWorkspace();
        var virtualProjectDirectory = Path.Combine(Path.GetTempPath(), "UnityBestPracticesAnalyzerTests");
        var projectId = ProjectId.CreateNewId();
        var stubsDocumentId = DocumentId.CreateNewId(projectId);
        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "AnalyzerTest", "AnalyzerTest", LanguageNames.CSharp)
            .WithProjectCompilationOptions(
                projectId,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.CSharp9));

        foreach (var reference in PlatformReferenceFixture.Discover())
        {
            solution = solution.AddMetadataReference(projectId, reference);
        }

        solution = solution.AddDocument(
            stubsDocumentId,
            "UnityStubs.cs",
            SourceText.From(FixtureSources.Unity));
        solution = solution.AddDocument(
            documentId,
            "Test.cs",
            SourceText.From(source),
            filePath: Path.Combine(virtualProjectDirectory, "Test.cs"));
        if (additionalSource != null)
        {
            solution = solution.AddDocument(
                DocumentId.CreateNewId(projectId),
                "Additional.cs",
                SourceText.From(additionalSource),
                filePath: Path.Combine(virtualProjectDirectory, "Additional.cs"));
        }

        if (secondAdditionalSource != null)
        {
            solution = solution.AddDocument(
                DocumentId.CreateNewId(projectId),
                "SecondAdditional.cs",
                SourceText.From(secondAdditionalSource),
                filePath: Path.Combine(virtualProjectDirectory, "SecondAdditional.cs"));
        }

        if (editorConfig != null)
        {
            var configDocumentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddAnalyzerConfigDocument(
                configDocumentId,
                ".editorconfig",
                SourceText.From(editorConfig),
                filePath: Path.Combine(virtualProjectDirectory, ".editorconfig"));
        }

        return solution.GetDocument(documentId)
            ?? throw new InvalidOperationException("Could not create the test document.");
    }

    private static void AssertSuggestion(Diagnostic diagnostic)
    {
        if (diagnostic.Severity != DiagnosticSeverity.Info)
        {
            throw new InvalidOperationException(
                $"{diagnostic.Id} must be an Info suggestion, but was {diagnostic.Severity}.");
        }
    }

    private static string FormatDiagnostics(IEnumerable<Diagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(item => item.ToString()));
    private async Task VerifyFixAsync(
        string source,
        string diagnosticId,
        string expected,
        string? additionalSource = null,
        string? secondAdditionalSource = null)
    {
        var document = CreateDocument(
            source,
            additionalSource: additionalSource,
            secondAdditionalSource: secondAdditionalSource);
        var diagnostics = await GetDiagnosticsAsync(document);
        var diagnostic = diagnostics.SingleOrDefault(item => item.Id == diagnosticId)
            ?? throw new InvalidOperationException($"Expected {diagnosticId}, got: {FormatDiagnostics(diagnostics)}");

        AssertSuggestion(diagnostic);
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);
        await _codeFix.RegisterCodeFixesAsync(context);

        var action = actions.SingleOrDefault()
            ?? throw new InvalidOperationException($"No code fix was registered for {diagnosticId}.");
        if (string.IsNullOrWhiteSpace(action.Title))
        {
            throw new InvalidOperationException(
                $"The code action for {diagnosticId} must have a non-empty localized title.");
        }

        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var applyChanges = operations.OfType<ApplyChangesOperation>().Single();
        var changedDocument = applyChanges.ChangedSolution.GetDocument(document.Id)
            ?? throw new InvalidOperationException("The code fix removed the test document.");
        var changedCompilation = await changedDocument.Project.GetCompilationAsync()
            ?? throw new InvalidOperationException("Could not compile the fixed document.");
        var changedErrors = changedCompilation.GetDiagnostics()
            .Where(item => item.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (changedErrors.Length != 0)
        {
            throw new InvalidOperationException(
                $"The fix for {diagnosticId} did not compile: {FormatDiagnostics(changedErrors)}");
        }

        var changedRoot = await changedDocument.GetSyntaxRootAsync()
            ?? throw new InvalidOperationException("The fixed document has no syntax root.");
        var expectedRoot = CSharpSyntaxTree.ParseText(expected).GetRoot();

        var actualText = changedRoot.NormalizeWhitespace().ToFullString();
        var expectedText = expectedRoot.NormalizeWhitespace().ToFullString();
        if (!string.Equals(actualText, expectedText, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unexpected fix for {diagnosticId}.\nEXPECTED:\n{expectedText}\nACTUAL:\n{actualText}");
        }

        _positiveCaseCounts.TryGetValue(diagnosticId, out var count);
        _positiveCaseCounts[diagnosticId] = count + 1;
    }

    private async Task VerifyNoDiagnosticAsync(string source)
    {
        var diagnostics = await GetDiagnosticsAsync(CreateDocument(source));
        if (!diagnostics.IsEmpty)
        {
            throw new InvalidOperationException($"Expected no analyzer diagnostics, got: {FormatDiagnostics(diagnostics)}");
        }
    }

}
