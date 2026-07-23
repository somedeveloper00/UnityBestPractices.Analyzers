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
}
