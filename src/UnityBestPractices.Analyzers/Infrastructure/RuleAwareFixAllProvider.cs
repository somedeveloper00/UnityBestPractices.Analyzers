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
        DiagnosticCatalog.All
            .Where(metadata => metadata.SupportsFixAll)
            .Select(metadata => metadata.DiagnosticId);

    public override Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
    {
        fixAllContext.CancellationToken.ThrowIfCancellationRequested();
        if (fixAllContext.DiagnosticIds.Any(
                diagnosticId =>
                    !DiagnosticCatalog.TryGet(diagnosticId, out var metadata) ||
                    !metadata.SupportsFixAll))
        {
            return Task.FromResult<CodeAction?>(null);
        }

        return WellKnownFixAllProviders.BatchFixer.GetFixAsync(fixAllContext);
    }
}
