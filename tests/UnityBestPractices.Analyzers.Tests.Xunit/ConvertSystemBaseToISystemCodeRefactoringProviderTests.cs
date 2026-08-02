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

public sealed class ConvertSystemBaseToISystemCodeRefactoringProviderTests
{
    private const string UnityTypes = """
        namespace Unity.Entities
        {
            public struct SystemState
            {
                public EntityManager EntityManager;
                public World World;
                public object Dependency { get; set; }
                public bool Enabled { get; set; }
                public uint LastSystemVersion => 0;
                public void RequireForUpdate<T>() { }
                public void RequireForUpdate(EntityQuery query) { }
                public EntityQuery GetEntityQuery(object builder) => default;
                public void CompleteDependency() { }
                public bool ShouldRunSystem() => true;
            }
            public struct EntityManager { }
            public struct EntityQuery { }
            public sealed class World { }
            public interface ISystem { }
            public abstract class SystemBase
            {
                protected EntityManager EntityManager => default;
                protected World World => default;
                protected object Dependency { get; set; }
                protected bool Enabled { get; set; }
                protected uint LastSystemVersion => 0;
                protected void RequireForUpdate<T>() { }
                protected void RequireForUpdate(EntityQuery query) { }
                protected EntityQuery GetEntityQuery(object builder) => default;
                protected void CompleteDependency() { }
                protected bool ShouldRunSystem() => true;
                protected virtual void OnCreate() { }
                protected virtual void OnUpdate() { }
                protected virtual void OnDestroy() { }
            }
        }
        """;

    [Fact]
    public async Task ConvertsAFieldlessSystemAndItsLifecycleMethods()
    {
        var changed = await ApplyAsync("""
            using Unity.Entities;
            partial class $$MovementSystem : SystemBase
            {
                protected override void OnCreate() { }
                protected override void OnUpdate() { }
                protected override void OnDestroy() { }
            }
            """);

        Assert.Contains("partial struct MovementSystem : ISystem", Normalize(changed));
        Assert.Contains("public void OnCreate(ref SystemState state)", Normalize(changed));
        Assert.Contains("public void OnUpdate(ref SystemState state)", Normalize(changed));
        Assert.Contains("public void OnDestroy(ref SystemState state)", Normalize(changed));
    }

    [Fact]
    public async Task RewritesEntityManagerWorldAndQuerySetupInLifecycleMethods()
    {
        var changed = Normalize(await ApplyAsync("""
            using Unity.Entities;
            partial class $$SpawnerSystem : SystemBase
            {
                protected override void OnCreate()
                {
                    var builder = new object();
                    RequireForUpdate(GetEntityQuery(builder));
                    RequireForUpdate<int>();
                }

                protected override void OnUpdate()
                {
                    Use(EntityManager, World);
                }

                private static void Use(EntityManager entityManager, World world) { }
            }
            """));

        Assert.Contains("state.RequireForUpdate(state.GetEntityQuery(builder));", changed);
        Assert.Contains("state.RequireForUpdate<int>();", changed);
        Assert.Contains("Use(state.EntityManager, state.World);", changed);
    }

    [Fact]
    public async Task RewritesDependencyAndOtherDirectSystemStateMembers()
    {
        var changed = Normalize(await ApplyAsync("""
            using Unity.Entities;
            partial class $$SchedulingSystem : SystemBase
            {
                protected override void OnUpdate()
                {
                    Dependency = Schedule(Dependency);
                    Enabled = ShouldRunSystem();
                    var version = LastSystemVersion;
                    CompleteDependency();
                }

                private static object Schedule(object dependency) => dependency;
            }
            """));

        Assert.Contains("state.Dependency = Schedule(state.Dependency);", changed);
        Assert.Contains("state.Enabled = state.ShouldRunSystem();", changed);
        Assert.Contains("var version = state.LastSystemVersion;", changed);
        Assert.Contains("state.CompleteDependency();", changed);
    }

