using UnityBestPractices.Analyzers;
using UnityBestPractices.Analyzers.Infrastructure;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;

namespace UnityBestPractices.Analyzers.Rules.Correctness;

internal static class ModernObjectFindRule
{
    private const string SortModeInstanceId = "global::UnityEngine.FindObjectsSortMode.InstanceID";
    private const string InactiveInclude = "global::UnityEngine.FindObjectsInactive.Include";
    private const string InactiveExclude = "global::UnityEngine.FindObjectsInactive.Exclude";

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
            !TryClassify(method, out var replacementName, out var needsSortMode, out var needsInactiveEnum) ||
            !objectType.GetMembers(replacementName).OfType<IMethodSymbol>().Any(candidate => candidate.IsStatic))
        {
            return;
        }

        if (needsSortMode &&
            UnitySymbolCache.GetTypeByMetadataName(
                context.SemanticModel.Compilation,
                "UnityEngine.FindObjectsSortMode") is null)
        {
            return;
        }

        if (needsInactiveEnum &&
            UnitySymbolCache.GetTypeByMetadataName(
                context.SemanticModel.Compilation,
                "UnityEngine.FindObjectsInactive") is null)
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
            !TryClassify(method, out var replacementName, out var needsSortMode, out _))
        {
            return document;
        }

        var includeInactive = TryGetIncludeInactiveArgument(method, invocation);

        // Prefer typeof(T) + matching cast → generic form.
        if (!method.IsGenericMethod &&
            TryGetTypeOfArgumentType(invocation, out var typeOfTypeSyntax) &&
            TryGetMatchingCast(invocation, typeOfTypeSyntax, isMultiple: needsSortMode, out var castNode))
        {
            var genericInvocation = BuildReplacement(
                invocation,
                replacementName,
                typeArgument: typeOfTypeSyntax,
                keepTypeArgument: false,
                includeInactiveArgument: includeInactive,
                needsSortMode: needsSortMode);

            return document.WithSyntaxRoot(
                root.ReplaceNode(castNode, genericInvocation.WithTriviaFrom(castNode)));
        }

        var replacement = BuildReplacement(
            invocation,
            replacementName,
            typeArgument: null,
            keepTypeArgument: !method.IsGenericMethod,
            includeInactiveArgument: includeInactive,
            needsSortMode: needsSortMode);

        return document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement));
    }

    private static InvocationExpressionSyntax BuildReplacement(
        InvocationExpressionSyntax invocation,
        string replacementName,
        TypeSyntax? typeArgument,
        bool keepTypeArgument,
        ExpressionSyntax? includeInactiveArgument,
        bool needsSortMode)
    {
        ExpressionSyntax newExpression;
        if (typeArgument is not null)
        {
            var genericName = SyntaxFactory.GenericName(
                SyntaxFactory.Identifier(replacementName),
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(typeArgument.WithoutTrivia())));

            newExpression = invocation.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess =>
                    memberAccess.WithName(genericName.WithTriviaFrom(memberAccess.Name)),
                _ => genericName.WithTriviaFrom(invocation.Expression),
            };
        }
        else
        {
            newExpression = Rename(invocation.Expression, replacementName);
        }

        var newArgs = new List<ArgumentSyntax>();

        if (keepTypeArgument && invocation.ArgumentList.Arguments.Count > 0)
        {
            newArgs.Add(invocation.ArgumentList.Arguments[0]);
        }

        if (includeInactiveArgument is not null)
        {
            newArgs.Add(SyntaxFactory.Argument(includeInactiveArgument));
        }

        if (needsSortMode)
        {
            newArgs.Add(SyntaxFactory.Argument(SyntaxFactory.ParseExpression(SortModeInstanceId)));
        }

        return invocation
            .WithExpression(newExpression)
            .WithArgumentList(
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(newArgs))
                    .WithTriviaFrom(invocation.ArgumentList))
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    private static ExpressionSyntax? TryGetIncludeInactiveArgument(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation)
    {
        if (method.Parameters.Length == 0)
        {
            return null;
        }

        var lastParam = method.Parameters[method.Parameters.Length - 1];
        if (lastParam.Type.SpecialType != SpecialType.System_Boolean)
        {
            return null;
        }

        var inactiveArgIndex = method.IsGenericMethod ? 0 : 1;
        if (invocation.ArgumentList.Arguments.Count <= inactiveArgIndex)
        {
            return null;
        }

        return ConvertBoolToFindObjectsInactive(
            invocation.ArgumentList.Arguments[inactiveArgIndex].Expression);
    }

    private static ExpressionSyntax ConvertBoolToFindObjectsInactive(ExpressionSyntax boolExpression)
    {
        if (boolExpression.IsKind(SyntaxKind.TrueLiteralExpression))
        {
            return SyntaxFactory.ParseExpression(InactiveInclude);
        }

        if (boolExpression.IsKind(SyntaxKind.FalseLiteralExpression))
        {
            return SyntaxFactory.ParseExpression(InactiveExclude);
        }

        return SyntaxFactory.ConditionalExpression(
            boolExpression.WithoutTrivia(),
            SyntaxFactory.ParseExpression(InactiveInclude),
            SyntaxFactory.ParseExpression(InactiveExclude));
    }

    private static bool TryGetTypeOfArgumentType(
        InvocationExpressionSyntax invocation,
        out TypeSyntax typeSyntax)
    {
        typeSyntax = null!;
        if (invocation.ArgumentList.Arguments.Count < 1)
        {
            return false;
        }

        if (invocation.ArgumentList.Arguments[0].Expression is not TypeOfExpressionSyntax typeOfExpression)
        {
            return false;
        }

        typeSyntax = typeOfExpression.Type;
        return true;
    }

    private static bool TryGetMatchingCast(
        InvocationExpressionSyntax invocation,
        TypeSyntax typeOfTypeSyntax,
        bool isMultiple,
        out ExpressionSyntax castExpression)
    {
        castExpression = null!;

        var parent = invocation.Parent;
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

        if (isMultiple)
        {
            if (candidateType is not ArrayTypeSyntax arrayType ||
                !TypesMatchIgnoringTrivia(arrayType.ElementType, typeOfTypeSyntax))
            {
                return false;
            }
        }
        else if (!TypesMatchIgnoringTrivia(candidateType, typeOfTypeSyntax))
        {
            return false;
        }

        ExpressionSyntax nodeToReplace = candidateNode;
        while (nodeToReplace.Parent is ParenthesizedExpressionSyntax outer)
        {
            nodeToReplace = outer;
        }

        castExpression = nodeToReplace;
        return true;
    }

    private static bool TypesMatchIgnoringTrivia(TypeSyntax left, TypeSyntax right) =>
        left.WithoutTrivia().ToFullString() == right.WithoutTrivia().ToFullString();

    private static bool TryClassify(
        IMethodSymbol method,
        out string replacementName,
        out bool needsSortMode,
        out bool needsInactiveEnum)
    {
        replacementName = string.Empty;
        needsSortMode = false;
        needsInactiveEnum = false;

        if (method.Name is not ("FindObjectOfType" or "FindObjectsOfType"))
        {
            return false;
        }

        var isMultiple = method.Name == "FindObjectsOfType";
        replacementName = isMultiple ? "FindObjectsByType" : "FindFirstObjectByType";
        needsSortMode = isMultiple;

        if (method.IsGenericMethod)
        {
            if (method.TypeArguments.Length != 1)
            {
                return false;
            }

            if (method.Parameters.Length == 0)
            {
                return true;
            }

            if (method.Parameters.Length == 1 &&
                method.Parameters[0].Type.SpecialType == SpecialType.System_Boolean)
            {
                needsInactiveEnum = true;
                return true;
            }

            return false;
        }

        if (method.Parameters.Length == 0)
        {
            return false;
        }

        var typeParam = method.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (typeParam != "global::System.Type")
        {
            return false;
        }

        if (method.Parameters.Length == 1)
        {
            return true;
        }

        if (method.Parameters.Length == 2 &&
            method.Parameters[1].Type.SpecialType == SpecialType.System_Boolean)
        {
            needsInactiveEnum = true;
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
