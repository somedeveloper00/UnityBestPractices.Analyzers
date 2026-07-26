using System;
using System.Threading.Tasks;
using UnityBestPractices.Analyzers;

internal sealed partial class AnalyzerTests
{
    private async Task VerifyAdditionalLegacyQuickFixCasesAsync()
    {
        await VerifySerializedFieldCasesAsync();
        await VerifyCoroutineCasesAsync();
        await VerifyMagnitudeCasesAsync();
        await VerifyBurstCasesAsync();
        await VerifyReadOnlyJobCasesAsync();
        await VerifyStackallocCasesAsync();
        await VerifyRefLocalCasesAsync();
        await VerifyCameraCacheCasesAsync();
        await VerifyListCapacityCasesAsync();
        await VerifyMathfSquareCasesAsync();
        await VerifyNativeArrayInitializationCasesAsync();
    }

    private async Task VerifySerializedFieldCasesAsync()
    {
        await VerifyFixAsync(
            "using UnityEngine; class A : MonoBehaviour { public int Count; }",
            DiagnosticIds.EncapsulateSerializedField,
            "using UnityEngine; class A : MonoBehaviour { [UnityEngine.SerializeField] private int Count; }");
        await VerifyFixAsync(
            "using UnityEngine; class B : ScriptableObject { public string Label = \"x\"; }",
            DiagnosticIds.EncapsulateSerializedField,
            "using UnityEngine; class B : ScriptableObject { [UnityEngine.SerializeField] private string Label = \"x\"; }");
        await VerifyFixAsync(
            "class C : UnityEngine.MonoBehaviour { public double Weight = 2d; }",
            DiagnosticIds.EncapsulateSerializedField,
            "class C : UnityEngine.MonoBehaviour { [UnityEngine.SerializeField] private double Weight = 2d; }");
    }

    private async Task VerifyCoroutineCasesAsync()
    {
        await VerifyYieldFixAsync("false");
        await VerifyYieldFixAsync("true");
        await VerifyYieldFixAsync("default(bool)");
    }

    private Task VerifyYieldFixAsync(string value) => VerifyFixAsync(
        "using System.Collections; using UnityEngine; class Waiter : MonoBehaviour { IEnumerator Wait() { yield return " + value + "; } }",
        DiagnosticIds.YieldNull,
        "using System.Collections; using UnityEngine; class Waiter : MonoBehaviour { IEnumerator Wait() { yield return null; } }");

    private async Task VerifyMagnitudeCasesAsync()
    {
        await VerifyFixAsync(
            "using UnityEngine; class A { bool Test(Vector2 value) => value.magnitude <= 2f; }",
            DiagnosticIds.UseSquaredMagnitude,
            "using UnityEngine; class A { bool Test(Vector2 value) => value.sqrMagnitude <= (2f * 2f); }");
        await VerifyFixAsync(
            "using UnityEngine; class B { bool Test(Vector3 value) => 3f > value.magnitude; }",
            DiagnosticIds.UseSquaredMagnitude,
            "using UnityEngine; class B { bool Test(Vector3 value) => (3f * 3f) > value.sqrMagnitude; }");
    }

    private async Task VerifyBurstCasesAsync()
    {
        for (var index = 0; index < 3; index++)
        {
            var name = "WorkerJob" + index;
            await VerifyFixAsync(
                "using Unity.Jobs; struct " + name + " : IJob { public void Execute() { } }",
                DiagnosticIds.AddBurstCompile,
                "using Unity.Jobs; [Unity.Burst.BurstCompile] struct " + name + " : IJob { public void Execute() { } }");
        }
    }

    private async Task VerifyReadOnlyJobCasesAsync()
    {
        for (var index = 0; index < 3; index++)
        {
            var name = "ReaderJob" + index;
            var read = index == 0 ? "var x = Input[0];" : index == 1 ? "var x = Input.Length;" : "var x = Input[Input.Length - 1];";
            await VerifyFixAsync(
                "using Unity.Collections; using Unity.Jobs; struct " + name + " : IJob { public NativeArray<int> Input; public void Execute() { " + read + " } }",
                DiagnosticIds.MarkNativeArrayReadOnly,
                "using Unity.Collections; using Unity.Jobs; struct " + name + " : IJob { [Unity.Collections.ReadOnly] public NativeArray<int> Input; public void Execute() { " + read + " } }");
        }
    }

