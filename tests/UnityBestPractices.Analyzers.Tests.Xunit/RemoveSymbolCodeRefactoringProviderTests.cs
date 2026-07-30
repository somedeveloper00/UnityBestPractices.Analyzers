using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using UnityBestPractices.Analyzers;
using Xunit;

public sealed class RemoveSymbolCodeRefactoringProviderTests
{
    [Fact]
    public async Task RemovesLocalVariableAndStatementsThatUseIt()
    {
        var changed = await ApplyAsync("""
            public sealed class Example
            {
                public void Run()
                {
                    var $$value = 1;
                    System.Console.WriteLine(value);
                    System.Console.WriteLine("kept");
                }
            }
            """);

        Assert.DoesNotContain("value", changed, StringComparison.Ordinal);
        Assert.Contains("WriteLine(\"kept\")", changed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemovesOneFieldDeclaratorAndAllUsageStatements()
    {
        var changed = await ApplyAsync("""
            public sealed class Example
            {
                private int kept, $$removed;

                public void Run()
                {
                    removed = 1;
                    kept = 2;
                }
            }
            """);

        var normalized = Normalize(changed);
        Assert.Contains("private int kept;", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("removed", normalized, StringComparison.Ordinal);
        Assert.Contains("kept = 2;", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemovesMethodAndEveryInvocationStatement()
    {
        var changed = await ApplyAsync("""
            public sealed class Example
            {
                public void Run()
                {
                    DeleteMe();
                    KeepMe();
                }

                private void $$DeleteMe() { }
                private void KeepMe() { }
            }
            """);

        Assert.DoesNotContain("DeleteMe", changed, StringComparison.Ordinal);
        Assert.Contains("KeepMe();", changed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemovesClassAndMembersWhoseTypesUseIt()
    {
        var changed = await ApplyAsync("""
            public sealed class $$DisposableService { }

            public sealed class Consumer
            {
                private DisposableService service = new DisposableService();
                public void Keep() { }
            }
            """);

        Assert.DoesNotContain("DisposableService", changed, StringComparison.Ordinal);
        Assert.Contains("class Consumer", changed, StringComparison.Ordinal);
        Assert.Contains("void Keep()", changed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CanBeInvokedFromAUsage()
    {
        var changed = await ApplyAsync("""
            public sealed class Example
            {
                private int obsolete;
                public void Run() => System.Console.WriteLine($$obsolete);
            }
            """);

        Assert.DoesNotContain("obsolete", changed, StringComparison.Ordinal);
        Assert.DoesNotContain("Run", changed, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("public int $$Property { get; set; }")]
    [InlineData("public struct $$Value { }")]
    [InlineData("public void Run(int $$parameter) { }")]
    public async Task DoesNotOfferForUnsupportedSymbols(string declaration)
    {
        Assert.Empty(await GetActionsAsync($"public sealed class Example {{ {declaration} }}"));
    }

    private static async Task<string> ApplyAsync(string sourceWithCursor)
    {
        var (workspace, document, cursor) = CreateDocument(sourceWithCursor);
        using (workspace)
        {
            var action = Assert.Single(await GetActionsAsync(document, cursor));
            Assert.Equal(RemoveSymbolCodeRefactoringProvider.Title, action.Title);
            var operations = await action.GetOperationsAsync(CancellationToken.None);
            var solution = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution;
            var changedDocument = solution.GetDocument(document.Id)!;
            var compilation = await changedDocument.Project.GetCompilationAsync();
            Assert.Empty(compilation!.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            return (await changedDocument.GetTextAsync()).ToString();
        }
    }

    private static async Task<IReadOnlyList<CodeAction>> GetActionsAsync(string sourceWithCursor)
    {
        var (workspace, document, cursor) = CreateDocument(sourceWithCursor);
        using (workspace)
        {
            return await GetActionsAsync(document, cursor);
        }
    }

    private static async Task<IReadOnlyList<CodeAction>> GetActionsAsync(Document document, int cursor)
    {
        var actions = new List<CodeAction>();
        await new RemoveSymbolCodeRefactoringProvider().ComputeRefactoringsAsync(
            new CodeRefactoringContext(document, new TextSpan(cursor, 0), actions.Add, CancellationToken.None));
        return actions;
    }

    private static (AdhocWorkspace Workspace, Document Document, int Cursor) CreateDocument(string sourceWithCursor)
    {
        var cursor = sourceWithCursor.IndexOf("$$", StringComparison.Ordinal);
        Assert.True(cursor >= 0);
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("RemoveSymbolTest", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.CSharp9));
        foreach (var reference in GetPlatformReferences())
        {
            project = project.AddMetadataReference(reference);
        }

        Assert.True(workspace.TryApplyChanges(project.Solution));
        return (workspace, workspace.AddDocument(
            project.Id,
            "Test.cs",
            SourceText.From(sourceWithCursor.Remove(cursor, 2))), cursor);
    }

    private static IEnumerable<MetadataReference> GetPlatformReferences() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(System.IO.Path.PathSeparator)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));

    private static string Normalize(string source) =>
        CSharpSyntaxTree.ParseText(source).GetRoot().NormalizeWhitespace().ToFullString();
}
