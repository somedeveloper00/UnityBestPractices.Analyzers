using System;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace UnityBestPractices.Analyzers;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(MoveStatementCodeRefactoringProvider)), Shared]
public sealed class MoveStatementCodeRefactoringProvider : CodeRefactoringProvider
{
    public const string MoveUpTitle = "Move statement up";
    public const string MoveDownTitle = "Move statement down";

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var node = FindMovableNode(root, context.Span);
        if (node is null || !TryGetPosition(node, out var index, out var count))
        {
            return;
        }

        if (index > 0)
        {
            Register(context, node, index - 1, MoveUpTitle);
        }

        if (index < count - 1)
        {
            Register(context, node, index + 1, MoveDownTitle);
        }
    }

    private static void Register(
        CodeRefactoringContext context,
        SyntaxNode node,
        int destinationIndex,
        string title) =>
        context.RegisterRefactoring(CodeAction.Create(
            title,
            cancellationToken => MoveAsync(context.Document, node.Span, destinationIndex, cancellationToken),
            title));

    private static SyntaxNode? FindMovableNode(SyntaxNode root, TextSpan span)
    {
        var position = Math.Min(span.Start, root.FullSpan.End);
        var token = root.FindToken(position);

        return token.Parent?
            .AncestorsAndSelf()
            .FirstOrDefault(node =>
                (span.IsEmpty ? node.FullSpan.Contains(position) : node.FullSpan.IntersectsWith(span)) &&
                TryGetPosition(node, out _, out _));
    }

    private static bool TryGetPosition(SyntaxNode node, out int index, out int count)
    {
        index = -1;
        count = 0;

        switch (node)
        {
            case StatementSyntax statement when statement.Parent is BlockSyntax block:
                index = block.Statements.IndexOf(statement);
                count = block.Statements.Count;
                break;
            case StatementSyntax statement when statement.Parent is SwitchSectionSyntax section:
                index = section.Statements.IndexOf(statement);
                count = section.Statements.Count;
                break;
            case MemberDeclarationSyntax member when member.Parent is CompilationUnitSyntax compilation:
                index = compilation.Members.IndexOf(member);
                count = compilation.Members.Count;
                break;
            case MemberDeclarationSyntax member when member.Parent is NamespaceDeclarationSyntax namespaceDeclaration:
                index = namespaceDeclaration.Members.IndexOf(member);
                count = namespaceDeclaration.Members.Count;
                break;
            case MemberDeclarationSyntax member when member.Parent is TypeDeclarationSyntax type:
                index = type.Members.IndexOf(member);
                count = type.Members.Count;
                break;
            case AccessorDeclarationSyntax accessor when accessor.Parent is AccessorListSyntax accessorList:
                index = accessorList.Accessors.IndexOf(accessor);
                count = accessorList.Accessors.Count;
                break;
            case SwitchSectionSyntax switchSection when switchSection.Parent is SwitchStatementSyntax switchStatement:
                index = switchStatement.Sections.IndexOf(switchSection);
                count = switchStatement.Sections.Count;
                break;
            case EnumMemberDeclarationSyntax enumMember when enumMember.Parent is EnumDeclarationSyntax enumDeclaration:
                index = enumDeclaration.Members.IndexOf(enumMember);
                count = enumDeclaration.Members.Count;
                break;
        }

        return index >= 0;
    }

    private static async Task<Document> MoveAsync(
        Document document,
        TextSpan originalSpan,
        int destinationIndex,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        SyntaxNode? node = root.FindNode(originalSpan, getInnermostNodeForTie: true);
        var sourceIndex = -1;
        var count = 0;
        while (node is not null && !TryGetPosition(node, out sourceIndex, out count))
        {
            node = node.Parent;
        }

        if (node?.Parent is null || destinationIndex < 0 || destinationIndex >= count)
        {
            return document;
        }

        var siblings = node.Parent.ChildNodes().Where(candidate => TryGetPosition(candidate, out _, out _)).ToArray();
        if (sourceIndex >= siblings.Length || destinationIndex >= siblings.Length)
        {
            return document;
        }

        var destination = siblings[destinationIndex];
        var changedRoot = root.ReplaceNodes(
            new[] { node, destination },
            (original, _) => original == node ? destination : node);
        return document.WithSyntaxRoot(changedRoot);
    }
}
