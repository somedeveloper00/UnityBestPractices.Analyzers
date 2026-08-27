using Microsoft.CodeAnalysis.Diagnostics;

namespace UnityBestPractices.Analyzers.Infrastructure;

/// <summary>Registers one cohesive analyzer rule (or rule family) for a compilation.</summary>
internal interface IAnalyzerRuleModule
{
    void Register(CompilationStartAnalysisContext context);
}
