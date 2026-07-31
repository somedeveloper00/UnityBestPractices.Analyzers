using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using UnityBestPractices.Analyzers;

internal static class RuleDocumentationGenerator
{
    private static readonly Regex RuleRow = new(
        @"^\|\s*`(?<id>UBP\d{4})`\s*\|\s*(?<before>.*?)\s*\|\s*(?<after>.*?)\s*\|$",
        RegexOptions.Compiled);

    internal static void Generate(string repositoryRoot)
    {
        repositoryRoot = Path.GetFullPath(repositoryRoot);
        var rulesDirectory = Path.Combine(repositoryRoot, "docs", "rules");
        Directory.CreateDirectory(rulesDirectory);

        var examples = ReadExamples(Path.Combine(repositoryRoot, "README.md"));
        foreach (var rule in DiagnosticCatalog.All)
        {
            if (!examples.TryGetValue(rule.DiagnosticId, out var example))
            {
                throw new InvalidOperationException(
                    $"README.md does not contain a quick-fix row for {rule.DiagnosticId}.");
            }

            File.WriteAllText(
                Path.Combine(rulesDirectory, rule.DiagnosticId + ".md"),
                CreateRulePage(rule, example.Before, example.After),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        File.WriteAllText(
            Path.Combine(rulesDirectory, "index.md"),
            CreateIndex(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static Dictionary<string, (string Before, string After)> ReadExamples(string readmePath)
    {
        var result = new Dictionary<string, (string Before, string After)>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(readmePath))
        {
            var match = RuleRow.Match(line);
            if (match.Success)
            {
                result[match.Groups["id"].Value] = (
                    match.Groups["before"].Value.Trim(),
                    match.Groups["after"].Value.Trim());
            }
        }

        return result;
    }

    private static string CreateRulePage(RuleMetadata rule, string recognizedForm, string fixedForm)
    {
        var fixAll = !rule.HasCodeFix
            ? "Not applicable. This rule is diagnostic-only."
            : rule.SupportsFixAll
                ? "Yes. Document, project, and solution Fix All scopes are available."
                : "No. Apply this quick fix to one occurrence at a time.";
        var reviewNotice = rule.Safety == RuleSafety.ReviewRequired
            ? "\n> Review the resulting code and test its runtime behavior before committing it.\n"
            : string.Empty;
        var dotsNotice = rule.Category == RuleCategories.UnityDotsMigration
            ? "\nFor DOTS execution-mode changes, verify execution timing, synchronization points, dependency propagation, thread safety, and ECS scheduling behavior. The quick fix preserves the existing syntactic and semantic eligibility guards, but cannot prove application-level scheduling invariants.\n"
            : string.Empty;
        var symbols = rule.RequiredSymbols.Length == 0
            ? "No package-specific symbol beyond the C# base class library."
            : string.Join(", ", rule.RequiredSymbols.Select(symbol => $"`{symbol}`"));
        var outcome = rule.HasCodeFix
            ? $"The quick fix uses `{EscapeInline(fixedForm)}` to express the documented Unity or C# best practice while retaining the rule's semantic preconditions."
            : "The rule is diagnostic-only because choosing the correct ownership or lifetime requires application context.";
        var afterExample = rule.HasCodeFix
            ? $"// Simplified result of \"{EscapeComment(rule.FixTitle)}\":\n// {EscapeComment(fixedForm)}"
            : "// No automatic transformation is offered.";
        var fixConstraint = rule.HasCodeFix
            ? "- The quick fix is offered only when it can construct a complete local transformation."
            : "- No code action is registered for this diagnostic.";

        return $"""
            # {rule.DiagnosticId}: {rule.Title}

            | Property | Value |
            | --- | --- |
            | Rule ID | `{rule.DiagnosticId}` |
            | Category | `{rule.Category}` |
            | Default severity | `{rule.DefaultSeverity}` |
            | Safety classification | `{rule.Safety}` |
            | Fix All | {(rule.SupportsFixAll ? "Supported" : "Not supported")} |

            ## Summary

            {rule.Description}
            {reviewNotice}
            ## Why the rule exists

            The recognized form is `{EscapeInline(recognizedForm)}`. {outcome}
            {dotsNotice}
            ## Before

            ```csharp
            // Simplified recognized form:
            // {EscapeComment(recognizedForm)}
            ```

            ## After

            ```csharp
            {afterExample}
            ```

            ## Exact applicability constraints

            - The compiler must resolve every required API to the expected symbol rather than a same-named look-alike.
            - Required symbols: {symbols}
            - The syntax must match the conservative shape documented in the summary and must pass the rule's semantic guards.
            - The analyzer excludes generated code and remains silent when required Unity or package symbols are unavailable.
            {fixConstraint}
            {GetRuleSpecificConstraints(rule)}

            ## Known exclusions

            - Dynamic dispatch, unresolved overloads, malformed syntax, and same-named user APIs are excluded.
            - Cases requiring whole-program behavioral inference are excluded.
            - More complex but potentially equivalent source shapes may intentionally receive no suggestion.
            {GetRuleSpecificExclusions(rule)}

            ## Fix All support

            {fixAll}

            ## Required Unity or package version

            {rule.MinimumUnityVersion}. Availability is additionally determined from the required symbols in the active compilation.

            ## Official documentation

            - [Unity Roslyn analyzers](https://docs.unity3d.com/Manual/roslyn-analyzers.html)
            - [Configure .NET code analysis](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/configuration-options)
            {GetFamilyLink(rule)}
            """;
    }

    private static string CreateIndex()
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Rule index");
        builder.AppendLine();
        builder.AppendLine("This index is generated from the analyzer's central diagnostic catalog. Run `dotnet run --project tests/UnityBestPractices.Analyzers.Tests -- --generate-rule-docs .` after changing rule metadata.");
        builder.AppendLine();
        builder.AppendLine("| ID | Title | Category | Severity | Safety | Fix All |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (var rule in DiagnosticCatalog.All)
        {
            builder.Append("| [")
                .Append(rule.DiagnosticId)
                .Append("](")
                .Append(rule.DiagnosticId)
                .Append(".md) | ")
                .Append(rule.Title)
                .Append(" | `")
                .Append(rule.Category)
                .Append("` | ")
                .Append(rule.DefaultSeverity)
                .Append(" | ")
                .Append(rule.Safety)
                .Append(" | ")
                .Append(rule.SupportsFixAll ? "Yes" : "No")
                .AppendLine(" |");
        }

        return builder.ToString();
    }

    private static string GetFamilyLink(RuleMetadata rule)
    {
        if (rule.Category == RuleCategories.UnityDotsMigration)
        {
            return "- [Unity Entities systems](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/systems-intro.html)";
        }

        if (rule.RequiredSymbols.Any(symbol => symbol.StartsWith("Unity.Burst.", StringComparison.Ordinal)))
        {
            return "- [Unity Burst package](https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/index.html)";
        }

        if (rule.RequiredSymbols.Any(symbol => symbol.StartsWith("Unity.Collections.", StringComparison.Ordinal)))
        {
            return "- [Unity Collections package](https://docs.unity3d.com/Packages/com.unity.collections@2.1/manual/index.html)";
        }

        return "- [C# language documentation](https://learn.microsoft.com/dotnet/csharp/)";
    }

    private static string GetRuleSpecificConstraints(RuleMetadata rule)
    {
        if (rule.DiagnosticId == DiagnosticIds.EncapsulateSerializedField)
        {
            return "- Before registering the fix, solution-wide symbol reference analysis must prove that every reference is inside the declaring type or one of its nested types.";
        }

        if (rule.Category == RuleCategories.UnityDotsMigration)
        {
            if (rule.DiagnosticId == DiagnosticIds.EntitiesForEachToSystemApiQuery)
            {
                return "- Synchronous conversions support local and instance captures, `WithoutBurst()`, component parameters, mutable `ref DynamicBuffer<T>` parameters, and entity-only lambdas constrained by at least one `WithAll<T>()` filter. Mutable buffers become bare `DynamicBuffer<T>` query parameters, and `entityInQueryIndex` becomes a packed loop counter. Entity-only queries use `SystemAPI.Query<RefRO<T>>().WithEntityAccess()`. `WithStructuralChanges()` loops first copy matching entity IDs into a disposable `NativeList<Entity>`, then retrieve their components while processing the snapshot so structural `EntityManager` operations remain immediate and safe.";
            }

            if (IsEntitiesForEachJobConversion(rule))
            {
                return "- Query parameters, filters, entity access, captures, wrapper access, and the resolved Unity.Entities API must match the supported Entities 1.x model. `DynamicBuffer<T>` parameters retain their `ref` or `in` modifier, the special `entityInQueryIndex` parameter becomes `[EntityIndexInQuery]`, read-only unmanaged captures become initialized job fields, and `SystemAPI.Time.ElapsedTime` / `DeltaTime` are evaluated by the system and passed into the job.";
            }

            return "- Query parameters, filters, entity access, captures, wrapper access, and the resolved Unity.Entities API must match the supported Entities 1.x model.";
        }

        return rule.DiagnosticId switch
        {
            DiagnosticIds.DiscardedScheduledJobHandle =>
                "- The discarded invocation must resolve to a supported Unity.Jobs scheduling API or `Unity.Entities.IJobEntityExtensions`, and its exact return type must be `Unity.Jobs.JobHandle`.",
            DiagnosticIds.UndisposedPersistentNativeContainer =>
                "- The initial implementation reports only a directly constructed, locally owned `NativeArray<T>` using `Allocator.Persistent` when the local has no subsequent references.",
            DiagnosticIds.InvalidTemporaryAllocatorEscape =>
                "- The allocation must be a semantically resolved `NativeArray<T>` using `Allocator.Temp` or `Allocator.TempJob`, and the escape must be a direct return, field/property assignment, or captured escaping delegate.",
            DiagnosticIds.CacheShaderPropertyId =>
                "- At least two calls in the same non-nested type declaration must resolve to `UnityEngine.Shader.PropertyToID` with the same string literal.",
            DiagnosticIds.CombineLocalPositionAndRotation =>
                "- Two consecutive simple assignments must target `Transform.localPosition` and `Transform.localRotation` on the same local, parameter, or field receiver, and the active Unity API must expose `SetLocalPositionAndRotation`.",
            DiagnosticIds.UseRefLocal =>
                "- The collection must expose a real ref-returning indexer or `ElementAt(int)`. A semantically resolved `NativeArray<T>` may instead expose a public `AsSpan()` returning the matching mutable `System.Span<T>`.",
            DiagnosticIds.MatchFolderNamespace =>
                "- The target must contain only global type or delegate declarations, and non-generated sibling files in the same folder must establish one uniquely most-common namespace.",
            _ => string.Empty,
        };
    }

    private static string GetRuleSpecificExclusions(RuleMetadata rule)
    {
        if (rule.DiagnosticId == DiagnosticIds.EncapsulateSerializedField)
        {
            return "- The diagnostic remains visible, but the fix is withheld when a derived type, unrelated type, `nameof`, or any other external solution reference would lose access.";
        }

        if (rule.Category == RuleCategories.UnityDotsMigration)
        {
            if (rule.DiagnosticId == DiagnosticIds.EntitiesForEachToSystemApiQuery)
            {
                return "- Nested lambdas, unresolved or unsupported filters, read-only `in DynamicBuffer<T>` parameters, entity-only loops without a usable query component, and structural-change component loops that directly invoke unsupported `EntityManager` methods are excluded. Scheduled job conversions remain unavailable for managed or mutated captures, unsupported `SystemAPI` access, `WithoutBurst()`, or structural-change pipelines.";
            }

            if (IsEntitiesForEachJobConversion(rule))
            {
                return "- Managed or mutated captures, nested lambdas, structural-change pipelines, unsupported `SystemAPI` access, unsupported filters, system-instance access, and ambiguous package symbols are excluded.";
            }

            return "- Captured locals, nested lambdas, structural-change pipelines, unsupported filters, system-instance access, and ambiguous package symbols are excluded.";
        }

        return rule.DiagnosticId switch
        {
            DiagnosticIds.DiscardedScheduledJobHandle =>
                "- Calls returning `void`, already assigned/returned handles, look-alike scheduling APIs, and Entities APIs that may update a system dependency internally are excluded.",
            DiagnosticIds.UndisposedPersistentNativeContainer =>
                "- Containers passed elsewhere, returned, conditionally owned, referenced later, or allocated with another allocator are excluded pending reliable path-sensitive ownership analysis.",
            DiagnosticIds.InvalidTemporaryAllocatorEscape =>
                "- Reassigned locals, non-native look-alike containers, and uses that remain within the allocating method are excluded.",
            DiagnosticIds.CacheShaderPropertyId =>
                "- Dynamic strings, a single call, nested-type calls, and same-named non-Unity APIs are excluded.",
            DiagnosticIds.CombineLocalPositionAndRotation =>
                "- Reversed or non-adjacent assignments, compound assignments, different or repeatedly evaluated receivers, intervening comments or directives, same-named non-Unity APIs, and Unity versions without the combined API are excluded.",
            DiagnosticIds.UseRefLocal =>
                "- By-value-only indexers, read-only spans, async or iterator methods, jump statements before the write-back, lambda or local-function captures, and changed container or index values are excluded.",
            DiagnosticIds.MatchFolderNamespace =>
                "- Existing namespaces, top-level statements, generated files, missing file paths, no namespace examples, and ties between neighboring namespaces are excluded.",
            _ => string.Empty,
        };
    }

    private static bool IsEntitiesForEachJobConversion(RuleMetadata rule) =>
        rule.DiagnosticId == DiagnosticIds.EntitiesForEachToJobEntityRun ||
        rule.DiagnosticId == DiagnosticIds.EntitiesForEachToJobEntitySchedule ||
        rule.DiagnosticId == DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel;

    private static string EscapeInline(string value) => value.Replace("`", "\\`");

    private static string EscapeComment(string value) =>
        value.Replace("\r", " ").Replace("\n", " ").Replace("*/", "* /");
}

internal static class RepositoryConsistencyVerifier
{
    private static readonly string[] RequiredSections =
    {
        "## Summary",
        "## Why the rule exists",
        "## Before",
        "## After",
        "## Exact applicability constraints",
        "## Known exclusions",
        "## Fix All support",
        "## Required Unity or package version",
        "## Official documentation",
    };

    internal static void Verify()
    {
        var repositoryRoot = FindRepositoryRoot();
        var rulesDirectory = Path.Combine(repositoryRoot, "docs", "rules");
        var catalogIds = DiagnosticCatalog.All
            .Select(rule => rule.DiagnosticId)
            .ToHashSet(StringComparer.Ordinal);
        var descriptors = new UnityBestPracticesAnalyzer().SupportedDiagnostics
            .ToDictionary(descriptor => descriptor.Id, StringComparer.Ordinal);
        var readmeIds = File.ReadLines(Path.Combine(repositoryRoot, "README.md"))
            .Select(line => Regex.Match(line, @"^\|\s*`(?<id>UBP\d{4})`"))
            .Where(match => match.Success)
            .Select(match => match.Groups["id"].Value)
            .ToHashSet(StringComparer.Ordinal);

        if (!catalogIds.SetEquals(readmeIds))
        {
            throw new InvalidOperationException("README rule IDs have drifted from DiagnosticCatalog.");
        }

        foreach (var rule in DiagnosticCatalog.All)
        {
            var descriptor = descriptors[rule.DiagnosticId];
            if (descriptor.Id != rule.DiagnosticId ||
                descriptor.Title.ToString() != rule.Title ||
                descriptor.MessageFormat.ToString() != rule.MessageFormat ||
                descriptor.Description.ToString() != rule.Description ||
                descriptor.Category != rule.Category ||
                descriptor.DefaultSeverity != rule.DefaultSeverity ||
                descriptor.HelpLinkUri != rule.DocumentationUrl)
            {
                throw new InvalidOperationException($"{rule.DiagnosticId} descriptor drifted from its metadata.");
            }

            var documentPath = Path.Combine(rulesDirectory, rule.DiagnosticId + ".md");
            if (!File.Exists(documentPath))
            {
                throw new InvalidOperationException($"Missing documentation for {rule.DiagnosticId}.");
            }

            var document = File.ReadAllText(documentPath);
            if (!document.Contains($"# {rule.DiagnosticId}: {rule.Title}", StringComparison.Ordinal) ||
                !document.Contains($"| Category | `{rule.Category}` |", StringComparison.Ordinal) ||
                !document.Contains($"| Safety classification | `{rule.Safety}` |", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{rule.DiagnosticId} documentation metadata is stale.");
            }

            foreach (var section in RequiredSections)
            {
                if (!document.Contains(section, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"{rule.DiagnosticId} documentation is missing {section}.");
                }
            }
        }

        var documentedIds = Directory.EnumerateFiles(rulesDirectory, "UBP*.md")
            .Select(path => Path.GetFileNameWithoutExtension(path)!)
            .ToHashSet(StringComparer.Ordinal);
        if (!catalogIds.SetEquals(documentedIds))
        {
            throw new InvalidOperationException("Rule documentation files have drifted from DiagnosticCatalog.");
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UnityBestPractices.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
