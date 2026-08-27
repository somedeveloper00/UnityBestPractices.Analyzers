using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UnityBestPractices.Analyzers;

internal static class DiagnosticReporter
{
    internal static void Report(
        SyntaxNodeAnalysisContext context,
        DiagnosticDescriptor descriptor,
        Location location,
        params object[] messageArguments)
    {
        if (AnalyzerConfiguration.For(context).IsRuleEnabled(descriptor.Id))
        {
            context.ReportDiagnostic(Diagnostic.Create(descriptor, location, messageArguments));
        }
    }
}
