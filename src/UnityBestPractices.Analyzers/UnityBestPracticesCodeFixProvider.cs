using UnityBestPractices.Analyzers.Infrastructure;
using UnityBestPractices.Analyzers.Rules.Core;
using UnityBestPractices.Analyzers.Rules.Correctness;
using UnityBestPractices.Analyzers.Rules.Expressions;
using UnityBestPractices.Analyzers.Rules.Dots;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace UnityBestPractices.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnityBestPracticesCodeFixProvider)), Shared]
public sealed class UnityBestPracticesCodeFixProvider : CodeFixProvider
{
    // Derived from the central catalog so HasCodeFix and IDE registration cannot drift.
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        DiagnosticCatalog.All
            .Where(metadata => metadata.HasCodeFix)
            .Select(metadata => metadata.DiagnosticId)
            .ToImmutableArray();

    public override FixAllProvider GetFixAllProvider() => RuleAwareFixAllProvider.Instance;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        foreach (var diagnostic in context.Diagnostics)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (diagnostic.Id == DiagnosticIds.DiscardedScheduledJobHandle)
            {
                CodeFixRegistration.Register(
                    context,
                    diagnostic,
                    cancellationToken => AdvancedUnityRules.AssignJobHandleAsync(
                        context.Document,
                        diagnostic,
                        cancellationToken));
                continue;
            }

            if (diagnostic.Id == DiagnosticIds.CacheShaderPropertyId)
            {
                CodeFixRegistration.Register(
                    context,
                    diagnostic,
                    cancellationToken => AdvancedUnityRules.CacheShaderPropertyIdAsync(
                        context.Document,
                        diagnostic,
                        cancellationToken));
                continue;
            }

            if (diagnostic.Id == DiagnosticIds.CombineLocalPositionAndRotation)
            {
                CodeFixRegistration.Register(
                    context,
                    diagnostic,
                    cancellationToken => AdvancedUnityRules.CombineLocalPositionAndRotationAsync(
                        context.Document,
                        diagnostic,
                        cancellationToken));
                continue;
            }

            if (diagnostic.Id == DiagnosticIds.RemoveUnusedEntityAccess)
            {
                CodeFixRegistration.Register(
                    context,
                    diagnostic,
                    cancellationToken => UnusedEntityAccessRule.RemoveAsync(
                        context.Document,
                        diagnostic,
                        cancellationToken));
                continue;
            }

            if (diagnostic.Id == DiagnosticIds.MatchFolderNamespace)
            {
                CodeFixRegistration.Register(
                    context,
                    diagnostic,
                    cancellationToken => NamespaceConsistencyRules.AddNamespaceAsync(
                        context.Document,
                        diagnostic,
                        cancellationToken));
                continue;
            }

            if (diagnostic.Id == DiagnosticIds.UseModernObjectFindApi)
            {
                CodeFixRegistration.Register(
                    context,
                    diagnostic,
                    cancellationToken => ModernObjectFindRule.ApplyFixAsync(
                        context.Document,
                        diagnostic,
                        cancellationToken));
                continue;
            }

            if (DotsQueryRules.TryGetRule(diagnostic.Id, out var dotsRule))
            {
                CodeFixRegistration.Register(
                    context,
                    diagnostic,
                    cancellationToken => DotsQueryCodeFixes.ApplyFixAsync(
                        context.Document,
                        diagnostic,
                        dotsRule,
                        cancellationToken));
                continue;
            }

            if (ExpressionQuickFixRegistry.TryGetRule(diagnostic.Id, out var expressionRule))
            {
                CodeFixRegistration.Register(
                    context,
                    diagnostic,
                    cancellationToken => ApplyExpressionQuickFixAsync(
                        context.Document,
                        diagnostic,
                        expressionRule,
                        cancellationToken));
                continue;
            }

