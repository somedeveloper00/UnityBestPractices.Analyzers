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

    [Fact]
    public async Task PreservesExpressionsThatAreNotObjectCreations()
    {
        var source = """
            using UnityEngine;
            class View
            {
                void Layout(Transform transform, Vector3 position, Quaternion rotation)
                {
                    {|#0:transform.localPosition = position;|}
                    transform.localRotation = rotation;
                }
            }
            """ + "\n" + TestSources.Transform;
        var fixedSource = """
            using UnityEngine;
            class View
            {
                void Layout(Transform transform, Vector3 position, Quaternion rotation)
                {
                    transform.SetLocalPositionAndRotation(position, rotation);
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
    public async Task IgnoresReverseAssignmentOrder()
    {
        var source = """
            using UnityEngine;
            class View
            {
                void Layout(Transform transform, Vector3 position, Quaternion rotation)
                {
                    transform.localRotation = rotation;
                    transform.localPosition = position;
                }
            }
            """ + "\n" + TestSources.Transform;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresCompoundAssignments()
    {
        var source = """
            using UnityEngine;
            class View
            {
                void Layout(Transform transform, Vector3 position, Quaternion rotation)
                {
                    transform.localPosition += position;
                    transform.localRotation = rotation;
                }
            }
            """ + "\n" + TestSources.Transform;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresReceiverWithRepeatedEvaluation()
    {
        var source = """
            using UnityEngine;
            class View
            {
                Transform GetTransform() => null;
                void Layout(Vector3 position, Quaternion rotation)
                {
                    GetTransform().localPosition = position;
                    GetTransform().localRotation = rotation;
                }
            }
            """ + "\n" + TestSources.Transform;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresSameNamedNonUnityProperties()
    {
        var source = """
            class FakeTransform
            {
                public int localPosition { get; set; }
                public int localRotation { get; set; }
                public void SetLocalPositionAndRotation(int position, int rotation) { }
            }
            class View
            {
                void Layout(FakeTransform transform)
                {
                    transform.localPosition = 1;
                    transform.localRotation = 2;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresUnityVersionWithoutCombinedApi()
    {
        var source = """
            namespace UnityEngine
            {
                public struct Vector3 { }
                public struct Quaternion { }
                public class Transform
                {
                    public Vector3 localPosition { get; set; }
                    public Quaternion localRotation { get; set; }
                }
            }
            class View
            {
                void Layout(UnityEngine.Transform transform, UnityEngine.Vector3 position, UnityEngine.Quaternion rotation)
                {
                    transform.localPosition = position;
                    transform.localRotation = rotation;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresAssignmentsSeparatedByCommentTrivia()
    {
        var source = """
            using UnityEngine;
            class View
            {
                void Layout(Transform transform, Vector3 position, Quaternion rotation)
                {
                    transform.localPosition = position;
                    // Rotation is updated after position for a documented reason.
                    transform.localRotation = rotation;
                }
            }
            """ + "\n" + TestSources.Transform;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresAssignmentsThroughFieldReceiver()
    {
        var source = """
            using UnityEngine;
            class View
            {
                private Transform iconRect;
                void Layout(Vector3 position, Quaternion rotation)
                {
                    iconRect.localPosition = position;
                    iconRect.localRotation = rotation;
                }
            }
            """ + "\n" + TestSources.Transform;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresInstanceFieldReceiverMutatedByFirstRightHandSide()
    {
        var source = """
            using UnityEngine;
            class View
            {
                private Transform iconRect;
                Vector3 MoveToNextTransform()
                {
                    iconRect = new Transform();
                    return new Vector3();
                }
                void Layout(Quaternion rotation)
                {
                    iconRect.localPosition = MoveToNextTransform();
                    iconRect.localRotation = rotation;
                }
            }
            """ + "\n" + TestSources.Transform;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresStaticFieldReceiverMutatedByFirstRightHandSide()
    {
        var source = """
            using UnityEngine;
            class View
            {
                private static Transform iconRect;
                static Vector3 MoveToNextTransform()
                {
                    iconRect = new Transform();
                    return new Vector3();
                }
                static void Layout(Quaternion rotation)
                {
                    iconRect.localPosition = MoveToNextTransform();
                    iconRect.localRotation = rotation;
                }
            }
            """ + "\n" + TestSources.Transform;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }
}
