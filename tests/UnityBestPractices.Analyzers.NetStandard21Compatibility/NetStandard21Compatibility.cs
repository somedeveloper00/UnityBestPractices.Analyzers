using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using UnityBestPractices.Analyzers;

namespace UnityBestPractices.Analyzers.NetStandard21Compatibility;

public static class NetStandard21Compatibility
{
    public static void VerifyPublicSurface()
    {
        DiagnosticAnalyzer analyzer = new UnityBestPracticesAnalyzer();
        CodeFixProvider codeFixProvider = new UnityBestPracticesCodeFixProvider();

        _ = analyzer.SupportedDiagnostics.Length;
        _ = codeFixProvider.FixableDiagnosticIds.Length;
    }
}