            switch (diagnostic.Id)
            {
                case DiagnosticIds.EncapsulateSerializedField:
                    if (!await LegacyCoreCodeFixes.CanSafelyEncapsulateFieldAsync(
                            context.Document,
                            diagnostic,
                            context.CancellationToken).ConfigureAwait(false))
                    {
                        break;
                    }

                    CodeFixRegistration.Register(
                        context,
                        diagnostic,
                        cancellationToken => LegacyCoreCodeFixes.EncapsulateFieldAsync(
                            context.Document,
                            diagnostic,
                            cancellationToken));
                    break;

                case DiagnosticIds.YieldNull:
                    CodeFixRegistration.Register(
                        context,
                        diagnostic,
                        cancellationToken => LegacyCoreCodeFixes.YieldNullAsync(
                            context.Document,
                            diagnostic,
                            cancellationToken));
                    break;

                case DiagnosticIds.UseSquaredMagnitude:
                    CodeFixRegistration.Register(
                        context,
                        diagnostic,
                        cancellationToken => LegacyCoreCodeFixes.UseSquaredMagnitudeAsync(
                            context.Document,
                            diagnostic,
                            cancellationToken));
                    break;

                case DiagnosticIds.AddBurstCompile:
                    CodeFixRegistration.Register(
                        context,
                        diagnostic,
                        cancellationToken => LegacyCoreCodeFixes.AddBurstCompileAsync(
                            context.Document,
                            diagnostic,
                            cancellationToken));
                    break;

                case DiagnosticIds.MarkNativeArrayReadOnly:
                    CodeFixRegistration.Register(
                        context,
                        diagnostic,
                        cancellationToken => LegacyCoreCodeFixes.MarkNativeArrayReadOnlyAsync(
                            context.Document,
                            diagnostic,
                            cancellationToken));
                    break;

                case DiagnosticIds.UseStackalloc:
                    CodeFixRegistration.Register(
                        context,
                        diagnostic,
                        cancellationToken => LegacyCoreCodeFixes.UseStackallocAsync(
                            context.Document,
                            diagnostic,
                            cancellationToken));
                    break;

                case DiagnosticIds.UseRefLocal:
                    CodeFixRegistration.Register(
                        context,
                        diagnostic,
                        cancellationToken => LegacyCoreCodeFixes.UseRefLocalAsync(
                            context.Document,
                            diagnostic,
                            cancellationToken));
                    break;

                case DiagnosticIds.CacheCameraMain:
                    CodeFixRegistration.Register(
                        context,
                        diagnostic,
                        cancellationToken => LegacyCoreCodeFixes.CacheCameraMainAsync(
                            context.Document,
                            diagnostic,
                            cancellationToken));
                    break;

                case DiagnosticIds.PreallocateList:
                    CodeFixRegistration.Register(
                        context,
                        diagnostic,
                        cancellationToken => LegacyCoreCodeFixes.PreallocateListAsync(
                            context.Document,
                            diagnostic,
                            cancellationToken));
                    break;

                case DiagnosticIds.UseMultiplicationForSquare:
                    CodeFixRegistration.Register(
                        context,
                        diagnostic,
                        cancellationToken => LegacyCoreCodeFixes.UseMultiplicationForSquareAsync(
                            context.Document,
                            diagnostic,
                            cancellationToken));
                    break;

                case DiagnosticIds.UseUninitializedNativeArray:
                    CodeFixRegistration.Register(
                        context,
                        diagnostic,
                        cancellationToken => LegacyCoreCodeFixes.UseUninitializedNativeArrayAsync(
                            context.Document,
                            diagnostic,
                            cancellationToken));
                    break;
            }
        }
    }

    private static async Task<Document> ApplyExpressionQuickFixAsync(
        Document document,
        Diagnostic diagnostic,
        ExpressionQuickFixRule rule,
        System.Threading.CancellationToken cancellationToken)
    {
        var (root, semanticModel) = await CodeFixDocument.TryLoadAsync(document, cancellationToken)
            .ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var expression = CodeFixDocument.FindExpression(root, diagnostic);
        while (expression is not null)
        {
            if (expression.Kind() == rule.SyntaxKind &&
                rule.TryGetReplacement(
                    expression,
                    semanticModel,
                    cancellationToken,
                    out var replacement))
            {
                replacement = replacement
                    .WithTriviaFrom(expression)
                    .WithAdditionalAnnotations(Formatter.Annotation);
                return document.WithSyntaxRoot(root.ReplaceNode(expression, replacement));
            }

            expression = expression.Parent?.FirstAncestorOrSelf<ExpressionSyntax>();
        }

        return document;
    }
}
