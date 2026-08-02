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
                "SingleWriterOut", "", "out Position position", "position = new Position { Value = 3f };",
                "var position", "Unity.Entities.RefRW<Position>", "",
                "position.ValueRW = new Position { Value = 3f };", "ref Position position"),
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
        await VerifyRequestedDotsExtractionCoverageAsync();
    }

    // Keep this matrix deliberately data-driven: every entry still compares the complete
    // source produced by the fixer, while making it inexpensive to add a regression for a
    // new capture shape. Together with the rejected matrix below this contributes 30 cases.
    private async Task VerifyRequestedDotsExtractionCoverageAsync()
    {
        var positiveCases = new[]
        {
            ("CompactSchedule", "var scale = 2f;", "", "p.Value *= scale;", "Scale", "float", "scale", false, "Schedule"),
            ("CompactRun", "var scale = 2f;", "", "p.Value *= scale;", "Scale", "float", "scale", false, "Run"),
            ("ReadOnlyOne", "var data = new Sample { Value = 2f };", ".WithReadOnly(data)", "p.Value += data.Value;", "Data", "global::Sample", "data", true, "Schedule"),
            ("ReadOnlyTwoA", "var alpha = new Sample { Value = 2f };", ".WithReadOnly(alpha)", "p.Value += alpha.Value;", "Alpha", "global::Sample", "alpha", true, "Schedule"),
            ("ReadOnlyTwoB", "var beta = new Sample { Value = 3f };", ".WithReadOnly(beta)", "p.Value += beta.Value;", "Beta", "global::Sample", "beta", true, "Schedule"),
            ("ReadOnlySeveral", "var gamma = new Sample { Value = 4f };", ".WithReadOnly(gamma)", "p.Value += gamma.Value;", "Gamma", "global::Sample", "gamma", true, "Schedule"),
            ("JobHandleName", "var jobHandle = 2f;", "", "p.Value += jobHandle;", "JobHandle", "float", "jobHandle", false, "Schedule"),
            ("JobHandle2Name", "var jobHandle2 = 2f;", "", "p.Value += jobHandle2;", "JobHandle2", "float", "jobHandle2", false, "Schedule"),
            ("LookupCase", "var aircraftDataLookup = new Sample();", ".WithReadOnly(aircraftDataLookup)", "p.Value += aircraftDataLookup.Value;", "AircraftDataLookup", "global::Sample", "aircraftDataLookup", true, "Schedule"),
            ("LookupSimilarCase", "var AircraftDataLookup = new Sample();", ".WithReadOnly(AircraftDataLookup)", "p.Value += AircraftDataLookup.Value;", "AircraftDataLookup2", "global::Sample", "AircraftDataLookup", true, "Schedule"),
            ("Conditional", "var amount = true ? 1f : 2f;", "", "p.Value += amount;", "Amount", "float", "amount", false, "Schedule"),
            ("NestedBlock", "var amount = 1f;", "", "if (p.Value > 0) { p.Value += amount; }", "Amount", "float", "amount", false, "Schedule"),
            ("LoopCondition", "var limit = 3f;", "", "while (p.Value < limit) p.Value++;", "Limit", "float", "limit", false, "Schedule"),
            ("EntityParameter", "var amount = 1f;", "", "p.Value += amount + entity.GetHashCode();", "Amount", "float", "amount", false, "Schedule"),
            ("FormattingTrivia", "var amount = 1f;", "", "p.Value += amount;", "Amount", "float", "amount", false, "Schedule"),
            ("JobTypeCollision", "var amount = 1f;", "", "p.Value += amount;", "Amount", "float", "amount", false, "Schedule"),
            ("FieldCollision", "var amount = 1f;", "", "p.Value += amount;", "Amount", "float", "amount", false, "Run"),
            ("ParallelBehavior", "var amount = 1f;", "", "p.Value += amount;", "Amount", "float", "amount", false, "ScheduleParallel"),
        };

        foreach (var item in positiveCases)
        {
            var entityParameter = item.Item1 == "EntityParameter" ? "Entity entity, " : string.Empty;
            var existingMember = item.Item1 == "JobTypeCollision" ? "private struct EntitiesForEachJob { } " : string.Empty;
            var jobName = item.Item1 == "JobTypeCollision" ? "EntitiesForEachJob2" : "EntitiesForEachJob";
            var source = "using Unity.Entities; partial class " + item.Item1 + "System : SystemBase { void Update() { " +
                item.Item2 + " Entities" + item.Item3 + ".ForEach((" + entityParameter + "ref Position p) => { " +
                item.Item4 + " })." + item.Rest.Item2 + "(); } " + existingMember + "} " + CoverageDeclarations;
            var attribute = item.Rest.Item1 ? "[Unity.Collections.ReadOnly] " : string.Empty;
            var fixedSource = "using Unity.Entities; partial class " + item.Item1 + "System : SystemBase { void Update() { " +
                item.Item2 + " new " + jobName + " { " + item.Item5 + " = " + item.Item7 + " }." + item.Rest.Item2 + "(); } " +
                existingMember + "[Unity.Burst.BurstCompile] private partial struct " + jobName +
                " : Unity.Entities.IJobEntity { " + attribute + "public " + item.Item6 + " " + item.Item5 +
                "; public void Execute(" + entityParameter + "ref Position p) { " +
                item.Item4.Replace(item.Item7, item.Item5, StringComparison.Ordinal) + " } } } " + CoverageDeclarations;
            await VerifyFixAsync(source, item.Rest.Item2 == "Run" ? DiagnosticIds.EntitiesForEachToJobEntityRun :
                item.Rest.Item2 == "ScheduleParallel" ? DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel :
                DiagnosticIds.EntitiesForEachToJobEntitySchedule, fixedSource);
        }

        var rejectedBodies = new[]
        {
            "Entities.WithDisposeOnCompletion(Create()).ForEach((ref Position p) => { p.Value++; }).Schedule();",
            "Entities.WithDisposeOnCompletion(flag ? first : second).ForEach((ref Position p) => { p.Value++; }).Schedule();",
            "Entities.WithReadOnly(values).WithDisposeOnCompletion(values).ForEach((ref Position p) => { p.Value++; }).Schedule();",
            "Entities.WithDisposeOnCompletion(managed).ForEach((ref Position p) => { p.Value++; }).Schedule();",
            "Fake.WithDisposeOnCompletion(values).ForEach((ref Position p) => { p.Value++; }).Schedule();",
            "Entities.ForEach((Entity entity, ref Position p) => { FakeSystemAPI.HasComponent<Tag>(entity); }).Schedule();",
            "Entities.ForEach((Entity entity, ref Position p) => { SystemAPI.GetComponentRW<Tag>(entity); SystemAPI.HasComponent<Tag>(entity); }).Schedule();",
            "Entities.ForEach((Entity entity, ref Position p) => { System.Func<bool> f = () => SystemAPI.HasComponent<Tag>(entity); }).Schedule();",
            "Entities.ForEach((Entity entity, ref Position p) => { bool Local() => SystemAPI.HasComponent<Tag>(entity); }).Schedule();",
            "if (flag) Entities.WithDisposeOnCompletion(values).ForEach((ref Position p) => { p.Value++; }).Schedule();",
            "Entities.WithDisposeOnCompletion(values).ForEach((ref Position p) => { p.Value++; }).ScheduleParallel();",
            "Entities.WithDisposeOnCompletion(@event).ForEach((ref Position p) => { p.Value++; }).Schedule();",
        };
        foreach (var body in rejectedBodies)
        {
            await VerifyNoDiagnosticAsync("using System; using Unity.Entities; using Unity.Collections; partial class RejectedCoverage : SystemBase { " +
                "bool flag; NativeList<int> values, first, second; NativeList<int> @event; ManagedDisposable managed; NativeList<int> Create() => default; " +
                "void Update() { " + body + " } } static class FakeSystemAPI { public static bool HasComponent<T>(Entity e) => false; } " +
                "struct ManagedDisposable { public IDisposable Value; } static class Fake { public static EntitiesBuilder WithDisposeOnCompletion<T>(T value) where T : struct => default; } " + CoverageDeclarations);
        }
    }

    private const string CoverageDeclarations =
        "struct Sample { public float Value; } struct Position : IComponentData { public float Value; } " +
        "struct Velocity : IComponentData { public float Value; } struct Tag : IComponentData { }";

    private async Task VerifyEntitiesForEachRegressionCasesAsync()
    {
        await VerifyFixAsync(
            "using Unity.Entities; partial class TargetSystem : SystemBase { void Update() { " +
            "var collisionWorld = new Capture(); var transformLookup = new Capture(); " +
            "Entities.WithReadOnly(collisionWorld).WithReadOnly(transformLookup).WithAll<PlayerTag, LocalTag>()" +
            ".ForEach((Entity playerEntity, ref TargetData targetData, in DynamicBuffer<DetectedElement> detectedBuffer, " +
            "in PlayerInputs input, in CharacterStance stance, in AgentShape shape) => { " +
            "if (SystemAPI.Exists(targetData.Current) == false) { targetData.Current = playerEntity; } " +
            "if (detectedBuffer.Length == 0) return; targetData.Value += collisionWorld.Value + transformLookup.Value + " +
            "input.Value + stance.Value + shape.Value; }).ScheduleParallel(); } } " +
            "struct Capture { public int Value; } struct PlayerTag : IComponentData { } struct LocalTag : IComponentData { } " +
            "struct TargetData : IComponentData { public Entity Current; public int Value; } " +
            "struct DetectedElement : IBufferElementData { } struct PlayerInputs : IComponentData { public int Value; } " +
            "struct CharacterStance : IComponentData { public int Value; } struct AgentShape : IComponentData { public int Value; }",
            DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel,
            "using Unity.Entities; partial class TargetSystem : SystemBase { void Update() { " +
            "var collisionWorld = new Capture(); var transformLookup = new Capture(); " +
            "new EntitiesForEachJob { CollisionWorld = collisionWorld, TransformLookup = transformLookup, " +
            "EntityStorageInfoLookup = Unity.Entities.SystemAPI.GetEntityStorageInfoLookup() }.ScheduleParallel(); } " +
            "[Unity.Burst.BurstCompile] [Unity.Entities.WithAll(typeof(PlayerTag), typeof(LocalTag))] " +
            "private partial struct EntitiesForEachJob : Unity.Entities.IJobEntity { " +
            "[Unity.Collections.ReadOnly] public global::Capture CollisionWorld; " +
            "[Unity.Collections.ReadOnly] public global::Capture TransformLookup; " +
            "[Unity.Collections.ReadOnly] public Unity.Entities.EntityStorageInfoLookup EntityStorageInfoLookup; " +
            "public void Execute(Entity playerEntity, ref TargetData targetData, in DynamicBuffer<DetectedElement> detectedBuffer, " +
            "in PlayerInputs input, in CharacterStance stance, in AgentShape shape) { " +
            "if (EntityStorageInfoLookup.Exists(targetData.Current) == false) { targetData.Current = playerEntity; } " +
            "if (detectedBuffer.Length == 0) return; targetData.Value += CollisionWorld.Value + TransformLookup.Value + " +
            "input.Value + stance.Value + shape.Value; } } } " +
            "struct Capture { public int Value; } struct PlayerTag : IComponentData { } struct LocalTag : IComponentData { } " +
            "struct TargetData : IComponentData { public Entity Current; public int Value; } " +
            "struct DetectedElement : IBufferElementData { } struct PlayerInputs : IComponentData { public int Value; } " +
            "struct CharacterStance : IComponentData { public int Value; } struct AgentShape : IComponentData { public int Value; }");

        await VerifyExactDotsDiagnosticsAsync(
            "using Unity.Entities; using Unity.Jobs; partial class LargeCaptureSystem : SystemBase { " +
            "void Update() { var collisionWorld = new Capture(); var slots = new Capture(); " +
            "var transforms = new Capture(); var characters = new Capture(); var slotMap = new DisposableCapture(); " +
            "var hits = new DisposableCapture(); Entities.WithAll<Tag>().WithReadOnly(collisionWorld)" +
            ".WithReadOnly(slots).WithReadOnly(transforms).WithDisposeOnCompletion(slotMap)" +
            ".WithReadOnly(characters).WithDisposeOnCompletion(hits).ForEach((Entity entity, " +
            "ref DynamicBuffer<BufferA> a, ref DynamicBuffer<BufferB> b, ref DynamicBuffer<BufferC> c, " +
            "ref DynamicBuffer<BufferD> d, ref State first, ref StateTwo second, in Initial initial) => { " +
            "Helper.Read(ref collisionWorld, ref slots); Helper.Update(ref slotMap, ref hits); " +
            "first.Value += transforms.Value + characters.Value + initial.Value + entity.GetHashCode(); }).Schedule(); } } " +
            "static class Helper { public static void Read(ref Capture first, ref Capture second) { } " +
            "public static void Update(ref DisposableCapture first, ref DisposableCapture second) { } } " +
            "struct Capture { public int Value; } struct DisposableCapture { public JobHandle Dispose(JobHandle input) => default; } " +
            "struct BufferA : IBufferElementData { } struct BufferB : IBufferElementData { } " +
            "struct BufferC : IBufferElementData { } struct BufferD : IBufferElementData { } " +
            "struct State : IComponentData { public int Value; } struct StateTwo : IComponentData { public int Value; } " +
            "struct Initial : IComponentData { public int Value; } struct Tag : IComponentData { }",
            DiagnosticIds.EntitiesForEachToJobEntityRun,
            DiagnosticIds.EntitiesForEachToJobEntitySchedule,
            DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel);

        await VerifyFixAsync(
            "using Unity.Entities; using Unity.Jobs; partial class DependencyDisposalSystem : SystemBase { " +
            "void Update() { var hits = new Disposable(); Entities.WithDisposeOnCompletion(hits).ForEach(" +
            "(ref Position p) => { p.Value += hits.Value; }).Schedule(); } } " +
            "struct Disposable { public float Value; public JobHandle Dispose(JobHandle dependency) => default; } " +
            "struct Position : IComponentData { public float Value; }",
            DiagnosticIds.EntitiesForEachToJobEntitySchedule,
            "using Unity.Entities; using Unity.Jobs; partial class DependencyDisposalSystem : SystemBase { " +
            "void Update() { var hits = new Disposable(); var jobHandle = new EntitiesForEachJob { Hits = hits }" +
            ".Schedule(Dependency); Dependency = hits.Dispose(jobHandle); } " +
            "[Unity.Burst.BurstCompile] private partial struct EntitiesForEachJob : Unity.Entities.IJobEntity { " +
            "public global::Disposable Hits; public void Execute(ref Position p) { p.Value += Hits.Value; } } } " +
            "struct Disposable { public float Value; public JobHandle Dispose(JobHandle dependency) => default; } " +
            "struct Position : IComponentData { public float Value; }");

        await VerifyFixAsync(
            "using Unity.Entities; using Unity.Jobs; partial class DisposalSystem : SystemBase { " +
            "void Update() { var hits = new Disposable { Value = 1f }; var misses = new Disposable(); " +
            "var dependency = default(JobHandle); var jobHandle = default(JobHandle); " +
            "// retain scheduling trivia\nEntities.WithReadOnly(hits).WithDisposeOnCompletion(hits)" +
            ".WithDisposeOnCompletion(misses).ForEach((ref Position p) => { " +
            "p.Value += hits.Value + misses.Value; }).Schedule(dependency); } } " +
            "struct Disposable { public float Value; public JobHandle Dispose(JobHandle dependency) => default; } " +
            "struct Position : IComponentData { public float Value; }",
            DiagnosticIds.EntitiesForEachToJobEntitySchedule,
            "using Unity.Entities; using Unity.Jobs; partial class DisposalSystem : SystemBase { " +
            "void Update() { var hits = new Disposable { Value = 1f }; var misses = new Disposable(); " +
            "var dependency = default(JobHandle); var jobHandle = default(JobHandle); " +
            "// retain scheduling trivia\nvar jobHandle2 = new EntitiesForEachJob { Hits = hits, Misses = misses }.Schedule(dependency); " +
            "jobHandle2 = hits.Dispose(jobHandle2); Dependency = misses.Dispose(jobHandle2); } " +
            "[Unity.Burst.BurstCompile] private partial struct EntitiesForEachJob : Unity.Entities.IJobEntity { " +
            "public global::Disposable Hits; public global::Disposable Misses; public void Execute(ref Position p) { " +
            "p.Value += Hits.Value + Misses.Value; } } } " +
            "struct Disposable { public float Value; public JobHandle Dispose(JobHandle dependency) => default; } " +
            "struct Position : IComponentData { public float Value; }");

        await VerifyFixAsync(
            "using Unity.Entities; partial class LookupSystem : SystemBase { void Update() { " +
            "var aircraftDataLookup = 1; Entities.ForEach((Entity entity, ref Position p) => { " +
            "if (SystemAPI.HasComponent<AircraftData>(entity) && " +
            "SystemAPI.HasComponent<AircraftData>(entity)) p.Value++; }).Schedule(); } } " +
            "struct Position : IComponentData { public float Value; } " +
            "struct AircraftData : IComponentData { }",
            DiagnosticIds.EntitiesForEachToJobEntitySchedule,
            "using Unity.Entities; partial class LookupSystem : SystemBase { void Update() { " +
            "var aircraftDataLookup = 1; var aircraftDataLookup2 = " +
            "Unity.Entities.SystemAPI.GetComponentLookup<global::AircraftData>(true); " +
            "new EntitiesForEachJob { AircraftDataLookup = aircraftDataLookup2 }.Schedule(); } " +
            "[Unity.Burst.BurstCompile] private partial struct EntitiesForEachJob : Unity.Entities.IJobEntity { " +
            "[Unity.Collections.ReadOnly] public Unity.Entities.ComponentLookup<global::AircraftData> " +
            "AircraftDataLookup; public void Execute(Entity entity, ref Position p) { " +
            "if (AircraftDataLookup.HasComponent(entity) && AircraftDataLookup.HasComponent(entity)) " +
            "p.Value++; } } } struct Position : IComponentData { public float Value; } " +
            "struct AircraftData : IComponentData { }");

        await VerifyFixAsync(
            "using Unity.Entities; partial class ReuseLookupSystem : SystemBase { void Update() { " +
            "var aircraftDataLookup = SystemAPI.GetComponentLookup<AircraftData>(true); " +
            "Entities.ForEach((Entity entity, ref Position p) => { " +
            "if (SystemAPI.HasComponent<AircraftData>(entity)) p.Value++; }).Run(); } } " +
            "struct Position : IComponentData { public float Value; } " +
            "struct AircraftData : IComponentData { }",
            DiagnosticIds.EntitiesForEachToJobEntityRun,
            "using Unity.Entities; partial class ReuseLookupSystem : SystemBase { void Update() { " +
            "var aircraftDataLookup = SystemAPI.GetComponentLookup<AircraftData>(true); " +
            "new EntitiesForEachJob { AircraftDataLookup = aircraftDataLookup }.Run(); } " +
            "[Unity.Burst.BurstCompile] private partial struct EntitiesForEachJob : Unity.Entities.IJobEntity { " +
            "[Unity.Collections.ReadOnly] public Unity.Entities.ComponentLookup<global::AircraftData> " +
            "AircraftDataLookup; public void Execute(Entity entity, ref Position p) { " +
            "if (AircraftDataLookup.HasComponent(entity)) p.Value++; } } } " +
            "struct Position : IComponentData { public float Value; } " +
            "struct AircraftData : IComponentData { }");

        const string readOnlySource =
            "using Unity.Entities; partial class ReadOnlySystem : SystemBase { void Update() { " +
            "var lookup = new Lookup { Value = 2f }; Entities.WithReadOnly(lookup).ForEach(" +
            "(ref Position p) => { p.Value += lookup.Value; }).Schedule(); } } " +
            "struct Lookup { public float Value; } struct Position : IComponentData { public float Value; }";
        await VerifyFixAsync(
            readOnlySource,
            DiagnosticIds.EntitiesForEachToJobEntitySchedule,
            "using Unity.Entities; partial class ReadOnlySystem : SystemBase { void Update() { " +
            "var lookup = new Lookup { Value = 2f }; new EntitiesForEachJob { Lookup = lookup }.Schedule(); } " +
            "[Unity.Burst.BurstCompile] private partial struct EntitiesForEachJob : Unity.Entities.IJobEntity { " +
            "[Unity.Collections.ReadOnly] public global::Lookup Lookup; public void Execute(ref Position p) " +
            "{ p.Value += Lookup.Value; } } } struct Lookup { public float Value; } " +
            "struct Position : IComponentData { public float Value; }");

        const string readOnlyRunSource =
            "using Unity.Entities; partial class ReadOnlyRunSystem : SystemBase { void Update() { " +
            "var lookup = new Lookup { Value = 2f }; Entities.WithReadOnly(lookup).ForEach(" +
            "(ref Position p) => { p.Value += lookup.Value; }).Run(); } } " +
            "struct Lookup { public float Value; } struct Position : IComponentData { public float Value; }";
        await VerifyFixAsync(
            readOnlyRunSource,
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            "using Unity.Entities; partial class ReadOnlyRunSystem : SystemBase { void Update() { " +
            "var lookup = new Lookup { Value = 2f }; foreach (var p in " +
            "Unity.Entities.SystemAPI.Query<Unity.Entities.RefRW<Position>>()) " +
            "{ p.ValueRW.Value += lookup.Value; } } } struct Lookup { public float Value; } " +
            "struct Position : IComponentData { public float Value; }");

        await VerifyFixAsync(
            "using Unity.Entities; partial class ReturnSystem : SystemBase { void Update() { " +
            "Entities.ForEach((ref Position p) => { if (p.Value == 0) return; p.Value++; }).Run(); } } " +
            "struct Position : IComponentData { public float Value; }",
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            "using Unity.Entities; partial class ReturnSystem : SystemBase { void Update() { " +
            "foreach (var p in Unity.Entities.SystemAPI.Query<Unity.Entities.RefRW<Position>>()) " +
            "{ if (p.ValueRW.Value == 0) continue; p.ValueRW.Value++; } } } " +
            "struct Position : IComponentData { public float Value; }");

        await VerifyFixAsync(
            "using Unity.Entities; partial class NestedReturnSystem : SystemBase { void Update() { " +
            "Entities.ForEach((ref Position p) => { for (var i = 0; i < 3; i++) { " +
            "if (p.Value == i) return; p.Value++; } }).Run(); } } " +
            "struct Position : IComponentData { public float Value; }",
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            "using Unity.Entities; partial class NestedReturnSystem : SystemBase { void Update() { " +
            "foreach (var p in Unity.Entities.SystemAPI.Query<Unity.Entities.RefRW<Position>>()) " +
            "{ for (var i = 0; i < 3; i++) { if (p.ValueRW.Value == i) goto systemApiQueryContinue; " +
            "p.ValueRW.Value++; } systemApiQueryContinue:; } } } " +
            "struct Position : IComponentData { public float Value; }");

        var nestedLoopCases = new[]
        {
            (Name: "While", Loop: "while (p.Value < 3) { if (p.Value == 1) return; p.Value++; }",
                FixedLoop: "while (p.ValueRW.Value < 3) { if (p.ValueRW.Value == 1) goto systemApiQueryContinue; p.ValueRW.Value++; }"),
            (Name: "Do", Loop: "do { if (p.Value == 1) return; p.Value++; } while (p.Value < 3);",
                FixedLoop: "do { if (p.ValueRW.Value == 1) goto systemApiQueryContinue; p.ValueRW.Value++; } while (p.ValueRW.Value < 3);"),
            (Name: "ForEach", Loop: "foreach (var value in new[] { 1, 2 }) { if (p.Value == value) return; p.Value++; }",
                FixedLoop: "foreach (var value in new[] { 1, 2 }) { if (p.ValueRW.Value == value) goto systemApiQueryContinue; p.ValueRW.Value++; }"),
        };
        foreach (var item in nestedLoopCases)
        {
            await VerifyFixAsync(
                "using Unity.Entities; partial class " + item.Name + "ReturnSystem : SystemBase { void Update() { " +
                "Entities.ForEach((ref Position p) => { " + item.Loop + " }).Run(); } } " +
                "struct Position : IComponentData { public float Value; }",
                DiagnosticIds.EntitiesForEachToSystemApiQuery,
                "using Unity.Entities; partial class " + item.Name + "ReturnSystem : SystemBase { void Update() { " +
                "foreach (var p in Unity.Entities.SystemAPI.Query<Unity.Entities.RefRW<Position>>()) { " +
                item.FixedLoop + " systemApiQueryContinue:; } } } " +
                "struct Position : IComponentData { public float Value; }");
        }

        await VerifyFixAsync(
            "using Unity.Entities; partial class MixedReturnSystem : SystemBase { void Update() { " +
            "Entities.ForEach((ref Position p) => { if (p.Value < 0) return; " +
            "for (var i = 0; i < 3; i++) if (p.Value == i) return; p.Value++; }).Run(); } } " +
            "struct Position : IComponentData { public float Value; }",
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            "using Unity.Entities; partial class MixedReturnSystem : SystemBase { void Update() { " +
            "foreach (var p in Unity.Entities.SystemAPI.Query<Unity.Entities.RefRW<Position>>()) { " +
            "if (p.ValueRW.Value < 0) continue; for (var i = 0; i < 3; i++) " +
            "if (p.ValueRW.Value == i) goto systemApiQueryContinue; p.ValueRW.Value++; " +
            "systemApiQueryContinue:; } } } struct Position : IComponentData { public float Value; }");

        await VerifyFixAsync(
            "using Unity.Entities; partial class NestedLambdaReturnSystem : SystemBase { void Update() { " +
            "Entities.ForEach((ref Position p) => { System.Action callback = () => { return; }; " +
            "if (p.Value == 0) return; callback(); }).Run(); } } " +
            "struct Position : IComponentData { public float Value; }",
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            "using Unity.Entities; partial class NestedLambdaReturnSystem : SystemBase { void Update() { " +
            "foreach (var p in Unity.Entities.SystemAPI.Query<Unity.Entities.RefRW<Position>>()) { " +
            "System.Action callback = () => { return; }; if (p.ValueRW.Value == 0) continue; callback(); } } } " +
            "struct Position : IComponentData { public float Value; }");

        await VerifyFixAsync(
            "using Unity.Entities; partial class LabelCollisionSystem : SystemBase { void Update() { " +
            "systemApiQueryContinue:; Entities.ForEach((ref Position p) => { " +
            "while (p.Value < 3) if (p.Value == 1) return; }).Run(); } } " +
            "struct Position : IComponentData { public float Value; }",
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            "using Unity.Entities; partial class LabelCollisionSystem : SystemBase { void Update() { " +
            "systemApiQueryContinue:; foreach (var p in Unity.Entities.SystemAPI.Query<Unity.Entities.RefRW<Position>>()) { " +
            "while (p.ValueRW.Value < 3) if (p.ValueRW.Value == 1) goto systemApiQueryContinue2; " +
            "systemApiQueryContinue2:; } } } struct Position : IComponentData { public float Value; }");

        await VerifyFixAsync(
            "using Unity.Entities; partial class StructuralReturnSystem : SystemBase { void Update() { " +
            "Entities.WithStructuralChanges().ForEach((ref Position p) => { " +
            "if (p.Value == 0) return; p.Value++; }).Run(); } } " +
            "struct Position : IComponentData { public float Value; }",
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            "using Unity.Entities; partial class StructuralReturnSystem : SystemBase { void Update() { " +
            "using (var entitiesSnapshot = new Unity.Collections.NativeList<Unity.Entities.Entity>" +
            "(Unity.Collections.Allocator.Temp)) { foreach (var (_, entity) in Unity.Entities.SystemAPI" +
            ".Query<Unity.Entities.RefRW<Position>>().WithEntityAccess()) { entitiesSnapshot.Add(entity); } " +
            "foreach (var entity in entitiesSnapshot) { ref Position p = ref Unity.Entities.SystemAPI" +
            ".GetComponentRW<Position>(entity).ValueRW; if (p.Value == 0) continue; p.Value++; } } } } " +
            "struct Position : IComponentData { public float Value; }");

        await VerifyFixAsync(
            "using Unity.Entities; partial class StructuralEntityReturnSystem : SystemBase { void Update() { " +
            "Entities.WithStructuralChanges().WithAll<Tag>().ForEach((Entity entity) => { " +
            "for (var i = 0; i < 2; i++) if (entity.GetHashCode() == i) return; }).Run(); } } " +
            "struct Tag : IComponentData { }",
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            "using Unity.Entities; partial class StructuralEntityReturnSystem : SystemBase { void Update() { { " +
            "using (var entitiesSnapshot = new Unity.Collections.NativeList<Unity.Entities.Entity>" +
            "(Unity.Collections.Allocator.Temp)) { foreach (var (_, entity) in Unity.Entities.SystemAPI" +
            ".Query<Unity.Entities.RefRO<Tag>>().WithAll<Tag>().WithEntityAccess()) { entitiesSnapshot.Add(entity); } " +
            "foreach (var entity in entitiesSnapshot) { for (var i = 0; i < 2; i++) " +
            "if (entity.GetHashCode() == i) goto systemApiQueryContinue; systemApiQueryContinue:; } } } } } " +
            "struct Tag : IComponentData { }");

        await VerifyFixAsync(
            "using Unity.Entities; partial class FooSystem : SystemBase { void Update() { " +
            "Entities.WithoutBurst().WithStructuralChanges().WithNone<BarTag>().ForEach(" +
            "(Entity fooEntity, ref FooData fooData) => { var bar = EntityManager.GetComponentData<BarData>" +
            "(fooEntity); fooData.Value += bar.Value; EntityManager.AddComponent<BarTag>(fooEntity); }).Run(); } } " +
            "struct FooData : IComponentData { public int Value; } struct BarData : IComponentData " +
            "{ public int Value; } struct BarTag : IComponentData { }",
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            "using Unity.Entities; partial class FooSystem : SystemBase { void Update() { using (var " +
            "entitiesSnapshot = new Unity.Collections.NativeList<Unity.Entities.Entity>" +
            "(Unity.Collections.Allocator.Temp)) { foreach (var (_, fooEntity) in Unity.Entities.SystemAPI" +
            ".Query<Unity.Entities.RefRW<FooData>>().WithNone<BarTag>().WithEntityAccess()) " +
            "{ entitiesSnapshot.Add(fooEntity); } foreach (var fooEntity in entitiesSnapshot) { " +
            "ref FooData fooData = ref Unity.Entities.SystemAPI.GetComponentRW<FooData>(fooEntity).ValueRW; " +
            "var bar = EntityManager.GetComponentData<BarData>(fooEntity); fooData.Value += bar.Value; " +
            "EntityManager.AddComponent<BarTag>(fooEntity); } } } } struct FooData : IComponentData " +
            "{ public int Value; } struct BarData : IComponentData { public int Value; } " +
            "struct BarTag : IComponentData { }");

        const string dynamicBufferAndIndexSource = """
            using Unity.Entities;

            partial class ThirdEyeSystem : SystemBase
            {
                void Update()
                {
                    var ecbParallelWriter = new EntityCommandBuffer().AsParallelWriter();
                    Entities
                        .WithAll<PlayerTag>()
                        .ForEach(
                            (Entity playerEntity, int entityInQueryIndex,
                                ref CharacterStance characterStance,
                                ref DetectionData detectionData,
                                ref DynamicBuffer<DetectedElement> detectedBuffer,
                                in PerceptionData perceptionData,
                                in PlayerInputs input) =>
                            {
                                characterStance.Mode += input.Toggle;
                                detectionData.Count += detectedBuffer.Length + perceptionData.Value;
                                ecbParallelWriter.AddComponent(
                                    entityInQueryIndex,
                                    playerEntity,
                                    new Result());
                            }).Run();
                }
            }

            struct PlayerTag : IComponentData { }
            struct CharacterStance : IComponentData { public int Mode; }
            struct DetectionData : IComponentData { public int Count; }
            struct DetectedElement : IBufferElementData { }
            struct PerceptionData : IComponentData { public int Value; }
            struct PlayerInputs : IComponentData { public int Toggle; }
            struct Result : IComponentData { }
            """;

        await VerifyFixAsync(
            dynamicBufferAndIndexSource,
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            """
            using Unity.Entities;

            partial class ThirdEyeSystem : SystemBase
            {
                void Update()
                {
                    var ecb = new EntityCommandBuffer();
                    foreach (var (characterStance, detectionData, detectedBuffer, perceptionData, input, playerEntity) in Unity.Entities.SystemAPI.Query<Unity.Entities.RefRW<CharacterStance>, Unity.Entities.RefRW<DetectionData>, DynamicBuffer<DetectedElement>, Unity.Entities.RefRO<PerceptionData>, Unity.Entities.RefRO<PlayerInputs>>().WithAll<PlayerTag>().WithEntityAccess())
                    {
                        characterStance.ValueRW.Mode += input.ValueRO.Toggle;
                        detectionData.ValueRW.Count += detectedBuffer.Length + perceptionData.ValueRO.Value;
                        ecb.AddComponent(
                            playerEntity,
                            new Result());
                    }
                }
            }

            struct PlayerTag : IComponentData { }
            struct CharacterStance : IComponentData { public int Mode; }
            struct DetectionData : IComponentData { public int Count; }
            struct DetectedElement : IBufferElementData { }
            struct PerceptionData : IComponentData { public int Value; }
            struct PlayerInputs : IComponentData { public int Toggle; }
            struct Result : IComponentData { }
            """);

        await VerifyFixAsync(
            "using Unity.Entities; partial class ConstraintSystem : SystemBase { void Update() { " +
            "var lookup = SystemAPI.GetComponentLookup<LookupData>(true); " +
            "var buffer = default(DynamicBuffer<BufferItem>); " +
            "var ecbParallelWriter = new EntityCommandBuffer().AsParallelWriter(); " +
            "Entities.WithAll<MagiNPCTag>().WithoutBurst().ForEach((Entity entity, int entityInQueryIndex, " +
            "in AIIsTransforming transforming, in ReadA a, in ReadB b, in ReadC c, in ReadD d, in ReadE e) => { " +
            "if (lookup.HasComponent(entity)) buffer.Clear(); var created = ecbParallelWriter.CreateEntity(entityInQueryIndex); " +
            "ecbParallelWriter.AddComponent(entityInQueryIndex, created, new Result { Value = a.Value + b.Value + c.Value + d.Value + e.Value }); }).Run(); } } " +
            "struct MagiNPCTag : IComponentData { } struct AIIsTransforming : IComponentData { } " +
            "struct ReadA : IComponentData { public int Value; } struct ReadB : IComponentData { public int Value; } " +
            "struct ReadC : IComponentData { public int Value; } struct ReadD : IComponentData { public int Value; } " +
            "struct ReadE : IComponentData { public int Value; } struct LookupData : IComponentData { } " +
            "struct BufferItem : IBufferElementData { } struct Result : IComponentData { public int Value; }",
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            "using Unity.Entities; partial class ConstraintSystem : SystemBase { void Update() { " +
            "var lookup = SystemAPI.GetComponentLookup<LookupData>(true); " +
            "var buffer = default(DynamicBuffer<BufferItem>); var ecb = new EntityCommandBuffer(); " +
            "foreach (var (a, b, c, d, e, entity) in Unity.Entities.SystemAPI.Query<Unity.Entities.RefRO<ReadA>, Unity.Entities.RefRO<ReadB>, Unity.Entities.RefRO<ReadC>, Unity.Entities.RefRO<ReadD>, Unity.Entities.RefRO<ReadE>>().WithAll<MagiNPCTag>().WithAll<AIIsTransforming>().WithEntityAccess()) { " +
            "if (lookup.HasComponent(entity)) buffer.Clear(); var created = ecb.CreateEntity(); " +
            "ecb.AddComponent(created, new Result { Value = a.ValueRO.Value + b.ValueRO.Value + c.ValueRO.Value + d.ValueRO.Value + e.ValueRO.Value }); } } } " +
            "struct MagiNPCTag : IComponentData { } struct AIIsTransforming : IComponentData { } " +
            "struct ReadA : IComponentData { public int Value; } struct ReadB : IComponentData { public int Value; } " +
            "struct ReadC : IComponentData { public int Value; } struct ReadD : IComponentData { public int Value; } " +
            "struct ReadE : IComponentData { public int Value; } struct LookupData : IComponentData { } " +
            "struct BufferItem : IBufferElementData { } struct Result : IComponentData { public int Value; }");

        await VerifyFixAsync(
            """
            using Unity.Entities;

            partial class RpcSystem : SystemBase
            {
                void Update()
                {
                    var ecbParallelWriter = new EntityCommandBuffer(default).AsParallelWriter();
                    Entities.WithAll<PlayerTag>().ForEach(
                        (Entity playerEntity, int entityInQueryIndex, in PlayerTag playerTag) =>
                        {
                            var rpcEntity = ecbParallelWriter.CreateEntity(entityInQueryIndex);
                            ecbParallelWriter.AddComponent(entityInQueryIndex, rpcEntity, new Rpc());
                        }).Run();
                }
            }

            struct PlayerTag : IComponentData { }
            struct Rpc : IComponentData { }
            """,
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            """
            using Unity.Entities;

            partial class RpcSystem : SystemBase
            {
                void Update()
                {
                    var ecb = new EntityCommandBuffer(default);
                    foreach (var (_, playerEntity) in Unity.Entities.SystemAPI.Query<Unity.Entities.RefRO<PlayerTag>>().WithAll<PlayerTag>().WithEntityAccess())
                    {
                        var rpcEntity = ecb.CreateEntity();
                        ecb.AddComponent(rpcEntity, new Rpc());
                    }
                }
            }

            struct PlayerTag : IComponentData { }
            struct Rpc : IComponentData { }
            """);

        await VerifyFixAsync(
            dynamicBufferAndIndexSource,
            DiagnosticIds.EntitiesForEachToJobEntityRun,
            """
            using Unity.Entities;

            partial class ThirdEyeSystem : SystemBase
            {
                void Update()
                {
                    var ecbParallelWriter = new EntityCommandBuffer().AsParallelWriter();
                    new EntitiesForEachJob
                    {
                        EcbParallelWriter = ecbParallelWriter
                    }.Run();
                }

                [Unity.Burst.BurstCompile]
                [Unity.Entities.WithAll(typeof(PlayerTag))]
                private partial struct EntitiesForEachJob : Unity.Entities.IJobEntity
                {
                    public global::Unity.Entities.EntityCommandBuffer.ParallelWriter EcbParallelWriter;

                    public void Execute(Entity playerEntity, [Unity.Entities.EntityIndexInQuery] int entityInQueryIndex, ref CharacterStance characterStance, ref DetectionData detectionData, ref DynamicBuffer<DetectedElement> detectedBuffer, in PerceptionData perceptionData, in PlayerInputs input)
                    {
                        characterStance.Mode += input.Toggle;
                        detectionData.Count += detectedBuffer.Length + perceptionData.Value;
                        EcbParallelWriter.AddComponent(
                            entityInQueryIndex,
                            playerEntity,
                            new Result());
                    }
                }
            }

            struct PlayerTag : IComponentData { }
            struct CharacterStance : IComponentData { public int Mode; }
            struct DetectionData : IComponentData { public int Count; }
            struct DetectedElement : IBufferElementData { }
            struct PerceptionData : IComponentData { public int Value; }
            struct PlayerInputs : IComponentData { public int Toggle; }
            struct Result : IComponentData { }
            """);

        await VerifyFixAsync(
            """
            using Unity.Entities;

            partial class WritableBufferSystem : SystemBase
            {
                void Update()
                {
                    Entities
                        .WithoutBurst()
                        .WithAll<PlayerTag>()
                        .ForEach(
                            (Entity playerEntity, int entityInQueryIndex,
                                ref CharacterStance characterStance,
                                ref DetectionData detectionData,
                                ref DynamicBuffer<DetectedElement> detectedBuffer,
                                in PerceptionData perceptionData,
                                in PlayerInputs input) =>
                            {
                                for (var i = 0; i < detectedBuffer.Length; i++)
                                {
                                    if (detectedBuffer[i].Value == 0)
                                        continue;

                                    var element = detectedBuffer[i];
                                    element.Value += input.Toggle;
                                    detectedBuffer[i] = element;
                                }
                            }).Run();
                }
            }

            struct PlayerTag : IComponentData { }
            struct CharacterStance : IComponentData { public int Mode; }
            struct DetectionData : IComponentData { public int Count; }
            struct DetectedElement : IBufferElementData { public int Value; }
            struct PerceptionData : IComponentData { public int Value; }
            struct PlayerInputs : IComponentData { public int Toggle; }
            """,
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            """
            using Unity.Entities;

            partial class WritableBufferSystem : SystemBase
            {
                void Update()
                {
                    {
                        var entityInQueryIndexCounter = 0;
                        foreach (var (characterStance, detectionData, detectedBuffer, input, playerEntity) in Unity.Entities.SystemAPI.Query<Unity.Entities.RefRW<CharacterStance>, Unity.Entities.RefRW<DetectionData>, DynamicBuffer<DetectedElement>, Unity.Entities.RefRO<PlayerInputs>>().WithAll<PlayerTag>().WithAll<PerceptionData>().WithEntityAccess())
                        {
                            var entityInQueryIndex = entityInQueryIndexCounter++;
                            for (var i = 0; i < detectedBuffer.Length; i++)
                            {
                                if (detectedBuffer[i].Value == 0)
                                    continue;

                                var element = detectedBuffer[i];
                                element.Value += input.ValueRO.Toggle;
                                detectedBuffer.ElementAt(i) = element;
                            }
                        }
                    }
                }
            }

            struct PlayerTag : IComponentData { }
            struct CharacterStance : IComponentData { public int Mode; }
            struct DetectionData : IComponentData { public int Count; }
            struct DetectedElement : IBufferElementData { public int Value; }
            struct PerceptionData : IComponentData { public int Value; }
            struct PlayerInputs : IComponentData { public int Toggle; }
            """);

        await VerifyFixAsync(
            "using Unity.Entities; partial class CapturedSystem : SystemBase { void Update() { " +
            "var scale = 2f; Entities.ForEach((ref Position p) => { p.Value *= scale; }).Run(); } } " +
            "struct Position : IComponentData { public float Value; }",
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            "using Unity.Entities; partial class CapturedSystem : SystemBase { void Update() { " +
            "var scale = 2f; foreach (var p in Unity.Entities.SystemAPI.Query<Unity.Entities.RefRW<Position>>()) " +
            "{ p.ValueRW.Value *= scale; } } } struct Position : IComponentData { public float Value; }");

        await VerifyFixAsync(
            "using Unity.Entities; partial class CapturedFieldSystem : SystemBase { " +
            "private float scale = 2f; void Update() { Entities.ForEach((ref Position p) => " +
            "{ p.Value *= scale; }).Run(); } } struct Position : IComponentData { public float Value; }",
            DiagnosticIds.EntitiesForEachToJobEntityRun,
            "using Unity.Entities; partial class CapturedFieldSystem : SystemBase { " +
            "private float scale = 2f; void Update() { new EntitiesForEachJob { Scale = this.scale }.Run(); } " +
            "[Unity.Burst.BurstCompile] private partial struct EntitiesForEachJob : Unity.Entities.IJobEntity { " +
            "public float Scale; public void Execute(ref Position p) { p.Value *= Scale; } } } " +
            "struct Position : IComponentData { public float Value; }");

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
            "using Unity.Entities; using Unity.Collections; partial class BalanceSystem : SystemBase { " +
            "BalanceVariables varsToRead; void Update() { Entities.WithStructuralChanges().WithoutBurst()" +
            ".ForEach((Entity entity, in BalanceVariables vars, in BalanceVariablesUpdateRequest request) => " +
            "{ varsToRead = vars; EntityManager.DestroyEntity(entity); }).Run(); } } " +
            "struct BalanceVariables : IComponentData { } " +
            "struct BalanceVariablesUpdateRequest : IComponentData { }",
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            "using Unity.Entities; using Unity.Collections; partial class BalanceSystem : SystemBase { " +
            "BalanceVariables varsToRead; void Update() { using var ecb = new Unity.Entities.EntityCommandBuffer" +
            "(Unity.Collections.Allocator.Temp); foreach (var (vars, request, entity) in Unity.Entities.SystemAPI" +
            ".Query<Unity.Entities.RefRO<BalanceVariables>, Unity.Entities.RefRO<BalanceVariablesUpdateRequest>>()" +
            ".WithEntityAccess()) { varsToRead = vars.ValueRO; ecb.DestroyEntity(entity); } " +
            "ecb.Playback(EntityManager); } } struct BalanceVariables : IComponentData { } " +
            "struct BalanceVariablesUpdateRequest : IComponentData { }");

        await VerifyFixAsync(
            """
            using Unity.Entities;

            partial class StructuralComponentSystem : SystemBase
            {
                void Update()
                {
                    Entities.WithoutBurst().WithStructuralChanges().ForEach(
                        (Entity entity, ref RemovalRequest request) =>
                        {
                            if (!request.IsSubmitted)
                            {
                                request.IsSubmitted = true;
                                RequestService.Submit(request.TargetId.ToString(), EntityManager, entity);
                            }
                            else if (request.IsComplete && request.StatusCode <= 0)
                            {
                                if (!request.IsRemovalScheduled)
                                {
                                    request.IsRemovalScheduled = true;
                                    request.RemoveAfterSeconds = 15;
                                }
                                else
                                {
                                    request.RemoveAfterSeconds -= World.Time.DeltaTime;
                                    if (request.RemoveAfterSeconds <= 0)
                                    {
                                        EntityManager.RemoveComponent<RemovalRequest>(entity);
                                    }
                                }
                            }
                        }).Run();
                }
            }

            static class RequestService
            {
                public static void Submit(string targetId, EntityManager entityManager, Entity entity) { }
            }

            struct RemovalRequest : IComponentData
            {
                public bool IsSubmitted;
                public bool IsComplete;
                public bool IsRemovalScheduled;
                public int StatusCode;
                public int TargetId;
                public float RemoveAfterSeconds;
            }
            """,
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            """
            using Unity.Entities;

            partial class StructuralComponentSystem : SystemBase
            {
                void Update()
                {
                    using (var entitiesSnapshot = new Unity.Collections.NativeList<Unity.Entities.Entity>(Unity.Collections.Allocator.Temp))
                    {
                        foreach (var (_, entity) in Unity.Entities.SystemAPI.Query<Unity.Entities.RefRW<RemovalRequest>>().WithEntityAccess())
                        {
                            entitiesSnapshot.Add(entity);
                        }
                        foreach (var entity in entitiesSnapshot)
                        {
                            ref RemovalRequest request = ref Unity.Entities.SystemAPI.GetComponentRW<RemovalRequest>(entity).ValueRW;
                            if (!request.IsSubmitted)
                            {
                                request.IsSubmitted = true;
                                RequestService.Submit(request.TargetId.ToString(), EntityManager, entity);
                            }
                            else if (request.IsComplete && request.StatusCode <= 0)
                            {
                                if (!request.IsRemovalScheduled)
                                {
                                    request.IsRemovalScheduled = true;
                                    request.RemoveAfterSeconds = 15;
                                }
                                else
                                {
                                    request.RemoveAfterSeconds -= Unity.Entities.SystemAPI.Time.DeltaTime;
                                    if (request.RemoveAfterSeconds <= 0)
                                    {
                                        EntityManager.RemoveComponent<RemovalRequest>(entity);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            static class RequestService
            {
                public static void Submit(string targetId, EntityManager entityManager, Entity entity) { }
            }

            struct RemovalRequest : IComponentData
            {
                public bool IsSubmitted;
                public bool IsComplete;
                public bool IsRemovalScheduled;
                public int StatusCode;
                public int TargetId;
                public float RemoveAfterSeconds;
            }
            """);

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
            "EntitiesForEachJob : Unity.Entities.IJobEntity { public global::Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb; " +
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
            "struct Position : IComponentData { public float Value; }",
            DiagnosticIds.EntitiesForEachToSystemApiQuery);

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