    private async Task VerifyStackallocCasesAsync()
    {
        var cases = new (string Type, int Size)[]
        {
            ("byte", 32),
            ("sbyte", 24),
            ("short", 20),
            ("ushort", 18),
            ("uint", 12),
            ("long", 8),
            ("float", 10),
            ("double", 6),
        };
        for (var index = 0; index < cases.Length; index++)
        {
            var item = cases[index];
            var className = "StackCase" + index;
            await VerifyFixAsync(
                "using System; class " + className + " { object Run() { Span<" + item.Type + "> buffer = new " + item.Type + "[" + item.Size + "]; return buffer[0]; } }",
                DiagnosticIds.UseStackalloc,
                "using System; class " + className + " { object Run() { Span<" + item.Type + "> buffer = stackalloc " + item.Type + "[" + item.Size + "]; buffer.Clear(); return buffer[0]; } }");
        }

        // A stackalloc span cannot leave the method, so spans that escape must keep
        // their heap-backed array.
        await VerifyDiagnosticPresenceAsync(
            "using System; class SpanProvider { Span<int> Rent() { Span<int> buffer = new int[8]; return buffer; } }",
            DiagnosticIds.UseStackalloc,
            expected: false);
        await VerifyDiagnosticPresenceAsync(
            "using System; class SpanSlicer { Span<byte> Prefix() { Span<byte> buffer = new byte[16]; return buffer.Slice(0, 4); } }",
            DiagnosticIds.UseStackalloc,
            expected: false);
        await VerifyDiagnosticPresenceAsync(
            "using System; class SpanOutParameter { void Rent(out Span<int> result) { Span<int> buffer = new int[8]; result = buffer; } }",
            DiagnosticIds.UseStackalloc,
            expected: false);
        await VerifyDiagnosticPresenceAsync(
            "using System; class SpanByReference { static void Swap(ref Span<int> value) { } void Run() { Span<int> buffer = new int[8]; Swap(ref buffer); } }",
            DiagnosticIds.UseStackalloc,
            expected: false);
    }

    private async Task VerifyRefLocalCasesAsync()
    {
        var mutations = new[]
        {
            "item.Value++;",
            "item.Value--;",
            "item.Value += 1;",
            "item.Value -= 2;",
            "item.Value *= 2;",
            "item.Value /= 2;",
            "item.Value |= 1;",
            "item.Value ^= 1;",
        };
        for (var index = 0; index < mutations.Length; index++)
        {
            var typeName = "Item" + index;
            var className = "Mutator" + index;
            await VerifyFixAsync(
                "struct " + typeName + " { public int Value; } class " + className + " { void Change(" + typeName + "[] items, int index) { var item = items[index]; " + mutations[index] + " items[index] = item; } }",
                DiagnosticIds.UseRefLocal,
                "struct " + typeName + " { public int Value; } class " + className + " { void Change(" + typeName + "[] items, int index) { ref var item = ref items[index]; " + mutations[index] + " } }");
        }

        // An early exit between the mutation and the write-back means the ref local
        // would persist changes the original code discarded.
        await VerifyDiagnosticPresenceAsync(
            "struct GuardedItem { public int Value; } class GuardedMutator { void Change(GuardedItem[] items, int index, bool skip) { var item = items[index]; item.Value++; if (skip) { return; } items[index] = item; } }",
            DiagnosticIds.UseRefLocal,
            expected: false);
        await VerifyDiagnosticPresenceAsync(
            "struct LoopItem { public int Value; } class LoopMutator { void Change(LoopItem[] items, int index, bool skip) { while (true) { var item = items[index]; item.Value++; if (skip) { continue; } items[index] = item; break; } } }",
            DiagnosticIds.UseRefLocal,
            expected: false);

        // A ref local cannot be captured, so lambdas between the copy and the
        // write-back must keep the plain local.
        await VerifyDiagnosticPresenceAsync(
            "struct CapturedItem { public int Value; } class CapturedMutator { void Change(CapturedItem[] items, int index) { var item = items[index]; System.Action mutate = () => item.Value++; mutate(); items[index] = item; } }",
            DiagnosticIds.UseRefLocal,
            expected: false);

        // A by-value indexer alone does not establish a safe ref-return path.
        await VerifyDiagnosticPresenceAsync(
            "struct CopyOnly<T> where T : struct { public T this[int index] { get => default; set { } } } struct CopyOnlyItem { public int Value; } class CopyOnlyMutator { void Change(CopyOnly<CopyOnlyItem> items) { var item = items[2]; item.Value = 1; items[2] = item; } }",
            DiagnosticIds.UseRefLocal,
            expected: false);
    }

