using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using UnityBestPractices.Analyzers;
using Xunit;

using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    UnityBestPractices.Analyzers.UnityBestPracticesAnalyzer,
    UnityBestPractices.Analyzers.UnityBestPracticesCodeFixProvider>;

public sealed class UBP0002Tests
{
    [Fact]
    public async Task ReplacesBoxedFrameValueAndPreservesTrivia()
    {
        var test = new CSharpCodeFixTest<
            UnityBestPracticesAnalyzer,
            UnityBestPracticesCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = """
                using System.Collections;
                using UnityEngine;
                class Animation : MonoBehaviour
                {
                    IEnumerator Run()
                    {
                        yield return {|#0:0|}; // wait exactly one frame
                    }
                }
                """,
            FixedCode = """
                using System.Collections;
                using UnityEngine;
                class Animation : MonoBehaviour
                {
                    IEnumerator Run()
                    {
                        yield return null; // wait exactly one frame
                    }
                }
                """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
        };
        test.TestState.Sources.Add(("UnityEngine.g.cs", TestSources.UnityEngine));
        test.FixedState.Sources.Add(("UnityEngine.g.cs", TestSources.UnityEngine));
        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(DiagnosticIds.YieldNull).WithLocation(0));

        await test.RunAsync();
    }

    [Fact]
    public async Task IgnoresNonUnityIterator()
    {
        await VerifyCS.VerifyAnalyzerAsync(
            """
            using System.Collections;
            class Sequence
            {
                IEnumerator Run()
                {
                    yield return 0;
                }
            }
            """);
    }
}
