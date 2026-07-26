using System;
using System.Linq;
using System.Threading.Tasks;
using UnityBestPractices.Analyzers;

internal sealed partial class AnalyzerTests
{
    private sealed class DotsMigrationCase
    {
        internal DotsMigrationCase(
            string name,
            string chain,
            string lambdaParameters,
            string body,
            string iterationVariable,
            string queryTypes,
            string querySuffix,
            string systemApiBody,
            string jobParameters,
            string jobAttributes = "",
            string existingMembers = "",
            string? systemApiJobParameters = null)
        {
            Name = name;
            Chain = chain;
            LambdaParameters = lambdaParameters;
            Body = body;
            IterationVariable = iterationVariable;
            QueryTypes = queryTypes;
            QuerySuffix = querySuffix;
            SystemApiBody = systemApiBody;
            JobParameters = jobParameters;
            SystemApiJobParameters = systemApiJobParameters ?? jobParameters;
            JobAttributes = jobAttributes;
            ExistingMembers = existingMembers;
        }

        internal string Name { get; }
        internal string Chain { get; }
        internal string LambdaParameters { get; }
        internal string Body { get; }
        internal string IterationVariable { get; }
        internal string QueryTypes { get; }
        internal string QuerySuffix { get; }
        internal string SystemApiBody { get; }
        internal string JobParameters { get; }
        internal string SystemApiJobParameters { get; }
        internal string JobAttributes { get; }
        internal string ExistingMembers { get; }
    }

    private async Task VerifyDotsEdgeCaseMatrixAsync()
    {
        var cases = new[]
        {
            new DotsMigrationCase(
                "SingleWriter", "", "ref Position position", "position.Value++;",
                "var position", "Unity.Entities.RefRW<Position>", "", "position.ValueRW.Value++;",
                "ref Position position"),
            new DotsMigrationCase(
                "SingleReader", "", "in Velocity velocity", "var sample = velocity.Value;",
                "var velocity", "Unity.Entities.RefRO<Velocity>", "", "var sample = velocity.ValueRO.Value;",
                "in Velocity velocity"),
            new DotsMigrationCase(
                "ReaderWriter", "", "ref Position position, in Velocity velocity", "position.Value += velocity.Value;",
                "var (position, velocity)", "Unity.Entities.RefRW<Position>, Unity.Entities.RefRO<Velocity>", "",
                "position.ValueRW.Value += velocity.ValueRO.Value;", "ref Position position, in Velocity velocity"),
            new DotsMigrationCase(
                "EntityAccess", "", "Entity entity, ref Position position, in Velocity velocity",
                "position.Value += velocity.Value + entity.GetHashCode();",
                "var (position, velocity, entity)", "Unity.Entities.RefRW<Position>, Unity.Entities.RefRO<Velocity>",
                ".WithEntityAccess()", "position.ValueRW.Value += velocity.ValueRO.Value + entity.GetHashCode();",
                "Entity entity, ref Position position, in Velocity velocity",
                systemApiJobParameters:
                    "ref Position position, in Velocity velocity, Unity.Entities.Entity entity"),
            new DotsMigrationCase(
                "WithAll", ".WithAll<Tag>()", "ref Position position", "position.Value += 1f;",
                "var position", "Unity.Entities.RefRW<Position>", ".WithAll<Tag>()", "position.ValueRW.Value += 1f;",
                "ref Position position", "[Unity.Entities.WithAll(typeof(Tag))]"),
            new DotsMigrationCase(
                "WithNone", ".WithNone<Tag>()", "ref Position position", "position.Value -= 1f;",
                "var position", "Unity.Entities.RefRW<Position>", ".WithNone<Tag>()", "position.ValueRW.Value -= 1f;",
                "ref Position position", "[Unity.Entities.WithNone(typeof(Tag))]"),
            new DotsMigrationCase(
                "WithAny", ".WithAny<Tag>()", "in Velocity velocity", "var sample = velocity.Value * 2f;",
                "var velocity", "Unity.Entities.RefRO<Velocity>", ".WithAny<Tag>()", "var sample = velocity.ValueRO.Value * 2f;",
                "in Velocity velocity", "[Unity.Entities.WithAny(typeof(Tag))]"),
            new DotsMigrationCase(
                "ChangeFilter", ".WithChangeFilter<Velocity>()", "ref Position position, in Velocity velocity",
                "position.Value = velocity.Value;", "var (position, velocity)",
                "Unity.Entities.RefRW<Position>, Unity.Entities.RefRO<Velocity>", ".WithChangeFilter<Velocity>()",
                "position.ValueRW.Value = velocity.ValueRO.Value;", "ref Position position, in Velocity velocity",
                "[Unity.Entities.WithChangeFilter(typeof(Velocity))]"),
            new DotsMigrationCase(
                "QueryOptions", ".WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities)",
                "ref Position position", "position.Value = 4f;", "var position", "Unity.Entities.RefRW<Position>",
                ".WithOptions(EntityQueryOptions.IncludeDisabledEntities)", "position.ValueRW.Value = 4f;",
                "ref Position position", "[Unity.Entities.WithOptions(EntityQueryOptions.IncludeDisabledEntities)]"),
            new DotsMigrationCase(
                "NameCollision", ".WithAll<Tag>().WithNone<Velocity>()", "ref Position position",
                "position.Value *= 2f;", "var position", "Unity.Entities.RefRW<Position>",
                ".WithAll<Tag>().WithNone<Velocity>()", "position.ValueRW.Value *= 2f;", "ref Position position",
                "[Unity.Entities.WithAll(typeof(Tag))][Unity.Entities.WithNone(typeof(Velocity))]",
                "private struct EntitiesForEachJob { } private struct SystemApiQueryJob { }")
        };

        foreach (var item in cases)
        {
            await VerifyEntitiesMigrationCaseAsync(item);
            await VerifySystemApiMigrationCaseAsync(item);
        }

        await VerifyAdditionalJobModeCasesAsync();
        await VerifyDotsDiagnosticSetsAsync();
        await VerifyEntitiesForEachRegressionCasesAsync();
        await VerifyRejectedDotsMigrationsAsync();
    }