    private async Task VerifyCameraCacheCasesAsync()
    {
        for (var count = 2; count <= 10; count++)
        {
            var sourceStatements = string.Empty;
            var expectedStatements = string.Empty;
            for (var index = 0; index < count; index++)
            {
                sourceStatements += "Camera.main.fieldOfView = " + (50 + index) + "f; ";
                expectedStatements += "mainCamera.fieldOfView = " + (50 + index) + "f; ";
            }

            await VerifyFixAsync(
                "using UnityEngine; class CameraCase" + count + " { void Configure() { " + sourceStatements + "} }",
                DiagnosticIds.CacheCameraMain,
                "using UnityEngine; class CameraCase" + count + " { void Configure() { var mainCamera = UnityEngine.Camera.main; " + expectedStatements + "} }");
        }
    }

    private async Task VerifyListCapacityCasesAsync()
    {
        for (var count = 6; count <= 14; count++)
        {
            var additions = string.Empty;
            for (var index = 0; index < count; index++)
            {
                additions += "values.Add(" + index + "); ";
            }

            await VerifyFixAsync(
                "using System.Collections.Generic; class ListCase" + count + " { object Build() { var values = new List<int>(); " + additions + "return values; } }",
                DiagnosticIds.PreallocateList,
                "using System.Collections.Generic; class ListCase" + count + " { object Build() { var values = new List<int>(" + count + "); " + additions + "return values; } }");
        }
    }

    private async Task VerifyMathfSquareCasesAsync()
    {
        for (var index = 0; index < 3; index++)
        {
            var name = "value" + index;
            await VerifyFixAsync(
                "using UnityEngine; class SquareCase" + index + " { float Run(float " + name + ") => Mathf.Pow(" + name + ", 2f); }",
                DiagnosticIds.UseMultiplicationForSquare,
                "using UnityEngine; class SquareCase" + index + " { float Run(float " + name + ") => (" + name + " * " + name + "); }");
        }
    }

    private async Task VerifyNativeArrayInitializationCasesAsync()
    {
        for (var offset = 1; offset <= 9; offset++)
        {
            await VerifyFixAsync(
                "using Unity.Collections; class NativeCase" + offset + " { object Build(int length) { var values = new NativeArray<int>(length, Allocator.Temp); for (var i = 0; i < values.Length; i++) { values[i] = i + " + offset + "; } return values; } }",
                DiagnosticIds.UseUninitializedNativeArray,
                "using Unity.Collections; class NativeCase" + offset + " { object Build(int length) { var values = new NativeArray<int>(length, Allocator.Temp, Unity.Collections.NativeArrayOptions.UninitializedMemory); for (var i = 0; i < values.Length; i++) { values[i] = i + " + offset + "; } return values; } }");
        }

        // The copy constructor has no NativeArrayOptions overload, so appending an
        // options argument would not compile.
        await VerifyDiagnosticPresenceAsync(
            "using Unity.Collections; class NativeCopyCase { void Build(int[] source) { var values = new NativeArray<int>(source, Allocator.Persistent); for (var i = 0; i < values.Length; i++) { values[i] = i; } values.Dispose(); } }",
            DiagnosticIds.UseUninitializedNativeArray,
            expected: false);
    }
}
