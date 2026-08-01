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

public sealed class MoveParameterCodeRefactoringProviderTests
{
    [Fact]
    public async Task RemoveParameterUpdatesRelatedDeclarationsAndCallSites()
    {
        var declaration = """
            public interface IHandler
            {
                string Handle(string text, int cou$$nt, bool enabled);
            }

            public sealed class Handler : IHandler
            {
                public string Handle(string text, int count, bool enabled) => text + enabled;
            }
            """;
        var calls = """
            public sealed class Caller
            {
                public string Run(IHandler abstraction, Handler concrete)
                {
                    return abstraction.Handle("interface", 1, true) +
                           concrete.Handle(text: "concrete", count: 2, enabled: false);
                }
            }
            """;

        var changed = await ApplyRefactoringAsync(
            declaration,
            calls,
            RemoveParameterCodeRefactoringProvider.Title,
            new RemoveParameterCodeRefactoringProvider());

        AssertDocument(
            changed,
            "Declaration.cs",
            """
            public interface IHandler
            {
                string Handle(string text, bool enabled);
            }

            public sealed class Handler : IHandler
            {
                public string Handle(string text, bool enabled) => text + enabled;
            }
            """);
        AssertDocument(
            changed,
            "Calls.cs",
            """
            public sealed class Caller
            {
                public string Run(IHandler abstraction, Handler concrete)
                {
                    return abstraction.Handle("interface", true) +
                           concrete.Handle(text: "concrete", enabled: false);
                }
            }
            """);
    }

    [Fact]
    public async Task RemoveParamsParameterRemovesEveryExpandedArgument()
    {
        var declaration = """
            public static class Logger
            {
                public static void Write(string message, params object[] ar$$gs) { }
            }
            """;
        var calls = """
            public sealed class Caller
            {
                public void Run() => Logger.Write("message", 1, "two", true);
            }
            """;

        var changed = await ApplyRefactoringAsync(
            declaration,
            calls,
            RemoveParameterCodeRefactoringProvider.Title,
            new RemoveParameterCodeRefactoringProvider());

        AssertDocument(
            changed,
            "Declaration.cs",
            """
            public static class Logger
            {
                public static void Write(string message) { }
            }
            """);
        AssertDocument(
            changed,
            "Calls.cs",
            """
            public sealed class Caller
            {
                public void Run() => Logger.Write("message");
            }
            """);
    }

    [Fact]
    public async Task RemoveParameterAllowsUsedBurstMethodParameter()
    {
        var declaration = """
            using System;

            [AttributeUsage(AttributeTargets.Method)]
            public sealed class BurstCompileAttribute : Attribute { }

            public struct Random
            {
                public float NextFloat(float min, float max) => min;
            }

            public sealed class Spawner
            {
                private float nextSpawnTime;
                private float spawnRateMin;
                private float spawnRateMax;

                [BurstCompile]
                public void UpdateNextSpawnTime(float ti$$me, ref Random random) =>
                    nextSpawnTime = time + random.NextFloat(spawnRateMin, spawnRateMax);
            }
            """;
        var calls = """
            public sealed class Caller
            {
                public void Run(Spawner spawner, ref Random random) =>
                    spawner.UpdateNextSpawnTime(1f, ref random);
            }
            """;

        var changed = await ApplyRefactoringAsync(
            declaration,
            calls,
            RemoveParameterCodeRefactoringProvider.Title,
            new RemoveParameterCodeRefactoringProvider(),
            expectedErrorId: "CS0103");

        AssertDocument(
            changed,
            "Declaration.cs",
            """
            using System;

            [AttributeUsage(AttributeTargets.Method)]
            public sealed class BurstCompileAttribute : Attribute { }

            public struct Random
            {
                public float NextFloat(float min, float max) => min;
            }

            public sealed class Spawner
            {
                private float nextSpawnTime;
                private float spawnRateMin;
                private float spawnRateMax;

                [BurstCompile]
                public void UpdateNextSpawnTime(ref Random random) =>
                    nextSpawnTime = time + random.NextFloat(spawnRateMin, spawnRateMax);
            }
            """);
        AssertDocument(
            changed,
            "Calls.cs",
            """
            public sealed class Caller
            {
                public void Run(Spawner spawner, ref Random random) =>
                    spawner.UpdateNextSpawnTime(ref random);
            }
            """);
    }