    private async Task VerifyEntitiesForEachRegressionCasesAsync()
    {
        await VerifyFixAsync(
            "using Unity.Entities; partial class CapturedSystem : SystemBase { void Update() { " +
            "var scale = 2f; Entities.ForEach((ref Position p) => { p.Value *= scale; }).Run(); } } " +
            "struct Position : IComponentData { public float Value; }",
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            "using Unity.Entities; partial class CapturedSystem : SystemBase { void Update() { " +
            "var scale = 2f; foreach (var p in Unity.Entities.SystemAPI.Query<Unity.Entities.RefRW<Position>>()) " +
            "{ p.ValueRW.Value *= scale; } } } struct Position : IComponentData { public float Value; }");

        await VerifyFixAsync(
            "using Unity.Entities; partial class EntityOnlySystem : SystemBase { void Update() { " +
            "Entities.WithoutBurst().WithAll<Tag>().ForEach((Entity entity) => " +
            "{ var hash = entity.GetHashCode(); }).Run(); } } struct Tag : IComponentData { }",
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            "using Unity.Entities; partial class EntityOnlySystem : SystemBase { void Update() { " +
            "foreach (var (_, entity) in Unity.Entities.SystemAPI.Query<Unity.Entities.RefRO<Tag>>()" +
            ".WithAll<Tag>().WithEntityAccess()) " +
            "{ var hash = entity.GetHashCode(); } } } struct Tag : IComponentData { }");

        await VerifyFixAsync(
            "using Unity.Entities; partial class StructuralEntitySystem : SystemBase { void Update() { " +
            "Entities.WithoutBurst().WithStructuralChanges().WithAll<Tag>().ForEach((Entity entity) => " +
            "{ var hash = entity.GetHashCode(); }).Run(); } } struct Tag : IComponentData { }",
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            "using Unity.Entities; partial class StructuralEntitySystem : SystemBase { void Update() { { " +
            "using (var entitiesSnapshot = new Unity.Collections.NativeList<Unity.Entities.Entity>" +
            "(Unity.Collections.Allocator.Temp)) { foreach (var (_, entity) in Unity.Entities.SystemAPI" +
            ".Query<Unity.Entities.RefRO<Tag>>().WithAll<Tag>().WithEntityAccess()) " +
            "{ entitiesSnapshot.Add(entity); } foreach (var entity in entitiesSnapshot) " +
            "{ var hash = entity.GetHashCode(); } } } } } struct Tag : IComponentData { }");

        await VerifyFixAsync(
            "using Unity.Entities; partial class EcbSystem : SystemBase { void Update() { " +
            "var ecb = new EntityCommandBuffer().AsParallelWriter(); Entities.WithAll<Tag>().ForEach(" +
            "(Entity entity, int entityInQueryIndex, in Health health) => { if (health.Value <= 0) { " +
            "ecb.AddComponent(entityInQueryIndex, entity, new Finish { StartTime = " +
            "SystemAPI.Time.ElapsedTime }); } }).ScheduleParallel(); } } " +
            "struct Tag : IComponentData { } struct Health : IComponentData { public int Value; } " +
            "struct Finish : IComponentData { public double StartTime; }",
            DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel,
            "using Unity.Entities; partial class EcbSystem : SystemBase { void Update() { " +
            "var ecb = new EntityCommandBuffer().AsParallelWriter(); new EntitiesForEachJob { Ecb = ecb, " +
            "ElapsedTime = Unity.Entities.SystemAPI.Time.ElapsedTime }.ScheduleParallel(); } " +
            "[Unity.Burst.BurstCompile] [Unity.Entities.WithAll(typeof(Tag))] private partial struct " +
            "EntitiesForEachJob : Unity.Entities.IJobEntity { public EntityCommandBuffer.ParallelWriter Ecb; " +
            "public double ElapsedTime; public void Execute(Entity entity, " +
            "[Unity.Entities.EntityIndexInQuery] int entityInQueryIndex, in Health health) { " +
            "if (health.Value <= 0) { Ecb.AddComponent(entityInQueryIndex, entity, new Finish { " +
            "StartTime = ElapsedTime }); } } } } struct Tag : IComponentData { } " +
            "struct Health : IComponentData { public int Value; } " +
            "struct Finish : IComponentData { public double StartTime; }");
    }

