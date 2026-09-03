using UnityBestPractices.Analyzers.Infrastructure;
using System;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace UnityBestPractices.Analyzers;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(MoveStatementCodeRefactoringProvider)), Shared]
public sealed class MoveStatementCodeRefactoringProvider : CodeRefactoringProvider
{
    public const string MoveUpTitle = "Move statement up";
    public const string MoveDownTitle = "Move statement down";
    public const string MoveLeftTitle = "Move statement left";
    public const string MoveRightTitle = "Move statement right";

    // NavigationAnnotation is internal in the Roslyn 3.8 API used by Unity, so use
    // the annotation kind understood by the workspace host directly.
    internal const string NavigationAnnotationKind = "CodeAction_Navigation";

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

        var horizontal = node is ExpressionSyntax or ArgumentSyntax;
        if (index > 0)
        {
            Register(context, node, index - 1, horizontal ? MoveLeftTitle : MoveUpTitle);
        }

        if (index < count - 1)
        {
            Register(context, node, index + 1, horizontal ? MoveRightTitle : MoveDownTitle);
        }
    }

    private static void Register(
        CodeRefactoringContext context,
        SyntaxNode node,
        int destinationIndex,
        string title)
    {
        var movesBackward = title == MoveUpTitle || title == MoveLeftTitle;
        var resource = title == MoveUpTitle ? FixTitleLocalizer.MoveStatementUp
            : title == MoveDownTitle ? FixTitleLocalizer.MoveStatementDown
            : title == MoveLeftTitle ? FixTitleLocalizer.MoveStatementLeft
            : FixTitleLocalizer.MoveStatementRight;
        var localizedTitle = FixTitleLocalizer.Get(resource, title);

        // Legacy OmniSharp maps these title prefixes to VS Code's
        // refactor.inline and refactor.extract CodeActionKind values. Giving
        // each direction a distinct kind lets keybindings invoke it directly.
        var routedTitle = movesBackward
            ? OmniSharpRefactoringTitle.Inline(localizedTitle, title)
            : OmniSharpRefactoringTitle.Extract(localizedTitle, title);

        context.RegisterRefactoring(CodeAction.Create(
            routedTitle,
            cancellationToken => MoveAsync(context.Document, node.Span, destinationIndex, cancellationToken),
            title));
    }

    private static SyntaxNode? FindMovableNode(SyntaxNode root, TextSpan span)
    {
        var position = Math.Min(span.Start, root.FullSpan.End);
        var token = root.FindToken(position);

        if (token.Parent is ElseClauseSyntax elseClause &&
            token == elseClause.ElseKeyword &&
            elseClause.Statement is IfStatementSyntax elseIfBranch &&
            TryGetPosition(elseIfBranch, out _, out _))
        {
            return elseIfBranch;
        }

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

        if (node is IfStatementSyntax ifStatement &&
            TryGetIfBranchPosition(ifStatement, out index, out count))
        {
            return true;
        }

        if (node is ExpressionSyntax expression &&
            TryGetBinaryOperandPosition(expression, out index, out count))
        {
            return true;
        }

        if (node is ArgumentSyntax argument &&
            argument.Parent is BaseArgumentListSyntax argumentList)
        {
            index = argumentList.Arguments.IndexOf(argument);
            count = argumentList.Arguments.Count;
            return index >= 0;
        }

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

    private static bool TryGetBinaryOperandPosition(ExpressionSyntax expression, out int index, out int count)
    {
        index = -1;
        count = 0;

        // A binary chain is represented by Roslyn as a nested tree rather than a
        // list. Flatten only nodes with the same SyntaxKind, so precedence and
        // explicit parentheses continue to define the chain boundaries.
        var binary = expression.Parent as BinaryExpressionSyntax;
        if (binary is null)
        {
            return false;
        }

        var kind = binary.Kind();
        var root = binary;
        while (root.Parent is BinaryExpressionSyntax parent && parent.IsKind(kind))
        {
            root = parent;
        }

        var operands = GetBinaryOperands(root, kind).ToArray();
        index = Array.IndexOf(operands, expression);
        count = operands.Length;
        return index >= 0 && count > 1;
    }

    private static System.Collections.Generic.IEnumerable<ExpressionSyntax> GetBinaryOperands(
        ExpressionSyntax expression,
        SyntaxKind kind)
    {
        if (expression is BinaryExpressionSyntax binary && binary.IsKind(kind))
        {
            foreach (var operand in GetBinaryOperands(binary.Left, kind))
            {
                yield return operand;
            }

            foreach (var operand in GetBinaryOperands(binary.Right, kind))
            {
                yield return operand;
            }

            yield break;
        }

        yield return expression;
    }

    private static bool TryGetIfBranchPosition(
        IfStatementSyntax statement,
        out int index,
        out int count)
    {
        var first = GetFirstIfBranch(statement);
        var branches = GetIfBranches(first);
        index = Array.IndexOf(branches, statement);
        count = branches.Length;
        return index >= 0 && count > 1;
    }

    private static IfStatementSyntax GetFirstIfBranch(IfStatementSyntax statement)
    {
        var first = statement;
        while (first.Parent is ElseClauseSyntax elseClause &&
               elseClause.Parent is IfStatementSyntax precedingStatement)
        {
            first = precedingStatement;
        }

        return first;
    }

    private static IfStatementSyntax[] GetIfBranches(IfStatementSyntax first)
    {
        var count = 1;
        var current = first;
        while (current.Else?.Statement is IfStatementSyntax next)
        {
            count++;
            current = next;
        }

        var branches = new IfStatementSyntax[count];
        current = first;
        for (var index = 0; index < count; index++)
        {
            branches[index] = current;
            if (current.Else?.Statement is IfStatementSyntax next)
            {
                current = next;
            }
        }

        return branches;
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

        if (node is ExpressionSyntax expression &&
            TryGetBinaryOperandPosition(expression, out _, out _))
        {
            var binary = (BinaryExpressionSyntax)expression.Parent!;
            var kind = binary.Kind();
            var binaryRoot = binary;
            while (binaryRoot.Parent is BinaryExpressionSyntax parent && parent.IsKind(kind))
            {
                binaryRoot = parent;
            }

            var operands = GetBinaryOperands(binaryRoot, kind).ToArray();
            var destinationOperand = operands[destinationIndex];
            // Binary operator whitespace is stored on the adjacent operands.
            // Retain each slot's exterior trivia while exchanging its syntax;
            // otherwise a right move can produce text such as `C&& B `.
            var replacementAtSource = destinationOperand.WithTriviaFrom(expression);
            var movedExpression = expression
                .WithTriviaFrom(destinationOperand)
                .WithAdditionalAnnotations(new SyntaxAnnotation(NavigationAnnotationKind));
            return document.WithSyntaxRoot(root.ReplaceNodes(
                new[] { expression, destinationOperand },
                (original, _) => original == expression ? replacementAtSource : movedExpression));
        }

        if (node is IfStatementSyntax ifBranch &&
            TryGetIfBranchPosition(ifBranch, out _, out _))
        {
            var navigationAnnotation = new SyntaxAnnotation(NavigationAnnotationKind);
            var first = GetFirstIfBranch(ifBranch);
            var branchDestination = GetIfBranches(first)[destinationIndex];
            var branchChangedRoot = root.ReplaceNodes(
                new SyntaxNode[]
                {
                    ifBranch.Condition,
                    ifBranch.Statement,
                    branchDestination.Condition,
                    branchDestination.Statement,
                },
                (original, _) =>
                {
                    if (original == ifBranch.Condition)
                    {
                        return branchDestination.Condition;
                    }

                    if (original == ifBranch.Statement)
                    {
                        return branchDestination.Statement;
                    }

                    if (original == branchDestination.Condition)
                    {
                        return ifBranch.Condition;
                    }

                    return ifBranch.Statement.WithAdditionalAnnotations(navigationAnnotation);
                });
            return document.WithSyntaxRoot(branchChangedRoot);
        }

        if (node is ArgumentSyntax argument &&
            argument.Parent is BaseArgumentListSyntax argumentList)
        {
            var destinationArgument = argumentList.Arguments[destinationIndex];
            // Whitespace and comments around commas belong to the argument
            // slots. Exchange the syntax while retaining each slot's trivia.
            var replacementAtSource = destinationArgument.WithTriviaFrom(argument);
            var movedArgument = argument
                .WithTriviaFrom(destinationArgument)
                .WithAdditionalAnnotations(new SyntaxAnnotation(NavigationAnnotationKind));
            return document.WithSyntaxRoot(root.ReplaceNodes(
                new[] { argument, destinationArgument },
                (original, _) => original == argument ? replacementAtSource : movedArgument));
        }

        var siblings = node.Parent.ChildNodes().Where(candidate => TryGetPosition(candidate, out _, out _)).ToArray();
        if (sourceIndex >= siblings.Length || destinationIndex >= siblings.Length)
        {
            return document;
        }

        var destination = siblings[destinationIndex];
        var movedNode = node.WithAdditionalAnnotations(new SyntaxAnnotation(NavigationAnnotationKind));
        var changedRoot = root.ReplaceNodes(
            new[] { node, destination },
            (original, _) => original == node ? destination : movedNode);
        return document.WithSyntaxRoot(changedRoot);
    }
}
