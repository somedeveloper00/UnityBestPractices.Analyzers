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

public sealed class ConvertLocalToFieldCodeRefactoringProviderTests
{
    [Theory]
    [MemberData(nameof(ValidConversions))]
    public async Task ConvertsLocalInSupportedTypeAndMemberContexts(string source, string expected)
    {
        var changed = await ApplyAsync(source);
        Assert.Equal(Normalize(expected), Normalize(changed));
    }

    public static IEnumerable<object[]> ValidConversions()
    {
        yield return Case(
            "public class C { int M() { int $$value = 42; return value; } }",
            "public class C { private int value; int M() { value = 42; return value; } }");
        yield return Case(
            "public static class C { static int M() { var $$value = 42; return value; } }",
            "public static class C { private static int value; static int M() { value = 42; return value; } }");
        yield return Case(
            "public struct S { int M() { string $$text = \"ok\"; return text.Length; } }",
            "public struct S { private string text; int M() { text = \"ok\"; return text.Length; } }");
        yield return Case(
            "public struct S { static int M() { int $$count = 1; return count; } }",
            "public struct S { private static int count; static int M() { count = 1; return count; } }");
        yield return Case(
            "public class C { public C(int input) { int $$value = input; Use(value); } void Use(int x) {} }",
            "public class C { private int value; public C(int input) { value = input; Use(value); } void Use(int x) {} }");
        yield return Case(
            "public class C { int M(int input) { { int $$nested = input; return nested; } } }",
            "public class C { private int nested; int M(int input) { { nested = input; return nested; } } }");
        yield return Case(
            "public class C<T> { T M(T input) { var $$item = input; return item; } }",
            "public class C<T> { private T item; T M(T input) { item = input; return item; } }");
        yield return Case(
            "public class C { void M() { int $$value; value = 3; } }",
            "public class C { private int value; void M() { value = 3; } }");
        yield return Case(
            "public class C { static int P { get { int $$value = 2; return value; } } }",
            "public class C { private static int value; static int P { get { value = 2; return value; } } }");
        yield return Case(
            "public class C { static int M() { int Local() { int $$value = 2; return value; } return Local(); } }",
            "public class C { private static int value; static int M() { int Local() { value = 2; return value; } return Local(); } }");
        yield return Case(
            "public class C { int M() { for (int i = 0; i < 1; i++) { int $$result = i; return result; } return 0; } }",
            "public class C { private int result; int M() { for (int i = 0; i < 1; i++) { result = i; return result; } return 0; } }");
        yield return Case(
            "public record C { public int M() { int $$answer = 42; return answer; } }",
            "public record C { private int answer; public int M() { answer = 42; return answer; } }");
        yield return Case(
            "using System; public class C { int M() { Func<int> f = () => { int $$value = 1; return value; }; return f(); } }",
            "using System; public class C { private int value; int M() { Func<int> f = () => { value = 1; return value; }; return f(); } }");
        yield return Case(
            "using System; public class C { static int M() { Func<int> f = () => { int $$value = 1; return value; }; return f(); } }",
            "using System; public class C { private static int value; static int M() { Func<int> f = () => { value = 1; return value; }; return f(); } }");
        yield return Case(
            "public unsafe class C { int M() { int* $$pointer = null; return pointer == null ? 0 : *pointer; } }",
            "public unsafe class C { private unsafe int* pointer; int M() { pointer = null; return pointer == null ? 0 : *pointer; } }");
    }

    [Theory]
    [MemberData(nameof(InvalidConversions))]
    public async Task DoesNotOfferWhenAFieldWouldBeInvalidOrUnsafe(string source)
    {
        Assert.Empty(await GetActionsAsync(source));
    }

    public static IEnumerable<object[]> InvalidConversions()
    {
        yield return Single("public class C { void M() { const int $$value = 1; } }");
        yield return Single("public class C { int value; void M() { int $$value = 1; } }");
        yield return Single("public class C { void M() { int $$first = 1, second = 2; } }");
        yield return Single("public readonly struct S { int M() { int $$value = 1; return value; } }");
        yield return Single("public struct S { readonly int M() { int $$value = 1; return value; } }");
        yield return Single("using System; public class C { void M() { using IDisposable $$value = null; } }");
        yield return Single("public class C { void M() { ref int $$alias = ref Get(); } ref int Get() => throw null; }");
        yield return Single("int $$value = 1; System.Console.WriteLine(value);");
        yield return Single("using System; public class C { void M() { Span<int> $$span = stackalloc int[1]; } }");
    }

    [Fact]
    public async Task OffersWhenTheVariableNameIsSelected()
    {
        var (workspace, document, cursor) = CreateDocument("public class C { void M() { int $$value = 1; } }");
        using (workspace)
        {
            var actions = new List<CodeAction>();
            await new ConvertLocalToFieldCodeRefactoringProvider().ComputeRefactoringsAsync(
                new CodeRefactoringContext(document, new TextSpan(cursor, "value".Length), actions.Add, CancellationToken.None));
            Assert.Single(actions);
        }
    }

    [Fact]
    public async Task DoesNotOfferWhenCursorIsOnTheInitializer()
    {
        Assert.Empty(await GetActionsAsync("public class C { void M() { int value = $$Get(); } int Get() => 1; }"));
    }

    private static object[] Case(string source, string expected) => new object[] { source, expected };
    private static object[] Single(string source) => new object[] { source };

    private static async Task<string> ApplyAsync(string source)
    {
        var (workspace, document, cursor) = CreateDocument(source);
        using (workspace)
        {
            var action = Assert.Single(await GetActionsAsync(document, cursor));
            Assert.Equal(ConvertLocalToFieldCodeRefactoringProvider.Title, action.Title);
            var operations = await action.GetOperationsAsync(CancellationToken.None);
            var changed = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution.GetDocument(document.Id)!;
            var compilation = await changed.Project.GetCompilationAsync();
            Assert.Empty(compilation!.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            return (await changed.GetTextAsync()).ToString();
        }
    }

    private static async Task<IReadOnlyList<CodeAction>> GetActionsAsync(string source)
    {
        var (workspace, document, cursor) = CreateDocument(source);
        using (workspace) { return await GetActionsAsync(document, cursor); }
    }

    private static async Task<IReadOnlyList<CodeAction>> GetActionsAsync(Document document, int cursor)
    {
        var actions = new List<CodeAction>();
        await new ConvertLocalToFieldCodeRefactoringProvider().ComputeRefactoringsAsync(
            new CodeRefactoringContext(document, new TextSpan(cursor, 0), actions.Add, CancellationToken.None));
        return actions;
    }

    private static (AdhocWorkspace Workspace, Document Document, int Cursor) CreateDocument(string source)
    {
        var cursor = source.IndexOf("$$", StringComparison.Ordinal);
        Assert.True(cursor >= 0);
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("ConvertLocalToFieldTest", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true))
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.CSharp9));
        foreach (var reference in GetPlatformReferences()) project = project.AddMetadataReference(reference);
        Assert.True(workspace.TryApplyChanges(project.Solution));
        return (workspace, workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source.Remove(cursor, 2))), cursor);
    }

    private static IEnumerable<MetadataReference> GetPlatformReferences() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(System.IO.Path.PathSeparator)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));

    private static string Normalize(string source) =>
        CSharpSyntaxTree.ParseText(source).GetRoot().NormalizeWhitespace().ToFullString();
}
