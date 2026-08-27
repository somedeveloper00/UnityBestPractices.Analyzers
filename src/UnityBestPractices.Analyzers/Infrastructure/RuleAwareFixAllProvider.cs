using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace UnityBestPractices.Analyzers;

internal sealed class RuleAwareFixAllProvider : FixAllProvider
{
    internal static readonly RuleAwareFixAllProvider Instance = new();

    private RuleAwareFixAllProvider()
    {
    }

    public override IEnumerable<string> GetSupportedFixAllDiagnosticIds(
        CodeFixProvider originalCodeFixProvider) =>
        CodeFixRegistry.All
            .Where(handler => handler.SupportsFixAll)
            .Select(handler => handler.DiagnosticId);

    public override Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
    {
        if (fixAllContext.DiagnosticIds.Any(
                diagnosticId =>
                    !CodeFixRegistry.TryGet(diagnosticId, out var handler) ||
                    !handler.SupportsFixAll))
        {
            return Task.FromResult<CodeAction?>(null);
        }

        return WellKnownFixAllProviders.BatchFixer.GetFixAsync(fixAllContext);
    }
}
