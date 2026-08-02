using System.Collections.Generic;
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
using Microsoft.CodeAnalysis.Text;

namespace UnityBestPractices.Analyzers;

[ExportCodeRefactoringProvider(
    LanguageNames.CSharp,
    Name = nameof(ConvertSystemBaseToISystemCodeRefactoringProvider)),
 Shared]
public sealed class ConvertSystemBaseToISystemCodeRefactoringProvider : CodeRefactoringProvider
{
    public const string Title = "Convert SystemBase to ISystem";

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var declaration = FindClass(root, context.Span);
        if (declaration is null || semanticModel is null ||
            !CanConvert(declaration, semanticModel, context.CancellationToken))
        {
            return;
        }

        context.RegisterRefactoring(CodeAction.Create(
            Title,
            cancellationToken => ConvertAsync(context.Document, declaration, cancellationToken),
            Title));
    }

    private static ClassDeclarationSyntax? FindClass(SyntaxNode? root, TextSpan span)
    {
        if (root is null || root.FullSpan.IsEmpty || span.Start >= root.FullSpan.End)
        {
            return null;
        }

        var position = System.Math.Min(span.Start, root.FullSpan.End - 1);
        return root.FindToken(position).Parent?.AncestorsAndSelf()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(candidate => span.IsEmpty || candidate.Span.IntersectsWith(span));
    }

    private static bool CanConvert(
        ClassDeclarationSyntax declaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var type = semanticModel.GetDeclaredSymbol(declaration, cancellationToken);
        var systemBase = semanticModel.Compilation.GetTypeByMetadataName("Unity.Entities.SystemBase");
        if (type is null || systemBase is null ||
            !SymbolEqualityComparer.Default.Equals(type.BaseType, systemBase) ||
            type.DeclaringSyntaxReferences.Length != 1 ||
            type.GetMembers().OfType<IFieldSymbol>().Any(field =>
                !field.IsImplicitlyDeclared || !field.IsStatic) ||
            declaration.Modifiers.Any(SyntaxKind.AbstractKeyword) ||
            declaration.Modifiers.Any(SyntaxKind.SealedKeyword) ||
            declaration.BaseList?.Types.Count != 1 ||
            declaration.Members.Any(member =>
                member is ConstructorDeclarationSyntax || member is DestructorDeclarationSyntax))
        {
            return false;
        }

        foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>())
        {
            if (!method.Modifiers.Any(SyntaxKind.OverrideKeyword))
            {
                continue;
            }

            if (!IsLifecycleMethod(method) || method.ParameterList.Parameters.Count != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLifecycleMethod(MethodDeclarationSyntax method) =>
        method.Identifier.ValueText == "OnCreate" ||
        method.Identifier.ValueText == "OnUpdate" ||
        method.Identifier.ValueText == "OnDestroy";

    private static async Task<Document> ConvertAsync(
        Document document,
        ClassDeclarationSyntax declaration,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var members = declaration.Members.Select(member =>
            member is MethodDeclarationSyntax method &&
            method.Modifiers.Any(SyntaxKind.OverrideKeyword) &&
            IsLifecycleMethod(method)
                ? ConvertLifecycleMethod(method)
                : member);
        var baseType = declaration.BaseList!.Types[0];
        var replacement = SyntaxFactory.StructDeclaration(
                declaration.AttributeLists,
                declaration.Modifiers,
                SyntaxFactory.Token(SyntaxKind.StructKeyword).WithTriviaFrom(declaration.Keyword),
                declaration.Identifier,
                declaration.TypeParameterList,
                declaration.BaseList.WithTypes(SyntaxFactory.SingletonSeparatedList(
                    baseType.WithType(SyntaxFactory.IdentifierName("ISystem").WithTriviaFrom(baseType.Type)))),
                declaration.ConstraintClauses,
                declaration.OpenBraceToken,
                SyntaxFactory.List(members),
                declaration.CloseBraceToken,
                declaration.SemicolonToken)
            .WithTriviaFrom(declaration)
            .WithAdditionalAnnotations(Formatter.Annotation);

        return document.WithSyntaxRoot(root.ReplaceNode(declaration, replacement));
    }

    private static MethodDeclarationSyntax ConvertLifecycleMethod(MethodDeclarationSyntax method)
    {
        var modifiers = new List<SyntaxToken>(method.Modifiers.Count);
        foreach (var modifier in method.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.OverrideKeyword))
            {
                continue;
            }

            if (!modifier.IsKind(SyntaxKind.ProtectedKeyword))
            {
                modifiers.Add(modifier);
            }
        }

        modifiers.Insert(0, SyntaxFactory.Token(SyntaxKind.PublicKeyword));
        var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("state"))
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.RefKeyword)))
            .WithType(SyntaxFactory.IdentifierName("SystemState"));
        return method
            .WithModifiers(SyntaxFactory.TokenList(modifiers))
            .WithParameterList(method.ParameterList.AddParameters(parameter));
    }
}