    [Fact]
    public async Task RemoveParameterLeavesSolutionUnchangedForMethodGroupReference()
    {
        var declaration = """
            public static class Formatter
            {
                public static string Format(string text, int cou$$nt) => text;
            }
            """;
        var calls = """
            using System;

            public sealed class Caller
            {
                public Func<string, int, string> GetFormatter() => Formatter.Format;
            }
            """;

        var changed = await ApplyRefactoringAsync(
            declaration,
            calls,
            RemoveParameterCodeRefactoringProvider.Title,
            new RemoveParameterCodeRefactoringProvider());

        AssertDocument(
            changed,
            "Declaration.cs",
            """
            public static class Formatter
            {
                public static string Format(string text, int count) => text;
            }
            """);
        AssertDocument(changed, "Calls.cs", calls);
    }

    [Fact]
    public async Task RemoveParameterAllowsEventSubscriptionMethodGroupReference()
    {
        var declaration = """
            using System;

            public sealed class RiddleOfTheSphinx
            {
                public event Action<bool> onClose = delegate { };
            }

            public sealed class Page
            {
                private readonly RiddleOfTheSphinx riddleOfTheSphinx = new RiddleOfTheSphinx();

                public void Attach() => riddleOfTheSphinx.onClose += OnRiddleClosed;

                private void OnRiddleClosed(bool clicked$$No) => NextPage();

                private void NextPage() { }
            }
            """;
        var calls = "public sealed class Other { }";

        var changed = await ApplyRefactoringAsync(
            declaration,
            calls,
            RemoveParameterCodeRefactoringProvider.Title,
            new RemoveParameterCodeRefactoringProvider(),
            expectedErrorId: "CS0123");

        AssertDocument(
            changed,
            "Declaration.cs",
            """
            using System;

            public sealed class RiddleOfTheSphinx
            {
                public event Action<bool> onClose = delegate { };
            }

            public sealed class Page
            {
                private readonly RiddleOfTheSphinx riddleOfTheSphinx = new RiddleOfTheSphinx();

                public void Attach() => riddleOfTheSphinx.onClose += OnRiddleClosed;

                private void OnRiddleClosed() => NextPage();

                private void NextPage() { }
            }
            """);
        AssertDocument(changed, "Calls.cs", calls);
    }

    [Fact]
    public async Task MoveRightUpdatesOnlySemanticallyMatchingCallsAcrossDocuments()
    {
        var declaration = """
            public static class Handler
            {
                public static string HandleThis(string ar$$g1, int arg2) => arg1 + arg2;
                public static string HandleThis(string arg1, double arg2) => arg1 + arg2;
            }
            """;
        var calls = """
            public sealed class Caller
            {
                public string Run()
                {
                    var first = Handler.HandleThis("value", 42);
                    var second = Handler.HandleThis(arg1: "named", arg2: 7);
                    var nested = Handler.HandleThis(Handler.HandleThis("inner", 1), 2);
                    var otherOverload = Handler.HandleThis("other", 1.5);
                    return first + second + nested + otherOverload;
                }
            }
            """;

        var changed = await ApplyRefactoringAsync(
            declaration,
            calls,
            MoveParameterCodeRefactoringProvider.MoveRightTitle);

        AssertDocument(
            changed,
            "Declaration.cs",
            """
            public static class Handler
            {
                public static string HandleThis(int arg2, string arg1) => arg1 + arg2;
                public static string HandleThis(string arg1, double arg2) => arg1 + arg2;
            }
            """);
        AssertDocument(
            changed,
            "Calls.cs",
            """
            public sealed class Caller
            {
                public string Run()
                {
                    var first = Handler.HandleThis(42, "value");
                    var second = Handler.HandleThis(arg2: 7, arg1: "named");
                    var nested = Handler.HandleThis(2, Handler.HandleThis(1, "inner"));
                    var otherOverload = Handler.HandleThis("other", 1.5);
                    return first + second + nested + otherOverload;
                }
            }
            """);
    }

