using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using UnityBestPractices.Analyzers;
using Xunit;

using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    UnityBestPractices.Analyzers.UnityBestPracticesAnalyzer,
    UnityBestPractices.Analyzers.UnityBestPracticesCodeFixProvider>;

public sealed class UBP0074Tests
{
    [Fact]
    public async Task ReportsFirstOfRepeatedConstantPropertyCalls()
    {
        var source = """
                using UnityEngine;
                class MaterialProperties
                {
                    int First() => {|#0:Shader.PropertyToID("_Color")|};
                    int Second() => Shader.PropertyToID("_Color");
                }
                """ + "\n" + TestSources.Shader;
        await VerifyCS.VerifyAnalyzerAsync(
            source,
            new DiagnosticResult(DiagnosticIds.CacheShaderPropertyId, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("_Color"));
    }
}
