// DOTS query transformations and job extraction.
using System.Collections.Immutable;
using System.Linq;
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
                : query.SupportsJobConversion
                    ? ApplyJobEntityFix(
                    document,
                    root,
                    query.Statement,
                    query.ContainingType,
                    query.JobName,
                    query.CreateJobBody(),
                    query.CreateJobParameters(),
                    query.CreateJobAttributes(),
                    query.JobFields,
                    query.DisposalCaptures,
                    query.ExplicitDependency,
                    GetTargetExecutionMode(rule.Kind),
                    semanticModel)
                    : document;
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
                System.Collections.Immutable.ImmutableArray<DotsJobField>.Empty,
                System.Collections.Immutable.ImmutableArray<string>.Empty,
                null,
                GetTargetExecutionMode(rule.Kind),
                semanticModel);
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
                out var replacement,
                out var parallelEcbConversion))
        {
            return document;
        }

        if (parallelEcbConversion is not null &&
            query.Statement.Parent is BlockSyntax ecbParentBlock &&
            parallelEcbConversion.Declaration.Parent == ecbParentBlock)
        {
            var replacementStatement = replacement
                .WithTriviaFrom(query.Statement)
                .WithAdditionalAnnotations(Formatter.Annotation);
            var updatedDeclaration = parallelEcbConversion.Declaration
                .WithDeclaration(parallelEcbConversion.Declaration.Declaration.WithVariables(
                    SyntaxFactory.SingletonSeparatedList(
                        parallelEcbConversion.Declaration.Declaration.Variables[0]
                            .WithIdentifier(SyntaxFactory.Identifier(parallelEcbConversion.NewName))
                            .WithInitializer(SyntaxFactory.EqualsValueClause(parallelEcbConversion.Initializer)))))
                .WithAdditionalAnnotations(Formatter.Annotation);
            var updatedBlock = ecbParentBlock.ReplaceNodes(
                new SyntaxNode[] { parallelEcbConversion.Declaration, query.Statement },
                (original, _) => original == query.Statement ? replacementStatement : updatedDeclaration);
            return document.WithSyntaxRoot(root.ReplaceNode(ecbParentBlock, updatedBlock));
        }

        if (query.InlineSystemApiReplacementBlock &&
            replacement is BlockSyntax replacementBlock &&
            query.Statement.Parent is BlockSyntax parentBlock)
        {
            var statementIndex = parentBlock.Statements.IndexOf(query.Statement);
            if (statementIndex >= 0)
            {
                var statements = replacementBlock.Statements;
                if (statements.Count != 0)
                {
                    statements = statements.Replace(
                        statements[0],
                        statements[0].WithLeadingTrivia(query.Statement.GetLeadingTrivia()));
                    var lastStatementIndex = statements.Count - 1;
                    statements = statements.Replace(
                        statements[lastStatementIndex],
                        statements[lastStatementIndex]
                            .WithTrailingTrivia(query.Statement.GetTrailingTrivia()));
                }

                var updatedStatements = parentBlock.Statements
                    .RemoveAt(statementIndex)
                    .InsertRange(statementIndex, statements);
                var updatedBlock = parentBlock
                    .WithStatements(updatedStatements)
                    .WithAdditionalAnnotations(Formatter.Annotation);
                return document.WithSyntaxRoot(root.ReplaceNode(parentBlock, updatedBlock));
            }
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
        System.Collections.Immutable.ImmutableArray<DotsJobField> jobFields,
        System.Collections.Immutable.ImmutableArray<string> disposalCaptures,
        string? explicitDependency,
        string executionMode,
        SemanticModel semanticModel)
    {
        var needsHandleBookkeeping = executionMode != "Run" && !disposalCaptures.IsEmpty;
        if (needsHandleBookkeeping && sourceStatement.Parent is not BlockSyntax)
        {
            return document;
        }

        var burstAttribute = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "Unity.Burst.BurstCompileAttribute") is null
            ? string.Empty
            : "[Unity.Burst.BurstCompile]\n";
        var jobText =
            burstAttribute +
            jobAttributes +
            "private partial struct " + jobName + " : Unity.Entities.IJobEntity\n" +
            "{\n" +
            string.Concat(jobFields.Select(field =>
                (field.IsReadOnly ? "    [Unity.Collections.ReadOnly]\n" : string.Empty) +
                "    public " + field.TypeName + " " + field.Name + ";\n")) +
            "    public void Execute(" + jobParameters + ")\n" +
            jobBody.WithoutTrivia().ToFullString() + "\n" +
            "}";
        var jobDeclaration = SyntaxFactory.ParseMemberDeclaration(jobText);
        if (jobDeclaration is null || jobDeclaration.ContainsDiagnostics)
        {
            return document;
        }

        var initialization = jobFields.IsEmpty
            ? "()"
            : "\n{\n" +
              string.Join(",\n", jobFields.Select(field =>
                  "    " + field.Name + " = " + field.Initializer)) +
              "\n}";
        var invocation = "new " + jobName + initialization + "." + executionMode +
            "(" + (explicitDependency ?? (needsHandleBookkeeping && IsSystemBase(containingType, semanticModel)
                ? "Dependency"
                : string.Empty)) + ")";
        TypeDeclarationSyntax updatedType;
        if (needsHandleBookkeeping && sourceStatement.Parent is BlockSyntax parentBlock)
        {
            var handleName = CreateUniqueLocalName(semanticModel, sourceStatement, "jobHandle");
            var statementTexts = new System.Collections.Generic.List<string>
            {
                "var " + handleName + " = " + invocation + ";",
            };
            foreach (var capture in disposalCaptures)
            {
                statementTexts.Add(handleName + " = " + capture + ".Dispose(" + handleName + ");");
            }

            if (IsSystemBase(containingType, semanticModel))
            {
                statementTexts[statementTexts.Count - 1] =
                    "Dependency = " + disposalCaptures[disposalCaptures.Length - 1] +
                    ".Dispose(" + handleName + ");";
            }

            var statements = statementTexts.Select(SyntaxFactory.ParseStatement).ToArray();
            statements[0] = statements[0].WithLeadingTrivia(sourceStatement.GetLeadingTrivia());
            statements[statements.Length - 1] = statements[statements.Length - 1]
                .WithTrailingTrivia(sourceStatement.GetTrailingTrivia());
            var index = parentBlock.Statements.IndexOf(sourceStatement);
            var replacementBlock = parentBlock.WithStatements(
                parentBlock.Statements.RemoveAt(index).InsertRange(index, statements));
            updatedType = containingType.ReplaceNode(parentBlock, replacementBlock).AddMembers(jobDeclaration);
        }
        else
        {
            var executionStatement = SyntaxFactory.ParseStatement(invocation + ";")
                .WithTriviaFrom(sourceStatement);
            updatedType = containingType.ReplaceNode(sourceStatement, executionStatement).AddMembers(jobDeclaration);
        }

        updatedType = updatedType
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(containingType, updatedType));
    }

    private static bool IsSystemBase(TypeDeclarationSyntax containingType, SemanticModel semanticModel)
    {
        for (var type = semanticModel.GetDeclaredSymbol(containingType) as INamedTypeSymbol;
             type is not null;
             type = type.BaseType)
        {
            if (type.ToDisplayString() == "Unity.Entities.SystemBase")
            {
                return true;
            }
        }

        return false;
    }

    private static string CreateUniqueLocalName(
        SemanticModel semanticModel,
        StatementSyntax statement,
        string baseName)
    {
        var scope = statement.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>() as SyntaxNode ?? statement.Parent;
        var names = scope?.DescendantTokens()
            .Where(token => token.IsKind(SyntaxKind.IdentifierToken))
            .Select(token => token.ValueText)
            .ToImmutableHashSet(System.StringComparer.Ordinal) ??
            System.Collections.Immutable.ImmutableHashSet<string>.Empty;
        var name = baseName;
        for (var suffix = 2; names.Contains(name) || semanticModel.LookupSymbols(statement.SpanStart, name: name).Length != 0; suffix++)
        {
            name = baseName + suffix;
        }

        return name;
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
