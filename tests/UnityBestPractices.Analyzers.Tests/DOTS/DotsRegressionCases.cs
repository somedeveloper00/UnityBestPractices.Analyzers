using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using UnityBestPractices.Analyzers;

internal sealed partial class AnalyzerTests
{
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

        await VerifyExactDotsDiagnosticsAsync(
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
            """,
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            DiagnosticIds.EntitiesForEachToJobEntityRun,
            DiagnosticIds.EntitiesForEachToJobEntitySchedule,
            DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel);
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

        const string expressionBodiedForEachSource = """
            using Unity.Entities;

            partial class ExpressionMovementSystem : SystemBase
            {
                void Update()
                {
                    Entities.ForEach((ref Position position, in Velocity velocity) => position.Value += velocity.Value).Run();
                }
            }

            struct Position : IComponentData { public float Value; }
            struct Velocity : IComponentData { public float Value; }
            """;

        await VerifyFixAsync(
            expressionBodiedForEachSource,
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            """
            using Unity.Entities;

            partial class ExpressionMovementSystem : SystemBase
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

        const string braceLessSystemApiQuerySource = """
            using Unity.Entities;

            partial class MovementSystem : SystemBase
            {
                void Update()
                {
                    foreach (var (position, velocity) in SystemAPI.Query<RefRW<Position>, RefRO<Velocity>>())
                        position.ValueRW.Value += velocity.ValueRO.Value;
                }
            }

            struct Position : IComponentData { public float Value; }
            struct Velocity : IComponentData { public float Value; }
            """;

        await VerifySystemApiJobFixAsync(
            braceLessSystemApiQuerySource,
            DiagnosticIds.SystemApiQueryToJobEntityRun,
            "Run");

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

}
