using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using UnityBestPractices.Analyzers;

const double maximumTotalSeconds = 30;
const long maximumTotalAllocatedBytes = 1_500_000_000;

var references = GetPlatformReferences().ToImmutableArray();
var analyzer = new UnityBestPracticesAnalyzer();

await AnalyzeAsync("warmup", new[] { "class Warmup { int Value => 1; }" }, expectedMinimum: 0);
var measurements = new List<Measurement>
{
    await AnalyzeAsync("large-no-unity-symbols", new[] { CreateNoUnitySource(2000) }, expectedMinimum: 0),
    await AnalyzeAsync("repeated-unity-patterns", new[] { CreateUnitySource(250) }, expectedMinimum: 250),
    await AnalyzeAsync("large-dots-job-file", new[] { CreateDotsSource(200) }, expectedMinimum: 400),
    await AnalyzeAsync("incomplete-syntax", new[] { CreateIncompleteSource(1000) }, expectedMinimum: 0),
    await AnalyzeAsync(
        "many-documents",
        Enumerable.Range(0, 100).Select(index => $"class Document{index} {{ int Value => {index}; }}"),
        expectedMinimum: 0),
};
measurements.Add(await MeasureIncrementalEditsAsync());

var totalSeconds = measurements.Sum(item => item.Elapsed.TotalSeconds);
var totalAllocated = measurements.Sum(item => item.AllocatedBytes);
foreach (var measurement in measurements)
{
    Console.WriteLine(
        $"{measurement.Name}: {measurement.Elapsed.TotalMilliseconds:F0} ms, " +
        $"{measurement.AllocatedBytes / 1024d / 1024d:F1} MiB, " +
        $"{measurement.Diagnostics} diagnostics, {measurement.DocumentCount} documents");
}

Console.WriteLine($"Total: {totalSeconds:F2} s, {totalAllocated / 1024d / 1024d:F1} MiB allocated");
if (totalSeconds > maximumTotalSeconds)
{
    throw new InvalidOperationException(
        $"Analyzer performance exceeded the broad {maximumTotalSeconds:F0} second CI threshold.");
}

if (totalAllocated > maximumTotalAllocatedBytes)
{
    throw new InvalidOperationException(
        $"Analyzer allocations exceeded the broad {maximumTotalAllocatedBytes / 1024 / 1024} MiB CI threshold.");
}

async Task<Measurement> AnalyzeAsync(
    string name,
    IEnumerable<string> sources,
    int expectedMinimum)
{
    var trees = sources
        .Select((source, index) => CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.CSharp9),
            $"{name}-{index}.cs"))
        .ToImmutableArray();
    var compilation = CSharpCompilation.Create(
        name,
        trees,
        references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
    var stopwatch = Stopwatch.StartNew();
    var diagnostics = await compilation
        .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer))
        .GetAnalyzerDiagnosticsAsync();
    stopwatch.Stop();
    var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
    if (diagnostics.Length < expectedMinimum)
    {
        throw new InvalidOperationException(
            $"{name} produced {diagnostics.Length} diagnostics; expected at least {expectedMinimum}.");
    }

    if (name == "large-no-unity-symbols" && diagnostics.Length != 0)
    {
        throw new InvalidOperationException("Non-Unity workload unexpectedly produced diagnostics.");
    }

    return new Measurement(name, stopwatch.Elapsed, allocated, diagnostics.Length, trees.Length);
}

async Task<Measurement> MeasureIncrementalEditsAsync()
{
    var tree = CSharpSyntaxTree.ParseText(
        "class Incremental { int Value => 0; }",
        new CSharpParseOptions(LanguageVersion.CSharp9),
        "Incremental.cs");
    var compilation = CSharpCompilation.Create(
        "incremental-edits",
        new[] { tree },
        references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    var stopwatch = Stopwatch.StartNew();
    var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
    var diagnosticCount = 0;
    for (var edit = 1; edit <= 10; edit++)
    {
        var replacement = CSharpSyntaxTree.ParseText(
            $"class Incremental {{ int Value => {edit}; }}",
            new CSharpParseOptions(LanguageVersion.CSharp9),
            "Incremental.cs");
        compilation = compilation.ReplaceSyntaxTree(tree, replacement);
        tree = replacement;
        diagnosticCount += (await compilation
                .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer))
                .GetAnalyzerDiagnosticsAsync())
            .Length;
    }

    stopwatch.Stop();
    return new Measurement(
        "incremental-edits",
        stopwatch.Elapsed,
        GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore,
        diagnosticCount,
        1);
}

static string CreateNoUnitySource(int methodCount)
{
    var builder = new StringBuilder("using System;\nclass LargeFile\n{\n");
    for (var index = 0; index < methodCount; index++)
    {
        builder.Append("    int Method")
            .Append(index)
            .Append("(int value) => value + ")
            .Append(index)
            .AppendLine(";");
    }

    return builder.AppendLine("}").ToString();
}

static string CreateUnitySource(int typeCount)
{
    var builder = new StringBuilder(
        """
        using System.Collections;
        namespace UnityEngine
        {
            public class Object { }
            public class Component : Object { }
            public class MonoBehaviour : Component { }
        }
        """);
    for (var index = 0; index < typeCount; index++)
    {
        builder.Append(
            $$"""

            class Coroutine{{index}} : UnityEngine.MonoBehaviour
            {
                IEnumerator Run()
                {
                    yield return 0;
                }
            }
            """);
    }

    return builder.ToString();
}

static string CreateDotsSource(int typeCount)
{
    var builder = new StringBuilder(
        """
        using Unity.Entities;
        namespace Unity.Entities
        {
            public interface IJobEntity { }
            public static class IJobEntityExtensions
            {
                public static void Run<T>(this T job) where T : struct, IJobEntity { }
                public static void Schedule<T>(this T job) where T : struct, IJobEntity { }
                public static void ScheduleParallel<T>(this T job) where T : struct, IJobEntity { }
            }
            public static class SystemAPI { }
        }
        """);
    for (var index = 0; index < typeCount; index++)
    {
        builder.Append(
            $$"""

            struct Job{{index}} : Unity.Entities.IJobEntity { }
            class JobRunner{{index}}
            {
                void Update()
                {
                    new Job{{index}}().Run();
                }
            }
            """);
    }

    return builder.ToString();
}

static string CreateIncompleteSource(int statementCount)
{
    var builder = new StringBuilder("class Broken { void Update() {\n");
    for (var index = 0; index < statementCount; index++)
    {
        builder.Append("value").Append(index).AppendLine(" = Call(");
    }

    return builder.ToString();
}

static IEnumerable<MetadataReference> GetPlatformReferences()
{
    var trustedAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
        ?? throw new InvalidOperationException("The runtime did not provide platform assemblies.");
    return trustedAssemblies
        .Split(Path.PathSeparator)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(path => MetadataReference.CreateFromFile(path));
}

internal sealed record Measurement(
    string Name,
    TimeSpan Elapsed,
    long AllocatedBytes,
    int Diagnostics,
    int DocumentCount);
