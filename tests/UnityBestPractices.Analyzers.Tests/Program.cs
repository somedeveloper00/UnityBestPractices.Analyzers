using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using UnityBestPractices.Analyzers;

var tests = new AnalyzerTests();
await tests.RunAsync();

internal sealed partial class AnalyzerTests
{
    private const string UnityStubs = """
        namespace UnityEngine
        {
            public sealed class SerializeField : System.Attribute { }
            public class Object { }
            public class ScriptableObject : Object { }
            public class Component : Object { }
            public class MonoBehaviour : Component { }
            public class Camera : Component
            {
                public static Camera main => new Camera();
                public float fieldOfView { get; set; }
                public int cullingMask { get; set; }
            }

            public static class Mathf
            {
                public static float Pow(float value, float power) => 0f;
                public static float Clamp(float value, float minimum, float maximum) => 0f;
                public static float Clamp01(float value) => 0f;
                public static float Sqrt(float value) => 0f;
                public static float Floor(float value) => 0f;
                public static float Ceil(float value) => 0f;
                public static float Round(float value) => 0f;
                public static int FloorToInt(float value) => 0;
                public static int CeilToInt(float value) => 0;
                public static int RoundToInt(float value) => 0;
            }

            public struct Vector2
            {
                public Vector2(float x, float y) { }
                public static Vector2 zero => default;
                public static Vector2 one => default;
                public static Vector2 up => default;
                public static Vector2 down => default;
                public static Vector2 left => default;
                public static Vector2 right => default;
                public float magnitude => 0f;
                public float sqrMagnitude => 0f;
            }

            public struct Vector3
            {
                public Vector3(float x, float y, float z) { }
                public static Vector3 zero => default;
                public static Vector3 one => default;
                public static Vector3 up => default;
                public static Vector3 down => default;
                public static Vector3 left => default;
                public static Vector3 right => default;
                public static Vector3 forward => default;
                public static Vector3 back => default;
                public float magnitude => 0f;
                public float sqrMagnitude => 0f;
            }

            public struct Quaternion
            {
                public Quaternion(float x, float y, float z, float w) { }
                public static Quaternion identity => default;
                public static Quaternion Euler(float x, float y, float z) => default;
            }

            public struct Color
            {
                public Color(float r, float g, float b, float a) { }
                public static Color clear => default;
                public static Color black => default;
                public static Color white => default;
                public static Color red => default;
                public static Color green => default;
                public static Color blue => default;
                public static Color yellow => default;
                public static Color cyan => default;
                public static Color magenta => default;
            }
        }

        namespace Unity.Burst
        {
            public sealed class BurstCompileAttribute : System.Attribute { }
        }

        namespace Unity.Jobs
        {
            public interface IJob { void Execute(); }
        }

        namespace Unity.Entities
        {
            public interface IComponentData { }
            public interface IJobEntity { }
            public struct Entity { }

            public enum EntityQueryOptions { Default, IncludeDisabledEntities }

            public sealed class WithAllAttribute : System.Attribute
            {
                public WithAllAttribute(params System.Type[] types) { }
            }

            public sealed class WithAnyAttribute : System.Attribute
            {
                public WithAnyAttribute(params System.Type[] types) { }
            }

            public sealed class WithNoneAttribute : System.Attribute
            {
                public WithNoneAttribute(params System.Type[] types) { }
            }

            public sealed class WithChangeFilterAttribute : System.Attribute
            {
                public WithChangeFilterAttribute(params System.Type[] types) { }
            }

            public sealed class WithOptionsAttribute : System.Attribute
            {
                public WithOptionsAttribute(EntityQueryOptions options) { }
            }

            public sealed class RefRW<T> where T : struct, IComponentData
            {
                private T _value;
                public ref T ValueRW => ref _value;
            }

            public sealed class RefRO<T> where T : struct, IComponentData
            {
                private T _value;
                public ref readonly T ValueRO => ref _value;
            }

            public abstract class SystemBase
            {
                protected EntitiesBuilder Entities => default;
            }

            public delegate void RefAction<T>(ref T value) where T : struct, IComponentData;
            public delegate void InAction<T>(in T value) where T : struct, IComponentData;
            public delegate void RefInAction<T1, T2>(ref T1 first, in T2 second)
                where T1 : struct, IComponentData
                where T2 : struct, IComponentData;
            public delegate void EntityRefInAction<T1, T2>(Entity entity, ref T1 first, in T2 second)
                where T1 : struct, IComponentData
                where T2 : struct, IComponentData;

            public struct EntitiesBuilder
            {
                public EntitiesBuilder WithAll<T>() where T : struct, IComponentData => this;
                public EntitiesBuilder WithAny<T>() where T : struct, IComponentData => this;
                public EntitiesBuilder WithNone<T>() where T : struct, IComponentData => this;
                public EntitiesBuilder WithChangeFilter<T>() where T : struct, IComponentData => this;
                public EntitiesBuilder WithEntityQueryOptions(EntityQueryOptions options) => this;
                public EntitiesBuilder WithStructuralChanges() => this;
                public ForEachDescription ForEach<T>(RefAction<T> action) where T : struct, IComponentData => default;
                public ForEachDescription ForEach<T>(InAction<T> action) where T : struct, IComponentData => default;
                public ForEachDescription ForEach<T1, T2>(RefInAction<T1, T2> action)
                    where T1 : struct, IComponentData
                    where T2 : struct, IComponentData => default;
                public ForEachDescription ForEach<T1, T2>(EntityRefInAction<T1, T2> action)
                    where T1 : struct, IComponentData
                    where T2 : struct, IComponentData => default;
            }

            public struct ForEachDescription
            {
                public void Run() { }
                public void Schedule() { }
                public void ScheduleParallel() { }
            }

            public static class IJobEntityExtensions
            {
                public static void Run<T>(this T job) where T : struct, IJobEntity { }
                public static void Schedule<T>(this T job) where T : struct, IJobEntity { }
                public static void ScheduleParallel<T>(this T job) where T : struct, IJobEntity { }
            }

            public static class SystemAPI
            {
                public static QueryEnumerable<T1> Query<T1>() => default;
                public static QueryEnumerable<T1, T2> Query<T1, T2>() => default;
            }

            public struct QueryEnumerable<T1>
            {
                public QueryEnumerable<T1> WithAll<T>() => this;
                public QueryEnumerable<T1> WithAny<T>() => this;
                public QueryEnumerable<T1> WithNone<T>() => this;
                public QueryEnumerable<T1> WithChangeFilter<T>() => this;
                public QueryEnumerable<T1> WithOptions(EntityQueryOptions options) => this;
                public QueryEnumerableWithEntity<T1> WithEntityAccess() => default;
                public Enumerator GetEnumerator() => default;
                public struct Enumerator
                {
                    public T1 Current => default;
                    public bool MoveNext() => false;
                }
            }

            public struct QueryEnumerable<T1, T2>
            {
                public QueryEnumerable<T1, T2> WithAll<T>() => this;
                public QueryEnumerable<T1, T2> WithAny<T>() => this;
                public QueryEnumerable<T1, T2> WithNone<T>() => this;
                public QueryEnumerable<T1, T2> WithChangeFilter<T>() => this;
                public QueryEnumerable<T1, T2> WithOptions(EntityQueryOptions options) => this;
                public QueryEnumerableWithEntity<T1, T2> WithEntityAccess() => default;
                public Enumerator GetEnumerator() => default;
                public struct Enumerator
                {
                    public (T1, T2) Current => default;
                    public bool MoveNext() => false;
                }
            }

            public struct QueryEnumerableWithEntity<T1>
            {
                public Enumerator GetEnumerator() => default;
                public struct Enumerator
                {
                    public (T1, Entity) Current => default;
                    public bool MoveNext() => false;
                }
            }

            public struct QueryEnumerableWithEntity<T1, T2>
            {
                public Enumerator GetEnumerator() => default;
                public struct Enumerator
                {
                    public (T1, T2, Entity) Current => default;
                    public bool MoveNext() => false;
                }
            }
        }

        namespace Unity.Collections
        {
            public sealed class ReadOnlyAttribute : System.Attribute { }
            public enum Allocator { Temp, TempJob, Persistent }
            public enum NativeArrayOptions { UninitializedMemory, ClearMemory }

            public struct NativeArray<T> where T : struct
            {
                private T[] _items;
                public NativeArray(
                    int length,
                    Allocator allocator,
                    NativeArrayOptions options = NativeArrayOptions.ClearMemory)
                {
                    _items = new T[length];
                }

                public int Length => _items.Length;
                public T this[int index]
                {
                    get => _items[index];
                    set => _items[index] = value;
                }
            }

            public struct NativeList<T> where T : struct
            {
                private T[] _items;
                public T this[int index]
                {
                    get => _items[index];
                    set => _items[index] = value;
                }

                public ref T ElementAt(int index) => ref _items[index];
            }
        }
        """;