    [Fact]
    public async Task MoveLeftUpdatesInterfaceImplementationAndBothCallShapes()
    {
        var declaration = """
            public interface IHandler
            {
                string Handle(string text, int cou$$nt);
            }

            public sealed class Handler : IHandler
            {
                public string Handle(string text, int count) => text + count;
            }
            """;
        var calls = """
            public sealed class Caller
            {
                public string Run(IHandler abstraction, Handler concrete)
                {
                    return abstraction.Handle("interface", 1) +
                           concrete.Handle("implementation", 2);
                }
            }
            """;

        var changed = await ApplyRefactoringAsync(
            declaration,
            calls,
            MoveParameterCodeRefactoringProvider.MoveLeftTitle);

        AssertDocument(
            changed,
            "Declaration.cs",
            """
            public interface IHandler
            {
                string Handle(int count, string text);
            }

            public sealed class Handler : IHandler
            {
                public string Handle(int count, string text) => text + count;
            }
            """);
        AssertDocument(
            changed,
            "Calls.cs",
            """
            public sealed class Caller
            {
                public string Run(IHandler abstraction, Handler concrete)
                {
                    return abstraction.Handle(1, "interface") +
                           concrete.Handle(2, "implementation");
                }
            }
            """);
    }

    [Fact]
    public async Task MoveLeftUpdatesReducedAndStaticExtensionCalls()
    {
        var declaration = """
            public static class TextExtensions
            {
                public static string Repeat(
                    this string text,
                    string separator,
                    int cou$$nt) => text;
            }
            """;
        var calls = """
            public sealed class Caller
            {
                public string Run()
                {
                    var reduced = "x".Repeat(",", 3);
                    var normal = TextExtensions.Repeat("y", "-", 4);
                    return reduced + normal;
                }
            }
            """;

        var changed = await ApplyRefactoringAsync(
            declaration,
            calls,
            MoveParameterCodeRefactoringProvider.MoveLeftTitle);

        AssertDocument(
            changed,
            "Declaration.cs",
            """
            public static class TextExtensions
            {
                public static string Repeat(
                    this string text,
                    int count,
                    string separator) => text;
            }
            """);
        AssertDocument(
            changed,
            "Calls.cs",
            """
            public sealed class Caller
            {
                public string Run()
                {
                    var reduced = "x".Repeat(3, ",");
                    var normal = TextExtensions.Repeat("y", 4, "-");
                    return reduced + normal;
                }
            }
            """);
    }

    [Fact]
    public async Task MoveRightUpdatesObjectCreationAndConstructorInitializers()
    {
        var declaration = """
            public class Base
            {
                public Base(string te$$xt, int count) { }
            }

            public sealed class Derived : Base
            {
                public Derived() : base("base", 1) { }
            }
            """;
        var calls = """
            public sealed class Caller
            {
                public Base Create() => new Base("created", 2);
            }
            """;

        var changed = await ApplyRefactoringAsync(
            declaration,
            calls,
            MoveParameterCodeRefactoringProvider.MoveRightTitle);

        AssertDocument(
            changed,
            "Declaration.cs",
            """
            public class Base
            {
                public Base(int count, string text) { }
            }

            public sealed class Derived : Base
            {
                public Derived() : base(1, "base") { }
            }
            """);
        AssertDocument(
            changed,
            "Calls.cs",
            """
            public sealed class Caller
            {
                public Base Create() => new Base(2, "created");
            }
            """);
    }

