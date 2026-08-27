using UnityBestPractices.Analyzers;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace UnityBestPractices.Analyzers.Infrastructure;

/// <summary>
/// Shared registration policy for diagnostic quick fixes: catalog title, localized
/// display string, and equivalence key equal to the diagnostic ID.
/// </summary>
internal static class CodeFixRegistration
{
    internal static void Register(
        CodeFixContext context,
        Diagnostic diagnostic,
        CodeFixHandler handler)
    {
        if (!DiagnosticCatalog.TryGet(diagnostic.Id, out var metadata) ||
            !metadata.HasCodeFix ||
            handler.DiagnosticId != diagnostic.Id)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                FixTitleLocalizer.Get(diagnostic.Id, handler.FixTitle),
                cancellationToken => handler.ApplyAsync(context.Document, diagnostic, cancellationToken),
                equivalenceKey: handler.EquivalenceKey),
            diagnostic);
    }
}
