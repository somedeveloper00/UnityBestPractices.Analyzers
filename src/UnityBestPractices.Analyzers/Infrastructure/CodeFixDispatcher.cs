using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;

namespace UnityBestPractices.Analyzers;

internal static class CodeFixDispatcher
{
    internal static async Task RegisterAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
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
                    cancellationToken => UnityBestPracticesCodeFixProvider.ApplyExpressionQuickFixAsync(
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
}
