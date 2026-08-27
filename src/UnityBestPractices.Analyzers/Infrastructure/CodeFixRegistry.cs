using System;
using System.Collections.Immutable;
using System.Linq;

namespace UnityBestPractices.Analyzers;

internal static class CodeFixRegistry
{
    internal static ImmutableArray<CodeFixHandler> All { get; } = Create(
        ExpressionQuickFixRegistry.Handlers,
        DotsQueryRules.Handlers,
        LegacyCoreCodeFixes.Handlers,
        AdvancedUnityRules.Handlers,
        ModernObjectFindRule.Handlers,
        NamespaceConsistencyRules.Handlers,
        UnusedEntityAccessRule.Handlers);

    private static readonly ImmutableDictionary<string, CodeFixHandler> ByDiagnosticId =
        All.ToImmutableDictionary(handler => handler.DiagnosticId, StringComparer.Ordinal);

    internal static bool TryGet(string diagnosticId, out CodeFixHandler handler) =>
        ByDiagnosticId.TryGetValue(diagnosticId, out handler!);

    internal static ImmutableArray<CodeFixHandler> Create(
        params ImmutableArray<CodeFixHandler>[] families)
    {
        var handlers = families.SelectMany(family => family).ToImmutableArray();
        var duplicate = handlers.GroupBy(handler => handler.DiagnosticId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate code-fix handler for diagnostic ID '{duplicate.Key}'.");
        }

        return handlers;
    }
}
