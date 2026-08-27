using UnityBestPractices.Analyzers.Infrastructure;
using UnityBestPractices.Analyzers.Rules.Core;
using UnityBestPractices.Analyzers.Rules.Correctness;
using UnityBestPractices.Analyzers.Rules.Expressions;
using UnityBestPractices.Analyzers.Rules.Dots;
using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UnityBestPractices.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnityBestPracticesAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableArray<IAnalyzerRuleModule> RuleModules =
        ImmutableArray.Create<IAnalyzerRuleModule>(
            new SerializedFieldEncapsulationModule(),
            new BoxedCoroutineYieldModule(),
            new SquaredMagnitudeComparisonModule(),
            new BurstAttributeDetectionModule(),
            new NativeArrayReadOnlyModule(),
            new StackAllocationModule(),
            new RefLocalCopyBackModule(),
            new CameraMainCachingModule(),
            new ListPreallocationModule(),
            new MultiplicationForSquareModule(),
            new UninitializedNativeArrayModule(),
            new ExistingRuleFamiliesModule());

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        DiagnosticCatalog.All.Select(metadata => metadata.Descriptor).ToImmutableArray();

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(startContext =>
        {
            _ = UnitySymbolCache.For(startContext.Compilation);
            foreach (var module in RuleModules)
            {
                module.Register(startContext);
            }
        });
    }
}
