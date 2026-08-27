using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UnityBestPractices.Analyzers;

public sealed partial class UnityBestPracticesAnalyzer
{
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(startContext =>
        {
            _ = UnitySymbolCache.For(startContext.Compilation);
            RegisterCoreActions(startContext);
            RegisterCorrectnessActions(startContext);
            RegisterExpressionActions(startContext);
            RegisterDotsActions(startContext);
        });
    }

    private static void RegisterCoreActions(CompilationStartAnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeYieldStatement, SyntaxKind.YieldReturnStatement);
        context.RegisterSyntaxNodeAction(AnalyzeStructDeclaration, SyntaxKind.StructDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeVariableDeclarator, SyntaxKind.VariableDeclarator);
        context.RegisterSyntaxNodeAction(AnalyzeLocalDeclaration, SyntaxKind.LocalDeclarationStatement);
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(
            AnalyzeRelationalExpression,
            SyntaxKind.LessThanExpression,
            SyntaxKind.LessThanOrEqualExpression,
            SyntaxKind.GreaterThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression);
        context.RegisterCompilationEndAction(NamespaceConsistencyRules.AnalyzeCompilation);
    }

    private static void RegisterCorrectnessActions(CompilationStartAnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(AdvancedUnityRules.AnalyzeReturn, SyntaxKind.ReturnStatement);
        context.RegisterSyntaxNodeAction(
            AdvancedUnityRules.AnalyzeAssignment,
            SyntaxKind.SimpleAssignmentExpression);
        context.RegisterSyntaxNodeAction(
            AdvancedUnityRules.AnalyzeExpressionStatement,
            SyntaxKind.ExpressionStatement);
        context.RegisterSyntaxNodeAction(
            AdvancedUnityRules.AnalyzeTypeDeclaration,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration);
    }

    private static void RegisterExpressionActions(CompilationStartAnalysisContext context) =>
        context.RegisterSyntaxNodeAction(
            AnalyzeExpressionQuickFix,
            ExpressionQuickFixRegistry.SyntaxKinds.ToArray());

    private static void RegisterDotsActions(CompilationStartAnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(
            DotsQueryRules.AnalyzeExpressionStatement,
            SyntaxKind.ExpressionStatement);
        context.RegisterSyntaxNodeAction(
            DotsQueryRules.AnalyzeSystemApiQuery,
            SyntaxKind.ForEachStatement,
            SyntaxKind.ForEachVariableStatement);
        context.RegisterSyntaxNodeAction(
            UnusedEntityAccessRule.Analyze,
            SyntaxKind.ForEachVariableStatement);
    }
}