    private readonly UnityBestPracticesAnalyzer _analyzer = new();
    private readonly UnityBestPracticesCodeFixProvider _codeFix = new();
    private readonly Dictionary<string, int> _positiveCaseCounts = new(StringComparer.Ordinal);

    public async Task RunAsync()
    {
        VerifyCatalogIntegrity();

        await VerifyFixAsync(
            """
            using UnityEngine;
            class PlayerSettings : MonoBehaviour
            {
                public float movementSpeed = 5f;
            }
            """,
            DiagnosticIds.EncapsulateSerializedField,
            """
            using UnityEngine;
            class PlayerSettings : MonoBehaviour
            {
                [UnityEngine.SerializeField]
                private float movementSpeed = 5f;
            }
            """);

        await VerifyFixAsync(
            """
            using UnityEngine;
            class CameraSettings
            {
                void Configure()
                {
                    Camera.main.fieldOfView = 70f;
                    Camera.main.cullingMask = 1;
                }
            }
            """,
            DiagnosticIds.CacheCameraMain,
            """
            using UnityEngine;
            class CameraSettings
            {
                void Configure()
                {
                    var mainCamera = UnityEngine.Camera.main;
                    mainCamera.fieldOfView = 70f;
                    mainCamera.cullingMask = 1;
                }
            }
            """);

        await VerifyFixAsync(
            """
            using System.Collections.Generic;
            class PathBuilder
            {
                List<int> Build()
                {
                    var points = new List<int>();
                    points.Add(1);
                    points.Add(2);
                    points.Add(3);
                    points.Add(4);
                    points.Add(5);
                    return points;
                }
            }
            """,
            DiagnosticIds.PreallocateList,
            """
            using System.Collections.Generic;
            class PathBuilder
            {
                List<int> Build()
                {
                    var points = new List<int>(5);
                    points.Add(1);
                    points.Add(2);
                    points.Add(3);
                    points.Add(4);
                    points.Add(5);
                    return points;
                }
            }
            """);

        await VerifyFixAsync(
            """
            using UnityEngine;
            class MathHelpers
            {
                float Square(float value) => Mathf.Pow(value, 2f);
            }
            """,
            DiagnosticIds.UseMultiplicationForSquare,
            """
            using UnityEngine;
            class MathHelpers
            {
                float Square(float value) => (value * value);
            }
            """);

        await VerifyFixAsync(
            """
            using Unity.Collections;
            class NativeBufferBuilder
            {
                NativeArray<int> Create(int length)
                {
                    var values = new NativeArray<int>(length, Allocator.Temp);
                    for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = i;
                    }

                    return values;
                }
            }
            """,
            DiagnosticIds.UseUninitializedNativeArray,
            """
            using Unity.Collections;
            class NativeBufferBuilder
            {
                NativeArray<int> Create(int length)
                {
                    var values = new NativeArray<int>(length, Allocator.Temp, Unity.Collections.NativeArrayOptions.UninitializedMemory);
                    for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = i;
                    }

                    return values;
                }
            }
            """);

        await VerifyFixAsync(
            """
            using Unity.Collections;
            using Unity.Jobs;
            struct SumJob : IJob
            {
                public NativeArray<int> Input;
                public NativeArray<int> Output;
                public void Execute() => Output[0] = Input[0];
            }
            """,
            DiagnosticIds.AddBurstCompile,
            """
            using Unity.Collections;
            using Unity.Jobs;
            [Unity.Burst.BurstCompile]
            struct SumJob : IJob
            {
                public NativeArray<int> Input;
                public NativeArray<int> Output;
                public void Execute() => Output[0] = Input[0];
            }
            """);

        await VerifyFixAsync(
            """
            using Unity.Collections;
            using Unity.Jobs;
            struct SumJob : IJob
            {
                public NativeArray<int> Input;
                public NativeArray<int> Output;
                public void Execute() => Output[0] = Input[0];
            }
            """,
            DiagnosticIds.MarkNativeArrayReadOnly,
            """
            using Unity.Collections;
            using Unity.Jobs;
            struct SumJob : IJob
            {
                [Unity.Collections.ReadOnly]
                public NativeArray<int> Input;
                public NativeArray<int> Output;
                public void Execute() => Output[0] = Input[0];
            }
            """);

        await VerifyFixAsync(
            """
            using System;
            class BufferBuilder
            {
                int ReadFirst()
                {
                    Span<int> buffer = new int[16];
                    return buffer[0];
                }
            }
            """,
            DiagnosticIds.UseStackalloc,
            """
            using System;
            class BufferBuilder
            {
                int ReadFirst()
                {
                    Span<int> buffer = stackalloc int[16];
                    buffer.Clear();
                    return buffer[0];
                }
            }
            """);

        await VerifyFixAsync(
            """
            using System;
            class BufferBuilder
            {
                int ReadFirst()
                {
                    ReadOnlySpan<int> buffer = new int[] { 1, 2, 3 };
                    return buffer[0];
                }
            }
            """,
            DiagnosticIds.UseStackalloc,
            """
            using System;
            class BufferBuilder
            {
                int ReadFirst()
                {
                    ReadOnlySpan<int> buffer = stackalloc int[] { 1, 2, 3 };
                    return buffer[0];
                }
            }
            """);

        await VerifyFixAsync(
            """
            struct Particle { public int Health; }
            class ParticleSystem
            {
                void Damage(Particle[] particles, int index)
                {
                    var particle = particles[index];
                    particle.Health -= 1;
                    particles[index] = particle;
                }
            }
            """,
            DiagnosticIds.UseRefLocal,
            """
            struct Particle { public int Health; }
            class ParticleSystem
            {
                void Damage(Particle[] particles, int index)
                {
                    ref var particle = ref particles[index];
                    particle.Health -= 1;
                }
            }
            """);

        await VerifyFixAsync(
            """
            using Unity.Collections;
            struct Particle { public int Health; }
            class ParticleSystem
            {
                void Damage(NativeList<Particle> particles)
                {
                    var particle = particles[0];
                    particle.Health -= 1;
                    particles[0] = particle;
                }
            }
            """,
            DiagnosticIds.UseRefLocal,
            """
            using Unity.Collections;
            struct Particle { public int Health; }
            class ParticleSystem
            {
                void Damage(NativeList<Particle> particles)
                {
                    ref var particle = ref particles.ElementAt(0);
                    particle.Health -= 1;
                }
            }
            """);

        await VerifyFixAsync(
            """
            using System.Collections;
            using UnityEngine;
            class FrameWaiter : MonoBehaviour
            {
                IEnumerator WaitOneFrame()
                {
                    yield return 0;
                }
            }
            """,
            DiagnosticIds.YieldNull,
            """
            using System.Collections;
            using UnityEngine;
            class FrameWaiter : MonoBehaviour
            {
                IEnumerator WaitOneFrame()
                {
                    yield return null;
                }
            }
            """);

        await VerifyFixAsync(
            """
            using UnityEngine;
            class Targeting : MonoBehaviour
            {
                bool IsNear(Vector3 offset) => offset.magnitude < 10f;
            }
            """,
            DiagnosticIds.UseSquaredMagnitude,
            """
            using UnityEngine;
            class Targeting : MonoBehaviour
            {
                bool IsNear(Vector3 offset) => offset.sqrMagnitude < (10f * 10f);
            }
            """);

        await VerifyFixAsync(
            """
            using UnityEngine;
            class Targeting : MonoBehaviour
            {
                const float Range = 5f;
                bool IsFar(Vector2 offset) => Range <= offset.magnitude;
            }
            """,
            DiagnosticIds.UseSquaredMagnitude,
            """
            using UnityEngine;
            class Targeting : MonoBehaviour
            {
                const float Range = 5f;
                bool IsFar(Vector2 offset) => (Range * Range) <= offset.sqrMagnitude;
            }
            """);

        await VerifyExpressionQuickFixesAsync();
        await VerifyDotsQuickFixesAsync();
        await VerifyAdditionalLegacyQuickFixCasesAsync();
        await VerifyDotsEdgeCaseMatrixAsync();
        await VerifyDotsQueryQuickFixesAsync();

        await VerifyNoDiagnosticAsync(
            """
            using System;
            using System.Linq;
            using System.Text;
            using System.Threading;
            using UnityEngine;

            class QuickFixBoundaries
            {
                Vector2 DynamicVector(float x) => new Vector2(x, 0f);
                Color CustomColor() => new Color(0.25f, 0.5f, 0.75f, 1f);
                float WiderClamp(float value) => Mathf.Clamp(value, -1f, 1f);
                float FourthRoot(float value) => Mathf.Pow(value, 0.25f);
                int[] NonEmptyArray() => new int[1];
                bool IndexedPredicate(int[] values) => values.Where((item, index) => item > index).Any();
                StringBuilder LongerText() => new StringBuilder().Append("xy");
                StringBuilder NonEmptyLine() => new StringBuilder().AppendLine("text");
                CancellationToken CancelledToken() => new CancellationToken(true);
                Guid ParsedGuid() => new Guid("9d4f8f22-7dc8-4a19-90a3-b204b4854b4e");
            }
            """);

        await VerifyNoDiagnosticAsync(
            """
            using System.Collections;
            using UnityEngine;

            class SafeCases : MonoBehaviour
            {
                public static float SharedSpeed;
                public object RuntimeOnlyData = new();
                [System.NonSerialized] public float DeliberatelyPublic;

                IEnumerator WaitForValue()
                {
                    yield return "custom instruction";
                }

                bool IsNear(Vector3 offset, float dynamicRange) => offset.magnitude < dynamicRange;
            }

            class NotUnity
            {
                public float Value;
                IEnumerator Values() { yield return 0; }
            }
            """);

        await VerifyNoDiagnosticAsync(
            """
            using System;
            using Unity.Burst;
            using Unity.Collections;
            using Unity.Jobs;

            [BurstCompile]
            struct ExistingBurstJob : IJob
            {
                [ReadOnly] public NativeArray<int> Input;
                public void Execute() { var value = Input[0]; }
            }

            [BurstCompile]
            struct WritingJob : IJob
            {
                public NativeArray<int> Values;
                public void Execute() { Values[0] = 1; }
            }

            class ConservativeCases
            {
                void Buffers()
                {
                    for (var i = 0; i < 2; i++)
                    {
                        Span<int> loopBuffer = new int[8];
                    }

                    Span<int> largeBuffer = new int[1024];
                }

                void CopyBack(Particle[] particles, int index)
                {
                    var particle = particles[index];
                    index++;
                    particle.Health--;
                    particles[index] = particle;
                }
            }

            struct Particle { public int Health; }
            """);

        await VerifyNoDiagnosticAsync(
            """
            using System.Collections.Generic;
            using Unity.Collections;
            using UnityEngine;

            class OptimizationBoundaries
            {
                float GetValue() => 2f;

                void CameraOnce()
                {
                    Camera.main.fieldOfView = 60f;
                }

                List<int> FourItems()
                {
                    var values = new List<int>();
                    values.Add(1);
                    values.Add(2);
                    values.Add(3);
                    values.Add(4);
                    return values;
                }

                float SideEffectingSquare() => Mathf.Pow(GetValue(), 2f);

                NativeArray<int> PartialOverwrite(int length)
                {
                    var values = new NativeArray<int>(length, Allocator.Temp);
                    for (var i = 1; i < values.Length; i++)
                    {
                        values[i] = i;
                    }

                    return values;
                }

                NativeArray<int> ReadsOldValues(int length)
                {
                    var values = new NativeArray<int>(length, Allocator.Temp);
                    for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = values[i] + 1;
                    }

                    return values;
                }
            }
            """);

        VerifyQuickFixTestCoverage();
        Console.WriteLine("All analyzer and code-fix tests passed.");
    }

