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
                public static object FindObjectOfType(System.Type type) => default;
                public static T[] FindObjectsOfType<T>() => default;
                public static object[] FindObjectsOfType(System.Type type) => default;
                public static T FindFirstObjectByType<T>() => default;
                public static object FindFirstObjectByType(System.Type type) => default;
                public static T[] FindObjectsByType<T>(FindObjectsSortMode sortMode) => default;
                public static object[] FindObjectsByType(System.Type type, FindObjectsSortMode sortMode) => default;
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
    public async Task ConvertsNonGenericMultipleWithAsCastToGeneric()
    {
        var source = """
            class Panel { }
            class Example
            {
                Panel[] Get() => {|#0:UnityEngine.Object.FindObjectsOfType(typeof(Panel))|} as Panel[];
            }
            """ + ModernUnityObject;
        var fixedSource = """
            class Panel { }
            class Example
            {
                Panel[] Get() => UnityEngine.Object.FindObjectsByType<Panel>(global::UnityEngine.FindObjectsSortMode.InstanceID);
            }
            """ + ModernUnityObject;

        await VerifyFixAsync(source, fixedSource);
    }

    [Fact]
    public async Task ConvertsNonGenericMultipleWithExplicitCastToGeneric()
    {
        var source = """
            class Panel { }
            class Example
            {
                Panel[] Get() => (Panel[]){|#0:UnityEngine.Object.FindObjectsOfType(typeof(Panel))|};
            }
            """ + ModernUnityObject;
        var fixedSource = """
            class Panel { }
            class Example
            {
                Panel[] Get() => UnityEngine.Object.FindObjectsByType<Panel>(global::UnityEngine.FindObjectsSortMode.InstanceID);
            }
            """ + ModernUnityObject;

        await VerifyFixAsync(source, fixedSource);
    }

    [Fact]
    public async Task ConvertsNonGenericSingleWithAsCastToGeneric()
    {
        var source = """
            class Panel { }
            class Example
            {
                Panel Get() => {|#0:UnityEngine.Object.FindObjectOfType(typeof(Panel))|} as Panel;
            }
            """ + ModernUnityObject;
        var fixedSource = """
            class Panel { }
            class Example
            {
                Panel Get() => UnityEngine.Object.FindFirstObjectByType<Panel>();
            }
            """ + ModernUnityObject;

        await VerifyFixAsync(source, fixedSource);
    }

    [Fact]
    public async Task ConvertsNonGenericSingleWithExplicitCastToGeneric()
    {
        var source = """
            class Panel { }
            class Example
            {
                Panel Get() => (Panel){|#0:UnityEngine.Object.FindObjectOfType(typeof(Panel))|};
            }
            """ + ModernUnityObject;
        var fixedSource = """
            class Panel { }
            class Example
            {
                Panel Get() => UnityEngine.Object.FindFirstObjectByType<Panel>();
            }
            """ + ModernUnityObject;

        await VerifyFixAsync(source, fixedSource);
    }

    [Fact]
    public async Task ConvertsQualifiedTypeOfWithAsCast()
    {
        var source = """
            namespace Game.UI
            {
                class Panel { }
            }
            class Example
            {
                Game.UI.Panel[] Get() => {|#0:UnityEngine.Object.FindObjectsOfType(typeof(Game.UI.Panel))|} as Game.UI.Panel[];
            }
            """ + ModernUnityObject;
        var fixedSource = """
            namespace Game.UI
            {
                class Panel { }
            }
            class Example
            {
                Game.UI.Panel[] Get() => UnityEngine.Object.FindObjectsByType<Game.UI.Panel>(global::UnityEngine.FindObjectsSortMode.InstanceID);
            }
            """ + ModernUnityObject;

        await VerifyFixAsync(source, fixedSource);
    }

    [Fact]
    public async Task ConvertsWithParenthesizedCast()
    {
        var source = """
            class Panel { }
            class Example
            {
                object Get() => ((Panel[]){|#0:UnityEngine.Object.FindObjectsOfType(typeof(Panel))|});
            }
            """ + ModernUnityObject;
        var fixedSource = """
            class Panel { }
            class Example
            {
                object Get() => UnityEngine.Object.FindObjectsByType<Panel>(global::UnityEngine.FindObjectsSortMode.InstanceID);
            }
            """ + ModernUnityObject;

        await VerifyFixAsync(source, fixedSource);
    }

    [Fact]
    public async Task KeepsNonGenericWhenCastTypeDoesNotMatch()
    {
        var source = """
            class Panel { }
            class Other { }
            class Example
            {
                object Get() => {|#0:UnityEngine.Object.FindObjectsOfType(typeof(Panel))|} as Other[];
            }
            """ + ModernUnityObject;
        var fixedSource = """
            class Panel { }
            class Other { }
            class Example
            {
                object Get() => UnityEngine.Object.FindObjectsByType(typeof(Panel), global::UnityEngine.FindObjectsSortMode.InstanceID) as Other[];
            }
            """ + ModernUnityObject;

        await VerifyFixAsync(source, fixedSource);
    }

    [Fact]
    public async Task KeepsNonGenericWhenNoCastPresent()
    {
        var source = """
            class Panel { }
            class Example
            {
                object Get() => {|#0:UnityEngine.Object.FindObjectsOfType(typeof(Panel))|};
            }
            """ + ModernUnityObject;
        var fixedSource = """
            class Panel { }
            class Example
            {
                object Get() => UnityEngine.Object.FindObjectsByType(typeof(Panel), global::UnityEngine.FindObjectsSortMode.InstanceID);
            }
            """ + ModernUnityObject;

        await VerifyFixAsync(source, fixedSource);
    }

    [Fact]
    public async Task ConvertsIdentifierOnlyCallWithAsCast()
    {
        var source = """
            using UnityEngine;
            class Panel { }
            class Example
            {
                Panel[] Get() => {|#0:Object.FindObjectsOfType(typeof(Panel))|} as Panel[];
            }
            """ + ModernUnityObject;
        var fixedSource = """
            using UnityEngine;
            class Panel { }
            class Example
            {
                Panel[] Get() => Object.FindObjectsByType<Panel>(global::UnityEngine.FindObjectsSortMode.InstanceID);
            }
            """ + ModernUnityObject;

        await VerifyFixAsync(source, fixedSource);
    }

    [Fact]
    public async Task ConvertsNestedTypeWithAsCast()
    {
        var source = """
            class Outer
            {
                public class Panel { }
            }
            class Example
            {
                Outer.Panel[] Get() => {|#0:UnityEngine.Object.FindObjectsOfType(typeof(Outer.Panel))|} as Outer.Panel[];
            }
            """ + ModernUnityObject;
        var fixedSource = """
            class Outer
            {
                public class Panel { }
            }
            class Example
            {
                Outer.Panel[] Get() => UnityEngine.Object.FindObjectsByType<Outer.Panel>(global::UnityEngine.FindObjectsSortMode.InstanceID);
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

    [Fact]
    public async Task DoesNotConvertWhenArgumentIsNotTypeOf()
    {
        var source = """
            class Panel { }
            class Example
            {
                System.Type t = typeof(Panel);
                object Get() => {|#0:UnityEngine.Object.FindObjectsOfType(t)|};
            }
            """ + ModernUnityObject;
        var fixedSource = """
            class Panel { }
            class Example
            {
                System.Type t = typeof(Panel);
                object Get() => UnityEngine.Object.FindObjectsByType(t, global::UnityEngine.FindObjectsSortMode.InstanceID);
            }
            """ + ModernUnityObject;

        await VerifyFixAsync(source, fixedSource);
    }

    private static Task VerifyFixAsync(string source, string fixedSource) =>
        VerifyCS.VerifyCodeFixAsync(
            source,
            new DiagnosticResult(DiagnosticIds.UseModernObjectFindApi, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(0),
            fixedSource);
}
