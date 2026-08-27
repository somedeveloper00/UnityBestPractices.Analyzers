using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using UnityBestPractices.Analyzers;

internal sealed partial class AnalyzerTests
{
    private async Task VerifyAdvancedRulesAsync()
    {
        await VerifyFixAsync(
            """
            using Unity.Burst;
            using Unity.Jobs;
            class Runner
            {
                void Update()
                {
                    new WorkJob().Schedule();
                }
            }
            [BurstCompile]
            struct WorkJob : IJob { public void Execute() { } }
            """,
            DiagnosticIds.DiscardedScheduledJobHandle,
            """
            using Unity.Burst;
            using Unity.Jobs;
            class Runner
            {
                void Update()
                {
                    var jobHandle = new WorkJob().Schedule();
                }
            }
            [BurstCompile]
            struct WorkJob : IJob { public void Execute() { } }
            """);

        await VerifyFixAsync(
            """
            using Unity.Burst;
            using Unity.Jobs;
            class Runner
            {
                void Update()
                {
                    new WorkJob().ScheduleParallel();
                }
            }
            [BurstCompile]
            struct WorkJob : IJob { public void Execute() { } }
            """,
            DiagnosticIds.DiscardedScheduledJobHandle,
            """
            using Unity.Burst;
            using Unity.Jobs;
            class Runner
            {
                void Update()
                {
                    var jobHandle = new WorkJob().ScheduleParallel();
                }
            }
            [BurstCompile]
            struct WorkJob : IJob { public void Execute() { } }
            """);

        await VerifyFixAsync(
            """
            using Unity.Burst;
            using Unity.Jobs;
            class Runner
            {
                void Update()
                {
                    new WorkJob().Schedule();
                    var jobHandle = default(JobHandle);
                }
            }
            [BurstCompile]
            struct WorkJob : IJob { public void Execute() { } }
            """,
            DiagnosticIds.DiscardedScheduledJobHandle,
            """
            using Unity.Burst;
            using Unity.Jobs;
            class Runner
            {
                void Update()
                {
                    var jobHandle1 = new WorkJob().Schedule();
                    var jobHandle = default(JobHandle);
                }
            }
            [BurstCompile]
            struct WorkJob : IJob { public void Execute() { } }
            """);

        await VerifyFixAsync(
            """
            using Unity.Burst;
            using Unity.Jobs;
            class Runner
            {
                void Update()
                {
                    // dependency must be preserved
                    new WorkJob().Schedule();
                }
            }
            [BurstCompile]
            struct WorkJob : IJob { public void Execute() { } }
            """,
            DiagnosticIds.DiscardedScheduledJobHandle,
            """
            using Unity.Burst;
            using Unity.Jobs;
            class Runner
            {
                void Update()
                {
                    // dependency must be preserved
                    var jobHandle = new WorkJob().Schedule();
                }
            }
            [BurstCompile]
            struct WorkJob : IJob { public void Execute() { } }
            """);

        await VerifyFixAsync(
            """
            using Unity.Burst;
            using Unity.Jobs;
            class Runner
            {
                void Update(bool useParallel)
                {
                    if (useParallel)
                    {
                        new WorkJob().ScheduleParallel();
                    }
                }
            }
            [BurstCompile]
            struct WorkJob : IJob { public void Execute() { } }
            """,
            DiagnosticIds.DiscardedScheduledJobHandle,
            """
            using Unity.Burst;
            using Unity.Jobs;
            class Runner
            {
                void Update(bool useParallel)
                {
                    if (useParallel)
                    {
                        var jobHandle = new WorkJob().ScheduleParallel();
                    }
                }
            }
            [BurstCompile]
            struct WorkJob : IJob { public void Execute() { } }
            """);

        var shaderCases = new[]
        {
            (Property: "_Color", Field: "ColorId", Container: "class", Existing: ""),
            (Property: "_Base-Color", Field: "BaseColorId", Container: "class", Existing: ""),
            (Property: "123", Field: "ShaderProperty123Id", Container: "class", Existing: ""),
            (Property: "___", Field: "ShaderPropertyId", Container: "class", Existing: ""),
            (Property: "_Color", Field: "ColorId1", Container: "class", Existing: "private int ColorId;"),
            (Property: "_EmissionColor", Field: "EmissionColorId", Container: "struct", Existing: ""),
            (Property: "_MainTex", Field: "MainTexId", Container: "class", Existing: "private static int OtherId;"),
            (Property: "_Metallic", Field: "MetallicId", Container: "struct", Existing: "private float value;"),
            (Property: "_Smoothness", Field: "SmoothnessId", Container: "class", Existing: "private const int Count = 2;"),
            (Property: "_Detail_Mask", Field: "DetailMaskId", Container: "class", Existing: "private string name;"),
        };
        for (var index = 0; index < shaderCases.Length; index++)
        {
            var testCase = shaderCases[index];
            var source = $$"""
                using UnityEngine;
                {{testCase.Container}} ShaderUser{{index}}
                {
                    {{testCase.Existing}}
                    int First() => Shader.PropertyToID("{{testCase.Property}}");
                    int Second() => Shader.PropertyToID("{{testCase.Property}}");
                }
                """;
            var expected = $$"""
                using UnityEngine;
                {{testCase.Container}} ShaderUser{{index}}
                {
                    private static readonly int {{testCase.Field}} = UnityEngine.Shader.PropertyToID("{{testCase.Property}}");
                    {{testCase.Existing}}
                    int First() => {{testCase.Field}};
                    int Second() => {{testCase.Field}};
                }
                """;
            await VerifyFixAsync(source, DiagnosticIds.CacheShaderPropertyId, expected);
        }

        for (var index = 0; index < 5; index++)
        {
            var source = $$"""
                using UnityEngine;
                class TransformUser{{index}}
                {
                    void Apply(Transform target, Vector3 position, Quaternion rotation)
                    {
                        target.localPosition = position;
                        target.localRotation = rotation;
                    }
                }
                """;
            var expected = $$"""
                using UnityEngine;
                class TransformUser{{index}}
                {
                    void Apply(Transform target, Vector3 position, Quaternion rotation)
                    {
                        target.SetLocalPositionAndRotation(position, rotation);
                    }
                }
                """;
            await VerifyFixAsync(source, DiagnosticIds.CombineLocalPositionAndRotation, expected);
        }

        await VerifyDiagnosticPresenceAsync(
            """
            using Unity.Collections;
            class Owner
            {
                void Create()
                {
                    var values = new NativeArray<int>(8, Allocator.Persistent);
                }
            }
            """,
            DiagnosticIds.UndisposedPersistentNativeContainer,
            expected: true);
        await VerifyDiagnosticPresenceAsync(
            """
            using Unity.Collections;
            class Owner
            {
                void Create()
                {
                    var values = new NativeArray<int>(8, Allocator.Persistent);
                    values.Dispose();
                }
            }
            """,
            DiagnosticIds.UndisposedPersistentNativeContainer,
            expected: false);

        await VerifyDiagnosticPresenceAsync(
            """
            using Unity.Collections;
            class Owner
            {
                NativeArray<int> Create()
                {
                    return new NativeArray<int>(8, Allocator.Temp);
                }
            }
            """,
            DiagnosticIds.InvalidTemporaryAllocatorEscape,
            expected: true);
        await VerifyDiagnosticPresenceAsync(
            """
            using Unity.Collections;
            class Owner
            {
                NativeArray<int> Create()
                {
                    var values = new NativeArray<int>(8, Allocator.TempJob);
                    return values;
                }
            }
            """,
            DiagnosticIds.InvalidTemporaryAllocatorEscape,
            expected: true);
        await VerifyDiagnosticPresenceAsync(
            """
            using Unity.Collections;
            class Owner
            {
                private NativeArray<int> _values;
                void Create()
                {
                    _values = new NativeArray<int>(8, Allocator.Temp);
                }
            }
            """,
            DiagnosticIds.InvalidTemporaryAllocatorEscape,
            expected: true);
        await VerifyDiagnosticPresenceAsync(
            """
            using System;
            using Unity.Collections;
            class Owner
            {
                Func<int> Create()
                {
                    var values = new NativeArray<int>(8, Allocator.TempJob);
                    return () => values.Length;
                }
            }
            """,
            DiagnosticIds.InvalidTemporaryAllocatorEscape,
            expected: true);
        await VerifyDiagnosticPresenceAsync(
            """
            using Unity.Collections;
            class Owner
            {
                int Use()
                {
                    var values = new NativeArray<int>(8, Allocator.Temp);
                    return values.Length;
                }
            }
            """,
            DiagnosticIds.InvalidTemporaryAllocatorEscape,
            expected: false);

        await VerifyDiagnosticPresenceAsync(
            """
            using Unity.Burst;
            using Unity.Jobs;
            class Runner
            {
                JobHandle Update() => new WorkJob().Schedule();
            }
            [BurstCompile]
            struct WorkJob : IJob { public void Execute() { } }
            """,
            DiagnosticIds.DiscardedScheduledJobHandle,
            expected: false);
        await VerifyDiagnosticPresenceAsync(
            """
            using Unity.Jobs;
            namespace Other
            {
                static class LookAlike
                {
                    public static JobHandle Schedule() => default;
                }
            }
            class Runner
            {
                void Update()
                {
                    Other.LookAlike.Schedule();
                }
            }
            """,
            DiagnosticIds.DiscardedScheduledJobHandle,
            expected: false);
        await VerifyDiagnosticPresenceAsync(
            """
            using Unity.Jobs;
            namespace Unity.Entities
            {
                static class DependencyUpdatingApi
                {
                    public static JobHandle Schedule() => default;
                }
            }
            class Runner
            {
                void Update()
                {
                    Unity.Entities.DependencyUpdatingApi.Schedule();
                }
            }
            """,
            DiagnosticIds.DiscardedScheduledJobHandle,
            expected: false);
        await VerifyDiagnosticPresenceAsync(
            """
            using UnityEngine;
            class DynamicProperty
            {
                int Get(string name)
                {
                    var first = Shader.PropertyToID(name);
                    return Shader.PropertyToID(name);
                }
            }
            """,
            DiagnosticIds.CacheShaderPropertyId,
            expected: false);
        await VerifyDiagnosticPresenceAsync(
            """
            using UnityEngine;
            class ScopedConstant
            {
                int Get()
                {
                    const string propertyName = "_Color";
                    return Shader.PropertyToID(propertyName) + Shader.PropertyToID(propertyName);
                }
            }
            """,
            DiagnosticIds.CacheShaderPropertyId,
            expected: false);
    }

