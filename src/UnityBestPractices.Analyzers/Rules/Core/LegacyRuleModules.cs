using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UnityBestPractices.Analyzers;

internal abstract class AnalyzerRuleModule : IAnalyzerRuleModule
{
    protected static DiagnosticDescriptor Descriptor(string id) => DiagnosticCatalog.Get(id).Descriptor;
    public abstract void Register(CompilationStartAnalysisContext context);
}

internal sealed class SerializedFieldEncapsulationModule : AnalyzerRuleModule
{
    private static readonly DiagnosticDescriptor Rule = Descriptor(DiagnosticIds.EncapsulateSerializedField);
    public override void Register(CompilationStartAnalysisContext context) => context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = (FieldDeclarationSyntax)context.Node;
        if (LegacyRuleMatchers.IsEncapsulatableSerializedField(node, context.SemanticModel, context.CancellationToken))
        { var variable = node.Declaration.Variables[0]; DiagnosticReporter.Report(context, Rule, variable.Identifier.GetLocation(), variable.Identifier.ValueText); }
    }
}

internal sealed class BoxedCoroutineYieldModule : AnalyzerRuleModule
{
    private static readonly DiagnosticDescriptor Rule = Descriptor(DiagnosticIds.YieldNull);
    public override void Register(CompilationStartAnalysisContext context) => context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.YieldReturnStatement);
    private static void Analyze(SyntaxNodeAnalysisContext context)
    { var node = (YieldStatementSyntax)context.Node; if (LegacyRuleMatchers.IsBoxedNextFrameYield(node, context.SemanticModel, context.CancellationToken)) DiagnosticReporter.Report(context, Rule, node.Expression!.GetLocation()); }
}

internal sealed class SquaredMagnitudeComparisonModule : AnalyzerRuleModule
{
    private static readonly DiagnosticDescriptor Rule = Descriptor(DiagnosticIds.UseSquaredMagnitude);
    public override void Register(CompilationStartAnalysisContext context) => context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LessThanExpression, SyntaxKind.LessThanOrEqualExpression, SyntaxKind.GreaterThanExpression, SyntaxKind.GreaterThanOrEqualExpression);
    private static void Analyze(SyntaxNodeAnalysisContext context)
    { var node = (BinaryExpressionSyntax)context.Node; if (LegacyRuleMatchers.TryGetMagnitudeComparison(node, context.SemanticModel, context.CancellationToken, out _, out _)) DiagnosticReporter.Report(context, Rule, node.GetLocation()); }
}

internal sealed class BurstAttributeDetectionModule : AnalyzerRuleModule
{
    private static readonly DiagnosticDescriptor Rule = Descriptor(DiagnosticIds.AddBurstCompile);
    public override void Register(CompilationStartAnalysisContext context) => context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.StructDeclaration);
    private static void Analyze(SyntaxNodeAnalysisContext context)
    { var node = (StructDeclarationSyntax)context.Node; if (LegacyRuleMatchers.CanAddBurstCompile(node, context.SemanticModel, context.CancellationToken)) DiagnosticReporter.Report(context, Rule, node.Identifier.GetLocation(), node.Identifier.ValueText); }
}

internal sealed class NativeArrayReadOnlyModule : AnalyzerRuleModule
{
    private static readonly DiagnosticDescriptor Rule = Descriptor(DiagnosticIds.MarkNativeArrayReadOnly);
    public override void Register(CompilationStartAnalysisContext context) => context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
    private static void Analyze(SyntaxNodeAnalysisContext context)
    { var node = (FieldDeclarationSyntax)context.Node; if (LegacyRuleMatchers.IsReadOnlyNativeArrayCandidate(node, context.SemanticModel, context.CancellationToken)) { var variable=node.Declaration.Variables[0]; DiagnosticReporter.Report(context, Rule, variable.Identifier.GetLocation(), variable.Identifier.ValueText); } }
}

internal sealed class StackAllocationModule : AnalyzerRuleModule
{
    private static readonly DiagnosticDescriptor Rule = Descriptor(DiagnosticIds.UseStackalloc);
    public override void Register(CompilationStartAnalysisContext context) => context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.VariableDeclarator);
    private static void Analyze(SyntaxNodeAnalysisContext context)
    { var node=(VariableDeclaratorSyntax)context.Node; if (LegacyRuleMatchers.CanUseStackalloc(node, context.SemanticModel, context.CancellationToken, out _, AnalyzerConfiguration.For(context).MaxStackallocBytes)) DiagnosticReporter.Report(context, Rule, node.Initializer!.Value.GetLocation()); }
}

internal sealed class RefLocalCopyBackModule : AnalyzerRuleModule
{
    private static readonly DiagnosticDescriptor Rule = Descriptor(DiagnosticIds.UseRefLocal);
    public override void Register(CompilationStartAnalysisContext context) => context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LocalDeclarationStatement);
    private static void Analyze(SyntaxNodeAnalysisContext context)
    { var node=(LocalDeclarationStatementSyntax)context.Node; if (LegacyRuleMatchers.TryGetCopyBackPattern(node, context.SemanticModel, context.CancellationToken, out var pattern)) DiagnosticReporter.Report(context, Rule, pattern.Variable.Identifier.GetLocation(), pattern.Variable.Identifier.ValueText); }
}

