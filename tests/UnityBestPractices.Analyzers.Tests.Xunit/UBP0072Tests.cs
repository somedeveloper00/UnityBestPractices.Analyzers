using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using UnityBestPractices.Analyzers;
using Xunit;

using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    UnityBestPractices.Analyzers.UnityBestPracticesAnalyzer,
    UnityBestPractices.Analyzers.UnityBestPracticesCodeFixProvider>;

public sealed class UBP0072Tests
{
    [Fact]
    public async Task ReportsProvablyUnusedPersistentAllocation()
    {
        var source = """
                using Unity.Collections;
                class Owner
                {
                    void Create()
                    {
                        var {|#0:values|} = new NativeArray<int>(8, Allocator.Persistent);
                    }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(
            source,
            new DiagnosticResult(DiagnosticIds.UndisposedPersistentNativeContainer, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("values"));
    }

    [Fact]
    public async Task ReportsExplicitlyTypedUnusedPersistentAllocation()
    {
        var source = """
                using Unity.Collections;
                class Owner
                {
                    void Create() { NativeArray<int> {|#0:values|} = new NativeArray<int>(1, Allocator.Persistent); }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(
            source,
            new DiagnosticResult(DiagnosticIds.UndisposedPersistentNativeContainer, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("values"));
    }

    [Fact]
    public async Task ReportsNamedAllocatorArgument()
    {
        var source = """
                using Unity.Collections;
                class Owner
                {
                    void Create() { var {|#0:values|} = new NativeArray<int>(length: 1, allocator: Allocator.Persistent); }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(
            source,
            new DiagnosticResult(DiagnosticIds.UndisposedPersistentNativeContainer, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("values"));
    }

    [Theory]
    [InlineData("Temp")]
    [InlineData("TempJob")]
    public async Task IgnoresTemporaryAllocator(string allocator)
    {
        var source = $$"""
                using Unity.Collections;
                class Owner { void Create() { var values = new NativeArray<int>(1, Allocator.{{allocator}}); } }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresPersistentAllocationThatIsDisposed()
    {
        var source = """
                using Unity.Collections;
                class Owner
                {
                    void Create()
                    {
                        var values = new NativeArray<int>(1, Allocator.Persistent);
                        values.Dispose();
                    }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresPersistentAllocationPassedElsewhere()
    {
        var source = """
                using Unity.Collections;
                class Owner
                {
                    void Consume(NativeArray<int> values) { }
                    void Create()
                    {
                        var values = new NativeArray<int>(1, Allocator.Persistent);
                        Consume(values);
                    }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresUsingDeclaration()
    {
        var source = """
                using Unity.Collections;
                class Owner
                {
                    void Create() { using var values = new NativeArray<int>(1, Allocator.Persistent); }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresMultipleDeclarators()
    {
        var source = """
                using Unity.Collections;
                class Owner
                {
                    void Create()
                    {
                        NativeArray<int> first = new NativeArray<int>(1, Allocator.Persistent), second = default;
                    }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }
}
