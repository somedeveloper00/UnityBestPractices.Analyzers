using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace UnityBestPractices.Analyzers;

internal sealed class CodeFixHandler
{
    internal CodeFixHandler(
        RuleMetadata metadata,
        Func<Document, Diagnostic, CancellationToken, Task<Document>> applyAsync,
        Func<Document, Diagnostic, CancellationToken, Task<bool>>? isApplicableAsync = null)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        if (!metadata.HasCodeFix)
        {
            throw new ArgumentException(
                $"Diagnostic '{metadata.DiagnosticId}' is not declared to have a code fix.",
                nameof(metadata));
        }

        ApplyAsync = applyAsync ?? throw new ArgumentNullException(nameof(applyAsync));
        IsApplicableAsync = isApplicableAsync;
    }

    internal RuleMetadata Metadata { get; }

    internal string DiagnosticId => Metadata.DiagnosticId;

    internal Func<Document, Diagnostic, CancellationToken, Task<bool>>? IsApplicableAsync { get; }

    internal Func<Document, Diagnostic, CancellationToken, Task<Document>> ApplyAsync { get; }

    internal bool SupportsFixAll => Metadata.SupportsFixAll;

    internal string FixTitle => Metadata.FixTitle;

    internal string EquivalenceKey => DiagnosticId;

    internal Task<bool> CanApplyAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken) =>
        IsApplicableAsync is null
            ? Task.FromResult(true)
            : IsApplicableAsync(document, diagnostic, cancellationToken);
}