internal sealed class CameraMainCachingModule : AnalyzerRuleModule
{
    private static readonly DiagnosticDescriptor Rule = Descriptor(DiagnosticIds.CacheCameraMain);
    public override void Register(CompilationStartAnalysisContext context) => context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SimpleMemberAccessExpression);
    private static void Analyze(SyntaxNodeAnalysisContext context)
    { var node=(MemberAccessExpressionSyntax)context.Node; if (LegacyRuleMatchers.TryGetRepeatedCameraMain(node, context.SemanticModel, context.CancellationToken, out _, out var accesses, out _, out _)) DiagnosticReporter.Report(context, Rule, node.GetLocation(), accesses.Length); }
}

internal sealed class ListPreallocationModule : AnalyzerRuleModule
{
    private static readonly DiagnosticDescriptor Rule = Descriptor(DiagnosticIds.PreallocateList);
    public override void Register(CompilationStartAnalysisContext context) => context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ObjectCreationExpression);
    private static void Analyze(SyntaxNodeAnalysisContext context)
    { var node=(ObjectCreationExpressionSyntax)context.Node; if (LegacyRuleMatchers.TryGetListPreallocation(node, context.SemanticModel, context.CancellationToken, out var count, AnalyzerConfiguration.For(context).MinimumListAdds)) DiagnosticReporter.Report(context, Rule, node.GetLocation(), count); }
}

internal sealed class MultiplicationForSquareModule : AnalyzerRuleModule
{
    private static readonly DiagnosticDescriptor Rule = Descriptor(DiagnosticIds.UseMultiplicationForSquare);
    public override void Register(CompilationStartAnalysisContext context) => context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    private static void Analyze(SyntaxNodeAnalysisContext context)
    { var node=(InvocationExpressionSyntax)context.Node; if (LegacyRuleMatchers.TryGetMathfSquare(node, context.SemanticModel, context.CancellationToken, out var value)) DiagnosticReporter.Report(context, Rule, node.GetLocation(), value.ToString()); }
}

internal sealed class UninitializedNativeArrayModule : AnalyzerRuleModule
{
    private static readonly DiagnosticDescriptor Rule = Descriptor(DiagnosticIds.UseUninitializedNativeArray);
    public override void Register(CompilationStartAnalysisContext context) => context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ObjectCreationExpression);
    private static void Analyze(SyntaxNodeAnalysisContext context)
    { var node=(ObjectCreationExpressionSyntax)context.Node; if (LegacyRuleMatchers.TryGetNativeArrayInitialization(node, context.SemanticModel, context.CancellationToken, out _)) DiagnosticReporter.Report(context, Rule, node.GetLocation()); }
}

internal sealed class ExistingRuleFamiliesModule : AnalyzerRuleModule
{
    public override void Register(CompilationStartAnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(AdvancedUnityRules.AnalyzeReturn, SyntaxKind.ReturnStatement);
        context.RegisterSyntaxNodeAction(AdvancedUnityRules.AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
        context.RegisterSyntaxNodeAction(AdvancedUnityRules.AnalyzeExpressionStatement, SyntaxKind.ExpressionStatement);
        context.RegisterSyntaxNodeAction(AdvancedUnityRules.AnalyzeTypeDeclaration, SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeLocalDeclaration, SyntaxKind.LocalDeclarationStatement);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeExpressionQuickFix, ExpressionQuickFixRegistry.SyntaxKinds.ToArray());
        context.RegisterSyntaxNodeAction(DotsQueryRules.AnalyzeExpressionStatement, SyntaxKind.ExpressionStatement);
        context.RegisterSyntaxNodeAction(DotsQueryRules.AnalyzeSystemApiQuery, SyntaxKind.ForEachStatement, SyntaxKind.ForEachVariableStatement);
        context.RegisterSyntaxNodeAction(UnusedEntityAccessRule.Analyze, SyntaxKind.ForEachVariableStatement);
        context.RegisterCompilationEndAction(NamespaceConsistencyRules.AnalyzeCompilation);
    }

    private static void AnalyzeLocalDeclaration(SyntaxNodeAnalysisContext context) => AdvancedUnityRules.AnalyzeLocalDeclaration(context);

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        AdvancedUnityRules.AnalyzeInvocation(context);
        ModernObjectFindRule.AnalyzeInvocation(context, (InvocationExpressionSyntax)context.Node);
    }

    private static void AnalyzeExpressionQuickFix(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ExpressionSyntax expression) return;
        foreach (var rule in ExpressionQuickFixRegistry.ForSyntaxKind(expression.Kind()))
        {
            if (!rule.TryGetReplacement(expression, context.SemanticModel, context.CancellationToken, out _)) continue;
            DiagnosticReporter.Report(context, rule.Descriptor, expression.GetLocation());
            return;
        }
    }
}
