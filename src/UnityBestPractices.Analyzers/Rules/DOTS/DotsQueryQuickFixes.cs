using UnityBestPractices.Analyzers;
using UnityBestPractices.Analyzers.Infrastructure;
// Diagnostic registration and analysis for DOTS query migrations.
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UnityBestPractices.Analyzers.Rules.Dots;

internal enum DotsQueryQuickFixKind
{
    EntitiesForEachToSystemApiQuery,
    EntitiesForEachToJobEntityRun,
    EntitiesForEachToJobEntitySchedule,
    EntitiesForEachToJobEntityScheduleParallel,
    SystemApiQueryToJobEntityRun,
    SystemApiQueryToJobEntitySchedule,
    SystemApiQueryToJobEntityScheduleParallel,
    JobEntityRunToSchedule,
    JobEntityRunToScheduleParallel,
    JobEntityScheduleToRun,
    JobEntityScheduleToScheduleParallel,
    JobEntityScheduleParallelToRun,
    JobEntityScheduleParallelToSchedule,
}

internal sealed class DotsQueryQuickFixRule
{
    internal DotsQueryQuickFixRule(
        string diagnosticId,
        DotsQueryQuickFixKind kind,
        string title,
        string message,
        string description,
        string fixTitle)
    {
        Kind = kind;
        Metadata = RuleMetadataFactory.Create(
            diagnosticId,
            title,
            message,
            description,
            fixTitle,
            "Unity.Entities.SystemAPI",
            "Unity.Entities.IJobEntity");
    }

    internal DotsQueryQuickFixKind Kind { get; }

    internal string FixTitle => Metadata.FixTitle;

    internal RuleMetadata Metadata { get; }

    internal DiagnosticDescriptor Descriptor => Metadata.Descriptor;
}

internal static class DotsQueryRules
{
    private static readonly ImmutableArray<DotsQueryQuickFixRule> AllRules = ImmutableArray.Create(
        Rule(
            DiagnosticIds.EntitiesForEachToSystemApiQuery,
            DotsQueryQuickFixKind.EntitiesForEachToSystemApiQuery,
            "Convert Entities.ForEach to SystemAPI.Query",
            "Convert this main-thread Entities.ForEach to SystemAPI.Query",
            "SystemAPI.Query is the Entities 1.x main-thread replacement for Entities.ForEach.",
            "Convert to SystemAPI.Query foreach"),
        Rule(
            DiagnosticIds.EntitiesForEachToJobEntityRun,
            DotsQueryQuickFixKind.EntitiesForEachToJobEntityRun,
            "Convert Entities.ForEach to IJobEntity.Run",
            "Extract this query into an IJobEntity that runs immediately",
            "IJobEntity provides the supported job representation while Run keeps main-thread execution.",
            "Convert to IJobEntity.Run"),
        Rule(
            DiagnosticIds.EntitiesForEachToJobEntitySchedule,
            DotsQueryQuickFixKind.EntitiesForEachToJobEntitySchedule,
            "Convert Entities.ForEach to IJobEntity.Schedule",
            "Extract this query into a scheduled IJobEntity",
            "IJobEntity.Schedule queues component iteration as a single scheduled job.",
            "Convert to IJobEntity.Schedule"),
        Rule(
            DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel,
            DotsQueryQuickFixKind.EntitiesForEachToJobEntityScheduleParallel,
            "Convert Entities.ForEach to IJobEntity.ScheduleParallel",
            "Extract this query into a parallel IJobEntity",
            "IJobEntity.ScheduleParallel distributes matching chunks across worker threads.",
            "Convert to IJobEntity.ScheduleParallel"),
        Rule(
            DiagnosticIds.SystemApiQueryToJobEntityRun,
            DotsQueryQuickFixKind.SystemApiQueryToJobEntityRun,
            "Convert SystemAPI.Query to IJobEntity.Run",
            "Extract this SystemAPI.Query loop into an IJobEntity that runs immediately",
            "IJobEntity.Run retains immediate execution while moving query logic into a reusable job.",
            "Convert to IJobEntity.Run"),
        Rule(
            DiagnosticIds.SystemApiQueryToJobEntitySchedule,
            DotsQueryQuickFixKind.SystemApiQueryToJobEntitySchedule,
            "Convert SystemAPI.Query to IJobEntity.Schedule",
            "Extract this SystemAPI.Query loop into a scheduled IJobEntity",
            "IJobEntity.Schedule moves compatible component iteration to the job system.",
            "Convert to IJobEntity.Schedule"),
        Rule(
            DiagnosticIds.SystemApiQueryToJobEntityScheduleParallel,
            DotsQueryQuickFixKind.SystemApiQueryToJobEntityScheduleParallel,
            "Convert SystemAPI.Query to IJobEntity.ScheduleParallel",
            "Extract this SystemAPI.Query loop into a parallel IJobEntity",
            "IJobEntity.ScheduleParallel distributes compatible query work across worker threads.",
            "Convert to IJobEntity.ScheduleParallel"),
        ModeRule(DiagnosticIds.JobEntityRunToSchedule, DotsQueryQuickFixKind.JobEntityRunToSchedule, "Run", "Schedule"),
        ModeRule(DiagnosticIds.JobEntityRunToScheduleParallel, DotsQueryQuickFixKind.JobEntityRunToScheduleParallel, "Run", "ScheduleParallel"),
        ModeRule(DiagnosticIds.JobEntityScheduleToRun, DotsQueryQuickFixKind.JobEntityScheduleToRun, "Schedule", "Run"),
        ModeRule(DiagnosticIds.JobEntityScheduleToScheduleParallel, DotsQueryQuickFixKind.JobEntityScheduleToScheduleParallel, "Schedule", "ScheduleParallel"),
        ModeRule(DiagnosticIds.JobEntityScheduleParallelToRun, DotsQueryQuickFixKind.JobEntityScheduleParallelToRun, "ScheduleParallel", "Run"),
        ModeRule(DiagnosticIds.JobEntityScheduleParallelToSchedule, DotsQueryQuickFixKind.JobEntityScheduleParallelToSchedule, "ScheduleParallel", "Schedule"));

