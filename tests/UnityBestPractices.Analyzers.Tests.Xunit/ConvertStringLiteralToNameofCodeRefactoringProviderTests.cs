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

public sealed class ConvertStringLiteralToNameofCodeRefactoringProviderTests
{
    [Fact]
    public async Task ReplacesAFieldNameWithNameof()
    {
        var changed = await ApplyAsync("""
            class Player
            {
                private int movementSpeed;
                string SavedName => "$$movementSpeed";
            }
            """);

        Assert.Equal(Normalize("""
            class Player
            {
                private int movementSpeed;
                string SavedName => nameof(movementSpeed);
            }
            """), Normalize(changed));
    }

    [Theory]
    [InlineData("void Run(int value) { Use(\"$$value\"); }", "void Run(int value) { Use(nameof(value)); }")]
    [InlineData("void Run() { var value = 1; Use(@\"$$value\"); }", "void Run() { var value = 1; Use(nameof(value)); }")]
    [InlineData("void Run() { Use(\"$$Player\"); }", "void Run() { Use(nameof(Player)); }")]
    [InlineData("void Run() { Use(\"$$Reset\"); } void Reset() { }", "void Run() { Use(nameof(Reset)); } void Reset() { }")]
    public async Task ReplacesAccessibleSymbolNames(string sourceMember, string expectedMember)
    {
        var changed = await ApplyAsync(
            "class Player { " + sourceMember + " static void Use(string value) { } }");

        Assert.Equal(
            Normalize("class Player { " + expectedMember + " static void Use(string value) { } }"),
            Normalize(changed));
    }

