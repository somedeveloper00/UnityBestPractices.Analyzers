using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using UnityBestPractices.Analyzers;
using UnityBestPractices.Analyzers.Infrastructure;
using Xunit;

public sealed class SwitchIndexerCodeRefactoringProviderTests
{
    [Theory]
    [InlineData("ja-JP", "インデクサーを使用 [int index]")]
    [InlineData("fa-IR", "استفاده از نمایه‌ساز [int index]")]
    [InlineData("ru-RU", "Использовать индексатор [int index]")]
    [InlineData("de-DE", "Indexer verwenden [int index]")]
    [InlineData("pl-PL", "Użyj indeksatora [int index]")]
    public void LocalizesTheCompleteIndexerAction(string cultureName, string expected)
    {
        Assert.Equal(expected, FixTitleLocalizer.Get(
            FixTitleLocalizer.SwitchIndexer,
            "Use indexer [int index]",
            CultureInfo.GetCultureInfo(cultureName)));
    }

    [Fact]
    public async Task FindsEveryOverloadAndChangesItsArguments()
    {
        const string source = "class C { public int this[int i]=>0; public int this[string s]=>0; public int this[int x,int y]=>0; int M(C c)=>c[$$1]; }";
        var (workspace, document, span) = CreateDocument(source);
        using (workspace)
        {
            var actions = await GetActionsAsync(document, span);
            Assert.Equal(2, actions.Count);
            Assert.Contains(actions, action => action.Title.Contains("[string s]", StringComparison.Ordinal));
            Assert.Contains(actions, action => action.Title.Contains("[int x, int y]", StringComparison.Ordinal));
            Assert.Contains("c[default(string)]", await ApplyAsync(document, actions.Single(a => a.Title.Contains("string", StringComparison.Ordinal))));
            Assert.Contains("c[1, default(int)]", await ApplyAsync(document, actions.Single(a => a.Title.Contains("int x", StringComparison.Ordinal))));
        }
    }

    [Theory]
    [InlineData("interface I { int this[string s] {get;} } class C:I { public int this[int i]=>0; public int this[string s]=>0; int M()=>this[$$1]; }")]
    [InlineData("class B { public int this[string s]=>0; } class C:B { public int this[int i]=>0; int M()=>this[$$1]; }")]
    public async Task FindsInterfaceAndInheritedIndexers(string source)
    {
        var (workspace, document, span) = CreateDocument(source);
        using (workspace)
        {
            Assert.Single(await GetActionsAsync(document, span));
        }
    }

    [Theory]
    [InlineData("class C { int M(int[] value)=>value[$$0]; }")]
    [InlineData("class C { public int this[int i]=>0; int M(C c)=>c[$$0]; }")]
    [InlineData("class C { public int this[int i]=>0; void M(C c) { } $$ }")]
    [InlineData("class C { public int this[int i]=>0; public int this[string s]=>0; int M(dynamic c)=>c[$$0]; }")]
    public async Task DoesNotOfferWithoutAnotherStaticallyResolvedIndexer(string source)
    {
        var (workspace, document, span) = CreateDocument(source);
        using (workspace)
        {
            Assert.Empty(await GetActionsAsync(document, span));
        }
    }

    [Fact]
    public async Task DeduplicatesAnImplementedInterfaceSignature()
    {
        const string source = "interface I { int this[string s] {get;} } class C:I { public int this[int i]=>0; public int this[string s]=>0; int M()=>this[$$1]; }";
        var (workspace, document, span) = CreateDocument(source);
        using (workspace)
        {
            Assert.Single(await GetActionsAsync(document, span));
        }
    }

    [Fact]
    public async Task RetainsACompatibleNamedArgumentByParameterName()
    {
        const string source = "class C { public int this[int value]=>0; public int this[int other, int value]=>0; int M()=>this[value: $$1]; }";
        var (workspace, document, span) = CreateDocument(source);
        using (workspace)
        {
            var action = Assert.Single(await GetActionsAsync(document, span));
            var changed = await ApplyAsync(document, action);
            Assert.Contains("this[default(int), 1]", changed, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("class C { public int this[int i] { get=>0; set{} } public int this[string s]=>0; void M()=>this[$$0]=1; }")]
    [InlineData("class C { public int this[int i]=>0; public int this[string s] { set{} } int M()=>this[$$0]; }")]
    public async Task ExcludesIndexerWithoutTheRequiredAccessor(string source)
    {
        var (workspace, document, span) = CreateDocument(source);
        using (workspace)
        {
            Assert.Empty(await GetActionsAsync(document, span));
        }
    }

    private static async Task<IReadOnlyList<CodeAction>> GetActionsAsync(Document document, TextSpan span)
    {
        var actions = new List<CodeAction>();
        await new SwitchIndexerCodeRefactoringProvider().ComputeRefactoringsAsync(
            new CodeRefactoringContext(document, span, actions.Add, CancellationToken.None));
        return actions;
    }

    private static async Task<string> ApplyAsync(Document document, CodeAction action)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var solution = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution;
        return (await solution.GetDocument(document.Id)!.GetTextAsync()).ToString();
    }

    private static (AdhocWorkspace, Document, TextSpan) CreateDocument(string marked)
    {
        var cursor = marked.IndexOf("$$", StringComparison.Ordinal);
        Assert.True(cursor >= 0);
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("IndexerTest", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        foreach (var path in ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(System.IO.Path.PathSeparator))
        {
            project = project.AddMetadataReference(MetadataReference.CreateFromFile(path));
        }

        Assert.True(workspace.TryApplyChanges(project.Solution));
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(marked.Remove(cursor, 2)));
        return (workspace, document, new TextSpan(cursor, 0));
    }
}
