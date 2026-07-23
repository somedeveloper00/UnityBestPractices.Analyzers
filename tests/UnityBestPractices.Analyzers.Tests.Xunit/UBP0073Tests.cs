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
}
