using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using UnityBestPractices.Analyzers;
using Xunit;

using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    UnityBestPractices.Analyzers.UnityBestPracticesAnalyzer,
    UnityBestPractices.Analyzers.UnityBestPracticesCodeFixProvider>;

public sealed class UBP0001Tests
{
    [Fact]
    public async Task EncapsulatesUnreferencedSerializedField()
    {
        var test = new CSharpCodeFixTest<
            UnityBestPracticesAnalyzer,
            UnityBestPracticesCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = """
                using UnityEngine;
                class Player : MonoBehaviour
                {
                    public float {|#0:speed|} = 5f;
                }
                """,
            // The inserted line break follows the source file's platform newline.
            FixedCode =
                "using UnityEngine;\n" +
                "class Player : MonoBehaviour\n" +
                "{\n" +
                "    [UnityEngine.SerializeField]" + System.Environment.NewLine +
                "    private float speed = 5f;\n" +
                "}",
            ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipFixAllCheck,
        };
        test.TestState.Sources.Add(("UnityEngine.g.cs", TestSources.UnityEngine));
        test.FixedState.Sources.Add(("UnityEngine.g.cs", TestSources.UnityEngine));
        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(DiagnosticIds.EncapsulateSerializedField)
                .WithLocation(0)
                .WithArguments("speed"));

        await test.RunAsync();
    }

    [Fact]
    public async Task DoesNotOfferFixWhenAnotherDocumentReferencesField()
    {
        var test = new CSharpCodeFixTest<
            UnityBestPracticesAnalyzer,
            UnityBestPracticesCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = """
                using UnityEngine;
                public class Player : MonoBehaviour
                {
                    public float {|#0:speed|};
                }
                """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipFixAllCheck,
        };
        test.TestState.Sources.Add(("UnityEngine.g.cs", TestSources.UnityEngine));
        test.TestState.Sources.Add(
            """
            class Consumer
            {
                float Read(Player player) => player.speed;
            }
            """);
        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(DiagnosticIds.EncapsulateSerializedField)
                .WithLocation(0)
                .WithArguments("speed"));

        await test.RunAsync();
    }

    [Fact]
    public async Task EncapsulatesScriptableObjectFieldWithExistingAttribute()
    {
        var test = new CSharpCodeFixTest<
            UnityBestPracticesAnalyzer,
            UnityBestPracticesCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = """
                using UnityEngine;
                class PlayerSettings : ScriptableObject
                {
                    [System.Obsolete]
                    public string {|#0:displayName|} = "Player";
                }
                """,
            FixedCode =
                "using UnityEngine;\n" +
                "class PlayerSettings : ScriptableObject\n" +
                "{\n" +
                "    [UnityEngine.SerializeField]" + System.Environment.NewLine +
                "    [System.Obsolete]\n" +
                "    private string displayName = \"Player\";\n" +
                "}",
            ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipFixAllCheck,
        };
        test.TestState.Sources.Add(("UnityEngine.g.cs", TestSources.UnityEngine));
        test.FixedState.Sources.Add(("UnityEngine.g.cs", TestSources.UnityEngine));
        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(DiagnosticIds.EncapsulateSerializedField)
                .WithLocation(0)
                .WithArguments("displayName"));

        await test.RunAsync();
    }

    [Fact]
    public async Task OffersFixWhenFieldIsOnlyReferencedInsideDeclaringType()
    {
        var test = new CSharpCodeFixTest<
            UnityBestPracticesAnalyzer,
            UnityBestPracticesCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = """
                using UnityEngine;
                class Player : MonoBehaviour
                {
                    public int {|#0:score|};
                    int DoubledScore => score * 2;
                }
                """,
            FixedCode =
                "using UnityEngine;\n" +
                "class Player : MonoBehaviour\n" +
                "{\n" +
                "    [UnityEngine.SerializeField]" + System.Environment.NewLine +
                "    private int score;\n" +
                "    int DoubledScore => score * 2;\n" +
                "}",
            ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipFixAllCheck,
        };
        test.TestState.Sources.Add(("UnityEngine.g.cs", TestSources.UnityEngine));
        test.FixedState.Sources.Add(("UnityEngine.g.cs", TestSources.UnityEngine));
        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(DiagnosticIds.EncapsulateSerializedField)
                .WithLocation(0)
                .WithArguments("score"));

        await test.RunAsync();
    }
}
