using UnityBestPractices.Analyzers;
using UnityBestPractices.Analyzers.Infrastructure;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityBestPractices.Analyzers.Rules.Dots;

internal sealed class ParallelEcbConversion
{
    internal ParallelEcbConversion(
        LocalDeclarationStatementSyntax declaration,
        string oldName,
        string newName,
        ExpressionSyntax initializer)
    {
        Declaration = declaration;
        OldName = oldName;
        NewName = newName;
        Initializer = initializer;
    }

    internal LocalDeclarationStatementSyntax Declaration { get; }
    internal string OldName { get; }
    internal string NewName { get; }
    internal ExpressionSyntax Initializer { get; }
}

internal sealed class ParallelEcbBodyRewriter : CSharpSyntaxRewriter
{
    private readonly string _oldName;
    private readonly string _newName;
    private readonly string _sortKeyName;

    internal ParallelEcbBodyRewriter(string oldName, string newName, string sortKeyName)
    {
        _oldName = oldName;
        _newName = newName;
        _sortKeyName = sortKeyName;
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var rewritten = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
        if (node.Expression is MemberAccessExpressionSyntax access &&
            access.Expression is IdentifierNameSyntax receiver &&
            receiver.Identifier.ValueText == _oldName &&
            node.ArgumentList.Arguments.FirstOrDefault()?.Expression is IdentifierNameSyntax sortKey &&
            sortKey.Identifier.ValueText == _sortKeyName)
        {
            rewritten = rewritten.WithArgumentList(rewritten.ArgumentList.WithArguments(
                rewritten.ArgumentList.Arguments.RemoveAt(0)));
        }

        return rewritten;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) =>
        node.Identifier.ValueText == _oldName
            ? node.WithIdentifier(Microsoft.CodeAnalysis.CSharp.SyntaxFactory.Identifier(_newName).WithTriviaFrom(node.Identifier))
            : base.VisitIdentifierName(node);
}
