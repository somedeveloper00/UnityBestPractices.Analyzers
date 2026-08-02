using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;

namespace UnityBestPractices.Analyzers;

internal static class ModernObjectFindRule
{
    internal static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken)
            .Symbol as IMethodSymbol;
        var objectType = UnitySymbolCache.GetTypeByMetadataName(
            context.SemanticModel.Compilation,
            "UnityEngine.Object");
        if (method is null || objectType is null || !method.IsStatic ||
            !SymbolEqualityComparer.Default.Equals(method.ContainingType, objectType) ||
            !TryGetReplacement(method, out var replacementName, out var addsSortMode) ||
            !objectType.GetMembers(replacementName).OfType<IMethodSymbol>().Any(candidate => candidate.IsStatic) ||
            (addsSortMode && UnitySymbolCache.GetTypeByMetadataName(
                context.SemanticModel.Compilation,
                "UnityEngine.FindObjectsSortMode") is null))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticCatalog.Get(DiagnosticIds.UseModernObjectFindApi).Descriptor,
            invocation.GetLocation(),
            method.Name,
            replacementName));
    }

    internal static async Task<Document> ApplyFixAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null ||
            root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
                .FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } invocation ||
            semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method ||
            !TryGetReplacement(method, out var replacementName, out var addsSortMode))
        {
            return document;
        }

        var expression = Rename(invocation.Expression, replacementName);
        var arguments = invocation.ArgumentList.Arguments;
        if (addsSortMode)
        {
            arguments = arguments.Add(
                SyntaxFactory.Argument(
                    SyntaxFactory.ParseExpression("global::UnityEngine.FindObjectsSortMode.InstanceID")));
        }

        var replacement = invocation
            .WithExpression(expression)
            .WithArgumentList(invocation.ArgumentList.WithArguments(arguments))
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement));
    }

    private static bool TryGetReplacement(
        IMethodSymbol method,
        out string replacementName,
        out bool addsSortMode)
    {
        replacementName = string.Empty;
        addsSortMode = false;
        var hasSupportedArguments = method.IsGenericMethod
            ? method.TypeArguments.Length == 1 && method.Parameters.Length == 0
            : method.Parameters.Length == 1 &&
              method.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
              "global::System.Type";
        if (!hasSupportedArguments)
        {
            return false;
        }

        if (method.Name == "FindObjectOfType")
        {
            replacementName = "FindFirstObjectByType";
            return true;
        }

        if (method.Name == "FindObjectsOfType")
        {
            replacementName = "FindObjectsByType";
            addsSortMode = true;
            return true;
        }

        return false;
    }

    private static ExpressionSyntax Rename(ExpressionSyntax expression, string replacementName)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.WithIdentifier(
                SyntaxFactory.Identifier(identifier.Identifier.LeadingTrivia, replacementName, identifier.Identifier.TrailingTrivia)),
            GenericNameSyntax generic => generic.WithIdentifier(
                SyntaxFactory.Identifier(generic.Identifier.LeadingTrivia, replacementName, generic.Identifier.TrailingTrivia)),
            MemberAccessExpressionSyntax memberAccess => memberAccess.WithName(
                (SimpleNameSyntax)Rename(memberAccess.Name, replacementName)),
            _ => expression,
        };
    }
}
