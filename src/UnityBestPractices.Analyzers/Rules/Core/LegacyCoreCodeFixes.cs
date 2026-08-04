// Apply paths for the original UBP0001–UBP0011 fixes (co-located apply logic).
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;

namespace UnityBestPractices.Analyzers;

internal static class LegacyCoreCodeFixes
{
    internal static async Task<bool> CanSafelyEncapsulateFieldAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var (root, semanticModel) = await CodeFixDocument.TryLoadAsync(document, cancellationToken)
            .ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return false;
        }

        var variable = CodeFixDocument.FindAncestor<VariableDeclaratorSyntax>(root, diagnostic);
        var fieldDeclaration = variable?.FirstAncestorOrSelf<FieldDeclarationSyntax>();
        var fieldSymbol = variable is null
            ? null
            : semanticModel.GetDeclaredSymbol(variable, cancellationToken) as IFieldSymbol;
        if (fieldDeclaration is null ||
            fieldSymbol?.ContainingType is null ||
            !UnityBestPracticesAnalyzer.IsEncapsulatableSerializedField(
                fieldDeclaration,
                semanticModel,
                cancellationToken))
        {
            return false;
        }

        var references = await SymbolFinder.FindReferencesAsync(
            fieldSymbol,
            document.Project.Solution,
            cancellationToken).ConfigureAwait(false);
        foreach (var reference in references)
        {
            foreach (var location in reference.Locations)
            {
                var referenceDocument = document.Project.Solution.GetDocument(location.Document.Id);
                var referenceModel = referenceDocument is null
                    ? null
                    : await referenceDocument.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                if (referenceModel is null)
                {
                    return false;
                }

                var enclosingType = referenceModel
                    .GetEnclosingSymbol(location.Location.SourceSpan.Start, cancellationToken)
                    ?.ContainingType;
                if (!IsWithinDeclaringType(enclosingType, fieldSymbol.ContainingType))
                {
                    return false;
                }
            }
        }

        return true;
    }

    internal static async Task<Document> EncapsulateFieldAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var (root, semanticModel) = await CodeFixDocument.TryLoadAsync(document, cancellationToken)
            .ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var field = CodeFixDocument.FindAncestor<FieldDeclarationSyntax>(root, diagnostic);
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
        var leadingTrivia = field.GetLeadingTrivia();
        var indentation = SyntaxFactory.TriviaList(
            leadingTrivia
                .Reverse()
                .TakeWhile(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                .Reverse());
        var endOfLine = root
            .DescendantTrivia(descendIntoTrivia: true)
            .FirstOrDefault(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia));
        if (endOfLine == default)
        {
            endOfLine = SyntaxFactory.EndOfLine("\n");
        }

        serializeFieldAttribute = serializeFieldAttribute
            .WithLeadingTrivia(leadingTrivia)
            .WithTrailingTrivia(
                SyntaxFactory.TriviaList(endOfLine)
                    .AddRange(indentation));
        var replacement = field
            .WithLeadingTrivia(default(SyntaxTriviaList))
            .WithModifiers(field.Modifiers.Replace(publicToken, privateToken))
            .WithAttributeLists(field.AttributeLists.Insert(0, serializeFieldAttribute));

        return document.WithSyntaxRoot(root.ReplaceNode(field, replacement));
    }

    internal static async Task<Document> YieldNullAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var (root, semanticModel) = await CodeFixDocument.TryLoadAsync(document, cancellationToken)
            .ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var yieldStatement = CodeFixDocument.FindAncestor<YieldStatementSyntax>(root, diagnostic);
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

    internal static async Task<Document> UseSquaredMagnitudeAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var (root, semanticModel) = await CodeFixDocument.TryLoadAsync(document, cancellationToken)
            .ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var binary = CodeFixDocument.FindAncestor<BinaryExpressionSyntax>(root, diagnostic);
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

    internal static async Task<Document> AddBurstCompileAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var (root, semanticModel) = await CodeFixDocument.TryLoadAsync(document, cancellationToken)
            .ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var declaration = CodeFixDocument.FindAncestor<StructDeclarationSyntax>(root, diagnostic);
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

    internal static async Task<Document> MarkNativeArrayReadOnlyAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var (root, semanticModel) = await CodeFixDocument.TryLoadAsync(document, cancellationToken)
            .ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var field = CodeFixDocument.FindAncestor<FieldDeclarationSyntax>(root, diagnostic);
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

    internal static async Task<Document> UseStackallocAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var (root, semanticModel) = await CodeFixDocument.TryLoadAsync(document, cancellationToken)
            .ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var declarator = CodeFixDocument.FindAncestor<VariableDeclaratorSyntax>(root, diagnostic);
        var configuration = AnalyzerConfiguration.For(
            document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider,
            root.SyntaxTree);
        if (declarator is null ||
            !UnityBestPracticesAnalyzer.CanUseStackalloc(
                declarator,
                semanticModel,
                cancellationToken,
                out var arrayCreation,
                configuration.MaxStackallocBytes))
        {
            return document;
        }

        var stackAllocation = SyntaxFactory.StackAllocArrayCreationExpression(
                arrayCreation.Type.WithoutTrivia(),
                arrayCreation.Initializer)
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
        var clearStatement = SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(declarator.Identifier),
                        SyntaxFactory.IdentifierName("Clear"))))
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

    internal static async Task<Document> UseRefLocalAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var (root, semanticModel) = await CodeFixDocument.TryLoadAsync(document, cancellationToken)
            .ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var declaration = CodeFixDocument.FindAncestor<LocalDeclarationStatementSyntax>(root, diagnostic);
        if (declaration is null ||
            !UnityBestPracticesAnalyzer.TryGetCopyBackPattern(
                declaration,
                semanticModel,
                cancellationToken,
                out var pattern))
        {
            return document;
        }

        var refLocal = SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.RefType(pattern.Declaration.Declaration.Type.WithoutTrivia()))
                    .AddVariables(
                        SyntaxFactory.VariableDeclarator(pattern.Variable.Identifier)
                            .WithInitializer(
                                SyntaxFactory.EqualsValueClause(
                                    SyntaxFactory.RefExpression(pattern.RefTarget.WithoutTrivia())))))
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

    internal static async Task<Document> CacheCameraMainAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var (root, semanticModel) = await CodeFixDocument.TryLoadAsync(document, cancellationToken)
            .ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var access = CodeFixDocument.FindAncestor<MemberAccessExpressionSyntax>(root, diagnostic);
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
        var cameraMain = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("UnityEngine"),
                SyntaxFactory.IdentifierName("Camera")),
            SyntaxFactory.IdentifierName("main"));
        var declaration = SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"))
                    .AddVariables(
                        SyntaxFactory.VariableDeclarator(localName)
                            .WithInitializer(SyntaxFactory.EqualsValueClause(cameraMain))))
            .WithAdditionalAnnotations(Formatter.Annotation);
        var insertionIndex = block.Statements.IndexOf(insertionPoint);
        updatedBlock = updatedBlock
            .WithStatements(updatedBlock.Statements.Insert(insertionIndex, declaration))
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(block, updatedBlock));
    }

    internal static async Task<Document> PreallocateListAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var (root, semanticModel) = await CodeFixDocument.TryLoadAsync(document, cancellationToken)
            .ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var creation = CodeFixDocument.FindAncestor<ObjectCreationExpressionSyntax>(root, diagnostic);
        var configuration = AnalyzerConfiguration.For(
            document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider,
            root.SyntaxTree);
        if (creation is null ||
            !UnityBestPracticesAnalyzer.TryGetListPreallocation(
                creation,
                semanticModel,
                cancellationToken,
                out var addCount,
                configuration.MinimumListAdds))
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

    internal static async Task<Document> UseMultiplicationForSquareAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var (root, semanticModel) = await CodeFixDocument.TryLoadAsync(document, cancellationToken)
            .ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var invocation = CodeFixDocument.FindAncestor<InvocationExpressionSyntax>(root, diagnostic);
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

    internal static async Task<Document> UseUninitializedNativeArrayAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var (root, semanticModel) = await CodeFixDocument.TryLoadAsync(document, cancellationToken)
            .ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        var creation = CodeFixDocument.FindAncestor<ObjectCreationExpressionSyntax>(root, diagnostic);
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
            CreateQualifiedName(
                "Unity",
                "Collections",
                "NativeArrayOptions",
                "UninitializedMemory"));
        var replacement = creation
            .WithArgumentList(creation.ArgumentList!.AddArguments(option))
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(creation, replacement));
    }

    private static ExpressionSyntax CreateQualifiedName(params string[] parts)
    {
        ExpressionSyntax expression = SyntaxFactory.IdentifierName(parts[0]);
        for (var index = 1; index < parts.Length; index++)
        {
            expression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                expression,
                SyntaxFactory.IdentifierName(parts[index]));
        }

        return expression;
    }

    private static bool IsWithinDeclaringType(
        INamedTypeSymbol? referenceType,
        INamedTypeSymbol declaringType)
    {
        for (var current = referenceType; current is not null; current = current.ContainingType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, declaringType))
            {
                return true;
            }
        }

        return false;
    }

    private static SyntaxList<AttributeListSyntax> AddAttribute(
        SyntaxList<AttributeListSyntax> attributeLists,
        string qualifiedName) =>
        attributeLists.Add(
            SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(SyntaxFactory.ParseName(qualifiedName)))));
}
