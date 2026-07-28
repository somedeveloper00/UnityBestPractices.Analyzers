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

    [Fact]
    public async Task InlinesParameterlessConstantMethod()
    {
        var changed = await ApplyAsync("""
            public static class Calculator
            {
                private static int One() => 1;
                public static int Calculate() => One$$() + 2;
            }
            """);

        Assert.Contains("(1) + 2", Normalize(changed), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InlinesImplicitInstanceExpressionMethod()
    {
        var changed = await ApplyAsync("""
            public sealed class Calculator
            {
                private int Identity(int value) => value;
                public int Calculate(int value) => Identity$$(value);
            }
            """);

        Assert.Contains("Calculate(int value) => ((value));", Normalize(changed), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InlinesCheckedExpression()
    {
        var changed = await ApplyAsync("""
            public static class Calculator
            {
                private static int Increment(int value) => checked(value + 1);
                public static int Calculate(int value) => Increment$$(value);
            }
            """);

        Assert.Contains("checked((value) + 1)", Normalize(changed), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InlinesParameterlessVoidInstanceMethodStatement()
    {
        var changed = await ApplyAsync("""
            using System.Collections.Generic;

            public sealed class Garage
            {
                private readonly List<int> _cars = new List<int>();

                public void Reset()
                {
                    Test$$();
                }

                private void Test()
                {
                    _cars.Clear();
                }
            }
            """);

        var normalized = Normalize(changed);
        Assert.Contains("_cars.Clear();", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("Test();", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InlinesVoidMethodWithParametersLocalsStatementsAndEarlyReturn()
    {
        var changed = await ApplyAsync("""
            using System;

            public sealed class Example
            {
                public void Run()
                {
                    Write$$(GetValue());
                }

                private static short GetValue() => 2;

                private void Write(long value)
                {
                    var doubled = value * 2;
                    if (doubled == 0)
                    {
                        return;
                    }

                    Console.WriteLine(doubled);
                }
            }
            """);

        var normalized = Normalize(changed);
        Assert.DoesNotContain("Write(GetValue())", normalized, StringComparison.Ordinal);
        Assert.Contains("long __inlineValue = GetValue();", normalized, StringComparison.Ordinal);
        Assert.Contains("goto __inlineReturn;", normalized, StringComparison.Ordinal);
        Assert.Contains("Console.WriteLine(__inlineDoubled);", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InlinesConstructedGenericVoidMethod()
    {
        var changed = await ApplyAsync("""
            public sealed class Example
            {
                public void Run()
                {
                    Write$$(42);
                }

                private void Write<T>(T value)
                {
                    T copy = value;
                    System.Console.WriteLine(copy);
                }
            }
            """);

        var normalized = Normalize(changed);
        Assert.Contains("int __inlineValue = 42;", normalized, StringComparison.Ordinal);
        Assert.Contains("int __inlineCopy = __inlineValue;", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InlinesExpressionBodiedVoidMethodWithParameter()
    {
        var changed = await ApplyAsync("""
            public sealed class Example
            {
                public void Run() { Write$$(42); }
                private void Write(int value) => System.Console.WriteLine(value);
            }
            """);

        var normalized = Normalize(changed);
        Assert.Contains("int __inlineValue = 42;", normalized, StringComparison.Ordinal);
        Assert.Contains("System.Console.WriteLine(__inlineValue);", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InlinesEventHandlerMethodGroupAsLambda()
    {
        var changed = await ApplyAsync("""
            using System;

            public sealed class Example
            {
                private event Action<int> Changed;

                public void Subscribe()
                {
                    Changed += Handle$$;
                }

                private void Handle(int value)
                {
                    var text = value.ToString();
                    Console.WriteLine(text);
                }
            }
            """);

        var normalized = Normalize(changed);
        Assert.DoesNotContain("Changed += Handle", normalized, StringComparison.Ordinal);
        Assert.Contains("Changed += (__inlineValue) =>", normalized, StringComparison.Ordinal);
        Assert.Contains("Console.WriteLine(__inlineText);", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InlinesListenerArgumentAsLambda()
    {
        var changed = await ApplyAsync("""
            using System;

            public sealed class Example
            {
                public void Register()
                {
                    Subscribe(Handle$$);
                }

                private static void Subscribe(Action listener) { }
                private void Handle()
                {
                    Console.WriteLine("handled");
                }
            }
            """);

        var normalized = Normalize(changed);
        Assert.Contains("Subscribe(() =>", normalized, StringComparison.Ordinal);
        Assert.Contains("Console.WriteLine(\"handled\");", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InlinesAsyncEventHandlerMethodGroupAsAsyncLambda()
    {
        var changed = await ApplyAsync("""
            using System;
            using System.Threading.Tasks;

            public sealed class Example
            {
                private event Func<Task> Changed;

                public void Subscribe()
                {
                    Changed += Handle$$;
                }

                private async Task Handle()
                {
                    await Task.Yield();
                }
            }
            """);

        Assert.Contains(
            "Changed += async () =>",
            Normalize(changed),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task InlinesConstructedGenericEventHandlerAsLambda()
    {
        var changed = await ApplyAsync("""
            using System;

            public sealed class Example
            {
                private event Action<int> Changed;

                public void Subscribe()
                {
                    Changed += Handle<int>$$;
                }

                private void Handle<T>(T value)
                {
                    T copy = value;
                    Console.WriteLine(copy);
                }
            }
            """);

        var normalized = Normalize(changed);
        Assert.Contains("int __inlineCopy = __inlineValue;", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("Changed += Handle<int>", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DoesNotInlineEventUnsubscriptionAsANewLambda()
    {
        const string source = """
            using System;

            public sealed class Example
            {
                private event Action Changed;
                public void Unsubscribe() { Changed -= Handle$$; }
                private void Handle() { }
            }
            """;

        Assert.Empty(await GetActionsAsync(source));
    }

    [Fact]
    public async Task DoesNotInlineVoidInstanceMethodThroughExplicitReceiver()
    {
        const string source = """
            public sealed class Example
            {
                public void Run(Example other) { other.Test$$(); }
                private void Test() { Notify(); }
                private void Notify() { }
            }
            """;

        Assert.Empty(await GetActionsAsync(source));
    }

    [Fact]
    public async Task PreservesCommentsAttachedToAnArgument()
    {
        var changed = await ApplyAsync("""
            public static class Calculator
            {
                private static int Identity(int value) => value;
                public static int Calculate(int value) => Identity$$(/* keep this */ value);
            }
            """);

        Assert.Contains("/* keep this */", changed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InlinesTheNearestNestedInvocation()
    {
        var changed = await ApplyAsync("""
            public static class Calculator
            {
                private static int Identity(int value) => value;
                private static int Double(int value) => value * 2;
                public static int Calculate(int value) => Double(Identity$$(value));
            }
            """);

        var normalized = Normalize(changed);
        Assert.Contains("Double(((value)))", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Calculate(int value) => Double(Identity(",
            normalized,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("private static int Twice(int value) => value + value;", "Twice$$(GetValue())")]
    [InlineData("private static int Reverse(int first, int second) => second - first;", "Reverse$$(GetFirst(), GetSecond())")]
    [InlineData("private int Instance(int value) => value;", "new Calculator().Instance$$(1)")]
    [InlineData("private static int Ignore(int value) => 1;", "Ignore$$(GetValue())")]
    [InlineData("private static T Identity<T>(T value) => value;", "Identity$$(GetValue())")]
    [InlineData("private static int First(params int[] values) => values[0];", "First$$(1, 2)")]
    [InlineData("private static int Increment(int value = 1) => value + 1;", "Increment$$()")]
    [InlineData("private static int Absolute(int value) => System.Math.Abs(value);", "Absolute$$(-1)")]
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

    [Fact]
    public async Task DoesNotInlineWhenAnArgumentRequiresConversion()
    {
        const string source = """
            public static class Calculator
            {
                private static long Double(long value) => value * 2;
                public static long Calculate(int value) => Double$$(value);
            }
            """;

        Assert.Empty(await GetActionsAsync(source));
    }

    [Fact]
    public async Task DoesNotInlineWhenTheReturnExpressionRequiresConversion()
    {
        const string source = """
            public static class Calculator
            {
                private static object Box(string value) => value;
                public static object Calculate(string value) => Box$$(value);
            }
            """;

        Assert.Empty(await GetActionsAsync(source));
    }

    [Fact]
    public async Task OffersForASelectedMethodName()
    {
        var (workspace, document, cursor) = CreateDocument("""
            public static class Calculator
            {
                private static int Identity(int value) => value;
                public static int Calculate(int value) => $$Identity(value);
            }
            """);
        using (workspace)
        {
            var actions = new List<CodeAction>();
            await new InlineMethodCodeRefactoringProvider().ComputeRefactoringsAsync(
                new CodeRefactoringContext(
                    document,
                    new TextSpan(cursor, "Identity".Length),
                    actions.Add,
                    CancellationToken.None));
            Assert.Single(actions);
        }
    }

    [Fact]
    public async Task DoesNotOfferWhenOnlyAnArgumentIsSelected()
    {
        var (workspace, document, cursor) = CreateDocument("""
            public static class Calculator
            {
                private static int Identity(int value) => value;
                public static int Calculate(int value) => Identity($$value);
            }
            """);
        using (workspace)
        {
            var actions = new List<CodeAction>();
            await new InlineMethodCodeRefactoringProvider().ComputeRefactoringsAsync(
                new CodeRefactoringContext(
                    document,
                    new TextSpan(cursor, "value".Length),
                    actions.Add,
                    CancellationToken.None));
            Assert.Empty(actions);
        }
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
