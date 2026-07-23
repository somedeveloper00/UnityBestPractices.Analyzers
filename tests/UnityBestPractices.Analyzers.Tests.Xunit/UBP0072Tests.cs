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
}
