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

public sealed class MoveStatementCodeRefactoringProviderTests
{
    [Theory]
    [InlineData(MoveStatementCodeRefactoringProvider.MoveUpTitle, "Third();\n        First();\n        Second();")]
    [InlineData(MoveStatementCodeRefactoringProvider.MoveDownTitle, "First();\n        Second();\n        Third();")]
    public async Task MovesSimpleStatementsInEitherDirection(string title, string expectedBody)
    {
        var changed = await ApplyAsync(
            """
            class Example
            {
                void Run()
                {
                    First();
                    $$Third();
                    Second();
                }
            }
            """,
            title);

        Assert.Equal(
            "class Example\n{\n    void Run()\n    {\n        " + expectedBody + "\n    }\n}",
            changed);
    }

    [Fact]
    public async Task MovesAWholeBracedBlockAndItsComments()
    {
        var changed = await ApplyAsync(
            """
            class Example
            {
                void Run()
                {
                    First();
                    // Keep this explanation with the block.
                    $${
                        Second();
                        Third();
                    }
                }
            }
            """,
            MoveStatementCodeRefactoringProvider.MoveUpTitle);

        Assert.Equal(
            """
            class Example
            {
                void Run()
                {
                    // Keep this explanation with the block.
                    {
                        Second();
                        Third();
                    }
                    First();
                }
            }
            """,
            changed);
    }

    [Fact]
    public async Task MovesMethodsAndNestedClassesAsSingleMembers()
    {
        var changed = await ApplyAsync(
            """
            class Container
            {
                void First() { }

                class Nested
                {
                    void Inside() { }
                }

                void $$Last() { }
            }
            """,
            MoveStatementCodeRefactoringProvider.MoveUpTitle);

        Assert.Equal(
            """
            class Container
            {
                void First() { }

                void Last() { }

                class Nested
                {
                    void Inside() { }
                }
            }
            """,
            changed);
    }

    [Fact]
    public async Task CursorOnMethodBraceMovesTheMethodRatherThanItsBodyStatements()
    {
        var changed = await ApplyAsync(
            """
            class Example
            {
                void First() { }
                void Second() $${ Work(); }
            }
            """,
            MoveStatementCodeRefactoringProvider.MoveUpTitle);

        Assert.Equal(
            """
            class Example
            {
                void Second() { Work(); }
                void First() { }
            }
            """,
            changed);
    }

    [Fact]
    public async Task OffersOnlyDirectionsThatHaveASibling()
    {
        var firstActions = await GetActionsAsync("class C { void M() { $$First(); Second(); } }");
        Assert.DoesNotContain(firstActions, action => action.Title == MoveStatementCodeRefactoringProvider.MoveUpTitle);
        Assert.Contains(firstActions, action => action.Title == MoveStatementCodeRefactoringProvider.MoveDownTitle);

        var lastActions = await GetActionsAsync("class C { void M() { First(); $$Second(); } }");
        Assert.Contains(lastActions, action => action.Title == MoveStatementCodeRefactoringProvider.MoveUpTitle);
        Assert.DoesNotContain(lastActions, action => action.Title == MoveStatementCodeRefactoringProvider.MoveDownTitle);
    }

    [Fact]
    public async Task DoesNotOfferARefactoringForAnEmbeddedStatement()
    {
        var actions = await GetActionsAsync("class C { void M(bool value) { if (value) $$Run(); } }");
        Assert.Empty(actions);
    }

    private static async Task<string> ApplyAsync(string sourceWithCursor, string title)
    {
        var (workspace, document, cursor) = CreateDocument(sourceWithCursor);
        using (workspace)
        {
            var actions = await GetActionsAsync(document, cursor);
            var action = Assert.Single(actions.Where(candidate => candidate.Title == title));
            var operations = await action.GetOperationsAsync(CancellationToken.None);
            var changedSolution = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution;
            var changedDocument = changedSolution.GetDocument(document.Id)!;
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
        var context = new CodeRefactoringContext(
            document,
            new TextSpan(cursor, 0),
            actions.Add,
            CancellationToken.None);
        await new MoveStatementCodeRefactoringProvider().ComputeRefactoringsAsync(context);
        return actions;
    }

    private static (AdhocWorkspace Workspace, Document Document, int Cursor) CreateDocument(string sourceWithCursor)
    {
        var cursor = sourceWithCursor.IndexOf("$$", StringComparison.Ordinal);
        Assert.True(cursor >= 0);
        var source = sourceWithCursor.Remove(cursor, 2);
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("RefactoringTest", LanguageNames.CSharp)
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.CSharp9));
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));
        return (workspace, document, cursor);
    }
}
