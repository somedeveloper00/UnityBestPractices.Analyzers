using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using UnityBestPractices.Analyzers;
using Xunit;

using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    UnityBestPractices.Analyzers.UnityBestPracticesAnalyzer,
    UnityBestPractices.Analyzers.UnityBestPracticesCodeFixProvider>;

public sealed class UBP0078Tests
{
    private const string ModernUnityObject = """
        namespace UnityEngine
        {
            public enum FindObjectsSortMode { None, InstanceID }
            public class Object
            {
                public static T FindObjectOfType<T>() => default;
                public static Object FindObjectOfType(System.Type type) => default;
                public static T[] FindObjectsOfType<T>() => default;
                public static Object[] FindObjectsOfType(System.Type type) => default;
                public static T FindFirstObjectByType<T>() => default;
                public static Object FindFirstObjectByType(System.Type type) => default;
                public static T[] FindObjectsByType<T>(FindObjectsSortMode sortMode) => default;
                public static Object[] FindObjectsByType(System.Type type, FindObjectsSortMode sortMode) => default;
            }
        }
        """;

    [Fact]
    public async Task ConvertsGenericSingleObjectLookup()
    {
        var source = """
            class Component { }
            class Example
            {
                object Get() => {|#0:UnityEngine.Object.FindObjectOfType<Component>()|};
            }
            """ + ModernUnityObject;
        var fixedSource = """
            class Component { }
            class Example
            {
                object Get() => UnityEngine.Object.FindFirstObjectByType<Component>();
            }
            """ + ModernUnityObject;

        await VerifyFixAsync(source, fixedSource);
    }

    [Fact]
    public async Task ConvertsNonGenericSingleObjectLookup()
    {
        var source = """
            class Component { }
            class Example
            {
                object Get() => {|#0:UnityEngine.Object.FindObjectOfType(typeof(Component))|};
            }
            """ + ModernUnityObject;
        var fixedSource = """
            class Component { }
            class Example
            {
                object Get() => UnityEngine.Object.FindFirstObjectByType(typeof(Component));
            }
            """ + ModernUnityObject;

        await VerifyFixAsync(source, fixedSource);
    }

    [Fact]
    public async Task ConvertsGenericMultipleObjectLookupWithEquivalentOrdering()
    {
        var source = """
            class Component { }
            class Example
            {
                object Get() => {|#0:UnityEngine.Object.FindObjectsOfType<Component>()|};
            }
            """ + ModernUnityObject;
        var fixedSource = """
            class Component { }
            class Example
            {
                object Get() => UnityEngine.Object.FindObjectsByType<Component>(global::UnityEngine.FindObjectsSortMode.InstanceID);
            }
            """ + ModernUnityObject;

        await VerifyFixAsync(source, fixedSource);
    }

    [Fact]
    public async Task ConvertsNonGenericMultipleObjectLookupWithEquivalentOrdering()
    {
        var source = """
            class Component { }
            class Example
            {
                object Get() => {|#0:UnityEngine.Object.FindObjectsOfType(typeof(Component))|};
            }
            """ + ModernUnityObject;
        var fixedSource = """
            class Component { }
            class Example
            {
                object Get() => UnityEngine.Object.FindObjectsByType(typeof(Component), global::UnityEngine.FindObjectsSortMode.InstanceID);
            }
            """ + ModernUnityObject;

        await VerifyFixAsync(source, fixedSource);
    }

    [Fact]
    public async Task IgnoresSameNamedNonUnityMethod()
    {
        var source = """
            class Example
            {
                static T FindObjectOfType<T>() => default;
                object Get() => FindObjectOfType<object>();
            }
            """ + ModernUnityObject;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresProjectsWithoutModernApi()
    {
        var source = """
            class Component { }
            class Example
            {
                object Get() => UnityEngine.Object.FindObjectOfType<Component>();
            }
            namespace UnityEngine
            {
                public class Object
                {
                    public static T FindObjectOfType<T>() => default;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    private static Task VerifyFixAsync(string source, string fixedSource) =>
        VerifyCS.VerifyCodeFixAsync(
            source,
            new DiagnosticResult(DiagnosticIds.UseModernObjectFindApi, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0),
            fixedSource);
}
