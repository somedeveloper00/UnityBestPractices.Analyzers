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
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;

namespace UnityBestPractices.Analyzers;

/// <summary>Promotes a method-local variable to a field while preserving its initialization point.</summary>
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(ConvertLocalToFieldCodeRefactoringProvider)), Shared]
public sealed class ConvertLocalToFieldCodeRefactoringProvider : CodeRefactoringProvider
{
    public const string Title = "Convert local variable to field";

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var declarator = FindDeclarator(root, context.Span);
        if (root is null || model is null || declarator is null ||
            !TryGetConversion(declarator, model, context.CancellationToken, out var declaration, out var type, out var containingType, out var makeStatic))
        {
            return;
        }

        context.RegisterRefactoring(CodeAction.Create(
            FixTitleLocalizer.Get(FixTitleLocalizer.ConvertLocalToField, Title),
            cancellationToken => ConvertAsync(context.Document, root, declaration, declarator, type, containingType, makeStatic),
            Title));
    }

    internal static VariableDeclaratorSyntax? FindDeclarator(SyntaxNode? root, TextSpan span)
    {
        if (root is null || root.FullSpan.IsEmpty || span.Start >= root.FullSpan.End)
        {
            return null;
        }

        var position = Math.Min(span.Start, root.FullSpan.End - 1);
        return root.FindToken(position).Parent?.AncestorsAndSelf()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault(candidate => span.IsEmpty
                ? candidate.Identifier.FullSpan.Contains(position) || position == candidate.Identifier.Span.End
                : candidate.Identifier.FullSpan.IntersectsWith(span));
    }

    private static bool TryGetConversion(
        VariableDeclaratorSyntax declarator,
        SemanticModel model,
        CancellationToken cancellationToken,
        out LocalDeclarationStatementSyntax declaration,
        out TypeSyntax type,
        out TypeDeclarationSyntax containingType,
        out bool makeStatic)
    {
        declaration = null!;
        type = null!;
        containingType = null!;
        makeStatic = false;
        if (declarator.Parent is not VariableDeclarationSyntax variableDeclaration ||
            variableDeclaration.Parent is not LocalDeclarationStatementSyntax localDeclaration ||
            variableDeclaration.Variables.Count != 1 ||
            localDeclaration.IsConst ||
            localDeclaration.UsingKeyword != default ||
            declarator.ArgumentList is not null ||
            model.GetDeclaredSymbol(declarator, cancellationToken) is not ILocalSymbol local ||
            local.RefKind != RefKind.None ||
            local.Type.TypeKind == TypeKind.Error ||
            local.Type.IsAnonymousType)
        {
            return false;
        }

        var owner = declarator.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (owner is null || owner is InterfaceDeclarationSyntax ||
            owner.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
        {
            return false;
        }

        var enclosingMember = localDeclaration.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault();
        if (enclosingMember is null || enclosingMember == owner ||
            GetModifiers(enclosingMember).Any(SyntaxKind.ReadOnlyKeyword))
        {
            return false;
        }

        var ownerSymbol = model.GetDeclaredSymbol(owner, cancellationToken);
        if (ownerSymbol is null || ownerSymbol.GetMembers(local.Name).Length != 0)
        {
            return false;
        }

        makeStatic = owner.Modifiers.Any(SyntaxKind.StaticKeyword) || IsInStaticContext(localDeclaration, owner, model, cancellationToken);
        if (local.Type.IsRefLikeType && (makeStatic || !ownerSymbol.IsRefLikeType))
        {
            return false;
        }

        declaration = localDeclaration;
        containingType = owner;
        type = SyntaxFactory.ParseTypeName(local.Type.ToMinimalDisplayString(model, variableDeclaration.Type.SpanStart))
            .WithAdditionalAnnotations(Simplifier.Annotation);
        return true;
    }

    private static SyntaxTokenList GetModifiers(MemberDeclarationSyntax member) => member switch
    {
        BaseMethodDeclarationSyntax method => method.Modifiers,
        BasePropertyDeclarationSyntax property => property.Modifiers,
        FieldDeclarationSyntax field => field.Modifiers,
        EventFieldDeclarationSyntax eventField => eventField.Modifiers,
        _ => default,
    };

    private static bool IsInStaticContext(SyntaxNode node, TypeDeclarationSyntax owner, SemanticModel model, CancellationToken cancellationToken)
    {
        for (var symbol = model.GetEnclosingSymbol(node.SpanStart, cancellationToken); symbol is not null; symbol = symbol.ContainingSymbol)
        {
            if (SymbolEqualityComparer.Default.Equals(symbol, model.GetDeclaredSymbol(owner, cancellationToken)))
            {
                break;
            }

            if (symbol.IsStatic)
            {
                return true;
            }
        }

        return false;
    }

    private static Task<Document> ConvertAsync(
        Document document,
        SyntaxNode root,
        LocalDeclarationStatementSyntax declaration,
        VariableDeclaratorSyntax declarator,
        TypeSyntax type,
        TypeDeclarationSyntax containingType,
        bool makeStatic)
    {
        var modifierTokens = new System.Collections.Generic.List<SyntaxToken>
        {
            SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
        };
        if (makeStatic)
        {
            modifierTokens.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
        }

        if (type is PointerTypeSyntax or FunctionPointerTypeSyntax)
        {
            modifierTokens.Add(SyntaxFactory.Token(SyntaxKind.UnsafeKeyword));
        }

        var modifiers = SyntaxFactory.TokenList(modifierTokens);
        var field = SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(type, SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.VariableDeclarator(declarator.Identifier.WithoutTrivia()))))
            .WithModifiers(modifiers)
            .WithAdditionalAnnotations(Formatter.Annotation);

        StatementSyntax? replacement = null;
        if (declarator.Initializer is not null)
        {
            replacement = SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName(declarator.Identifier.WithoutTrivia()),
                        declarator.Initializer.Value.WithoutLeadingTrivia()))
                .WithTriviaFrom(declaration)
                .WithAdditionalAnnotations(Formatter.Annotation);
        }

        var trackedRoot = root.TrackNodes(containingType, declaration);
        var trackedType = trackedRoot.GetCurrentNode(containingType)!;
        var trackedDeclaration = trackedRoot.GetCurrentNode(declaration)!;
        var member = trackedDeclaration.Ancestors().OfType<MemberDeclarationSyntax>().First(candidate => candidate.Parent == trackedType);
        var memberIndex = trackedType.Members.IndexOf(member);
        var newType = trackedType.WithMembers(trackedType.Members.Insert(memberIndex, field));
        trackedRoot = trackedRoot.ReplaceNode(trackedType, newType);
        trackedDeclaration = trackedRoot.GetCurrentNode(declaration)!;
        var changedRoot = replacement is null
            ? trackedRoot.RemoveNode(trackedDeclaration, SyntaxRemoveOptions.KeepExteriorTrivia)!
            : trackedRoot.ReplaceNode(trackedDeclaration, replacement);
        return Task.FromResult(document.WithSyntaxRoot(changedRoot));
    }
}