    [Fact]
    public async Task RewritesExplicitThisAccessAndLeavesUnrelatedLocalsAlone()
    {
        var changed = Normalize(await ApplyAsync("""
            using Unity.Entities;
            partial class $$ExplicitAccessSystem : SystemBase
            {
                protected override void OnUpdate()
                {
                    var manager = this.EntityManager;
                    var worldNumber = 42;
                    Use(worldNumber, this.World);
                }

                private static void Use(int value, World world) { }
            }
            """));

        Assert.Contains("var manager = state.EntityManager;", changed);
        Assert.Contains("Use(worldNumber, state.World);", changed);
    }

    [Theory]
    [InlineData("private int count;")]
    [InlineData("private static int count;")]
    [InlineData("public int Count { get; set; }")]
    public async Task DoesNotOfferWhenTheSystemContainsInstanceState(string member)
    {
        Assert.Empty(await GetActionsAsync($$"""
            using Unity.Entities;
            partial class $$StatefulSystem : SystemBase
            {
                {{member}}
                protected override void OnUpdate() { }
            }
            """));
    }

    [Fact]
    public async Task DoesNotOfferForAnUnrelatedSystemBaseType()
    {
        Assert.Empty(await GetActionsAsync("""
            class SystemBase { }
            class $$Example : SystemBase { }
            """));
    }

    [Fact]
    public async Task DoesNotOfferForUnsupportedSystemBaseOverrides()
    {
        Assert.Empty(await GetActionsAsync("""
            using Unity.Entities;
            partial class $$Example : SystemBase
            {
                protected override void OnStartRunning() { }
            }
            """, "protected virtual void OnStartRunning() { }"));
    }

    private static async Task<string> ApplyAsync(string source)
    {
        var (workspace, document, span) = CreateDocument(source);
        using (workspace)
        {
            var action = Assert.Single(await GetActionsAsync(document, span));
            Assert.Equal(ConvertSystemBaseToISystemCodeRefactoringProvider.Title, action.Title);
            var operations = await action.GetOperationsAsync(CancellationToken.None);
            var solution = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution;
            var changed = solution.GetDocument(document.Id)!;
            var compilation = await changed.Project.GetCompilationAsync();
            Assert.Empty(compilation!.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));
            return (await changed.GetTextAsync()).ToString();
        }
    }

    private static async Task<IReadOnlyList<CodeAction>> GetActionsAsync(
        string source,
        string additionalSystemBaseMember = "")
    {
        var (workspace, document, span) = CreateDocument(source, additionalSystemBaseMember);
        using (workspace)
        {
            return await GetActionsAsync(document, span);
        }
    }

    private static async Task<IReadOnlyList<CodeAction>> GetActionsAsync(Document document, TextSpan span)
    {
        var actions = new List<CodeAction>();
        await new ConvertSystemBaseToISystemCodeRefactoringProvider().ComputeRefactoringsAsync(
            new CodeRefactoringContext(document, span, actions.Add, CancellationToken.None));
        return actions;
    }

    private static (AdhocWorkspace Workspace, Document Document, TextSpan Span) CreateDocument(
        string markedSource,
        string additionalSystemBaseMember = "")
    {
        var cursor = markedSource.IndexOf("$$", StringComparison.Ordinal);
        Assert.True(cursor >= 0);
        var source = markedSource.Remove(cursor, 2);
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("SystemConversionTest", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.CSharp9));
        foreach (var reference in GetPlatformReferences())
        {
            project = project.AddMetadataReference(reference);
        }

        Assert.True(workspace.TryApplyChanges(project.Solution));
        workspace.AddDocument(project.Id, "Unity.cs", SourceText.From(
            UnityTypes.Replace("protected virtual void OnDestroy() { }", "protected virtual void OnDestroy() { } " + additionalSystemBaseMember)));
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));
        return (workspace, document, new TextSpan(cursor, 0));
    }

    private static IEnumerable<MetadataReference> GetPlatformReferences() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(System.IO.Path.PathSeparator)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(path => MetadataReference.CreateFromFile(path));

    private static string Normalize(string source) =>
        CSharpSyntaxTree.ParseText(source).GetRoot().NormalizeWhitespace().ToFullString();
}