    private async Task VerifyEntitiesMigrationCaseAsync(DotsMigrationCase item)
    {
        var source = CreateEntitiesForEachSource(item);
        await VerifyFixAsync(
            source,
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            CreateSystemApiSource(item));

        await VerifyFixAsync(
            source,
            DiagnosticIds.EntitiesForEachToJobEntityRun,
            CreateJobSource(item, "EntitiesForEachJob", "Run", fromSystemApi: false));
        await VerifyFixAsync(
            source,
            DiagnosticIds.EntitiesForEachToJobEntitySchedule,
            CreateJobSource(item, "EntitiesForEachJob", "Schedule", fromSystemApi: false));
        await VerifyFixAsync(
            source,
            DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel,
            CreateJobSource(item, "EntitiesForEachJob", "ScheduleParallel", fromSystemApi: false));
    }

    private async Task VerifySystemApiMigrationCaseAsync(DotsMigrationCase item)
    {
        var source = CreateSystemApiSource(item);
        await VerifyFixAsync(
            source,
            DiagnosticIds.SystemApiQueryToJobEntityRun,
            CreateJobSource(item, "SystemApiQueryJob", "Run", fromSystemApi: true));
        await VerifyFixAsync(
            source,
            DiagnosticIds.SystemApiQueryToJobEntitySchedule,
            CreateJobSource(item, "SystemApiQueryJob", "Schedule", fromSystemApi: true));
        await VerifyFixAsync(
            source,
            DiagnosticIds.SystemApiQueryToJobEntityScheduleParallel,
            CreateJobSource(item, "SystemApiQueryJob", "ScheduleParallel", fromSystemApi: true));
    }

    private static string CreateEntitiesForEachSource(DotsMigrationCase item) =>
        "using Unity.Entities; partial class " + item.Name + "System : SystemBase { void Update() { Entities" +
        item.Chain + ".ForEach((" + item.LambdaParameters + ") => { " + item.Body + " }).Run(); } " +
        item.ExistingMembers + " } " + ComponentDeclarations;

    private static string CreateSystemApiSource(DotsMigrationCase item) =>
        "using Unity.Entities; partial class " + item.Name + "System : SystemBase { void Update() { foreach (" +
        item.IterationVariable + " in Unity.Entities.SystemAPI.Query<" + item.QueryTypes + ">()" + item.QuerySuffix +
        ") { " + item.SystemApiBody + " } } " + item.ExistingMembers + " } " + ComponentDeclarations;