    private void VerifyCatalogIntegrity()
    {
        var descriptors = _analyzer.SupportedDiagnostics;
        if (descriptors.Length != 70)
        {
            throw new InvalidOperationException($"Expected 70 diagnostics, got {descriptors.Length}.");
        }

        if (descriptors.Select(descriptor => descriptor.Id).Distinct(StringComparer.Ordinal).Count() != 70)
        {
            throw new InvalidOperationException("Diagnostic IDs must be unique.");
        }

        if (descriptors.Any(descriptor => descriptor.DefaultSeverity != DiagnosticSeverity.Hidden))
        {
            throw new InvalidOperationException("Every diagnostic must remain hidden.");
        }

        var fixableIds = _codeFix.FixableDiagnosticIds.ToImmutableHashSet(StringComparer.Ordinal);
        var missingFixes = descriptors.Where(descriptor => !fixableIds.Contains(descriptor.Id)).ToArray();
        if (missingFixes.Length != 0)
        {
            throw new InvalidOperationException(
                "Every diagnostic must have a quick fix: " + string.Join(", ", missingFixes.Select(item => item.Id)));
        }
    }

    private async Task VerifyDotsQueryQuickFixesAsync()
    {
        const string entitiesForEachSource = """
            using Unity.Entities;

            struct Position : IComponentData { public float Value; }
            struct Velocity : IComponentData { public float Value; }

            partial class MovementSystem : SystemBase
            {
                void OnUpdate()
                {
                    Entities.ForEach((ref Position position, in Velocity velocity) =>
                    {
                        position.Value += velocity.Value;
                    }).Run();
                }
            }
            """;

        const string systemApiQuerySource = """
            using Unity.Entities;

            struct Position : IComponentData { public float Value; }
            struct Velocity : IComponentData { public float Value; }

            partial class MovementSystem : SystemBase
            {
                void OnUpdate()
                {
                    foreach (var (position, velocity) in Unity.Entities.SystemAPI.Query<Unity.Entities.RefRW<Position>, Unity.Entities.RefRO<Velocity>>())
                    {
                        position.ValueRW.Value += velocity.ValueRO.Value;
                    }
                }
            }
            """;

        const string extractedEntitiesJob = """
            using Unity.Entities;

            struct Position : IComponentData { public float Value; }
            struct Velocity : IComponentData { public float Value; }

            partial class MovementSystem : SystemBase
            {
                void OnUpdate()
                {
                    new EntitiesForEachJob().__MODE__();
                }

                [Unity.Burst.BurstCompile]
                private partial struct EntitiesForEachJob : Unity.Entities.IJobEntity
                {
                    public void Execute(ref Position position, in Velocity velocity)
                    {
                        position.Value += velocity.Value;
                    }
                }
            }
            """;

        const string extractedSystemApiJob = """
            using Unity.Entities;

            struct Position : IComponentData { public float Value; }
            struct Velocity : IComponentData { public float Value; }

            partial class MovementSystem : SystemBase
            {
                void OnUpdate()
                {
                    new SystemApiQueryJob().__MODE__();
                }

                [Unity.Burst.BurstCompile]
                private partial struct SystemApiQueryJob : Unity.Entities.IJobEntity
                {
                    public void Execute(ref Position position, in Velocity velocity)
                    {
                        position.Value += velocity.Value;
                    }
                }
            }
            """;

        const string filteredEntitiesForEachSource = """
            using Unity.Entities;

            struct Position : IComponentData { public float Value; }
            struct Velocity : IComponentData { public float Value; }
            struct Moving : IComponentData { }
            struct Disabled : IComponentData { }

            partial class FilteredMovementSystem : SystemBase
            {
                void OnUpdate()
                {
                    Entities.WithAll<Moving>().WithNone<Disabled>().ForEach(
                        (Entity entity, ref Position position, in Velocity velocity) =>
                        {
                            position.Value += velocity.Value;
                        }).Run();
                }
            }
            """;

        const string filteredSystemApiQuery = """
            using Unity.Entities;

            struct Position : IComponentData { public float Value; }
            struct Velocity : IComponentData { public float Value; }
            struct Moving : IComponentData { }
            struct Disabled : IComponentData { }

            partial class FilteredMovementSystem : SystemBase
            {
                void OnUpdate()
                {
                    foreach (var (position, velocity, entity) in Unity.Entities.SystemAPI.Query<Unity.Entities.RefRW<Position>, Unity.Entities.RefRO<Velocity>>().WithAll<Moving>().WithNone<Disabled>().WithEntityAccess())
                    {
                        position.ValueRW.Value += velocity.ValueRO.Value;
                    }
                }
            }
            """;

        const string filteredParallelJob = """
            using Unity.Entities;

            struct Position : IComponentData { public float Value; }
            struct Velocity : IComponentData { public float Value; }
            struct Moving : IComponentData { }
            struct Disabled : IComponentData { }

            partial class FilteredMovementSystem : SystemBase
            {
                void OnUpdate()
                {
                    new EntitiesForEachJob().ScheduleParallel();
                }

                [Unity.Burst.BurstCompile]
                [Unity.Entities.WithAll(typeof(Moving))]
                [Unity.Entities.WithNone(typeof(Disabled))]
                private partial struct EntitiesForEachJob : Unity.Entities.IJobEntity
                {
                    public void Execute(Entity entity, ref Position position, in Velocity velocity)
                    {
                        position.Value += velocity.Value;
                    }
                }
            }
            """;

        await VerifyFixAsync(
            entitiesForEachSource,
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            systemApiQuerySource);
        await VerifyFixAsync(
            entitiesForEachSource,
            DiagnosticIds.EntitiesForEachToJobEntityRun,
            extractedEntitiesJob.Replace("__MODE__", "Run", StringComparison.Ordinal));
        await VerifyFixAsync(
            entitiesForEachSource,
            DiagnosticIds.EntitiesForEachToJobEntitySchedule,
            extractedEntitiesJob.Replace("__MODE__", "Schedule", StringComparison.Ordinal));
        await VerifyFixAsync(
            entitiesForEachSource,
            DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel,
            extractedEntitiesJob.Replace("__MODE__", "ScheduleParallel", StringComparison.Ordinal));
        await VerifyFixAsync(
            filteredEntitiesForEachSource,
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            filteredSystemApiQuery);
        await VerifyFixAsync(
            filteredEntitiesForEachSource,
            DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel,
            filteredParallelJob);

        await VerifyFixAsync(
            systemApiQuerySource,
            DiagnosticIds.SystemApiQueryToJobEntityRun,
            extractedSystemApiJob.Replace("__MODE__", "Run", StringComparison.Ordinal));
        await VerifyFixAsync(
            systemApiQuerySource,
            DiagnosticIds.SystemApiQueryToJobEntitySchedule,
            extractedSystemApiJob.Replace("__MODE__", "Schedule", StringComparison.Ordinal));
        await VerifyFixAsync(
            systemApiQuerySource,
            DiagnosticIds.SystemApiQueryToJobEntityScheduleParallel,
            extractedSystemApiJob.Replace("__MODE__", "ScheduleParallel", StringComparison.Ordinal));

        await VerifyJobEntityModeFixAsync("Run", "Schedule", DiagnosticIds.JobEntityRunToSchedule);
        await VerifyJobEntityModeFixAsync("Run", "ScheduleParallel", DiagnosticIds.JobEntityRunToScheduleParallel);
        await VerifyJobEntityModeFixAsync("Schedule", "Run", DiagnosticIds.JobEntityScheduleToRun);
        await VerifyJobEntityModeFixAsync("Schedule", "ScheduleParallel", DiagnosticIds.JobEntityScheduleToScheduleParallel);
        await VerifyJobEntityModeFixAsync("ScheduleParallel", "Run", DiagnosticIds.JobEntityScheduleParallelToRun);
        await VerifyJobEntityModeFixAsync("ScheduleParallel", "Schedule", DiagnosticIds.JobEntityScheduleParallelToSchedule);

        await VerifyNoDiagnosticAsync(
            """
            using Unity.Entities;

            struct Position : IComponentData { public float Value; }
            struct Velocity : IComponentData { public float Value; }

            class CapturingSystem : SystemBase
            {
                void OnUpdate()
                {
                    var scale = 2f;
                    Entities.ForEach((ref Position position, in Velocity velocity) =>
                    {
                        position.Value += velocity.Value * scale;
                    }).Run();
                }
            }
            """);
    }

