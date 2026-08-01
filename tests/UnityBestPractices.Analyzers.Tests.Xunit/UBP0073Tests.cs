using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using UnityBestPractices.Analyzers;
using Xunit;

using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    UnityBestPractices.Analyzers.UnityBestPracticesAnalyzer,
    UnityBestPractices.Analyzers.UnityBestPracticesCodeFixProvider>;

public sealed class UBP0073Tests
{
    [Fact]
    public async Task ReportsDirectTemporaryAllocationReturn()
    {
        var source = """
                using Unity.Collections;
                class Owner
                {
                    NativeArray<int> Create()
                    {
                        return {|#0:new NativeArray<int>(8, Allocator.TempJob)|};
                    }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(
            source,
            new DiagnosticResult(DiagnosticIds.InvalidTemporaryAllocatorEscape, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("TempJob"));
    }

    [Theory]
    [InlineData("Temp")]
    [InlineData("TempJob")]
    public async Task ReportsTemporaryLocalReturned(string allocator)
    {
        var source = $$"""
                using Unity.Collections;
                class Owner
                {
                    NativeArray<int> Create()
                    {
                        var values = new NativeArray<int>(8, Allocator.{{allocator}});
                        return {|#0:values|};
                    }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(
            source,
            new DiagnosticResult(DiagnosticIds.InvalidTemporaryAllocatorEscape, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments(allocator));
    }

    [Fact]
    public async Task ReportsTemporaryAllocationStoredInField()
    {
        var source = """
                using Unity.Collections;
                class Owner
                {
                    NativeArray<int> values;
                    void Create() { values = {|#0:new NativeArray<int>(8, Allocator.Temp)|}; }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(
            source,
            new DiagnosticResult(DiagnosticIds.InvalidTemporaryAllocatorEscape, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("Temp"));
    }

    [Fact]
    public async Task ReportsTemporaryLocalStoredInProperty()
    {
        var source = """
                using Unity.Collections;
                class Owner
                {
                    NativeArray<int> Values { get; set; }
                    void Create()
                    {
                        var local = new NativeArray<int>(8, Allocator.TempJob);
                        Values = {|#0:local|};
                    }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(
            source,
            new DiagnosticResult(DiagnosticIds.InvalidTemporaryAllocatorEscape, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("TempJob"));
    }

    [Theory]
    [InlineData("Temp")]
    [InlineData("TempJob")]
    public async Task IgnoresTemporaryAllocationPassedToJob(string allocator)
    {
        var source = $$"""
                using Unity.Collections;
                using Unity.Burst;
                using Unity.Jobs;
                class Owner
                {
                    void Create()
                    {
                        var handle = new WorkJob
                        {
                            Values = new NativeArray<int>(8, Allocator.{{allocator}})
                        }.Schedule();
                    }
                }
                [BurstCompile]
                struct WorkJob : IJob
                {
                    public NativeArray<int> Values;
                    public void Execute() { }
                }
                """ + "\n" + TestSources.Collections + "\n" + TestSources.Jobs;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresTemporaryLocalAssignedToJobField()
    {
        var source = """
                using Unity.Collections;
                using Unity.Burst;
                using Unity.Jobs;
                class Owner
                {
                    void Create()
                    {
                        var values = new NativeArray<int>(8, Allocator.TempJob);
                        var job = new WorkJob();
                        job.Values = values;
                        var handle = job.Schedule();
                    }
                }
                [BurstCompile]
                struct WorkJob : IJob
                {
                    public NativeArray<int> Values;
                    public void Execute() { }
                }
                """ + "\n" + TestSources.Collections + "\n" + TestSources.Jobs;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsTemporaryAllocationStoredInJobProperty()
    {
        var source = """
                using Unity.Collections;
                using Unity.Burst;
                using Unity.Jobs;
                class Owner
                {
                    void Create()
                    {
                        var job = new WorkJob
                        {
                            Values = {|#0:new NativeArray<int>(8, Allocator.TempJob)|}
                        };
                    }
                }
                [BurstCompile]
                struct WorkJob : IJob
                {
                    public NativeArray<int> Values { get; set; }
                    public void Execute() { }
                }
                """ + "\n" + TestSources.Collections + "\n" + TestSources.Jobs;
        await VerifyCS.VerifyAnalyzerAsync(
            source,
            new DiagnosticResult(DiagnosticIds.InvalidTemporaryAllocatorEscape, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("TempJob"));
    }

    [Fact]
    public async Task ReportsTemporaryCapturedByReturnedLambda()
    {
        var source = """
                using System;
                using Unity.Collections;
                class Owner
                {
                    Func<int> Create()
                    {
                        var values = new NativeArray<int>(8, Allocator.Temp);
                        return {|#0:() => values.Length|};
                    }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(
            source,
            new DiagnosticResult(DiagnosticIds.InvalidTemporaryAllocatorEscape, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("Temp"));
    }

    [Fact]
    public async Task ReportsTemporaryCapturedByDelegateStoredInField()
    {
        var source = """
                using System;
                using Unity.Collections;
                class Owner
                {
                    Func<int> callback;
                    void Create()
                    {
                        var values = new NativeArray<int>(8, Allocator.TempJob);
                        callback = {|#0:() => values.Length|};
                    }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(
            source,
            new DiagnosticResult(DiagnosticIds.InvalidTemporaryAllocatorEscape, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("TempJob"));
    }

    [Fact]
    public async Task IgnoresPersistentAllocationReturn()
    {
        var source = """
                using Unity.Collections;
                class Owner
                {
                    NativeArray<int> Create() => new NativeArray<int>(8, Allocator.Persistent);
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresTemporaryLocalReassignedBeforeReturn()
    {
        var source = """
                using Unity.Collections;
                class Owner
                {
                    NativeArray<int> Create()
                    {
                        var values = new NativeArray<int>(8, Allocator.Temp);
                        values = new NativeArray<int>(8, Allocator.Persistent);
                        return values;
                    }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresTemporaryCapturedByNonEscapingLocalDelegate()
    {
        var source = """
                using System;
                using Unity.Collections;
                class Owner
                {
                    void Create()
                    {
                        var values = new NativeArray<int>(8, Allocator.Temp);
                        Func<int> callback = () => values.Length;
                        callback();
                    }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Theory]
    [InlineData("var alias = values; return {|#0:alias|};")]
    [InlineData("var alias = values; var second = alias; return {|#0:second|};")]
    public async Task ReportsTemporaryReturnedThroughAliases(string statements)
    {
        var source = $$"""
                using Unity.Collections;
                class Owner
                {
                    NativeArray<int> Create()
                    {
                        var values = new NativeArray<int>(8, Allocator.TempJob);
                        {{statements}}
                    }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(
            source,
            new DiagnosticResult(DiagnosticIds.InvalidTemporaryAllocatorEscape, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("TempJob"));
    }

    [Fact]
    public async Task IgnoresAliasReassignedBeforeReturn()
    {
        var source = """
                using Unity.Collections;
                class Owner
                {
                    NativeArray<int> Create()
                    {
                        var values = new NativeArray<int>(8, Allocator.Temp);
                        var alias = values;
                        alias = new NativeArray<int>(8, Allocator.Persistent);
                        return alias;
                    }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresAliasFromAmbiguousControlFlow()
    {
        var source = """
                using Unity.Collections;
                class Owner
                {
                    NativeArray<int> Create(bool condition)
                    {
                        var values = new NativeArray<int>(8, Allocator.Temp);
                        var alias = values;
                        if (condition)
                            alias = new NativeArray<int>(8, Allocator.Persistent);
                        return alias;
                    }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresUnsupportedRefAliasFlow()
    {
        var source = """
                using Unity.Collections;
                class Owner
                {
                    NativeArray<int> Create()
                    {
                        var values = new NativeArray<int>(8, Allocator.Temp);
                        ref var alias = ref values;
                        return alias;
                    }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsTemporaryAliasStoredInField()
    {
        var source = """
                using Unity.Collections;
                class Owner
                {
                    NativeArray<int> stored;
                    void Create()
                    {
                        var values = new NativeArray<int>(8, Allocator.Temp);
                        var alias = values;
                        stored = {|#0:alias|};
                    }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(
            source,
            new DiagnosticResult(DiagnosticIds.InvalidTemporaryAllocatorEscape, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("Temp"));
    }

    [Theory]
    [InlineData("return {|#0:() => alias.Length|};")]
    [InlineData("callback = {|#0:() => alias.Length|}; return null;")]
    public async Task ReportsEscapingLambdaCapturingTemporaryAlias(string escape)
    {
        var source = $$"""
                using System;
                using Unity.Collections;
                class Owner
                {
                    Func<int> callback;
                    Func<int> Create()
                    {
                        var values = new NativeArray<int>(8, Allocator.TempJob);
                        var alias = values;
                        {{escape}}
                    }
                }
                """ + "\n" + TestSources.Collections;
        await VerifyCS.VerifyAnalyzerAsync(
            source,
            new DiagnosticResult(DiagnosticIds.InvalidTemporaryAllocatorEscape, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("TempJob"));
    }
}
