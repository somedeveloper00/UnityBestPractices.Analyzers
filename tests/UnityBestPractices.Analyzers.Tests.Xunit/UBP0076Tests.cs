using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using UnityBestPractices.Analyzers;
using Xunit;

using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    UnityBestPractices.Analyzers.UnityBestPracticesAnalyzer,
    UnityBestPractices.Analyzers.UnityBestPracticesCodeFixProvider>;

public sealed class UBP0076Tests
{
    [Fact]
    public async Task CombinesAdjacentLocalAssignments()
    {
        var source = """
            using UnityEngine;
            class View
            {
                void Layout(RectTransform iconRect, float x, float y, float angleY)
                {
                    {|#0:iconRect.localPosition = new Vector2(x, y);|}
                    iconRect.localRotation = Quaternion.Euler(0, 0, -angleY);
                }
            }
            """ + "\n" + TestSources.Transform;
        var fixedSource = """
            using UnityEngine;
            class View
            {
                void Layout(RectTransform iconRect, float x, float y, float angleY)
                {
                    iconRect.SetLocalPositionAndRotation(new(x, y), Quaternion.Euler(0, 0, -angleY));
                }
            }
            """ + "\n" + TestSources.Transform;

        await VerifyCS.VerifyCodeFixAsync(
            source,
            new DiagnosticResult(DiagnosticIds.CombineLocalPositionAndRotation, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0),
            fixedSource);
    }

    [Fact]
    public async Task IgnoresAssignmentsToDifferentTransforms()
    {
        var source = """
            using UnityEngine;
            class View
            {
                void Layout(Transform first, Transform second, Vector3 position, Quaternion rotation)
                {
                    first.localPosition = position;
                    second.localRotation = rotation;
                }
            }
            """ + "\n" + TestSources.Transform;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresNonAdjacentAssignments()
    {
        var source = """
            using UnityEngine;
            class View
            {
                void Layout(Transform transform, Vector3 position, Quaternion rotation)
                {
                    transform.localPosition = position;
                    System.Console.WriteLine();
                    transform.localRotation = rotation;
                }
            }
            """ + "\n" + TestSources.Transform;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }
}
