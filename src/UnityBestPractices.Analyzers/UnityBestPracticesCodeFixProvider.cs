using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace UnityBestPractices.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnityBestPracticesCodeFixProvider)), Shared]
public sealed class UnityBestPracticesCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
        DiagnosticIds.EncapsulateSerializedField,
        DiagnosticIds.YieldNull,
        DiagnosticIds.UseSquaredMagnitude,
        DiagnosticIds.AddBurstCompile,
        DiagnosticIds.MarkNativeArrayReadOnly,
        DiagnosticIds.UseStackalloc,
        DiagnosticIds.UseRefLocal,
        DiagnosticIds.CacheCameraMain,
        DiagnosticIds.PreallocateList,
        DiagnosticIds.UseMultiplicationForSquare,
        DiagnosticIds.UseUninitializedNativeArray)
        .AddRange(ExpressionQuickFixRegistry.DiagnosticIds)
        .AddRange(DotsQueryRules.FixableDiagnosticIds);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            if (DotsQueryRules.TryGetRule(diagnostic.Id, out var dotsRule))
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        dotsRule.FixTitle,
                        cancellationToken => DotsQueryCodeFixes.ApplyFixAsync(
                            context.Document,
                            diagnostic,
                            dotsRule,
                            cancellationToken),
                        diagnostic.Id),
                    diagnostic);
                continue;
            }

            if (ExpressionQuickFixRegistry.TryGetRule(diagnostic.Id, out var expressionRule))
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        expressionRule.FixTitle,
                        cancellationToken => ApplyExpressionQuickFixAsync(
                            context.Document,
                            diagnostic,
                            expressionRule,
                            cancellationToken),
                        diagnostic.Id),
                    diagnostic);
                continue;
            }

            switch (diagnostic.Id)
            {
                case DiagnosticIds.EncapsulateSerializedField:
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            "Make private and add SerializeField",
                            cancellationToken => EncapsulateFieldAsync(context.Document, diagnostic, cancellationToken),
                            nameof(DiagnosticIds.EncapsulateSerializedField)),
                        diagnostic);
                    break;

                case DiagnosticIds.YieldNull:
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            "Yield null",
                            cancellationToken => YieldNullAsync(context.Document, diagnostic, cancellationToken),
                            nameof(DiagnosticIds.YieldNull)),
                        diagnostic);
                    break;

                case DiagnosticIds.UseSquaredMagnitude:
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            "Use squared magnitude",
                            cancellationToken => UseSquaredMagnitudeAsync(context.Document, diagnostic, cancellationToken),
                            nameof(DiagnosticIds.UseSquaredMagnitude)),
                        diagnostic);
                    break;

                case DiagnosticIds.AddBurstCompile:
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            "Add BurstCompile",
                            cancellationToken => AddBurstCompileAsync(context.Document, diagnostic, cancellationToken),
                            nameof(DiagnosticIds.AddBurstCompile)),
                        diagnostic);
                    break;

                case DiagnosticIds.MarkNativeArrayReadOnly:
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            "Mark NativeArray as ReadOnly",
                            cancellationToken => MarkNativeArrayReadOnlyAsync(context.Document, diagnostic, cancellationToken),
                            nameof(DiagnosticIds.MarkNativeArrayReadOnly)),
                        diagnostic);
                    break;

                case DiagnosticIds.UseStackalloc:
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            "Use stackalloc",
                            cancellationToken => UseStackallocAsync(context.Document, diagnostic, cancellationToken),
                            nameof(DiagnosticIds.UseStackalloc)),
                        diagnostic);
                    break;

                case DiagnosticIds.UseRefLocal:
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            "Mutate through a ref local",
                            cancellationToken => UseRefLocalAsync(context.Document, diagnostic, cancellationToken),
                            nameof(DiagnosticIds.UseRefLocal)),
                        diagnostic);
                    break;

                case DiagnosticIds.CacheCameraMain:
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            "Cache Camera.main in this block",
                            cancellationToken => CacheCameraMainAsync(context.Document, diagnostic, cancellationToken),
                            nameof(DiagnosticIds.CacheCameraMain)),
                        diagnostic);
                    break;

                case DiagnosticIds.PreallocateList:
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            "Preallocate List capacity",
                            cancellationToken => PreallocateListAsync(context.Document, diagnostic, cancellationToken),
                            nameof(DiagnosticIds.PreallocateList)),
                        diagnostic);
                    break;

                case DiagnosticIds.UseMultiplicationForSquare:
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            "Multiply the value by itself",
                            cancellationToken => UseMultiplicationForSquareAsync(context.Document, diagnostic, cancellationToken),
                            nameof(DiagnosticIds.UseMultiplicationForSquare)),
                        diagnostic);
                    break;

                case DiagnosticIds.UseUninitializedNativeArray:
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            "Use uninitialized NativeArray memory",
                            cancellationToken => UseUninitializedNativeArrayAsync(context.Document, diagnostic, cancellationToken),
                            nameof(DiagnosticIds.UseUninitializedNativeArray)),
                        diagnostic);
                    break;
            }
        }

        return Task.CompletedTask;
    }

    private static async Task<Document> EncapsulateFieldAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var field = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<FieldDeclarationSyntax>();
        if (field is null ||
            !UnityBestPracticesAnalyzer.IsEncapsulatableSerializedField(
                field,
                semanticModel,
                cancellationToken))
        {
            return document;
        }

        var publicToken = field.Modifiers.First(token => token.IsKind(SyntaxKind.PublicKeyword));
        var privateToken = SyntaxFactory.Token(
            publicToken.LeadingTrivia,
            SyntaxKind.PrivateKeyword,
            publicToken.TrailingTrivia);
        var serializeFieldAttribute = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.ParseName("UnityEngine.SerializeField"))));
        var replacement = field
            .WithModifiers(field.Modifiers.Replace(publicToken, privateToken))
            .WithAttributeLists(field.AttributeLists.Add(serializeFieldAttribute))
            .WithAdditionalAnnotations(Formatter.Annotation);

        return document.WithSyntaxRoot(root.ReplaceNode(field, replacement));
    }

    private static async Task<Document> YieldNullAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var yieldStatement = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<YieldStatementSyntax>();
        if (yieldStatement is null ||
            !UnityBestPracticesAnalyzer.IsBoxedNextFrameYield(yieldStatement, semanticModel, cancellationToken))
        {
            return document;
        }

        var replacement = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
            .WithTriviaFrom(yieldStatement.Expression!);
        return document.WithSyntaxRoot(
            root.ReplaceNode(yieldStatement, yieldStatement.WithExpression(replacement)));
    }

    private static async Task<Document> UseSquaredMagnitudeAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var binary = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<BinaryExpressionSyntax>();
        if (binary is null ||
            !UnityBestPracticesAnalyzer.TryGetMagnitudeComparison(
                binary,
                semanticModel,
                cancellationToken,
                out var magnitude,
                out var threshold))
        {
            return document;
        }

        var squaredMagnitude = magnitude.WithName(SyntaxFactory.IdentifierName("sqrMagnitude"));
        var squaredThreshold = SyntaxFactory.ParenthesizedExpression(
            SyntaxFactory.BinaryExpression(
                SyntaxKind.MultiplyExpression,
                threshold.WithoutTrivia(),
                threshold.WithoutTrivia()));
        var replacement = binary
            .ReplaceNodes(
                new ExpressionSyntax[] { magnitude, threshold },
                (original, _) => original == magnitude ? squaredMagnitude : squaredThreshold)
            .WithTriviaFrom(binary)
            .WithAdditionalAnnotations(Formatter.Annotation);

        return document.WithSyntaxRoot(root.ReplaceNode(binary, replacement));
    }

    private static async Task<Document> AddBurstCompileAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var declaration = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<StructDeclarationSyntax>();
        if (declaration is null ||
            !UnityBestPracticesAnalyzer.CanAddBurstCompile(declaration, semanticModel, cancellationToken))
        {
            return document;
        }

        var replacement = declaration
            .WithAttributeLists(AddAttribute(declaration.AttributeLists, "Unity.Burst.BurstCompile"))
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(declaration, replacement));
    }

    private static async Task<Document> MarkNativeArrayReadOnlyAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var field = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<FieldDeclarationSyntax>();
        if (field is null ||
            !UnityBestPracticesAnalyzer.IsReadOnlyNativeArrayCandidate(field, semanticModel, cancellationToken))
        {
            return document;
        }

        var replacement = field
            .WithAttributeLists(AddAttribute(field.AttributeLists, "Unity.Collections.ReadOnly"))
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(field, replacement));
    }

    private static async Task<Document> UseStackallocAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var declarator = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<VariableDeclaratorSyntax>();
        if (declarator is null ||
            !UnityBestPracticesAnalyzer.CanUseStackalloc(
                declarator,
                semanticModel,
                cancellationToken,
                out var arrayCreation))
        {
            return document;
        }

        var initializerText = arrayCreation.Initializer is null
            ? string.Empty
            : " " + arrayCreation.Initializer.WithoutTrivia().ToFullString();
        var stackAllocation = SyntaxFactory.ParseExpression(
                "stackalloc " + arrayCreation.Type.WithoutTrivia().ToFullString() + initializerText)
            .WithTriviaFrom(arrayCreation)
            .WithAdditionalAnnotations(Formatter.Annotation);
        if (arrayCreation.Initializer is not null)
        {
            return document.WithSyntaxRoot(root.ReplaceNode(arrayCreation, stackAllocation));
        }

        var declaration = declarator.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();
        if (declaration?.Parent is not BlockSyntax block)
        {
            return document;
        }

        var updatedDeclaration = declaration.ReplaceNode(arrayCreation, stackAllocation);
        var clearStatement = SyntaxFactory.ParseStatement(declarator.Identifier.Text + ".Clear();")
            .WithAdditionalAnnotations(Formatter.Annotation);
        var declarationIndex = block.Statements.IndexOf(declaration);
        var updatedStatements = block.Statements
            .Replace(declaration, updatedDeclaration)
            .Insert(declarationIndex + 1, clearStatement);
        var updatedBlock = block
            .WithStatements(updatedStatements)
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(block, updatedBlock));
    }

    private static async Task<Document> UseRefLocalAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var declaration = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();
        if (declaration is null ||
            !UnityBestPracticesAnalyzer.TryGetCopyBackPattern(
                declaration,
                semanticModel,
                cancellationToken,
                out var pattern))
        {
            return document;
        }

        var refLocalText =
            "ref " + pattern.Declaration.Declaration.Type.WithoutTrivia().ToFullString() +
            " " + pattern.Variable.Identifier.Text +
            " = ref " + pattern.RefTarget.WithoutTrivia().ToFullString() + ";";
        var refLocal = ((LocalDeclarationStatementSyntax)SyntaxFactory.ParseStatement(refLocalText))
            .WithLeadingTrivia(pattern.Declaration.GetLeadingTrivia())
            .WithTrailingTrivia(pattern.Declaration.GetTrailingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);
        var statements = SyntaxFactory.List(pattern.Block.Statements
            .Where(statement => statement != pattern.WriteBack)
            .Select(statement => statement == pattern.Declaration ? refLocal : statement));
        var updatedBlock = pattern.Block
            .WithStatements(statements)
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(pattern.Block, updatedBlock));
    }

    private static async Task<Document> CacheCameraMainAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var access = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<MemberAccessExpressionSyntax>();
        if (access is null ||
            !UnityBestPracticesAnalyzer.TryGetRepeatedCameraMain(
                access,
                semanticModel,
                cancellationToken,
                out var block,
                out var accesses,
                out var insertionPoint,
                out var localName))
        {
            return document;
        }

        var updatedBlock = block.ReplaceNodes(
            accesses,
            (original, _) => SyntaxFactory.IdentifierName(localName).WithTriviaFrom(original));
        var declaration = SyntaxFactory.ParseStatement(
                "var " + localName + " = UnityEngine.Camera.main;")
            .WithAdditionalAnnotations(Formatter.Annotation);
        var insertionIndex = block.Statements.IndexOf(insertionPoint);
        updatedBlock = updatedBlock
            .WithStatements(updatedBlock.Statements.Insert(insertionIndex, declaration))
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(block, updatedBlock));
    }

    private static async Task<Document> PreallocateListAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var creation = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<ObjectCreationExpressionSyntax>();
        if (creation is null ||
            !UnityBestPracticesAnalyzer.TryGetListPreallocation(
                creation,
                semanticModel,
                cancellationToken,
                out var addCount))
        {
            return document;
        }

        var argument = SyntaxFactory.Argument(
            SyntaxFactory.LiteralExpression(
                SyntaxKind.NumericLiteralExpression,
                SyntaxFactory.Literal(addCount)));
        var replacement = creation
            .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(argument)))
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(creation, replacement));
    }

    private static async Task<Document> UseMultiplicationForSquareAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var invocation = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is null ||
            !UnityBestPracticesAnalyzer.TryGetMathfSquare(
                invocation,
                semanticModel,
                cancellationToken,
                out var value))
        {
            return document;
        }

        var replacement = SyntaxFactory.ParenthesizedExpression(
                SyntaxFactory.BinaryExpression(
                    SyntaxKind.MultiplyExpression,
                    value.WithoutTrivia(),
                    value.WithoutTrivia()))
            .WithTriviaFrom(invocation)
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement));
    }

    private static async Task<Document> UseUninitializedNativeArrayAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var creation = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<ObjectCreationExpressionSyntax>();
        if (creation is null ||
            !UnityBestPracticesAnalyzer.TryGetNativeArrayInitialization(
                creation,
                semanticModel,
                cancellationToken,
                out _))
        {
            return document;
        }

        var option = SyntaxFactory.Argument(
            SyntaxFactory.ParseExpression("Unity.Collections.NativeArrayOptions.UninitializedMemory"));
        var replacement = creation
            .WithArgumentList(creation.ArgumentList!.AddArguments(option))
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(creation, replacement));
    }

    private static async Task<Document> ApplyExpressionQuickFixAsync(
        Document document,
        Diagnostic diagnostic,
        ExpressionQuickFixRule rule,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var locatedNode = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var expression = locatedNode as ExpressionSyntax ?? locatedNode.FirstAncestorOrSelf<ExpressionSyntax>();
        while (expression is not null)
        {
            if (expression.Kind() == rule.SyntaxKind &&
                rule.TryGetReplacement(
                    expression,
                    semanticModel,
                    cancellationToken,
                    out var replacement))
            {
                replacement = replacement
                    .WithTriviaFrom(expression)
                    .WithAdditionalAnnotations(Formatter.Annotation);
                return document.WithSyntaxRoot(root.ReplaceNode(expression, replacement));
            }

            expression = expression.Parent?.FirstAncestorOrSelf<ExpressionSyntax>();
        }

        return document;
    }

    private static SyntaxList<AttributeListSyntax> AddAttribute(
        SyntaxList<AttributeListSyntax> attributeLists,
        string qualifiedName) =>
        attributeLists.Add(
            SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(SyntaxFactory.ParseName(qualifiedName)))));
}