    [Fact]
    public async Task EscapesAKeywordIdentifier()
    {
        var changed = await ApplyAsync(
            """class Example { void Run(int @class) { Use("$$class"); } void Use(string value) { } }""");

        Assert.Contains("Use(nameof(@class))", Normalize(changed), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SupportsSelectingTheLiteral()
    {
        var changed = await ApplyAsync(
            """class Example { int value; string Name => [|"value"|]; }""");

        Assert.Contains("nameof(value)", Normalize(changed), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        """class Example { int Count { get; set; } string Name => "$$Count"; }""",
        "nameof(Count)")]
    [InlineData(
        """class Example<TValue> { string Name => "$$TValue"; }""",
        "nameof(TValue)")]
    [InlineData(
        """class Example { void Reset() { } string Name => "$$Reset"; void Reset(int value) { } }""",
        "nameof(Reset)")]
    [InlineData(
        """class Example { int value; string Name => "\u0076$$alue"; }""",
        "nameof(value)")]
    [InlineData(
        """class Example { string Name { set { Use("$$value"); } } void Use(string text) { } }""",
        "nameof(value)")]
    public async Task HandlesAdditionalAccessibleSymbolKindsAndSpellings(
        string source,
        string expected)
    {
        var changed = await ApplyAsync(source);

        Assert.Contains(expected, Normalize(changed), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreservesCommentsFollowingTheLiteral()
    {
        var changed = await ApplyAsync(
            """class Example { int value; string Name => "$$value" /* keep this */; }""");

        Assert.Contains("nameof(value) /* keep this */", changed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorksInAnAttributeConstant()
    {
        var changed = await ApplyAsync("""
            class LabelAttribute : System.Attribute
            {
                public LabelAttribute(string value) { }
            }

            class Example
            {
                int value;
                [Label("$$value")]
                void Run() { }
            }
            """);

        Assert.Contains("[Label(nameof(value))]", Normalize(changed), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""class Example { int value; string Name => "$$Value"; }""")]
    [InlineData("""class Example { string Name => "$$missing"; }""")]
    [InlineData("""class Example { string Run() { { int local = 0; } return "$$local"; } }""")]
    [InlineData("""class Example { int value; char Name => '$$v'; }""")]
    [InlineData("""class Example { int value; string Name => $"{nameof(value)}$$"; }""")]
    [InlineData("""class Box<T> { } class Example { string Name => "$$Box"; }""")]
    [InlineData("""class Hidden { private int secret; } class Example { string Name => "$$secret"; }""")]
    [InlineData("""class Example { int value; string Name => [|"value";|] }""")]
    [InlineData("""class Example { int value; }$$""")]
    public async Task DoesNotOfferForTextThatIsNotAnAccessibleSymbolName(string source)
    {
        Assert.Empty(await GetActionsAsync(source));
    }

    [Fact]
    public async Task DoesNotOfferWhenNameofIsAUserDefinedMethod()
    {
        const string source = """
            class Example
            {
                int value;
                string Name => "$$value";
                static string nameof(object value) => "";
            }
            """;

        Assert.Empty(await GetActionsAsync(source));
    }

    [Fact]
    public async Task DoesNotOfferBeforeNameofIsAvailable()
    {
        var (workspace, document, span) = CreateDocument(
            """class Example { int value; string Name => "$$value"; }""",
            LanguageVersion.CSharp5);
        using (workspace)
        {
            Assert.Empty(await GetActionsAsync(document, span));
        }
    }

    private static async Task<string> ApplyAsync(string sourceWithMarker)
    {
        var (workspace, document, span) = CreateDocument(sourceWithMarker);
        using (workspace)
        {
            var action = Assert.Single(await GetActionsAsync(document, span));
            Assert.Equal(ConvertStringLiteralToNameofCodeRefactoringProvider.Title, action.Title);
            var operations = await action.GetOperationsAsync(CancellationToken.None);
            var solution = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution;
            var changedDocument = solution.GetDocument(document.Id)!;
            var compilation = await changedDocument.Project.GetCompilationAsync();
            Assert.Empty(compilation!.GetDiagnostics().Where(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error));
            return (await changedDocument.GetTextAsync()).ToString();
        }
    }

    private static async Task<IReadOnlyList<CodeAction>> GetActionsAsync(string sourceWithMarker)
    {
        var (workspace, document, span) = CreateDocument(sourceWithMarker);
        using (workspace)
        {
            return await GetActionsAsync(document, span);
        }
    }

    private static async Task<IReadOnlyList<CodeAction>> GetActionsAsync(
        Document document,
        TextSpan span)
    {
        var actions = new List<CodeAction>();
        await new ConvertStringLiteralToNameofCodeRefactoringProvider().ComputeRefactoringsAsync(
            new CodeRefactoringContext(
                document,
                span,
                actions.Add,
                CancellationToken.None));
        return actions;
    }

    private static (AdhocWorkspace Workspace, Document Document, TextSpan Span) CreateDocument(
        string sourceWithMarker,
        LanguageVersion languageVersion = LanguageVersion.CSharp9)
    {
        var selectionStart = sourceWithMarker.IndexOf("[|", StringComparison.Ordinal);
        string source;
        TextSpan span;
        if (selectionStart >= 0)
        {
            var selectionEnd = sourceWithMarker.IndexOf("|]", selectionStart, StringComparison.Ordinal);
            Assert.True(selectionEnd > selectionStart);
            source = sourceWithMarker.Remove(selectionEnd, 2).Remove(selectionStart, 2);
            span = TextSpan.FromBounds(selectionStart, selectionEnd - 2);
        }
        else
        {
            var cursor = sourceWithMarker.IndexOf("$$", StringComparison.Ordinal);
            Assert.True(cursor >= 0);
            source = sourceWithMarker.Remove(cursor, 2);
            span = new TextSpan(cursor, 0);
        }

        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("NameofTest", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithParseOptions(new CSharpParseOptions(languageVersion));
        foreach (var reference in GetPlatformReferences())
        {
            project = project.AddMetadataReference(reference);
        }

        Assert.True(workspace.TryApplyChanges(project.Solution));
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));
        return (workspace, document, span);
    }

    private static IEnumerable<MetadataReference> GetPlatformReferences() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(System.IO.Path.PathSeparator)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(path => MetadataReference.CreateFromFile(path));

    private static string Normalize(string source) =>
        CSharpSyntaxTree.ParseText(source).GetRoot().NormalizeWhitespace().ToFullString();
}
