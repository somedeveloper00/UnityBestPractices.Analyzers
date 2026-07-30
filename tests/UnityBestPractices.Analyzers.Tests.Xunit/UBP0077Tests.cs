using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using UnityBestPractices.Analyzers;
using Xunit;

using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    UnityBestPractices.Analyzers.UnityBestPracticesAnalyzer,
    UnityBestPractices.Analyzers.UnityBestPracticesCodeFixProvider>;

public sealed class UBP0077Tests
{
    private const string Entities = """
        namespace Unity.Entities
        {
            public struct Entity { }
            public struct Item<T> { }
            public struct QueryEnumerable<T>
            {
                public QueryEnumerator<T> GetEnumerator() => default;
                public QueryEnumerableWithEntity<T> WithEntityAccess() => default;
            }
            public struct QueryEnumerator<T>
            {
                public bool MoveNext() => false;
                public Item<T> Current => default;
            }
            public struct QueryEnumerableWithEntity<T>
            {
                public QueryEnumeratorWithEntity<T> GetEnumerator() => default;
            }
            public struct QueryEnumeratorWithEntity<T>
            {
                public bool MoveNext() => false;
                public (Item<T>, Entity) Current => default;
            }
            public static class SystemAPI
            {
                public static QueryEnumerable<T> Query<T>() => default;
            }
        }
        """;

    [Fact]
    public async Task RemovesDiscardedEntityAndCollapsesSingleItemTuple()
    {
        var source = """
            class MySystem
            {
                void Update()
                {
                    foreach (var (item, {|#0:_|}) in Unity.Entities.SystemAPI.Query<int>().WithEntityAccess())
                        global::System.Console.WriteLine(item);
                }
            }
            """ + Entities;
        var fixedSource = """
            class MySystem
            {
                void Update()
                {
                    foreach (var item in Unity.Entities.SystemAPI.Query<int>())
                        global::System.Console.WriteLine(item);
                }
            }
            """ + Entities;

        await VerifyCS.VerifyCodeFixAsync(
            source,
            new DiagnosticResult(DiagnosticIds.RemoveUnusedEntityAccess, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0),
            fixedSource);
    }

    [Fact]
    public async Task RemovesNamedEntityWhenItIsNotReferenced()
    {
        var source = """
            class MySystem
            {
                void Update()
                {
                    foreach (var (item, {|#0:entity|}) in Unity.Entities.SystemAPI.Query<int>().WithEntityAccess())
                        global::System.Console.WriteLine(item);
                }
            }
            """ + Entities;
        var fixedSource = """
            class MySystem
            {
                void Update()
                {
                    foreach (var item in Unity.Entities.SystemAPI.Query<int>())
                        global::System.Console.WriteLine(item);
                }
            }
            """ + Entities;

        await VerifyCS.VerifyCodeFixAsync(
            source,
            new DiagnosticResult(DiagnosticIds.RemoveUnusedEntityAccess, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0),
            fixedSource);
    }

    [Fact]
    public async Task KeepsEntityAccessWhenEntityIsReferenced()
    {
        var source = """
            class MySystem
            {
                void Update()
                {
                    foreach (var (item, entity) in Unity.Entities.SystemAPI.Query<int>().WithEntityAccess())
                        global::System.Console.WriteLine(entity);
                }
            }
            """ + Entities;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresUnrelatedMethodNamedWithEntityAccess()
    {
        var source = """
            class Query
            {
                public Query WithEntityAccess() => this;
                public Enumerator GetEnumerator() => default;
                public struct Enumerator
                {
                    public bool MoveNext() => false;
                    public (int, int) Current => default;
                }
            }
            class MySystem
            {
                void Update()
                {
                    foreach (var (item, _) in new Query().WithEntityAccess()) { }
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }
}
