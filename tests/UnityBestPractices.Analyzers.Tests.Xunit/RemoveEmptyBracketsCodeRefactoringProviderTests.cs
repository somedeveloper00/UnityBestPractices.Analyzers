using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using UnityBestPractices.Analyzers;
using Xunit;

public sealed class RemoveEmptyBracketsCodeRefactoringProviderTests
{
    [Theory]
    [InlineData("class C { void M() { M(); } }", 24, "class C { void M() { M; } }")]
    [InlineData("class C { int[] values; }", 14, "class C { int values; }")]
    [InlineData("class C { }", 10, "class C ")]
    [InlineData("class C { void M(  ) { } }", 17, "class C { void M { } }")]
    public async Task RemovesTheEmptyPairAtTheCaret(string source, int position, string expected)
    {
        Assert.Equal(expected, await ApplyAsync(source, new TextSpan(position, 0)));
    }

    [Fact]
    public async Task RemovesASelectedEmptyPair()
    {
        const string source = "class C { void M() { } }";
        Assert.Equal("class C { void M { } }", await ApplyAsync(source, new TextSpan(16, 2)));
    }

    [Theory]
    [InlineData("class C { void M(int value) { } }", 17)]
    [InlineData("class C { void M(/* keep */) { } }", 17)]
    [InlineData("class C { string value = \"()\"; }", 26)]
    [InlineData("class C { void M() { } }", 0)]
    public async Task IsNotOfferedForNonEmptyPairsOrUnrelatedText(string source, int position)
    {
        Assert.Empty(await GetActionsAsync(source, new TextSpan(position, 0)));
    }

    private static async Task<string> ApplyAsync(string source, TextSpan span)
    {
        var (workspace, document) = CreateDocument(source);
        using (workspace)
        {
            var action = Assert.Single(await GetActionsAsync(document, span));
            Assert.Equal(RemoveEmptyBracketsCodeRefactoringProvider.Title, action.Title);
            var operations = await action.GetOperationsAsync(CancellationToken.None);
            var solution = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution;
            return (await solution.GetDocument(document.Id)!.GetTextAsync()).ToString();
        }
    }

    private static async Task<IReadOnlyList<CodeAction>> GetActionsAsync(string source, TextSpan span)
    {
        var (workspace, document) = CreateDocument(source);
        using (workspace)
        {
            return await GetActionsAsync(document, span);
        }
    }

    private static async Task<IReadOnlyList<CodeAction>> GetActionsAsync(Document document, TextSpan span)
    {
        var actions = new List<CodeAction>();
        await new RemoveEmptyBracketsCodeRefactoringProvider().ComputeRefactoringsAsync(
            new CodeRefactoringContext(document, span, actions.Add, CancellationToken.None));
        return actions;
    }

    private static (AdhocWorkspace Workspace, Document Document) CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("RemoveEmptyBracketsTest", LanguageNames.CSharp);
        return (workspace, workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source)));
    }
}
