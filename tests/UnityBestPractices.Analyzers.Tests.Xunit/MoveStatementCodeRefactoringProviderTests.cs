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
    [InlineData(MoveStatementCodeRefactoringProvider.MoveLeftTitle, "return B && A && C;")]
    [InlineData(MoveStatementCodeRefactoringProvider.MoveRightTitle, "return A && C && B;")]
    public async Task MovesMiddleBinaryOperandInEitherDirection(string title, string expectedStatement)
    {
        var changed = await ApplyAsync(
            "class C { bool M(bool A, bool B, bool C) { return A && $$B && C; } }",
            title,
            "B");

        Assert.Contains(expectedStatement, changed);
    }

    [Fact]
    public async Task BinaryOperandActionsRespectChainBoundsAndOperatorPrecedence()
    {
        var first = await GetActionsAsync("class C { bool M(bool A, bool B, bool C) => $$A && B && C; }");
        Assert.DoesNotContain(first, action => action.EquivalenceKey == MoveStatementCodeRefactoringProvider.MoveLeftTitle);
        Assert.Contains(first, action => action.EquivalenceKey == MoveStatementCodeRefactoringProvider.MoveRightTitle);

        var middle = await GetActionsAsync("class C { bool M(bool A, bool B, bool C) => A || $$B && C; }");
        Assert.DoesNotContain(middle, action => action.EquivalenceKey == MoveStatementCodeRefactoringProvider.MoveLeftTitle);
        Assert.Contains(middle, action => action.EquivalenceKey == MoveStatementCodeRefactoringProvider.MoveRightTitle);
    }

    [Theory]
    [InlineData(
        MoveStatementCodeRefactoringProvider.MoveLeftTitle,
        "AddCommands(cmds.AsSpan()[..carSpawnAreasA.Length], carSpawnAreasA, mask.AsSpan()[..carSpawnAreasA.Length]);")]
    [InlineData(
        MoveStatementCodeRefactoringProvider.MoveRightTitle,
        "AddCommands(carSpawnAreasA, mask.AsSpan()[..carSpawnAreasA.Length], cmds.AsSpan()[..carSpawnAreasA.Length]);")]
    public async Task MovesFunctionArgumentInEitherDirection(string title, string expectedInvocation)
    {
        var changed = await ApplyAsync(
            "class C { void M() { AddCommands(carSpawnAreasA, $$cmds.AsSpan()[..carSpawnAreasA.Length], mask.AsSpan()[..carSpawnAreasA.Length]); } }",
            title,
            "cmds.AsSpan()[..carSpawnAreasA.Length]");

        Assert.Contains(expectedInvocation, changed);
    }

    [Fact]
    public async Task ArgumentActionsRespectListBounds()
    {
        var first = await GetActionsAsync("class C { void M() { AddCommands($$first, second, third); } }");
        Assert.DoesNotContain(first, action => action.EquivalenceKey == MoveStatementCodeRefactoringProvider.MoveLeftTitle);
        Assert.Contains(first, action => action.EquivalenceKey == MoveStatementCodeRefactoringProvider.MoveRightTitle);

        var last = await GetActionsAsync("class C { void M() { AddCommands(first, second, $$third); } }");
        Assert.Contains(last, action => action.EquivalenceKey == MoveStatementCodeRefactoringProvider.MoveLeftTitle);
        Assert.DoesNotContain(last, action => action.EquivalenceKey == MoveStatementCodeRefactoringProvider.MoveRightTitle);
    }

    [Theory]
    [InlineData(";")]
    [InlineData("Work();")]
    [InlineData("int value = 0;")]
    [InlineData("label: Work();")]
    [InlineData("goto label;")]
    [InlineData("break;")]
    [InlineData("continue;")]
    [InlineData("return;")]
    [InlineData("throw null;")]
    [InlineData("yield return null;")]
    [InlineData("yield break;")]
    [InlineData("while (false) { }")]
    [InlineData("do { } while (false);")]
    [InlineData("for (;;) { }")]
    [InlineData("foreach (var item in items) { }")]
    [InlineData("foreach (var (key, value) in pairs) { }")]
    [InlineData("using (resource) { }")]
    [InlineData("fixed (int* pointer = buffer) { }")]
    [InlineData("checked { }")]
    [InlineData("unchecked { }")]
    [InlineData("unsafe { }")]
    [InlineData("lock (gate) { }")]
    [InlineData("if (true) { }")]
    [InlineData("switch (value) { }")]
    [InlineData("try { } finally { }")]
    [InlineData("void Local() { }")]
    [InlineData("{ Work(); }")]
    public async Task MovesEveryCSharpStatementShape(string statement)
    {
        var source = "class C { void M() { Before(); $$" + statement + " After(); } }";
        var actions = await GetActionsAsync(source);

        Assert.Contains(actions, action => action.EquivalenceKey == MoveStatementCodeRefactoringProvider.MoveUpTitle);
        Assert.Contains(actions, action => action.EquivalenceKey == MoveStatementCodeRefactoringProvider.MoveDownTitle);
    }

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
            title,
            "Third();");

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
        Assert.DoesNotContain(firstActions, action => action.EquivalenceKey == MoveStatementCodeRefactoringProvider.MoveUpTitle);
        Assert.Contains(firstActions, action => action.EquivalenceKey == MoveStatementCodeRefactoringProvider.MoveDownTitle);

        var lastActions = await GetActionsAsync("class C { void M() { First(); $$Second(); } }");
        Assert.Contains(lastActions, action => action.EquivalenceKey == MoveStatementCodeRefactoringProvider.MoveUpTitle);
        Assert.DoesNotContain(lastActions, action => action.EquivalenceKey == MoveStatementCodeRefactoringProvider.MoveDownTitle);
    }

    [Theory]
    [InlineData(
        MoveStatementCodeRefactoringProvider.MoveUpTitle,
        """
        class Example
        {
            void Update()
            {
                if ((time -= secondDuration) < 0)
                    state = State.Second;
                else if ((time -= firstDuration) < 0)
                    state = State.First;
                else if ((time -= thirdDuration) < 0)
                    state = State.Third;
            }
        }
        """)]
    [InlineData(
        MoveStatementCodeRefactoringProvider.MoveDownTitle,
        """
        class Example
        {
            void Update()
            {
                if ((time -= firstDuration) < 0)
                    state = State.First;
                else if ((time -= thirdDuration) < 0)
                    state = State.Third;
                else if ((time -= secondDuration) < 0)
                    state = State.Second;
            }
        }
        """)]
    public async Task MovesElseIfBranchesInEitherDirection(string title, string expected)
    {
        var changed = await ApplyAsync(
            """
            class Example
            {
                void Update()
                {
                    if ((time -= firstDuration) < 0)
                        state = State.First;
                    else if ((time -= secondDuration) < 0)
                        $$state = State.Second;
                    else if ((time -= thirdDuration) < 0)
                        state = State.Third;
                }
            }
            """,
            title,
            "state = State.Second;");

        Assert.Equal(expected, changed);
    }

    [Fact]
    public async Task ElseIfBranchActionsRespectLadderBounds()
    {
        var firstActions = await GetActionsAsync(
            "class C { void M() { $$if (First()) A(); else if (Second()) B(); else Fallback(); } }");
        Assert.DoesNotContain(firstActions, action => action.EquivalenceKey == MoveStatementCodeRefactoringProvider.MoveUpTitle);
        Assert.Contains(firstActions, action => action.EquivalenceKey == MoveStatementCodeRefactoringProvider.MoveDownTitle);

        var lastActions = await GetActionsAsync(
            "class C { void M() { if (First()) A(); $$else if (Second()) B(); else Fallback(); } }");
        Assert.Contains(lastActions, action => action.EquivalenceKey == MoveStatementCodeRefactoringProvider.MoveUpTitle);
        Assert.DoesNotContain(lastActions, action => action.EquivalenceKey == MoveStatementCodeRefactoringProvider.MoveDownTitle);
    }

    [Fact]
    public async Task DoesNotOfferARefactoringForAnEmbeddedStatement()
    {
        var actions = await GetActionsAsync("class C { void M(bool value) { if (value) $$Run(); } }");
        Assert.Empty(actions);
    }

    private static async Task<string> ApplyAsync(
        string sourceWithCursor,
        string title,
        string? expectedNavigationTarget = null)
    {
        var (workspace, document, cursor) = CreateDocument(sourceWithCursor);
        using (workspace)
        {
            var actions = await GetActionsAsync(document, cursor);
            var action = Assert.Single(actions.Where(candidate => candidate.EquivalenceKey == title));
            var movesBackward = title == MoveStatementCodeRefactoringProvider.MoveUpTitle ||
                title == MoveStatementCodeRefactoringProvider.MoveLeftTitle;
            var resource = title == MoveStatementCodeRefactoringProvider.MoveUpTitle
                ? FixTitleLocalizer.MoveStatementUp
                : title == MoveStatementCodeRefactoringProvider.MoveDownTitle
                    ? FixTitleLocalizer.MoveStatementDown
                    : title == MoveStatementCodeRefactoringProvider.MoveLeftTitle
                        ? FixTitleLocalizer.MoveStatementLeft
                        : FixTitleLocalizer.MoveStatementRight;
            var localizedTitle = FixTitleLocalizer.Get(resource, title);
            Assert.Equal(
                movesBackward
                    ? OmniSharpRefactoringTitle.Inline(localizedTitle, title)
                    : OmniSharpRefactoringTitle.Extract(localizedTitle, title),
                action.Title);
            var operations = await action.GetOperationsAsync(CancellationToken.None);
            var changedSolution = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution;
            var changedDocument = changedSolution.GetDocument(document.Id)!;
            if (expectedNavigationTarget is not null)
            {
                var changedRoot = await changedDocument.GetSyntaxRootAsync();
                var navigationTarget = Assert.Single(
                    changedRoot!.GetAnnotatedNodesAndTokens(
                        MoveStatementCodeRefactoringProvider.NavigationAnnotationKind));
                Assert.Equal(expectedNavigationTarget, navigationTarget.ToString());
            }

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
