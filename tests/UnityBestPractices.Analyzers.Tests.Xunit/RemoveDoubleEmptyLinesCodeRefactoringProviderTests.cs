using System;
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

public sealed class RemoveDoubleEmptyLinesCodeRefactoringProviderTests
{
    [Fact]
    public async Task CollapsesEveryRunOfEmptyLinesInTheDocument()
    {
        var changed = await ApplyAsync("class First\n{\n\n\n}\n\n\nclass Second { }\n");

        Assert.Equal("class First\n{\n\n}\n\nclass Second { }\n", changed);
    }

    [Fact]
    public async Task TreatsLinesContainingSpacesAndTabsAsEmpty()
    {
        var changed = await ApplyAsync("class Example\r\n{\r\n \t \r\n\t\r\n    void Run() { }\r\n}\r\n");

        Assert.Equal("class Example\r\n{\r\n \t \r\n    void Run() { }\r\n}\r\n", changed);
    }

    [Fact]
    public async Task PreservesNonSpaceWhitespaceLines()
    {
        Assert.Empty(await GetActionsAsync("class Example\n{\n\n\u00a0\n}\n"));
    }

    [Theory]
    [InlineData("class Example\n{\n\n}\n")]
    [InlineData("class Example { }\n")]
    public async Task IsNotOfferedWithoutConsecutiveEmptyLines(string source)
    {
        Assert.Empty(await GetActionsAsync(source));
    }

    private static async Task<string> ApplyAsync(string source)
    {
        var (workspace, document) = CreateDocument(source);
        using (workspace)
        {
            var action = Assert.Single(await GetActionsAsync(document));
            Assert.Equal(
                FixTitleLocalizer.Get(
                    FixTitleLocalizer.RemoveDoubleEmptyLines,
                    RemoveDoubleEmptyLinesCodeRefactoringProvider.Title),
                action.Title);
            var operations = await action.GetOperationsAsync(CancellationToken.None);
            var solution = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution;
            return (await solution.GetDocument(document.Id)!.GetTextAsync()).ToString();
        }
    }

    private static async Task<IReadOnlyList<CodeAction>> GetActionsAsync(string source)
    {
        var (workspace, document) = CreateDocument(source);
        using (workspace)
        {
            return await GetActionsAsync(document);
        }
    }

    private static async Task<IReadOnlyList<CodeAction>> GetActionsAsync(Document document)
    {
        var actions = new List<CodeAction>();
        await new RemoveDoubleEmptyLinesCodeRefactoringProvider().ComputeRefactoringsAsync(
            new CodeRefactoringContext(document, new TextSpan(0, 0), actions.Add, CancellationToken.None));
        return actions;
    }

    private static (AdhocWorkspace Workspace, Document Document) CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("RemoveDoubleEmptyLinesTest", LanguageNames.CSharp);
        return (workspace, workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source)));
    }
}
