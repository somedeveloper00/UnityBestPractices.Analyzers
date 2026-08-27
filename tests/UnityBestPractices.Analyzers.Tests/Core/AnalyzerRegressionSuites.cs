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

internal sealed partial class AnalyzerTests : IRegressionSuite
{
    private readonly UnityBestPracticesAnalyzer _analyzer = new();
    private readonly UnityBestPracticesCodeFixProvider _codeFix = new();
    private readonly Dictionary<string, int> _positiveCaseCounts = new(StringComparer.Ordinal);

    public string Name => "Analyzer regression";

    public async Task RunAsync()
    {
        await RunDeclarativeCasesAsync();
        VerifyCatalogIntegrity();
        RepositoryConsistencyVerifier.Verify();
        await VerifyConfigurationAsync();
        await VerifyEncapsulationSafetyAsync();
        await VerifyFixAllScopesAsync();
        await VerifyNamespaceConsistencyAsync();
        await VerifyUnusedEntityAccessFixesAsync();
        await VerifyModernObjectFindFixesAsync();

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
            using Unity.Collections;
            struct NetCodeConnectionEvent { public int ConnectionEntity; }
            class ConnectionEvents
            {
                void Reset()
                {
                    NativeArray<NetCodeConnectionEvent> test = new();
                    var item = test[2];
                    item.ConnectionEntity = default;
                    test[2] = item;
                }
            }
            """,
            DiagnosticIds.UseRefLocal,
            """
            using Unity.Collections;
            struct NetCodeConnectionEvent { public int ConnectionEntity; }
            class ConnectionEvents
            {
                void Reset()
                {
                    NativeArray<NetCodeConnectionEvent> test = new();
                    ref var item = ref test.AsSpan()[2];
                    item.ConnectionEntity = default;
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
        await VerifyAdvancedRulesAsync();

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
                    var values = new NativeArray<int>(length, Allocator.Persistent);
                    for (var i = 1; i < values.Length; i++)
                    {
                        values[i] = i;
                    }

                    return values;
                }

                NativeArray<int> ReadsOldValues(int length)
                {
                    var values = new NativeArray<int>(length, Allocator.Persistent);
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
        const int expectedDiagnosticCount = 78;
        if (descriptors.Length != expectedDiagnosticCount)
        {
            throw new InvalidOperationException(
                $"Expected {expectedDiagnosticCount} diagnostics, got {descriptors.Length}.");
        }

        if (descriptors.Select(descriptor => descriptor.Id).Distinct(StringComparer.Ordinal).Count() !=
            expectedDiagnosticCount)
        {
            throw new InvalidOperationException("Diagnostic IDs must be unique.");
        }

        var expectedIds = Enumerable.Range(1, expectedDiagnosticCount)
            .Select(number => $"UBP{number:0000}")
            .ToArray();
        var actualIds = descriptors
            .Select(descriptor => descriptor.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (!actualIds.SequenceEqual(expectedIds, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The stable diagnostic ID sequence changed: " + string.Join(", ", actualIds));
        }

        if (descriptors.Any(descriptor => descriptor.DefaultSeverity != DiagnosticSeverity.Info))
        {
            throw new InvalidOperationException(
                "Every diagnostic must remain an Info suggestion so Rider can discover its quick fix without producing build warnings or errors.");
        }

        var fixableIds = _codeFix.FixableDiagnosticIds.ToImmutableHashSet(StringComparer.Ordinal);
        var missingFixes = DiagnosticCatalog.All
            .Where(metadata => metadata.HasCodeFix && !fixableIds.Contains(metadata.DiagnosticId))
            .ToArray();
        if (missingFixes.Length != 0)
        {
            throw new InvalidOperationException(
                "Every fixable diagnostic must have a quick fix: " +
                string.Join(", ", missingFixes.Select(item => item.DiagnosticId)));
        }

        var unexpectedFixes = DiagnosticCatalog.All
            .Where(metadata => !metadata.HasCodeFix && fixableIds.Contains(metadata.DiagnosticId))
            .ToArray();
        if (unexpectedFixes.Length != 0)
        {
            throw new InvalidOperationException(
                "Diagnostic-only rules registered code fixes: " +
                string.Join(", ", unexpectedFixes.Select(item => item.DiagnosticId)));
        }

        var fixAllIds = _codeFix.GetFixAllProvider()
            .GetSupportedFixAllDiagnosticIds(_codeFix)
            .ToImmutableHashSet(StringComparer.Ordinal);
        var expectedFixAllIds = DiagnosticCatalog.All
            .Where(rule => rule.SupportsFixAll)
            .Select(rule => rule.DiagnosticId)
            .ToImmutableHashSet(StringComparer.Ordinal);
        if (!fixAllIds.SetEquals(expectedFixAllIds))
        {
            throw new InvalidOperationException("The Fix All provider must exactly match the safe rule catalog.");
        }

        if (DiagnosticCatalog.All.Any(rule =>
                rule.Safety != RuleSafety.Safe && rule.SupportsFixAll))
        {
            throw new InvalidOperationException("Review-required and experimental rules cannot support Fix All.");
        }
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
        complicatedFixes = complicatedFixes.Add(DiagnosticIds.CacheShaderPropertyId);
        foreach (var metadata in DiagnosticCatalog.All.Where(metadata => metadata.HasCodeFix))
        {
            _positiveCaseCounts.TryGetValue(metadata.DiagnosticId, out var count);
            var required = complicatedFixes.Contains(metadata.DiagnosticId) ? 10 : 5;
            if (count < required)
            {
                failures.Add(metadata.DiagnosticId + ": " + count + "/" + required);
            }
        }

        if (failures.Count != 0)
        {
            throw new InvalidOperationException(
                "Quick-fix test coverage is below policy: " + string.Join(", ", failures));
        }
    }

    private async Task VerifyNamespaceConsistencyAsync()
    {
        const string firstNeighbor = """
            namespace Game.Player
            {
                class ExistingController { }
            }
            """;
        const string secondNeighbor = """
            namespace Game.Player
            {
                struct ExistingState { }
            }
            """;

        await VerifyFixAsync(
            "class PlayerController { }",
            DiagnosticIds.MatchFolderNamespace,
            """
            namespace Game.Player
            {
                class PlayerController { }
            }
            """,
            firstNeighbor);
        await VerifyFixAsync(
            "using System;\nclass PlayerClock { DateTime Value; }",
            DiagnosticIds.MatchFolderNamespace,
            """
            using System;
            namespace Game.Player
            {
                class PlayerClock { DateTime Value; }
            }
            """,
            firstNeighbor,
            secondNeighbor);
        await VerifyFixAsync(
            "class PlayerInput { }\nstruct PlayerCommand { }",
            DiagnosticIds.MatchFolderNamespace,
            """
            namespace Game.Player
            {
                class PlayerInput { }
                struct PlayerCommand { }
            }
            """,
            firstNeighbor,
            secondNeighbor);
        await VerifyFixAsync(
            "delegate void PlayerEvent();\nenum PlayerMode { Idle }",
            DiagnosticIds.MatchFolderNamespace,
            """
            namespace Game.Player
            {
                delegate void PlayerEvent();
                enum PlayerMode { Idle }
            }
            """,
            firstNeighbor,
            secondNeighbor);
        await VerifyFixAsync(
            "interface IPlayerState { }",
            DiagnosticIds.MatchFolderNamespace,
            """
            namespace Game.Player
            {
                interface IPlayerState { }
            }
            """,
            firstNeighbor,
            secondNeighbor);

        var conflictingDocument = CreateDocument(
            "class PlayerView { }",
            additionalSource: "namespace Game.Player { class NeighborA { } }",
            secondAdditionalSource: "namespace Game.UI { class NeighborB { } }");
        var conflictingDiagnostics = await GetDiagnosticsAsync(conflictingDocument);
        if (conflictingDiagnostics.Any(diagnostic =>
                diagnostic.Id == DiagnosticIds.MatchFolderNamespace))
        {
            throw new InvalidOperationException(
                "UBP0075 must remain silent when neighboring namespaces conflict.");
        }

        var noNeighborDocument = CreateDocument("class PlayerView { }");
        var noNeighborDiagnostics = await GetDiagnosticsAsync(noNeighborDocument);
        if (noNeighborDiagnostics.Any(diagnostic =>
                diagnostic.Id == DiagnosticIds.MatchFolderNamespace))
        {
            throw new InvalidOperationException(
                "UBP0075 must require at least one neighboring namespace example.");
        }

        var mixedNamespaceDocument = CreateDocument(
            "namespace Other { class Nested { } }\nclass GlobalType { }",
            additionalSource: firstNeighbor,
            secondAdditionalSource: secondNeighbor);
        var mixedNamespaceDiagnostics = await GetDiagnosticsAsync(mixedNamespaceDocument);
        if (mixedNamespaceDiagnostics.Any(diagnostic =>
                diagnostic.Id == DiagnosticIds.MatchFolderNamespace))
        {
            throw new InvalidOperationException(
                "UBP0075 must remain silent for files that mix namespace and global declarations.");
        }
    }

    private async Task VerifyConfigurationAsync()
    {
        const string threeAdds = """
            using System.Collections.Generic;
            class Builder
            {
                void Build()
                {
                    var values = new List<int>();
                    values.Add(1);
                    values.Add(2);
                    values.Add(3);
                }
            }
            """;

        await VerifyDiagnosticPresenceAsync(
            threeAdds,
            DiagnosticIds.PreallocateList,
            expected: false);
        await VerifyDiagnosticPresenceAsync(
            threeAdds,
            DiagnosticIds.PreallocateList,
            expected: true,
            """
            root = true

            [*.cs]
            ubp_minimum_list_adds = 3
            """);
        await VerifyDiagnosticPresenceAsync(
            threeAdds,
            DiagnosticIds.PreallocateList,
            expected: false,
            """
            root = true

            [*.cs]
            ubp_minimum_list_adds = invalid
            """);
        await VerifyDiagnosticPresenceAsync(
            threeAdds,
            DiagnosticIds.PreallocateList,
            expected: true,
            """
            root = true

            [*.cs]
            ubp_minimum_list_adds = 8

            [Test.cs]
            ubp_minimum_list_adds = 3
            """);

        const string reviewRule = """
            using UnityEngine;
            class Distance
            {
                bool Near(Vector3 value) => value.magnitude < 5f;
            }
            """;
        await VerifyDiagnosticPresenceAsync(
            reviewRule,
            DiagnosticIds.UseSquaredMagnitude,
            expected: false,
            """
            root = true

            [*.cs]
            ubp_enable_review_required = false
            """);

        const string dotsSource = """
            using Unity.Entities;

            class JobRunner
            {
                void Update()
                {
                    new MovementJob().Run();
                }
            }

            [Unity.Burst.BurstCompile]
            partial struct MovementJob : IJobEntity
            {
                public void Execute(ref Position position) { }
            }

            struct Position : IComponentData { public float Value; }
            """;
        await VerifyDiagnosticPresenceAsync(
            dotsSource,
            DiagnosticIds.JobEntityRunToSchedule,
            expected: false,
            """
            root = true

            [*.cs]
            ubp_enable_dots_migration = false
            """);
    }

    private async Task VerifyEncapsulationSafetyAsync()
    {
        const string declaration = """
            using UnityEngine;
            public partial class PlayerSettings : MonoBehaviour
            {
                public float movementSpeed = 5f;
            }
            """;

        await VerifyEncapsulationFixAvailabilityAsync(
            declaration,
            """
            public class Consumer
            {
                float Read(PlayerSettings settings) => settings.movementSpeed;
            }
            """,
            expectedFix: false);

        await VerifyEncapsulationFixAvailabilityAsync(
            declaration,
            """
            public sealed class SpecializedSettings : PlayerSettings
            {
                float Read() => movementSpeed;
            }
            """,
            expectedFix: false);

        await VerifyEncapsulationFixAvailabilityAsync(
            declaration,
            """
            public class MetadataConsumer
            {
                string FieldName => nameof(PlayerSettings.movementSpeed);
            }
            """,
            expectedFix: false);

        await VerifyEncapsulationFixAvailabilityAsync(
            declaration,
            """
            public partial class PlayerSettings
            {
                float Read() => movementSpeed;
            }
            """,
            expectedFix: true);

        await VerifyEncapsulationFixAvailabilityAsync(
            """
            using UnityEngine;
            public class PlayerSettings : MonoBehaviour
            {
                public float movementSpeed = 5f;

                private sealed class Reader
                {
                    float Read(PlayerSettings settings) => settings.movementSpeed;
                }
            }
            """,
            "public sealed class Unrelated { }",
            expectedFix: true);
    }

    private async Task VerifyEncapsulationFixAvailabilityAsync(
        string declaration,
        string additionalSource,
        bool expectedFix)
    {
        var document = CreateDocument(
            declaration,
            editorConfig: null,
            additionalSource: additionalSource);
        var diagnostics = await GetDiagnosticsAsync(document);
        var diagnostic = diagnostics.Single(item => item.Id == DiagnosticIds.EncapsulateSerializedField);
        var actions = new List<CodeAction>();
        await _codeFix.RegisterCodeFixesAsync(
            new CodeFixContext(
                document,
                diagnostic,
                (action, _) => actions.Add(action),
                CancellationToken.None));

        if ((actions.Count != 0) != expectedFix)
        {
            throw new InvalidOperationException(
                $"UBP0001 fix availability should be {expectedFix}, but {actions.Count} actions were offered.");
        }
    }

}
