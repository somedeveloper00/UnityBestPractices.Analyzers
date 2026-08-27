using UnityBestPractices.Analyzers.Infrastructure;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace UnityBestPractices.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnityBestPracticesCodeFixProvider)), Shared]
public sealed class UnityBestPracticesCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        CodeFixRegistry.All.Select(handler => handler.DiagnosticId).ToImmutableArray();

    public override FixAllProvider GetFixAllProvider() => RuleAwareFixAllProvider.Instance;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        foreach (var diagnostic in context.Diagnostics)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (CodeFixRegistry.TryGet(diagnostic.Id, out var handler) &&
                await handler.CanApplyAsync(
                    context.Document,
                    diagnostic,
                    context.CancellationToken).ConfigureAwait(false))
            {
                CodeFixRegistration.Register(context, diagnostic, handler);
            }
        }
    }
}
