using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace UnityBestPractices.Analyzers;

internal static class DotsQueryCodeFixes
{
    internal static async Task<Document> ApplyFixAsync(
        Document document,
        Diagnostic diagnostic,
        DotsQueryQuickFixRule rule,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        if (IsEntitiesForEachKind(rule.Kind))
        {
            var statement = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
                .FirstAncestorOrSelf<ExpressionStatementSyntax>();
            if (statement is null ||
                !EntitiesForEachQuery.TryCreate(
                    statement,
                    semanticModel,
                    cancellationToken,
                    out var query))
            {
                return document;
            }

            return rule.Kind == DotsQueryQuickFixKind.EntitiesForEachToSystemApiQuery
                ? ApplySystemApiQueryFix(document, root, semanticModel, query, cancellationToken)
                : ApplyJobEntityFix(
                    document,
                    root,
                    query.Statement,
                    query.ContainingType,
                    query.JobName,
                    query.CreateJobBody(),
                    query.CreateJobParameters(),
                    query.CreateJobAttributes(),
                    GetTargetExecutionMode(rule.Kind),
                    semanticModel.Compilation);
        }

        if (IsSystemApiQueryKind(rule.Kind))
        {
            var statement = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
                .FirstAncestorOrSelf<CommonForEachStatementSyntax>();
            if (statement is null ||
                !SystemApiQueryLoop.TryCreate(
                    statement,
                    semanticModel,
                    cancellationToken,
                    out var query))
            {
                return document;
            }

            return ApplyJobEntityFix(
                document,
                root,
                query.Statement,
                query.ContainingType,
                query.JobName,
                query.CreateJobBody(),
                query.CreateJobParameters(),
                query.CreateJobAttributes(),
                GetTargetExecutionMode(rule.Kind),
                semanticModel.Compilation);
        }

        var executionStatement = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<ExpressionStatementSyntax>();
        if (executionStatement is null ||
            !JobEntityExecution.TryCreate(
                executionStatement,
                semanticModel,
                cancellationToken,
                out var execution))
        {
            return document;
        }

        var targetMode = GetTargetExecutionMode(rule.Kind);
        var replacementInvocation = execution.Invocation.WithExpression(
            execution.MemberAccess.WithName(SyntaxFactory.IdentifierName(targetMode)));
        var replacementStatement = executionStatement
            .ReplaceNode(execution.Invocation, replacementInvocation)
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(executionStatement, replacementStatement));
    }

    private static Document ApplySystemApiQueryFix(
        Document document,
        SyntaxNode root,
        SemanticModel semanticModel,
        EntitiesForEachQuery query,
        CancellationToken cancellationToken)
    {
        if (!query.TryCreateSystemApiLoop(
                semanticModel,
                cancellationToken,
                out var replacement))
        {
            return document;
        }

        replacement = replacement
            .WithTriviaFrom(query.Statement)
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(query.Statement, replacement));
    }

    private static Document ApplyJobEntityFix(
        Document document,
        SyntaxNode root,
        StatementSyntax sourceStatement,
        TypeDeclarationSyntax containingType,
        string jobName,
        BlockSyntax jobBody,
        string jobParameters,
        string jobAttributes,
        string executionMode,
        Compilation compilation)
    {
        var burstAttribute = compilation.GetTypeByMetadataName("Unity.Burst.BurstCompileAttribute") is null
            ? string.Empty
            : "[Unity.Burst.BurstCompile]\n";
        var jobText =
            burstAttribute +
            jobAttributes +
            "private partial struct " + jobName + " : Unity.Entities.IJobEntity\n" +
            "{\n" +
            "    public void Execute(" + jobParameters + ")\n" +
            jobBody.WithoutTrivia().ToFullString() + "\n" +
            "}";
        var jobDeclaration = SyntaxFactory.ParseMemberDeclaration(jobText);
        if (jobDeclaration is null || jobDeclaration.ContainsDiagnostics)
        {
            return document;
        }

        var executionStatement = SyntaxFactory.ParseStatement(
                "new " + jobName + "()." + executionMode + "();")
            .WithTriviaFrom(sourceStatement)
            .WithAdditionalAnnotations(Formatter.Annotation);
        var updatedType = containingType
            .ReplaceNode(sourceStatement, executionStatement)
            .AddMembers(jobDeclaration)
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(containingType, updatedType));
    }

    private static bool IsEntitiesForEachKind(DotsQueryQuickFixKind kind) =>
        kind >= DotsQueryQuickFixKind.EntitiesForEachToSystemApiQuery &&
        kind <= DotsQueryQuickFixKind.EntitiesForEachToJobEntityScheduleParallel;

    private static bool IsSystemApiQueryKind(DotsQueryQuickFixKind kind) =>
        kind >= DotsQueryQuickFixKind.SystemApiQueryToJobEntityRun &&
        kind <= DotsQueryQuickFixKind.SystemApiQueryToJobEntityScheduleParallel;

    private static string GetTargetExecutionMode(DotsQueryQuickFixKind kind) => kind switch
    {
        DotsQueryQuickFixKind.EntitiesForEachToJobEntityRun => "Run",
        DotsQueryQuickFixKind.EntitiesForEachToJobEntitySchedule => "Schedule",
        DotsQueryQuickFixKind.EntitiesForEachToJobEntityScheduleParallel => "ScheduleParallel",
        DotsQueryQuickFixKind.SystemApiQueryToJobEntityRun => "Run",
        DotsQueryQuickFixKind.SystemApiQueryToJobEntitySchedule => "Schedule",
        DotsQueryQuickFixKind.SystemApiQueryToJobEntityScheduleParallel => "ScheduleParallel",
        DotsQueryQuickFixKind.JobEntityRunToSchedule => "Schedule",
        DotsQueryQuickFixKind.JobEntityRunToScheduleParallel => "ScheduleParallel",
        DotsQueryQuickFixKind.JobEntityScheduleToRun => "Run",
        DotsQueryQuickFixKind.JobEntityScheduleToScheduleParallel => "ScheduleParallel",
        DotsQueryQuickFixKind.JobEntityScheduleParallelToRun => "Run",
        DotsQueryQuickFixKind.JobEntityScheduleParallelToSchedule => "Schedule",
        _ => string.Empty,
    };
}
