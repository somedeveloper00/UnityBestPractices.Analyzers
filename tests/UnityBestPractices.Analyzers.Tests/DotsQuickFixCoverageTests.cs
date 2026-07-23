using System;
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
            string existingMembers = "")
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
                "Entity entity, ref Position position, in Velocity velocity"),
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
            CreateJobSource(item, "EntitiesForEachJob", "Run"));
        await VerifyFixAsync(
            source,
            DiagnosticIds.EntitiesForEachToJobEntitySchedule,
            CreateJobSource(item, "EntitiesForEachJob", "Schedule"));
        await VerifyFixAsync(
            source,
            DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel,
            CreateJobSource(item, "EntitiesForEachJob", "ScheduleParallel"));
    }

    private async Task VerifySystemApiMigrationCaseAsync(DotsMigrationCase item)
    {
        var source = CreateSystemApiSource(item);
        await VerifyFixAsync(
            source,
            DiagnosticIds.SystemApiQueryToJobEntityRun,
            CreateJobSource(item, "SystemApiQueryJob", "Run"));
        await VerifyFixAsync(
            source,
            DiagnosticIds.SystemApiQueryToJobEntitySchedule,
            CreateJobSource(item, "SystemApiQueryJob", "Schedule"));
        await VerifyFixAsync(
            source,
            DiagnosticIds.SystemApiQueryToJobEntityScheduleParallel,
            CreateJobSource(item, "SystemApiQueryJob", "ScheduleParallel"));
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
        string executionMode)
    {
        var jobName = item.Name == "NameCollision" ? baseJobName + "2" : baseJobName;
        var attributes = string.IsNullOrEmpty(item.JobAttributes) ? string.Empty : item.JobAttributes + " ";
        return "using Unity.Entities; partial class " + item.Name + "System : SystemBase { void Update() { new " +
               jobName + "()." + executionMode + "(); } " + item.ExistingMembers +
               " [Unity.Burst.BurstCompile] " + attributes + "private partial struct " + jobName +
               " : Unity.Entities.IJobEntity { public void Execute(" + item.JobParameters + ") { " + item.Body +
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

    private const string ComponentDeclarations =
        "struct Position : IComponentData { public float Value; } " +
        "struct Velocity : IComponentData { public float Value; } " +
        "struct Tag : IComponentData { }";
}