    private static readonly ImmutableDictionary<string, DotsQueryQuickFixRule> RulesById =
        AllRules.ToImmutableDictionary(rule => rule.Descriptor.Id, StringComparer.Ordinal);

    internal static ImmutableArray<DiagnosticDescriptor> Descriptors { get; } =
        AllRules.Select(rule => rule.Descriptor).ToImmutableArray();

    internal static ImmutableArray<RuleMetadata> Metadata { get; } =
        AllRules.Select(rule => rule.Metadata).ToImmutableArray();

    internal static ImmutableArray<string> FixableDiagnosticIds { get; } =
        AllRules.Select(rule => rule.Descriptor.Id).ToImmutableArray();

    internal static bool TryGetRule(string diagnosticId, out DotsQueryQuickFixRule rule) =>
        RulesById.TryGetValue(diagnosticId, out rule!);

    internal static void AnalyzeExpressionStatement(SyntaxNodeAnalysisContext context)
    {
        var statement = (ExpressionStatementSyntax)context.Node;
        if (EntitiesForEachQuery.TryCreate(
                statement,
                context.SemanticModel,
                context.CancellationToken,
                out var forEachQuery))
        {
            if (forEachQuery.ExecutionMode == "Run" &&
                forEachQuery.TryCreateSystemApiLoop(
                    context.SemanticModel,
                    context.CancellationToken,
                    out _,
                    out _))
            {
                Report(context, DiagnosticIds.EntitiesForEachToSystemApiQuery, statement.GetLocation());
            }

            if (forEachQuery.SupportsJobConversion)
            {
                Report(context, DiagnosticIds.EntitiesForEachToJobEntityRun, statement.GetLocation());
                Report(context, DiagnosticIds.EntitiesForEachToJobEntitySchedule, statement.GetLocation());
                Report(context, DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel, statement.GetLocation());
            }
            return;
        }

        if (!JobEntityExecution.TryCreate(
                statement,
                context.SemanticModel,
                context.CancellationToken,
                out var execution))
        {
            return;
        }

        foreach (var diagnosticId in GetExecutionModeDiagnosticIds(execution.Mode))
        {
            Report(context, diagnosticId, statement.GetLocation());
        }
    }

    internal static void AnalyzeSystemApiQuery(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not CommonForEachStatementSyntax statement ||
            !SystemApiQueryLoop.TryCreate(
                statement,
                context.SemanticModel,
                context.CancellationToken,
                out _))
        {
            return;
        }

        Report(context, DiagnosticIds.SystemApiQueryToJobEntityRun, statement.GetLocation());
        Report(context, DiagnosticIds.SystemApiQueryToJobEntitySchedule, statement.GetLocation());
        Report(context, DiagnosticIds.SystemApiQueryToJobEntityScheduleParallel, statement.GetLocation());
    }

    private static void Report(
        SyntaxNodeAnalysisContext context,
        string diagnosticId,
        Location location)
    {
        if (RulesById.TryGetValue(diagnosticId, out var rule) &&
            AnalyzerConfiguration.For(context).IsRuleEnabled(diagnosticId))
        {
            context.ReportDiagnostic(Diagnostic.Create(rule.Descriptor, location));
        }
    }

    private static ImmutableArray<string> GetExecutionModeDiagnosticIds(string mode) => mode switch
    {
        "Run" => ImmutableArray.Create(
            DiagnosticIds.JobEntityRunToSchedule,
            DiagnosticIds.JobEntityRunToScheduleParallel),
        "Schedule" => ImmutableArray.Create(
            DiagnosticIds.JobEntityScheduleToRun,
            DiagnosticIds.JobEntityScheduleToScheduleParallel),
        "ScheduleParallel" => ImmutableArray.Create(
            DiagnosticIds.JobEntityScheduleParallelToRun,
            DiagnosticIds.JobEntityScheduleParallelToSchedule),
        _ => ImmutableArray<string>.Empty,
    };

    private static DotsQueryQuickFixRule Rule(
        string diagnosticId,
        DotsQueryQuickFixKind kind,
        string title,
        string message,
        string description,
        string fixTitle) =>
        new(diagnosticId, kind, title, message, description, fixTitle);

    private static DotsQueryQuickFixRule ModeRule(
        string diagnosticId,
        DotsQueryQuickFixKind kind,
        string sourceMode,
        string targetMode) =>
        Rule(
            diagnosticId,
            kind,
            "Switch IJobEntity from " + sourceMode + " to " + targetMode,
            "Switch this IJobEntity execution from " + sourceMode + " to " + targetMode,
            "The IJobEntity execution mode can be changed without rewriting its query or Execute method.",
            "Use " + targetMode + " execution");
}