    private static string CreateJobSource(
        DotsMigrationCase item,
        string baseJobName,
        string executionMode,
        bool fromSystemApi)
    {
        var jobName = item.Name == "NameCollision" ? baseJobName + "2" : baseJobName;
        var attributes = string.IsNullOrEmpty(item.JobAttributes) ? string.Empty : item.JobAttributes + " ";
        return "using Unity.Entities; partial class " + item.Name + "System : SystemBase { void Update() { new " +
               jobName + "()." + executionMode + "(); } " + item.ExistingMembers +
               " [Unity.Burst.BurstCompile] " + attributes + "private partial struct " + jobName +
               " : Unity.Entities.IJobEntity { public void Execute(" +
               (fromSystemApi ? item.SystemApiJobParameters : item.JobParameters) + ") { " + item.Body +
               " } } } " + ComponentDeclarations;
    }

    private async Task VerifyAdditionalJobModeCasesAsync()
    {
        var transitions = new[]
        {
            ("Run", "Schedule", DiagnosticIds.JobEntityRunToSchedule),
            ("Run", "ScheduleParallel", DiagnosticIds.JobEntityRunToScheduleParallel),
            ("Schedule", "Run", DiagnosticIds.JobEntityScheduleToRun),
            ("Schedule", "ScheduleParallel", DiagnosticIds.JobEntityScheduleToScheduleParallel),
            ("ScheduleParallel", "Run", DiagnosticIds.JobEntityScheduleParallelToRun),
            ("ScheduleParallel", "Schedule", DiagnosticIds.JobEntityScheduleParallelToSchedule),
        };
        foreach (var transition in transitions)
        {
            await VerifyJobModeContextAsync(transition.Item1, transition.Item2, transition.Item3, 0);
            await VerifyJobModeContextAsync(transition.Item1, transition.Item2, transition.Item3, 1);
            await VerifyJobModeContextAsync(transition.Item1, transition.Item2, transition.Item3, 2);
        }
    }

    private Task VerifyJobModeContextAsync(string sourceMode, string targetMode, string diagnosticId, int context)
    {
        var sourceCall = context switch
        {
            0 => "var job = new MovementJob(); job." + sourceMode + "();",
            1 => "default(MovementJob)." + sourceMode + "();",
            _ => "_job." + sourceMode + "();",
        };
        var targetCall = context switch
        {
            0 => "var job = new MovementJob(); job." + targetMode + "();",
            1 => "default(MovementJob)." + targetMode + "();",
            _ => "_job." + targetMode + "();",
        };
        const string prefix =
            "using Unity.Entities; class Runner { private MovementJob _job; void Update() { ";
        const string suffix =
            " } } [Unity.Burst.BurstCompile] partial struct MovementJob : IJobEntity { public void Execute(ref Position position) { } } struct Position : IComponentData { public float Value; }";
        return VerifyFixAsync(prefix + sourceCall + suffix, diagnosticId, prefix + targetCall + suffix);
    }

    private async Task VerifyDotsDiagnosticSetsAsync()
    {
        const string components =
            "struct Position : IComponentData { public float Value; } " +
            "struct Velocity : IComponentData { public float Value; }";
        await VerifyExactDotsDiagnosticsAsync(
            "using Unity.Entities; partial class S : SystemBase { void Update() { " +
            "Entities.ForEach((ref Position p, in Velocity v) => { p.Value += v.Value; }).Run(); } } " +
            components,
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            DiagnosticIds.EntitiesForEachToJobEntityRun,
            DiagnosticIds.EntitiesForEachToJobEntitySchedule,
            DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel);
        await VerifyExactDotsDiagnosticsAsync(
            "using Unity.Entities; partial class S : SystemBase { void Update() { " +
            "Entities.ForEach((ref Position p, in Velocity v) => { p.Value += v.Value; }).Schedule(); } } " +
            components,
            DiagnosticIds.EntitiesForEachToJobEntityRun,
            DiagnosticIds.EntitiesForEachToJobEntitySchedule,
            DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel);
        await VerifyExactDotsDiagnosticsAsync(
            "using Unity.Entities; partial class S : SystemBase { void Update() { foreach " +
            "(var (p, v) in SystemAPI.Query<RefRW<Position>, RefRO<Velocity>>()) " +
            "{ p.ValueRW.Value += v.ValueRO.Value; } } } " + components,
            DiagnosticIds.SystemApiQueryToJobEntityRun,
            DiagnosticIds.SystemApiQueryToJobEntitySchedule,
            DiagnosticIds.SystemApiQueryToJobEntityScheduleParallel);
        await VerifyExactDotsDiagnosticsAsync(
            "using Unity.Entities; class S { void Update() { new MovementJob().Run(); } } " +
            "partial struct MovementJob : IJobEntity { public void Execute(ref Position p) { } } " +
            components,
            DiagnosticIds.JobEntityRunToSchedule,
            DiagnosticIds.JobEntityRunToScheduleParallel);
    }