    private async Task VerifyUnusedEntityAccessFixesAsync()
    {
        var cases = new[]
        {
            ("_", "position.ValueRW.Value++;"),
            ("entity", "position.ValueRW.Value++;"),
            ("unusedEntity", "position.ValueRW.Value += 2;"),
            ("ignored", "position.ValueRW.Value += 3;"),
            ("discarded", "position.ValueRW.Value *= 2f;"),
        };

        foreach (var (entityName, body) in cases)
        {
            await VerifyFixAsync(
                $$"""
                using Unity.Entities;

                class MovementSystem
                {
                    void Update()
                    {
                        foreach (var (position, {{entityName}}) in SystemAPI.Query<RefRW<Position>>().WithEntityAccess())
                        {
                            {{body}}
                        }
                    }
                }

                struct Position : IComponentData { public float Value; }
                """,
                DiagnosticIds.RemoveUnusedEntityAccess,
                $$"""
                using Unity.Entities;

                class MovementSystem
                {
                    void Update()
                    {
                        foreach (var position in SystemAPI.Query<RefRW<Position>>())
                        {
                            {{body}}
                        }
                    }
                }

                struct Position : IComponentData { public float Value; }
                """);
        }
    }

    private async Task VerifyModernObjectFindFixesAsync()
    {
        var cases = new[]
        {
            (
                "UnityEngine.Object.FindObjectOfType<UnityEngine.Component>()",
                "UnityEngine.Object.FindFirstObjectByType<UnityEngine.Component>()"),
            (
                "UnityEngine.Object.FindObjectOfType(typeof(UnityEngine.Component))",
                "UnityEngine.Object.FindFirstObjectByType(typeof(UnityEngine.Component))"),
            (
                "UnityEngine.Object.FindObjectsOfType<UnityEngine.Component>()",
                "UnityEngine.Object.FindObjectsByType<UnityEngine.Component>(global::UnityEngine.FindObjectsSortMode.InstanceID)"),
            (
                "UnityEngine.Object.FindObjectsOfType(typeof(UnityEngine.Component))",
                "UnityEngine.Object.FindObjectsByType(typeof(UnityEngine.Component), global::UnityEngine.FindObjectsSortMode.InstanceID)"),
            (
                "UnityEngine.Object.FindObjectOfType<UnityEngine.Camera>()",
                "UnityEngine.Object.FindFirstObjectByType<UnityEngine.Camera>()"),
        };

        foreach (var (invocation, replacement) in cases)
        {
            await VerifyFixAsync(
                $"class ObjectFinder {{ object Find() => {invocation}; }}",
                DiagnosticIds.UseModernObjectFindApi,
                $"class ObjectFinder {{ object Find() => {replacement}; }}");
        }
    }

}