    private Task VerifyJobEntityModeFixAsync(string sourceMode, string targetMode, string diagnosticId)
    {
        const string template = """
            using Unity.Entities;

            struct Position : IComponentData { public float Value; }

            class JobScheduler
            {
                void Update()
                {
                    new MovementJob().__MODE__();
                }

                [Unity.Burst.BurstCompile]
                private partial struct MovementJob : IJobEntity
                {
                    public void Execute(ref Position position) { position.Value++; }
                }
            }
            """;
        return VerifyFixAsync(
            template.Replace("__MODE__", sourceMode, StringComparison.Ordinal),
            diagnosticId,
            template.Replace("__MODE__", targetMode, StringComparison.Ordinal));
    }

    private async Task VerifyExpressionQuickFixesAsync()
    {
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector2(0f, 0f)", DiagnosticIds.UseVector2Zero, "UnityEngine.Vector2.zero");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector2(1f, 1f)", DiagnosticIds.UseVector2One, "UnityEngine.Vector2.one");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector2(0f, 1f)", DiagnosticIds.UseVector2Up, "UnityEngine.Vector2.up");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector2(0f, -1f)", DiagnosticIds.UseVector2Down, "UnityEngine.Vector2.down");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector2(-1f, 0f)", DiagnosticIds.UseVector2Left, "UnityEngine.Vector2.left");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector2(1f, 0f)", DiagnosticIds.UseVector2Right, "UnityEngine.Vector2.right");

        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector3(0f, 0f, 0f)", DiagnosticIds.UseVector3Zero, "UnityEngine.Vector3.zero");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector3(1f, 1f, 1f)", DiagnosticIds.UseVector3One, "UnityEngine.Vector3.one");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector3(0f, 1f, 0f)", DiagnosticIds.UseVector3Up, "UnityEngine.Vector3.up");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector3(0f, -1f, 0f)", DiagnosticIds.UseVector3Down, "UnityEngine.Vector3.down");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector3(-1f, 0f, 0f)", DiagnosticIds.UseVector3Left, "UnityEngine.Vector3.left");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector3(1f, 0f, 0f)", DiagnosticIds.UseVector3Right, "UnityEngine.Vector3.right");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector3(0f, 0f, 1f)", DiagnosticIds.UseVector3Forward, "UnityEngine.Vector3.forward");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector3(0f, 0f, -1f)", DiagnosticIds.UseVector3Back, "UnityEngine.Vector3.back");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Quaternion(0f, 0f, 0f, 1f)", DiagnosticIds.UseQuaternionIdentity, "UnityEngine.Quaternion.identity");
        await VerifyExpressionFixAsync("using UnityEngine;", "Quaternion.Euler(0f, 0f, 0f)", DiagnosticIds.UseQuaternionIdentityForEulerZero, "UnityEngine.Quaternion.identity");

        await VerifyExpressionFixAsync("using UnityEngine;", "new Color(0f, 0f, 0f, 0f)", DiagnosticIds.UseColorClear, "UnityEngine.Color.clear");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Color(0f, 0f, 0f, 1f)", DiagnosticIds.UseColorBlack, "UnityEngine.Color.black");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Color(1f, 1f, 1f, 1f)", DiagnosticIds.UseColorWhite, "UnityEngine.Color.white");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Color(1f, 0f, 0f, 1f)", DiagnosticIds.UseColorRed, "UnityEngine.Color.red");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Color(0f, 1f, 0f, 1f)", DiagnosticIds.UseColorGreen, "UnityEngine.Color.green");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Color(0f, 0f, 1f, 1f)", DiagnosticIds.UseColorBlue, "UnityEngine.Color.blue");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Color(1f, 0.9215686f, 0.01568628f, 1f)", DiagnosticIds.UseColorYellow, "UnityEngine.Color.yellow");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Color(0f, 1f, 1f, 1f)", DiagnosticIds.UseColorCyan, "UnityEngine.Color.cyan");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Color(1f, 0f, 1f, 1f)", DiagnosticIds.UseColorMagenta, "UnityEngine.Color.magenta");

        await VerifyExpressionFixAsync("using UnityEngine;", "Mathf.Clamp(2f, 0f, 1f)", DiagnosticIds.UseMathfClamp01, "UnityEngine.Mathf.Clamp01(2f)");
        await VerifyExpressionFixAsync("using UnityEngine;", "Mathf.Pow(4f, 0.5f)", DiagnosticIds.UseMathfSqrt, "UnityEngine.Mathf.Sqrt(4f)");
        await VerifyExpressionFixAsync("using UnityEngine;", "(int)Mathf.Floor(2.5f)", DiagnosticIds.UseMathfFloorToInt, "UnityEngine.Mathf.FloorToInt(2.5f)");
        await VerifyExpressionFixAsync("using UnityEngine;", "(int)Mathf.Ceil(2.5f)", DiagnosticIds.UseMathfCeilToInt, "UnityEngine.Mathf.CeilToInt(2.5f)");
        await VerifyExpressionFixAsync("using UnityEngine;", "(int)Mathf.Round(2.5f)", DiagnosticIds.UseMathfRoundToInt, "UnityEngine.Mathf.RoundToInt(2.5f)");

        await VerifyExpressionFixAsync(string.Empty, "new int[0]", DiagnosticIds.UseArrayEmpty, "System.Array.Empty<int>()");
        await VerifyExpressionFixAsync("using System.Linq;", "new[] { 1, 2 }.Where(item => item > 0).Any()", DiagnosticIds.FuseWhereAny, "new[] { 1, 2 }.Any(item => item > 0)");
        await VerifyExpressionFixAsync("using System.Linq;", "new[] { 1, 2 }.Where(item => item > 0).Count()", DiagnosticIds.FuseWhereCount, "new[] { 1, 2 }.Count(item => item > 0)");
        await VerifyExpressionFixAsync("using System.Linq;", "new[] { 1, 2 }.Where(item => item > 0).First()", DiagnosticIds.FuseWhereFirst, "new[] { 1, 2 }.First(item => item > 0)");
        await VerifyExpressionFixAsync("using System.Linq;", "new[] { 1, 2 }.Where(item => item > 0).FirstOrDefault()", DiagnosticIds.FuseWhereFirstOrDefault, "new[] { 1, 2 }.FirstOrDefault(item => item > 0)");
        await VerifyExpressionFixAsync("using System.Collections.Generic; using System.Linq;", "new Dictionary<int, int>().Keys.Contains(1)", DiagnosticIds.UseDictionaryContainsKey, "new Dictionary<int, int>().ContainsKey(1)");
        await VerifyExpressionFixAsync("using System.Collections.Generic; using System.Linq;", "new List<int> { 1 }.ElementAt(0)", DiagnosticIds.UseListIndexer, "new List<int> { 1 }[0]");
        await VerifyExpressionFixAsync("using System.Collections.Generic; using System.Linq;", "new List<int> { 1 }.Count()", DiagnosticIds.UseListCountProperty, "new List<int> { 1 }.Count");
        await VerifyExpressionFixAsync("using System.Linq;", "new[] { 1 }.Count()", DiagnosticIds.UseArrayLengthProperty, "new[] { 1 }.Length");
        await VerifyExpressionFixAsync("using System.Linq;", "new[] { 1 }.Any()", DiagnosticIds.UseArrayLengthForAny, "new[] { 1 }.Length != 0");
        await VerifyExpressionFixAsync("using System.Collections.Generic; using System.Linq;", "new List<int> { 1 }.Any()", DiagnosticIds.UseListCountForAny, "new List<int> { 1 }.Count != 0");
        await VerifyExpressionFixAsync("using System.Text;", "new StringBuilder().Append(\"x\")", DiagnosticIds.AppendCharacter, "new StringBuilder().Append('x')");
        await VerifyExpressionFixAsync("using System.Text;", "new StringBuilder().AppendLine(\"\")", DiagnosticIds.AppendLineWithoutEmptyString, "new StringBuilder().AppendLine()");
        await VerifyExpressionFixAsync("using System.Threading;", "new CancellationToken()", DiagnosticIds.UseCancellationTokenNone, "System.Threading.CancellationToken.None");
        await VerifyExpressionFixAsync("using System;", "new Guid()", DiagnosticIds.UseGuidEmpty, "System.Guid.Empty");
        await VerifyExpressionFixAsync("using System.Linq;", "Enumerable.Empty<int>().ToArray()", DiagnosticIds.UseArrayEmptyForEnumerableEmpty, "System.Array.Empty<int>()");
    }

    private async Task VerifyExpressionFixAsync(
        string usingDirectives,
        string expression,
        string diagnosticId,
        string expectedExpression)
    {
        await VerifyFixAsync(
            usingDirectives + "\nclass ExpressionQuickFixCase { object Run() => " + expression + "; }",
            diagnosticId,
            usingDirectives + "\nclass ExpressionQuickFixCase { object Run() => " + expectedExpression + "; }");

        await VerifyFixAsync(
            usingDirectives + "\nclass ExpressionQuickFixReturnCase { object Run() { return " + expression + "; } }",
            diagnosticId,
            usingDirectives + "\nclass ExpressionQuickFixReturnCase { object Run() { return " + expectedExpression + "; } }");

        await VerifyFixAsync(
            usingDirectives + "\nclass ExpressionQuickFixParenthesizedCase { object Run() { return (" + expression + "); } }",
            diagnosticId,
            usingDirectives + "\nclass ExpressionQuickFixParenthesizedCase { object Run() { return (" + expectedExpression + "); } }");

        await VerifyFixAsync(
            usingDirectives + "\nclass ExpressionQuickFixLocalCase { object Run() { object value = " + expression + "; return value; } }",
            diagnosticId,
            usingDirectives + "\nclass ExpressionQuickFixLocalCase { object Run() { object value = " + expectedExpression + "; return value; } }");
    }

    private async Task VerifyDotsQuickFixesAsync()
    {
        const string entitiesForEachSource = """
            using Unity.Entities;

            partial class MovementSystem : SystemBase
            {
                void Update()
                {
                    Entities.ForEach((ref Position position, in Velocity velocity) =>
                    {
                        position.Value += velocity.Value;
                    }).Run();
                }
            }

            struct Position : IComponentData { public float Value; }
            struct Velocity : IComponentData { public float Value; }
            """;

        await VerifyFixAsync(
            entitiesForEachSource,
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            """
            using Unity.Entities;

            partial class MovementSystem : SystemBase
            {
                void Update()
                {
                    foreach (var (position, velocity) in Unity.Entities.SystemAPI.Query<Unity.Entities.RefRW<Position>, Unity.Entities.RefRO<Velocity>>())
                    {
                        position.ValueRW.Value += velocity.ValueRO.Value;
                    }
                }
            }

            struct Position : IComponentData { public float Value; }
            struct Velocity : IComponentData { public float Value; }
            """);

        await VerifyEntitiesForEachJobFixAsync(
            entitiesForEachSource,
            DiagnosticIds.EntitiesForEachToJobEntityRun,
            "Run");
        await VerifyEntitiesForEachJobFixAsync(
            entitiesForEachSource,
            DiagnosticIds.EntitiesForEachToJobEntitySchedule,
            "Schedule");
        await VerifyEntitiesForEachJobFixAsync(
            entitiesForEachSource,
            DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel,
            "ScheduleParallel");

        const string systemApiQuerySource = """
            using Unity.Entities;

            partial class MovementSystem : SystemBase
            {
                void Update()
                {
                    foreach (var (position, velocity) in SystemAPI.Query<RefRW<Position>, RefRO<Velocity>>())
                    {
                        position.ValueRW.Value += velocity.ValueRO.Value;
                    }
                }
            }

            struct Position : IComponentData { public float Value; }
            struct Velocity : IComponentData { public float Value; }
            """;

        await VerifySystemApiJobFixAsync(
            systemApiQuerySource,
            DiagnosticIds.SystemApiQueryToJobEntityRun,
            "Run");
        await VerifySystemApiJobFixAsync(
            systemApiQuerySource,
            DiagnosticIds.SystemApiQueryToJobEntitySchedule,
            "Schedule");
        await VerifySystemApiJobFixAsync(
            systemApiQuerySource,
            DiagnosticIds.SystemApiQueryToJobEntityScheduleParallel,
            "ScheduleParallel");

        await VerifyJobModeFixAsync("Run", "Schedule", DiagnosticIds.JobEntityRunToSchedule);
        await VerifyJobModeFixAsync("Run", "ScheduleParallel", DiagnosticIds.JobEntityRunToScheduleParallel);
        await VerifyJobModeFixAsync("Schedule", "Run", DiagnosticIds.JobEntityScheduleToRun);
        await VerifyJobModeFixAsync("Schedule", "ScheduleParallel", DiagnosticIds.JobEntityScheduleToScheduleParallel);
        await VerifyJobModeFixAsync("ScheduleParallel", "Run", DiagnosticIds.JobEntityScheduleParallelToRun);
        await VerifyJobModeFixAsync("ScheduleParallel", "Schedule", DiagnosticIds.JobEntityScheduleParallelToSchedule);
    }

    private Task VerifyEntitiesForEachJobFixAsync(string source, string diagnosticId, string executionMode) =>
        VerifyFixAsync(
            source,
            diagnosticId,
            """
            using Unity.Entities;

            partial class MovementSystem : SystemBase
            {
                void Update()
                {
                    new EntitiesForEachJob().EXECUTION_MODE();
                }

                [Unity.Burst.BurstCompile]
                private partial struct EntitiesForEachJob : Unity.Entities.IJobEntity
                {
                    public void Execute(ref Position position, in Velocity velocity)
                    {
                        position.Value += velocity.Value;
                    }
                }
            }

            struct Position : IComponentData { public float Value; }
            struct Velocity : IComponentData { public float Value; }
            """.Replace("EXECUTION_MODE", executionMode, StringComparison.Ordinal));

    private Task VerifySystemApiJobFixAsync(string source, string diagnosticId, string executionMode) =>
        VerifyFixAsync(
            source,
            diagnosticId,
            """
            using Unity.Entities;

            partial class MovementSystem : SystemBase
            {
                void Update()
                {
                    new SystemApiQueryJob().EXECUTION_MODE();
                }

                [Unity.Burst.BurstCompile]
                private partial struct SystemApiQueryJob : Unity.Entities.IJobEntity
                {
                    public void Execute(ref Position position, in Velocity velocity)
                    {
                        position.Value += velocity.Value;
                    }
                }
            }

            struct Position : IComponentData { public float Value; }
            struct Velocity : IComponentData { public float Value; }
            """.Replace("EXECUTION_MODE", executionMode, StringComparison.Ordinal));

    private Task VerifyJobModeFixAsync(string sourceMode, string targetMode, string diagnosticId) =>
        VerifyFixAsync(
            """
            using Unity.Entities;

            class JobRunner
            {
                void Update()
                {
                    new MovementJob().SOURCE_MODE();
                }
            }

            [Unity.Burst.BurstCompile]
            partial struct MovementJob : IJobEntity
            {
                public void Execute(ref Position position) { }
            }

            struct Position : IComponentData { public float Value; }
            """.Replace("SOURCE_MODE", sourceMode, StringComparison.Ordinal),
            diagnosticId,
            """
            using Unity.Entities;

            class JobRunner
            {
                void Update()
                {
                    new MovementJob().TARGET_MODE();
                }
            }

            [Unity.Burst.BurstCompile]
            partial struct MovementJob : IJobEntity
            {
                public void Execute(ref Position position) { }
            }

            struct Position : IComponentData { public float Value; }
            """.Replace("TARGET_MODE", targetMode, StringComparison.Ordinal));

    private async Task VerifyFixAsync(string source, string diagnosticId, string expected)
    {
        var document = CreateDocument(source);
        var diagnostics = await GetDiagnosticsAsync(document);
        var diagnostic = diagnostics.SingleOrDefault(item => item.Id == diagnosticId)
            ?? throw new InvalidOperationException($"Expected {diagnosticId}, got: {FormatDiagnostics(diagnostics)}");

        AssertHidden(diagnostic);
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);
        await _codeFix.RegisterCodeFixesAsync(context);

        var action = actions.SingleOrDefault()
            ?? throw new InvalidOperationException($"No code fix was registered for {diagnosticId}.");
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var applyChanges = operations.OfType<ApplyChangesOperation>().Single();
        var changedDocument = applyChanges.ChangedSolution.GetDocument(document.Id)
            ?? throw new InvalidOperationException("The code fix removed the test document.");
        var changedCompilation = await changedDocument.Project.GetCompilationAsync()
            ?? throw new InvalidOperationException("Could not compile the fixed document.");
        var changedErrors = changedCompilation.GetDiagnostics()
            .Where(item => item.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (changedErrors.Length != 0)
        {
            throw new InvalidOperationException(
                $"The fix for {diagnosticId} did not compile: {FormatDiagnostics(changedErrors)}");
        }

        var changedRoot = await changedDocument.GetSyntaxRootAsync()
            ?? throw new InvalidOperationException("The fixed document has no syntax root.");
        var expectedRoot = CSharpSyntaxTree.ParseText(expected).GetRoot();

        var actualText = changedRoot.NormalizeWhitespace().ToFullString();
        var expectedText = expectedRoot.NormalizeWhitespace().ToFullString();
        if (!string.Equals(actualText, expectedText, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unexpected fix for {diagnosticId}.\nEXPECTED:\n{expectedText}\nACTUAL:\n{actualText}");
        }

        _positiveCaseCounts.TryGetValue(diagnosticId, out var count);
        _positiveCaseCounts[diagnosticId] = count + 1;
    }

    private void VerifyQuickFixTestCoverage()
    {
        var complicatedFixes = ImmutableHashSet.Create(
            StringComparer.Ordinal,
            DiagnosticIds.UseStackalloc,
            DiagnosticIds.UseRefLocal,
            DiagnosticIds.CacheCameraMain,
            DiagnosticIds.PreallocateList,
            DiagnosticIds.UseUninitializedNativeArray,
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            DiagnosticIds.EntitiesForEachToJobEntityRun,
            DiagnosticIds.EntitiesForEachToJobEntitySchedule,
            DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel,
            DiagnosticIds.SystemApiQueryToJobEntityRun,
            DiagnosticIds.SystemApiQueryToJobEntitySchedule,
            DiagnosticIds.SystemApiQueryToJobEntityScheduleParallel);
        var failures = new List<string>();
        foreach (var descriptor in _analyzer.SupportedDiagnostics)
        {
            _positiveCaseCounts.TryGetValue(descriptor.Id, out var count);
            var required = complicatedFixes.Contains(descriptor.Id) ? 10 : 4;
            if (count < required)
            {
                failures.Add(descriptor.Id + ": " + count + "/" + required);
            }
        }

        if (failures.Count != 0)
        {
            throw new InvalidOperationException(
                "Quick-fix test coverage is below policy: " + string.Join(", ", failures));
        }
    }

    private async Task VerifyNoDiagnosticAsync(string source)
    {
        var diagnostics = await GetDiagnosticsAsync(CreateDocument(source));
        if (!diagnostics.IsEmpty)
        {
            throw new InvalidOperationException($"Expected no analyzer diagnostics, got: {FormatDiagnostics(diagnostics)}");
        }
    }

    private async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(Document document)
    {
        var compilation = await document.Project.GetCompilationAsync()
            ?? throw new InvalidOperationException("Could not create a compilation.");
        var compilerErrors = compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        if (compilerErrors.Length != 0)
        {
            throw new InvalidOperationException($"Test input did not compile: {FormatDiagnostics(compilerErrors)}");
        }

        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(_analyzer))
            .GetAnalyzerDiagnosticsAsync();
        foreach (var diagnostic in diagnostics)
        {
            AssertHidden(diagnostic);
        }

        return diagnostics;
    }

    private static Document CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var stubsDocumentId = DocumentId.CreateNewId(projectId);
        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "AnalyzerTest", "AnalyzerTest", LanguageNames.CSharp)
            .WithProjectCompilationOptions(
                projectId,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.CSharp9));

        foreach (var reference in GetPlatformReferences())
        {
            solution = solution.AddMetadataReference(projectId, reference);
        }

        solution = solution.AddDocument(
            stubsDocumentId,
            "UnityStubs.cs",
            SourceText.From(UnityStubs));
        solution = solution.AddDocument(
            documentId,
            "Test.cs",
            SourceText.From(source));
        return solution.GetDocument(documentId)
            ?? throw new InvalidOperationException("Could not create the test document.");
    }

    private static IEnumerable<MetadataReference> GetPlatformReferences()
    {
        var trustedAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("The runtime did not provide platform assemblies.");
        return trustedAssemblies
            .Split(Path.PathSeparator)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
    }

    private static void AssertHidden(Diagnostic diagnostic)
    {
        if (diagnostic.Severity != DiagnosticSeverity.Hidden)
        {
            throw new InvalidOperationException(
                $"{diagnostic.Id} must be hidden, but was {diagnostic.Severity}.");
        }
    }

    private static string FormatDiagnostics(IEnumerable<Diagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(item => item.ToString()));
}