    private async Task VerifyRejectedDotsMigrationsAsync()
    {
        await VerifyExactDotsDiagnosticsAsync(
            "using Unity.Entities; partial class CapturedEntitiesSystem : SystemBase { void Update() { " +
            "var scale = 2f; Entities.ForEach((ref Position p) => { p.Value *= scale; }).Run(); } } " +
            "struct Position : IComponentData { public float Value; }",
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            DiagnosticIds.EntitiesForEachToJobEntityRun,
            DiagnosticIds.EntitiesForEachToJobEntitySchedule,
            DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel);

        await VerifyExactDotsDiagnosticsAsync(
            "using Unity.Entities; partial class CapturedQuerySystem : SystemBase { void Update() { " +
            "var scale = 2f; foreach (var p in SystemAPI.Query<RefRW<Position>>()) " +
            "{ p.ValueRW.Value *= scale; } } } struct Position : IComponentData { public float Value; }");

        await VerifyExactDotsDiagnosticsAsync(
            "using Unity.Entities; partial class MutatingCaptureSystem : SystemBase { void Update() { " +
            "var total = 0; Entities.ForEach((in Position p) => { total++; }).Run(); } } " +
            "struct Position : IComponentData { public float Value; }",
            DiagnosticIds.EntitiesForEachToSystemApiQuery);

        await VerifyExactDotsDiagnosticsAsync(
            "using Unity.Entities; partial class RawWrapperSystem : SystemBase { void Update() { " +
            "foreach (var p in SystemAPI.Query<RefRW<Position>>()) { object wrapper = p; } } } " +
            "struct Position : IComponentData { public float Value; }");

        await VerifyExactDotsDiagnosticsAsync(
            "using Unity.Entities; partial class StructuralSystem : SystemBase { void Update() { " +
            "Entities.WithStructuralChanges().ForEach((ref Position p) => { p.Value++; }).Run(); } } " +
            "struct Position : IComponentData { public float Value; }");

        await VerifyExactDotsDiagnosticsAsync(
            "using Unity.Entities; partial class UnsupportedWrapperSystem : SystemBase { void Update() { " +
            "foreach (var p in SystemAPI.Query<Position>()) { var value = p.Value; } } } " +
            "struct Position : IComponentData { public float Value; }");

        await VerifyExactDotsDiagnosticsAsync(
            "namespace Other { public delegate void RefAction<T>(ref T value); " +
            "public struct Builder { public Description ForEach<T>(RefAction<T> action) => default; } " +
            "public struct Description { public void Run() { } } " +
            "public abstract class SystemBase { protected Builder Entities => default; } } " +
            "class LookalikeSystem : Other.SystemBase { void Update() { " +
            "Entities.ForEach((ref Position p) => { p.Value++; }).Run(); } } " +
            "struct Position { public float Value; }");

        await VerifyExactDotsDiagnosticsAsync(
            "namespace Other { public interface IJob { } public struct FakeJob : IJob { } " +
            "public static class JobExtensions { public static void Run<T>(this T job) where T : struct, IJob { } } } " +
            "class Runner { void Update() { Other.JobExtensions.Run(new Other.FakeJob()); } }");
    }

    private async Task VerifyExactDotsDiagnosticsAsync(string source, params string[] expectedIds)
    {
        var document = CreateDocument(source);
        var diagnostics = await GetDiagnosticsAsync(document);
        var actualIds = diagnostics
            .Select(diagnostic => diagnostic.Id)
            .Where(IsDotsQueryDiagnostic)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var orderedExpectedIds = expectedIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (!actualIds.SequenceEqual(orderedExpectedIds, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Unexpected DOTS diagnostics. Expected: " + string.Join(", ", orderedExpectedIds) +
                "; actual: " + string.Join(", ", actualIds));
        }
    }

    private static bool IsDotsQueryDiagnostic(string diagnosticId) =>
        string.CompareOrdinal(diagnosticId, DiagnosticIds.EntitiesForEachToSystemApiQuery) >= 0 &&
        string.CompareOrdinal(diagnosticId, DiagnosticIds.JobEntityScheduleParallelToSchedule) <= 0;

    private const string ComponentDeclarations =
        "struct Position : IComponentData { public float Value; } " +
        "struct Velocity : IComponentData { public float Value; } " +
        "struct Tag : IComponentData { }";
}
