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

    [Fact]
    public async Task ReportsThreeCallsOnlyOnce()
    {
        var source = """
                using UnityEngine;
                class MaterialProperties
                {
                    int First() => {|#0:Shader.PropertyToID("_MainTex")|};
                    int Second() => Shader.PropertyToID("_MainTex");
                    int Third() => Shader.PropertyToID("_MainTex");
                }
                """ + "\n" + TestSources.Shader;
        await VerifyCS.VerifyAnalyzerAsync(
            source,
            new DiagnosticResult(DiagnosticIds.CacheShaderPropertyId, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("_MainTex"));
    }

    [Fact]
    public async Task ReportsEachRepeatedPropertyNameIndependently()
    {
        var source = """
                using UnityEngine;
                class MaterialProperties
                {
                    int A() => {|#0:Shader.PropertyToID("_Color")|};
                    int B() => {|#1:Shader.PropertyToID("_MainTex")|};
                    int C() => Shader.PropertyToID("_Color");
                    int D() => Shader.PropertyToID("_MainTex");
                }
                """ + "\n" + TestSources.Shader;
        await VerifyCS.VerifyAnalyzerAsync(
            source,
            new DiagnosticResult(DiagnosticIds.CacheShaderPropertyId, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0).WithArguments("_Color"),
            new DiagnosticResult(DiagnosticIds.CacheShaderPropertyId, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(1).WithArguments("_MainTex"));
    }

    [Fact]
    public async Task IgnoresSinglePropertyCall()
    {
        var source = """
                using UnityEngine;
                class MaterialProperties { int Color() => Shader.PropertyToID("_Color"); }
                """ + "\n" + TestSources.Shader;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresDifferentPropertyNames()
    {
        var source = """
                using UnityEngine;
                class MaterialProperties
                {
                    int Color() => Shader.PropertyToID("_Color");
                    int Texture() => Shader.PropertyToID("_MainTex");
                }
                """ + "\n" + TestSources.Shader;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresNonLiteralPropertyNames()
    {
        var source = """
                using UnityEngine;
                class MaterialProperties
                {
                    const string ColorName = "_Color";
                    int Color() => Shader.PropertyToID(ColorName);
                    int OtherColor() => Shader.PropertyToID(ColorName);
                }
                """ + "\n" + TestSources.Shader;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresCallsSplitAcrossNestedTypes()
    {
        var source = """
                using UnityEngine;
                class Outer
                {
                    int Color() => Shader.PropertyToID("_Color");
                    class Inner { int Color() => Shader.PropertyToID("_Color"); }
                }
                """ + "\n" + TestSources.Shader;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresSameNamedLookalikeMethod()
    {
        var source = """
                static class Shader { public static int PropertyToID(string value) => 0; }
                class MaterialProperties
                {
                    int First() => Shader.PropertyToID("_Color");
                    int Second() => Shader.PropertyToID("_Color");
                }
                """;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }
}
