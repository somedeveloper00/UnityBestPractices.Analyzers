using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using UnityBestPractices.Analyzers;

const int repeatedAnalysisCount = 5;
var outputPath = GetOption(args, "--output") ?? "artifacts/performance/results.json";
var baselinePath = GetOption(args, "--baseline");
var references = GetPlatformReferences().ToImmutableArray();
var workloads = CreateWorkloads();
var results = new List<Measurement>();
var allocationsStable = IsAllocationMeasurementStable();

foreach (var workload in workloads)
{
    results.Add(await AnalyzeAsync(workload, 1));
    results.Add(await AnalyzeAsync(workload, repeatedAnalysisCount));
}

var artifact = new PerformanceArtifact(
    SchemaVersion: 1,
    CreatedUtc: DateTimeOffset.UtcNow,
    Runtime: System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    Os: System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    ProcessorCount: Environment.ProcessorCount,
    AllocationsStable: allocationsStable,
    Results: results);

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
await File.WriteAllTextAsync(
    outputPath,
    JsonSerializer.Serialize(artifact, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine("Family       Workload          Scenario   Time       Trees   Source       Diagnostics   Allocated");
foreach (var result in results)
{
    Console.WriteLine(
        $"{result.Family,-12} {result.Workload,-17} {result.Scenario,-10} " +
        $"{result.ElapsedMilliseconds,7:F0} ms {result.SyntaxTreeCount,7} " +
        $"{result.SourceBytes / 1024d,8:F1} KiB {result.DiagnosticCount,13} " +
        (result.AllocatedBytes is long bytes ? $"{bytes / 1024d / 1024d,9:F1} MiB" : "       n/a"));
}

Console.WriteLine($"JSON artifact: {outputPath}");
ValidateFamilyThresholds(results);
if (baselinePath is not null)
{
    CompareWithBaseline(results, baselinePath);
}
else
{
    Console.WriteLine("No baseline selected; only conservative absolute thresholds were applied.");
}

async Task<Measurement> AnalyzeAsync(Workload workload, int iterations)
{
    var sources = workload.Sources.ToImmutableArray();
    var sourceBytes = sources.Sum(source => Encoding.UTF8.GetByteCount(source));
    var elapsed = TimeSpan.Zero;
    long allocated = 0;
    var diagnostics = 0;
    for (var iteration = 0; iteration < iterations; iteration++)
    {
        var trees = sources.Select((source, index) => CSharpSyntaxTree.ParseText(
            source, new CSharpParseOptions(LanguageVersion.CSharp9),
            $"/{workload.Family}/{workload.Kind}/{index}.cs")).ToImmutableArray();
        var compilation = CSharpCompilation.Create(
            $"{workload.Family}-{workload.Kind}-{iteration}", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var before = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = Stopwatch.StartNew();
        var current = await compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new UnityBestPracticesAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        stopwatch.Stop();
        elapsed += stopwatch.Elapsed;
        allocated += GC.GetTotalAllocatedBytes(precise: true) - before;
        diagnostics += current.Length;
    }

    if (workload.Kind == "Clean" && diagnostics != 0)
        throw new InvalidOperationException($"{workload.Family} clean workload produced {diagnostics} diagnostics.");
    if (workload.Kind == "DiagnosticHeavy" && diagnostics < iterations)
        throw new InvalidOperationException($"{workload.Family} diagnostic workload produced only {diagnostics} diagnostics.");

    return new Measurement(
        workload.Family, workload.Kind, iterations == 1 ? "ColdStart" : "Repeated",
        iterations, elapsed.TotalMilliseconds, sources.Length, sourceBytes, diagnostics,
        allocationsStable ? allocated : null);
}

static void ValidateFamilyThresholds(IEnumerable<Measurement> measurements)
{
    var limits = new Dictionary<string, (double Milliseconds, long Bytes)>(StringComparer.Ordinal)
    {
        ["Core"] = (12_000, 900_000_000),
        ["Correctness"] = (15_000, 1_100_000_000),
        ["Expressions"] = (12_000, 900_000_000),
        ["DOTS"] = (18_000, 1_300_000_000),
    };
    foreach (var family in measurements.GroupBy(result => result.Family))
    {
        var elapsed = family.Sum(result => result.ElapsedMilliseconds);
        var bytes = family.Sum(result => result.AllocatedBytes ?? 0);
        var limit = limits[family.Key];
        if (elapsed > limit.Milliseconds)
            throw new InvalidOperationException($"{family.Key} took {elapsed:F0} ms; limit is {limit.Milliseconds:F0} ms.");
        if (family.All(result => result.AllocatedBytes.HasValue) && bytes > limit.Bytes)
            throw new InvalidOperationException($"{family.Key} allocated {bytes} bytes; limit is {limit.Bytes} bytes.");
    }
}

static void CompareWithBaseline(IReadOnlyCollection<Measurement> current, string path)
{
    var baseline = JsonSerializer.Deserialize<PerformanceArtifact>(File.ReadAllText(path))
        ?? throw new InvalidOperationException($"Could not deserialize baseline '{path}'.");
    if (baseline.SchemaVersion != 1)
        throw new InvalidOperationException($"Baseline schema {baseline.SchemaVersion} is not supported.");
    var failures = new List<string>();
    foreach (var result in current)
    {
        var prior = baseline.Results.SingleOrDefault(candidate => candidate.Key == result.Key)
            ?? throw new InvalidOperationException($"Baseline has no result for '{result.Key}'.");
        if (prior.Iterations != result.Iterations ||
            prior.SyntaxTreeCount != result.SyntaxTreeCount ||
            prior.SourceBytes != result.SourceBytes)
        {
            throw new InvalidOperationException(
                $"Baseline workload shape for '{result.Key}' does not match the current workload.");
        }
        // Both a generous ratio and a meaningful absolute increase are required to absorb runner noise.
        var timeLimit = Math.Max(prior.ElapsedMilliseconds * 1.75, prior.ElapsedMilliseconds + 500);
        if (result.ElapsedMilliseconds > timeLimit)
            failures.Add($"{result.Key} time {result.ElapsedMilliseconds:F0} ms > {timeLimit:F0} ms");
        if (result.AllocatedBytes is long bytes && prior.AllocatedBytes is long priorBytes)
        {
            var allocationLimit = Math.Max(priorBytes * 1.75, priorBytes + 64L * 1024 * 1024);
            if (bytes > allocationLimit)
                failures.Add($"{result.Key} allocations {bytes} > {allocationLimit:F0} bytes");
        }
    }

    Console.WriteLine($"Baseline: {path}");
    if (failures.Count != 0)
        throw new InvalidOperationException("Material performance regressions:\n" + string.Join("\n", failures));
}

static List<Workload> CreateWorkloads() =>
[
    new("Core", "Clean", [CreatePlainSource(100)]),
    new("Core", "DiagnosticHeavy", CreateCoroutineSources(80)),
    new("Correctness", "Clean", ["namespace Unity.Collections { public struct NativeArray<T> { } } class Clean { int Value => 1; }"]),
    new("Correctness", "DiagnosticHeavy", [CreateCorrectnessSource(60)]),
    new("Expressions", "Clean", ["using System.Linq; class CleanExpressions { int Count(int[] x) => x.Length; }"]),
    new("Expressions", "DiagnosticHeavy", [CreateExpressionSource(100)]),
    new("DOTS", "Clean", ["namespace Unity.Entities { public interface IComponentData { } } struct Position : Unity.Entities.IComponentData { public int X; }"]),
    new("DOTS", "DiagnosticHeavy", [CreateDotsSource(60)]),
];

static string CreatePlainSource(int count) => "class Plain {\n" +
    string.Concat(Enumerable.Range(0, count).Select(i => $"int M{i}(int x) => x + {i};\n")) + "}";

static IReadOnlyList<string> CreateCoroutineSources(int count) =>
    new[]
    {
        "namespace UnityEngine { public class Object {} public class Component:Object {} public class MonoBehaviour:Component {} }",
    }.Concat(Enumerable.Range(0, count).Select(i =>
        $"using System.Collections; class C{i}:UnityEngine.MonoBehaviour {{ IEnumerator Run() {{ yield return {i}; }} }}"))
    .ToArray();

static string CreateCorrectnessSource(int count) => "namespace Unity.Collections { public enum Allocator { Persistent } public struct NativeArray<T> { public NativeArray(int n, Allocator a) {} public void Dispose() {} } }\n" +
    string.Concat(Enumerable.Range(0, count).Select(i => $"class Leak{i} {{ void M() {{ var data = new Unity.Collections.NativeArray<int>(4, Unity.Collections.Allocator.Persistent); }} }}\n"));

static string CreateExpressionSource(int count) => "using System; using System.Linq; using System.Collections.Generic; class Expressions {\n" +
    string.Concat(Enumerable.Range(0, count).Select(i => $"bool M{i}(IEnumerable<int> values) => values.Where(x => x > {i}).Any();\n")) + "}";

static string CreateDotsSource(int count) => "using Unity.Entities; namespace Unity.Entities { public interface IJobEntity {} public static class JobExt { public static void Run<T>(this T job) where T:struct,IJobEntity {} } }\n" +
    string.Concat(Enumerable.Range(0, count).Select(i => $"struct Job{i}:Unity.Entities.IJobEntity {{}} class Runner{i} {{ void M() {{ new Job{i}().Run(); }} }}\n"));

static string? GetOption(string[] arguments, string name)
{
    var index = Array.IndexOf(arguments, name);
    if (index < 0) return null;
    if (index == arguments.Length - 1) throw new ArgumentException($"{name} requires a path.");
    return arguments[index + 1];
}

static bool IsAllocationMeasurementStable() =>
    System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith(
        ".NET ",
        StringComparison.Ordinal);

static IEnumerable<MetadataReference> GetPlatformReferences()
{
    var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
        ?? throw new InvalidOperationException("The runtime did not provide platform assemblies.");
    return trusted.Split(Path.PathSeparator).Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(path => MetadataReference.CreateFromFile(path));
}

internal sealed record Workload(string Family, string Kind, IReadOnlyList<string> Sources);
internal sealed record Measurement(string Family, string Workload, string Scenario, int Iterations,
    double ElapsedMilliseconds, int SyntaxTreeCount, int SourceBytes, int DiagnosticCount, long? AllocatedBytes)
{
    [JsonIgnore]
    public string Key => $"{Family}/{Workload}/{Scenario}";
}
internal sealed record PerformanceArtifact(int SchemaVersion, DateTimeOffset CreatedUtc, string Runtime,
    string Os, int ProcessorCount, bool AllocationsStable, IReadOnlyList<Measurement> Results);