    [Fact]
    public async Task MoveRightNamesAnArgumentWhenAnOptionalParameterWasOmitted()
    {
        var declaration = """
            public static class Configuration
            {
                public static string Create(
                    string te$$xt = "default",
                    int count = 1) => text + count;
            }
            """;
        var calls = """
            public sealed class Caller
            {
                public string Create() => Configuration.Create("custom");
            }
            """;

        var changed = await ApplyRefactoringAsync(
            declaration,
            calls,
            MoveParameterCodeRefactoringProvider.MoveRightTitle);

        AssertDocument(
            changed,
            "Declaration.cs",
            """
            public static class Configuration
            {
                public static string Create(
                    int count = 1,
                    string text = "default") => text + count;
            }
            """);
        AssertDocument(
            changed,
            "Calls.cs",
            """
            public sealed class Caller
            {
                public string Create() => Configuration.Create(text: "custom");
            }
            """);
    }

    private static async Task<Solution> ApplyRefactoringAsync(
        string declarationWithCursor,
        string calls,
        string title,
        CodeRefactoringProvider? provider = null,
        string? expectedErrorId = null)
    {
        var cursor = declarationWithCursor.IndexOf("$$", StringComparison.Ordinal);
        Assert.True(cursor >= 0, "The declaration source must contain a $$ cursor marker.");
        var declaration = declarationWithCursor.Remove(cursor, 2);

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var declarationId = DocumentId.CreateNewId(projectId);
        var callsId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "RefactoringTest", "RefactoringTest", LanguageNames.CSharp)
            .WithProjectCompilationOptions(
                projectId,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.CSharp9));

        foreach (var reference in GetPlatformReferences())
        {
            solution = solution.AddMetadataReference(projectId, reference);
        }

        solution = solution
            .AddDocument(declarationId, "Declaration.cs", SourceText.From(declaration))
            .AddDocument(callsId, "Calls.cs", SourceText.From(calls));
        var document = solution.GetDocument(declarationId)
            ?? throw new InvalidOperationException("Could not create the declaration document.");
        var actions = new List<CodeAction>();
        var context = new CodeRefactoringContext(
            document,
            new TextSpan(cursor, 0),
            actions.Add,
            CancellationToken.None);

        var refactoringProvider = provider ?? new MoveParameterCodeRefactoringProvider();
        await refactoringProvider.ComputeRefactoringsAsync(context);
        var action = Assert.Single(actions, candidate => candidate.EquivalenceKey == title);
        if (refactoringProvider is MoveParameterCodeRefactoringProvider)
        {
            Assert.StartsWith(
                title == MoveParameterCodeRefactoringProvider.MoveLeftTitle ? "Inline " : "Extract ",
                action.Title);
        }
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changedSolution = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution;
        var compilation = await changedSolution.GetProject(projectId)!.GetCompilationAsync();
        var errors = compilation!.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (expectedErrorId is null)
        {
            Assert.True(
                errors.Length == 0,
                string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
        }
        else
        {
            Assert.Contains(errors, error => error.Id == expectedErrorId);
        }

        return changedSolution;
    }

    private static IEnumerable<MetadataReference> GetPlatformReferences()
    {
        var trustedAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("The runtime did not provide platform assemblies.");
        return trustedAssemblies
            .Split(System.IO.Path.PathSeparator)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
    }

    private static void AssertDocument(Solution solution, string name, string expected)
    {
        var document = solution.Projects.Single().Documents.Single(candidate => candidate.Name == name);
        var actualRoot = document.GetSyntaxRootAsync().GetAwaiter().GetResult()
            ?? throw new InvalidOperationException($"Document '{name}' did not have a syntax root.");
        var expectedRoot = CSharpSyntaxTree.ParseText(expected).GetRoot();
        Assert.Equal(
            expectedRoot.NormalizeWhitespace().ToFullString(),
            actualRoot.NormalizeWhitespace().ToFullString());
    }
}
