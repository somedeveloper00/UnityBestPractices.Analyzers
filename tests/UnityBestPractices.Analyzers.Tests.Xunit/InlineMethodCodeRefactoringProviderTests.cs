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

public sealed class InlineMethodCodeRefactoringProviderTests
{
    [Fact]
    public async Task InlinesExpressionBodiedMethodAndPreservesPrecedence()
    {
        var changed = await ApplyAsync("""
            public static class Calculator
            {
                private static int Add(int left, int right) => left + right;
                public static int Calculate(int value) => 2 * Add$$(value, 3);
            }
            """);

        Assert.Equal(Normalize("""
            public static class Calculator
            {
                private static int Add(int left, int right) => left + right;
                public static int Calculate(int value) => 2 * ((value) + (3));
            }
            """), Normalize(changed));
    }

    [Fact]
    public async Task InlinesSingleReturnBlockWithNamedArguments()
    {
        var changed = await ApplyAsync("""
            public static class Calculator
            {
                private static int Subtract(int left, int right) { return left - right; }
                public static int Calculate() => Subtract$$(left: GetValue(), right: 2);
                private static int GetValue() => 10;
            }
            """);

        Assert.Contains("(GetValue()) - (2)", Normalize(changed), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("private static int Twice(int value) => value + value;", "Twice$$(GetValue())")]
    [InlineData("private static int Reverse(int first, int second) => second - first;", "Reverse$$(GetFirst(), GetSecond())")]
    [InlineData("private int Instance(int value) => value;", "new Calculator().Instance$$(1)")]
    public async Task DoesNotOfferInlineWhenSubstitutionCouldChangeBehavior(string declaration, string invocation)
    {
        var source = $$"""
            public sealed class Calculator
            {
                {{declaration}}
                public int Calculate() => {{invocation}};
                private static int GetValue() => 1;
                private static int GetFirst() => 1;
                private static int GetSecond() => 2;
            }
            """;

        Assert.Empty(await GetActionsAsync(source));
    }

    private static async Task<string> ApplyAsync(string sourceWithCursor)
    {
        var (workspace, document, cursor) = CreateDocument(sourceWithCursor);
        using (workspace)
        {
            var action = Assert.Single(await GetActionsAsync(document, cursor));
            Assert.Equal(InlineMethodCodeRefactoringProvider.Title, action.Title);
            var operations = await action.GetOperationsAsync(CancellationToken.None);
            var solution = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution;
            var changedDocument = solution.GetDocument(document.Id)!;
            var compilation = await changedDocument.Project.GetCompilationAsync();
            Assert.Empty(compilation!.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
            return (await changedDocument.GetTextAsync()).ToString();
        }
    }

    private static async Task<IReadOnlyList<CodeAction>> GetActionsAsync(string sourceWithCursor)
    {
        var (workspace, document, cursor) = CreateDocument(sourceWithCursor);
        using (workspace) { return await GetActionsAsync(document, cursor); }
    }

    private static async Task<IReadOnlyList<CodeAction>> GetActionsAsync(Document document, int cursor)
    {
        var actions = new List<CodeAction>();
        await new InlineMethodCodeRefactoringProvider().ComputeRefactoringsAsync(
            new CodeRefactoringContext(document, new TextSpan(cursor, 0), actions.Add, CancellationToken.None));
        return actions;
    }

    private static (AdhocWorkspace Workspace, Document Document, int Cursor) CreateDocument(string sourceWithCursor)
    {
        var cursor = sourceWithCursor.IndexOf("$$", StringComparison.Ordinal);
        Assert.True(cursor >= 0);
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("InlineMethodTest", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.CSharp9));
        foreach (var reference in GetPlatformReferences()) project = project.AddMetadataReference(reference);
        Assert.True(workspace.TryApplyChanges(project.Solution));
        return (workspace, workspace.AddDocument(project.Id, "Test.cs", SourceText.From(sourceWithCursor.Remove(cursor, 2))), cursor);
    }

    private static IEnumerable<MetadataReference> GetPlatformReferences() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(System.IO.Path.PathSeparator)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));

    private static string Normalize(string source) =>
        CSharpSyntaxTree.ParseText(source).GetRoot().NormalizeWhitespace().ToFullString();
}
