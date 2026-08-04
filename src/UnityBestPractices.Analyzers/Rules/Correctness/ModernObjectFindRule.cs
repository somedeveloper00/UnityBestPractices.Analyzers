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

        // Prefer converting typeof(T) + cast to T / T[] into the generic form so the
        // resulting call matches modern Unity style while keeping InstanceID ordering.
        if (!method.IsGenericMethod &&
            TryGetTypeOfArgumentType(invocation, semanticModel, cancellationToken, out var typeOfTypeSyntax) &&
            TryGetMatchingCast(invocation, typeOfTypeSyntax, addsSortMode, out var castNode, out var castTypeSyntax))
        {
            var genericName = SyntaxFactory.GenericName(
                SyntaxFactory.Identifier(replacementName),
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(typeOfTypeSyntax.WithoutTrivia())));

            ExpressionSyntax newExpression = invocation.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess =>
                    memberAccess.WithName(genericName.WithTriviaFrom(memberAccess.Name)),
                _ => genericName.WithTriviaFrom(invocation.Expression),
            };

            SeparatedSyntaxList<ArgumentSyntax> newArguments = default;
            if (addsSortMode)
            {
                newArguments = SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(
                        SyntaxFactory.ParseExpression("global::UnityEngine.FindObjectsSortMode.InstanceID")));
            }

            var newInvocation = invocation
                .WithExpression(newExpression)
                .WithArgumentList(SyntaxFactory.ArgumentList(newArguments).WithTriviaFrom(invocation.ArgumentList))
                .WithAdditionalAnnotations(Formatter.Annotation);

            // Replace the outer cast (as or explicit) with the generic invocation so
            // the cast is no longer needed.
            var nodeToReplace = (SyntaxNode)castNode;
            var replacementNode = (SyntaxNode)newInvocation.WithTriviaFrom(castNode);

            return document.WithSyntaxRoot(root.ReplaceNode(nodeToReplace, replacementNode));
        }

        // Fallback: rename only (and add SortMode for the multi-object API).
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

    private static bool TryGetTypeOfArgumentType(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out TypeSyntax typeSyntax)
    {
        typeSyntax = null!;
        if (invocation.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        var argumentExpression = invocation.ArgumentList.Arguments[0].Expression;
        if (argumentExpression is not TypeOfExpressionSyntax typeOfExpression)
        {
            return false;
        }

        // Ensure the argument really is a System.Type typeof(...) and not some other expression.
        var typeInfo = semanticModel.GetTypeInfo(typeOfExpression.Type, cancellationToken);
        if (typeInfo.Type is null)
        {
            return false;
        }

        typeSyntax = typeOfExpression.Type;
        return true;
    }

    private static bool TryGetMatchingCast(
        InvocationExpressionSyntax invocation,
        TypeSyntax typeOfTypeSyntax,
        bool expectsArray,
        out ExpressionSyntax castExpression,
        out TypeSyntax castTypeSyntax)
    {
        castExpression = null!;
        castTypeSyntax = null!;

        var parent = invocation.Parent;
        // Unwrap parentheses: ((T[])FindObjectsOfType(...))
        while (parent is ParenthesizedExpressionSyntax parenthesized)
        {
            parent = parenthesized.Parent;
        }

        TypeSyntax? candidateType = null;
        ExpressionSyntax? candidateNode = null;

        if (parent is BinaryExpressionSyntax binary &&
            binary.IsKind(SyntaxKind.AsExpression) &&
            binary.Left == invocation)
        {
            candidateType = binary.Right as TypeSyntax;
            candidateNode = binary;
        }
        else if (parent is CastExpressionSyntax cast &&
                 cast.Expression == invocation)
        {
            candidateType = cast.Type;
            candidateNode = cast;
        }

        if (candidateType is null || candidateNode is null)
        {
            return false;
        }

        // Normalize array vs element: for FindObjectsOfType we expect T[], for FindObjectOfType we expect T.
        if (expectsArray)
        {
            if (candidateType is not ArrayTypeSyntax arrayType ||
                !TypesMatchIgnoringTrivia(arrayType.ElementType, typeOfTypeSyntax))
            {
                return false;
            }
        }
        else
        {
            if (!TypesMatchIgnoringTrivia(candidateType, typeOfTypeSyntax))
            {
                return false;
            }
        }

        // Prefer replacing the outermost parenthesized expression that wraps the cast
        // so leftover parentheses are not left behind after the fix.
        ExpressionSyntax nodeToReplace = candidateNode;
        while (nodeToReplace.Parent is ParenthesizedExpressionSyntax outer)
        {
            nodeToReplace = outer;
        }

        castExpression = nodeToReplace;
        castTypeSyntax = candidateType;
        return true;
    }

    private static bool TypesMatchIgnoringTrivia(TypeSyntax left, TypeSyntax right)
    {
        return left.WithoutTrivia().ToFullString() == right.WithoutTrivia().ToFullString();
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
