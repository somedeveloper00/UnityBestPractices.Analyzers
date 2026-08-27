using UnityBestPractices.Analyzers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityBestPractices.Analyzers.Infrastructure;

/// <summary>Common document loading and diagnostic-span node location for code fixes.</summary>
internal static class CodeFixDocument
{
    internal static async Task<(SyntaxNode? Root, SemanticModel? SemanticModel)> TryLoadAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        return (root, semanticModel);
    }

    internal static TNode? FindAncestor<TNode>(SyntaxNode root, Diagnostic diagnostic)
        where TNode : SyntaxNode =>
        root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<TNode>();

    internal static ExpressionSyntax? FindExpression(SyntaxNode root, Diagnostic diagnostic)
    {
        var locatedNode = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        return locatedNode as ExpressionSyntax ?? locatedNode.FirstAncestorOrSelf<ExpressionSyntax>();
    }
}
