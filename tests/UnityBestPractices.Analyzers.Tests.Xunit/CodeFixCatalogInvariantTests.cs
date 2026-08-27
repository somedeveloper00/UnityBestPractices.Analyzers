using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using Xunit;

public sealed class CodeFixCatalogInvariantTests
{
    private static readonly ImmutableArray<string> KnownMultiEditNoFixAllIds =
        ImmutableArray.Create(
            DiagnosticIds.DiscardedScheduledJobHandle,
            DiagnosticIds.CacheShaderPropertyId);

    [Fact]
    public void CatalogAndHandlerRegistryHaveOneToOneCodeFixCoverage()
    {
        var handlersById = CodeFixRegistry.All
            .GroupBy(handler => handler.DiagnosticId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        Assert.Equal(CodeFixRegistry.All.Length, handlersById.Count);
        foreach (var metadata in DiagnosticCatalog.All)
        {
            if (metadata.HasCodeFix)
            {
                var handler = Assert.Single(handlersById[metadata.DiagnosticId]);
                Assert.Equal(metadata.SupportsFixAll, handler.SupportsFixAll);
                Assert.Equal(metadata.FixTitle, handler.FixTitle);
                Assert.Equal(metadata.DiagnosticId, handler.EquivalenceKey);
            }
            else
            {
                Assert.False(handlersById.ContainsKey(metadata.DiagnosticId));
            }
        }

        foreach (var handler in CodeFixRegistry.All)
        {
            var metadata = DiagnosticCatalog.Get(handler.DiagnosticId);
            Assert.True(metadata.HasCodeFix);
            Assert.False(
                handler.SupportsFixAll && metadata.Safety != RuleSafety.Safe,
                $"{handler.DiagnosticId} exposes an unsafe Fix All handler.");
        }
    }

    [Fact]
    public void RegistryConstructionRejectsDuplicateDiagnosticIds()
    {
        var handler = CodeFixRegistry.All[0];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CodeFixRegistry.Create(ImmutableArray.Create(handler, handler)));

        Assert.Contains(handler.DiagnosticId, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HandlerConstructionRejectsDiagnosticOnlyMetadata()
    {
        var metadata = DiagnosticCatalog.All.First(rule => !rule.HasCodeFix);

        var exception = Assert.Throws<ArgumentException>(() => new CodeFixHandler(
            metadata,
            (document, _, _) => Task.FromResult(document)));

        Assert.Contains(metadata.DiagnosticId, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderRegistersExactlyTheCatalogCodeFixes()
    {
        var provider = new UnityBestPracticesCodeFixProvider();
        var catalogFixable = DiagnosticCatalog.All
            .Where(rule => rule.HasCodeFix)
            .Select(rule => rule.DiagnosticId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var providerFixable = provider.FixableDiagnosticIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(catalogFixable, providerFixable);
    }

    [Fact]
    public void ProviderDoesNotRegisterDiagnosticOnlyRules()
    {
        var provider = new UnityBestPracticesCodeFixProvider();
        var diagnosticOnly = DiagnosticCatalog.All
            .Where(rule => !rule.HasCodeFix)
            .Select(rule => rule.DiagnosticId)
            .ToArray();

        Assert.Contains(DiagnosticIds.UndisposedPersistentNativeContainer, diagnosticOnly);
        Assert.Contains(DiagnosticIds.InvalidTemporaryAllocatorEscape, diagnosticOnly);
        foreach (var id in diagnosticOnly)
        {
            Assert.DoesNotContain(id, provider.FixableDiagnosticIds);
        }
    }

    [Fact]
    public void AnalyzerSupportedDiagnosticsMatchCatalog()
    {
        var analyzer = new UnityBestPracticesAnalyzer();
        var catalogIds = DiagnosticCatalog.All
            .Select(rule => rule.DiagnosticId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var supportedIds = analyzer.SupportedDiagnostics
            .Select(descriptor => descriptor.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(catalogIds, supportedIds);
    }

    [Fact]
    public void EveryCatalogEntryHasStableIdentityAndHelpLink()
    {
        foreach (var rule in DiagnosticCatalog.All)
        {
            Assert.StartsWith("UBP", rule.DiagnosticId, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(rule.Title));
            Assert.False(string.IsNullOrWhiteSpace(rule.MessageFormat));
            Assert.False(string.IsNullOrWhiteSpace(rule.Description));
            Assert.False(string.IsNullOrWhiteSpace(rule.Category));
            Assert.False(string.IsNullOrWhiteSpace(rule.DocumentationUrl));
            Assert.Contains(rule.DiagnosticId, rule.DocumentationUrl, StringComparison.Ordinal);
            Assert.Equal(DiagnosticSeverity.Info, rule.DefaultSeverity);
            if (rule.HasCodeFix)
            {
                Assert.False(string.IsNullOrWhiteSpace(rule.FixTitle));
            }
            else
            {
                Assert.True(string.IsNullOrEmpty(rule.FixTitle));
            }
        }
    }

    [Fact]
    public void SafeRulesSupportFixAllUnlessDocumentedMultiEditExceptions()
    {
        foreach (var rule in DiagnosticCatalog.All.Where(rule => rule.Safety == RuleSafety.Safe && rule.HasCodeFix))
        {
            if (KnownMultiEditNoFixAllIds.Contains(rule.DiagnosticId))
            {
                Assert.False(rule.SupportsFixAll);
                continue;
            }

            Assert.True(
                rule.SupportsFixAll,
                $"{rule.DiagnosticId} is Safe with a code fix but does not support Fix All.");
        }
    }

    [Fact]
    public void FixAllProviderAdvertisesOnlySupportingSafeRules()
    {
        var provider = new UnityBestPracticesCodeFixProvider();
        var advertised = provider.GetFixAllProvider()
            .GetSupportedFixAllDiagnosticIds(provider)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var expected = DiagnosticCatalog.All
            .Where(rule => rule.SupportsFixAll)
            .Select(rule => rule.DiagnosticId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, advertised);
        Assert.Contains(DiagnosticIds.YieldNull, advertised);
        Assert.Contains(DiagnosticIds.UseVector3Zero, advertised);
        Assert.DoesNotContain(DiagnosticIds.UseSquaredMagnitude, advertised);
        Assert.DoesNotContain(DiagnosticIds.DiscardedScheduledJobHandle, advertised);
    }

    [Fact]
    public async Task RegisteredEquivalenceKeysEqualDiagnosticIdsForRepresentativeRules()
    {
        // Legacy core rule.
        await AssertEquivalenceKeyAsync(
            """
            using System.Collections;
            using UnityEngine;
            class Waiter : MonoBehaviour { IEnumerator Wait() { yield return 0; } }
            """,
            DiagnosticIds.YieldNull,
            TestSources.UnityEngine);

        // Expression-registry rule.
        await AssertEquivalenceKeyAsync(
            """
            using UnityEngine;
            class C { Vector3 V() => new Vector3(0f, 0f, 0f); }
            """,
            DiagnosticIds.UseVector3Zero,
            """
            namespace UnityEngine
            {
                public struct Vector3
                {
                    public Vector3(float x, float y, float z) { }
                    public static Vector3 zero => default;
                }
            }
            """);

        // Advanced multi-edit rule.
        await AssertEquivalenceKeyAsync(
            """
            using Unity.Jobs;
            class C
            {
                void Run()
                {
                    new Work().Schedule();
                }
            }
            struct Work : IJob { public void Execute() { } }
            """,
            DiagnosticIds.DiscardedScheduledJobHandle,
            TestSources.Jobs);
    }

    private static async Task AssertEquivalenceKeyAsync(
        string source,
        string diagnosticId,
        string stubs)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var stubsId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "Invariants", "Invariants", LanguageNames.CSharp)
            .WithProjectCompilationOptions(
                projectId,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(
                projectId,
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddDocument(stubsId, "Stubs.cs", SourceText.From(stubs))
            .AddDocument(documentId, "Test.cs", SourceText.From(source));
        var document = solution.GetDocument(documentId)
            ?? throw new InvalidOperationException("Missing test document.");

        var compilation = await document.Project.GetCompilationAsync()
            ?? throw new InvalidOperationException("Missing compilation.");
        var analyzer = new UnityBestPracticesAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        var diagnostic = Assert.Single(diagnostics, item => item.Id == diagnosticId);

        var actions = new List<CodeAction>();
        var provider = new UnityBestPracticesCodeFixProvider();
        await provider.RegisterCodeFixesAsync(
            new CodeFixContext(
                document,
                diagnostic,
                (action, _) => actions.Add(action),
                CancellationToken.None));

        var action = Assert.Single(actions);
        Assert.Equal(diagnosticId, action.EquivalenceKey);
        Assert.False(string.IsNullOrWhiteSpace(action.Title));
    }
}
