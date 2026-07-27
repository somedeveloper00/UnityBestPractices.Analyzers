using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using UnityBestPractices.Analyzers;
using Xunit;

using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    UnityBestPractices.Analyzers.UnityBestPracticesAnalyzer,
    UnityBestPractices.Analyzers.UnityBestPracticesCodeFixProvider>;

public sealed class UBP0071Tests
{
    [Fact]
    public async Task ReportsDiscardedSupportedScheduleHandle()
    {
        var source = """
                using Unity.Burst;
                using Unity.Jobs;
                class Runner
                {
                    void Update()
                    {
                        {|#0:new WorkJob().Schedule()|};
                    }
                }
                [BurstCompile]
                struct WorkJob : IJob { public void Execute() { } }
                """ + "\n" + TestSources.Jobs;
        await VerifyCS.VerifyAnalyzerAsync(
            source,
            new DiagnosticResult(DiagnosticIds.DiscardedScheduledJobHandle, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("Schedule"));
    }

    [Fact]
    public async Task ReportsDiscardedScheduleParallelHandle()
    {
        var source = """
                using Unity.Jobs;
                namespace Unity.Jobs
                {
                    public struct JobHandle { }
                    public static class ParallelExtensions
                    {
                        public static JobHandle ScheduleParallel(this Work job) => default;
                    }
                }
                public struct Work { }
                class Runner
                {
                    void Update() { {|#0:new Work().ScheduleParallel()|}; }
                }
                """;
        await VerifyCS.VerifyAnalyzerAsync(
            source,
            new DiagnosticResult(DiagnosticIds.DiscardedScheduledJobHandle, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("ScheduleParallel"));
    }

    [Fact]
    public async Task ReportsSupportedEntitiesExtension()
    {
        var source = """
                using Unity.Entities;
                namespace Unity.Jobs { public struct JobHandle { } }
                namespace Unity.Entities
                {
                    public struct Work { }
                    public static class IJobEntityExtensions
                    {
                        public static Unity.Jobs.JobHandle Schedule(this Work job) => default;
                    }
                }
                class Runner
                {
                    void Update() { {|#0:new Unity.Entities.Work().Schedule()|}; }
                }
                """;
        await VerifyCS.VerifyAnalyzerAsync(
            source,
            new DiagnosticResult(DiagnosticIds.DiscardedScheduledJobHandle, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("Schedule"));
    }

    [Fact]
    public async Task IgnoresHandleStoredInLocal()
    {
        var source = """
                using Unity.Burst;
                using Unity.Jobs;
                class Runner { void Update() { var handle = new WorkJob().Schedule(); } }
                [BurstCompile]
                struct WorkJob : IJob { public void Execute() { } }
                """ + "\n" + TestSources.Jobs;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresHandleReturnedToCaller()
    {
        var source = """
                using Unity.Burst;
                using Unity.Jobs;
                class Runner { JobHandle Start() { return new WorkJob().Schedule(); } }
                [BurstCompile]
                struct WorkJob : IJob { public void Execute() { } }
                """ + "\n" + TestSources.Jobs;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresSameNamedUserScheduleMethod()
    {
        var source = """
                namespace Unity.Jobs { public struct JobHandle { } }
                class Work { public Unity.Jobs.JobHandle Schedule() => default; }
                class Runner { void Update() { new Work().Schedule(); } }
                """;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresSupportedNamespaceMethodReturningVoid()
    {
        var source = """
                using Unity.Jobs;
                namespace Unity.Jobs
                {
                    public struct JobHandle { }
                    public static class Extensions { public static void Schedule(this Work job) { } }
                }
                public struct Work { }
                class Runner { void Update() { new Work().Schedule(); } }
                """;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }
}
