// Conservative expression optimization matchers.
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityBestPractices.Analyzers;

internal static class ExpressionOptimizationRules
{
    internal static ImmutableArray<ExpressionQuickFixRule> Create()
    {
        var rules = ImmutableArray.CreateBuilder<ExpressionQuickFixRule>(46);

        AddUnityConstantRules(rules);
        rules.Add(new UnityInvocationRule(
            DiagnosticIds.UseQuaternionIdentityForEulerZero,
            UnityInvocationKind.QuaternionEulerZero,
            "Use Quaternion.identity",
            "Use Quaternion.identity instead of a zero Euler rotation",
            "Quaternion.identity communicates and reuses the identity rotation.",
            "Use Quaternion.identity"));
        rules.Add(new UnityInvocationRule(
            DiagnosticIds.UseMathfClamp01,
            UnityInvocationKind.MathfClamp01,
            "Use Mathf.Clamp01",
            "Use Mathf.Clamp01 for a zero-to-one clamp",
            "Mathf.Clamp01 is the specialized and clearer zero-to-one clamp operation.",
            "Use Mathf.Clamp01"));
        rules.Add(new UnityInvocationRule(
            DiagnosticIds.UseMathfSqrt,
            UnityInvocationKind.MathfSqrt,
            "Use Mathf.Sqrt",
            "Use Mathf.Sqrt instead of raising to the power 0.5",
            "Mathf.Sqrt is the specialized square-root operation.",
            "Use Mathf.Sqrt"));

        rules.Add(new MathfCastRule(DiagnosticIds.UseMathfFloorToInt, "Floor", "FloorToInt"));
        rules.Add(new MathfCastRule(DiagnosticIds.UseMathfCeilToInt, "Ceil", "CeilToInt"));
        rules.Add(new MathfCastRule(DiagnosticIds.UseMathfRoundToInt, "Round", "RoundToInt"));
        rules.Add(new EmptyArrayRule());
        rules.Add(new LinqWhereFusionRule(DiagnosticIds.FuseWhereAny, "Any"));
        rules.Add(new LinqWhereFusionRule(DiagnosticIds.FuseWhereCount, "Count"));
        rules.Add(new LinqWhereFusionRule(DiagnosticIds.FuseWhereFirst, "First"));
        rules.Add(new LinqWhereFusionRule(DiagnosticIds.FuseWhereFirstOrDefault, "FirstOrDefault"));
        rules.Add(new DictionaryContainsKeyRule());
        rules.Add(new ListIndexerRule());
        rules.Add(new CollectionQueryRule(
            DiagnosticIds.UseListCountProperty,
            CollectionQueryKind.ListCount));
        rules.Add(new CollectionQueryRule(
            DiagnosticIds.UseArrayLengthProperty,
            CollectionQueryKind.ArrayCount));
        rules.Add(new CollectionQueryRule(
            DiagnosticIds.UseArrayLengthForAny,
            CollectionQueryKind.ArrayAny));
        rules.Add(new CollectionQueryRule(
            DiagnosticIds.UseListCountForAny,
            CollectionQueryKind.ListAny));
        rules.Add(new StringBuilderRule(DiagnosticIds.AppendCharacter, StringBuilderRuleKind.AppendCharacter));
        rules.Add(new StringBuilderRule(
            DiagnosticIds.AppendLineWithoutEmptyString,
            StringBuilderRuleKind.AppendLineWithoutEmptyString));
        rules.Add(new DefaultValueConstructionRule(
            DiagnosticIds.UseCancellationTokenNone,
            "System.Threading.CancellationToken",
            "System.Threading.CancellationToken.None",
            "Use CancellationToken.None",
            "Use CancellationToken.None instead of constructing an empty token"));
        rules.Add(new DefaultValueConstructionRule(
            DiagnosticIds.UseGuidEmpty,
            "System.Guid",
            "System.Guid.Empty",
            "Use Guid.Empty",
            "Use Guid.Empty instead of constructing an empty GUID"));
        rules.Add(new EnumerableEmptyToArrayRule());

        return rules.ToImmutable();
    }

    private static void AddUnityConstantRules(ImmutableArray<ExpressionQuickFixRule>.Builder rules)
    {
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseVector2Zero, "UnityEngine.Vector2", new[] { 0d, 0d }, "zero"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseVector2One, "UnityEngine.Vector2", new[] { 1d, 1d }, "one"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseVector2Up, "UnityEngine.Vector2", new[] { 0d, 1d }, "up"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseVector2Down, "UnityEngine.Vector2", new[] { 0d, -1d }, "down"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseVector2Left, "UnityEngine.Vector2", new[] { -1d, 0d }, "left"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseVector2Right, "UnityEngine.Vector2", new[] { 1d, 0d }, "right"));

        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseVector3Zero, "UnityEngine.Vector3", new[] { 0d, 0d, 0d }, "zero"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseVector3One, "UnityEngine.Vector3", new[] { 1d, 1d, 1d }, "one"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseVector3Up, "UnityEngine.Vector3", new[] { 0d, 1d, 0d }, "up"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseVector3Down, "UnityEngine.Vector3", new[] { 0d, -1d, 0d }, "down"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseVector3Left, "UnityEngine.Vector3", new[] { -1d, 0d, 0d }, "left"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseVector3Right, "UnityEngine.Vector3", new[] { 1d, 0d, 0d }, "right"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseVector3Forward, "UnityEngine.Vector3", new[] { 0d, 0d, 1d }, "forward"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseVector3Back, "UnityEngine.Vector3", new[] { 0d, 0d, -1d }, "back"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseQuaternionIdentity, "UnityEngine.Quaternion", new[] { 0d, 0d, 0d, 1d }, "identity"));

        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseColorClear, "UnityEngine.Color", new[] { 0d, 0d, 0d, 0d }, "clear"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseColorBlack, "UnityEngine.Color", new[] { 0d, 0d, 0d, 1d }, "black"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseColorWhite, "UnityEngine.Color", new[] { 1d, 1d, 1d, 1d }, "white"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseColorRed, "UnityEngine.Color", new[] { 1d, 0d, 0d, 1d }, "red"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseColorGreen, "UnityEngine.Color", new[] { 0d, 1d, 0d, 1d }, "green"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseColorBlue, "UnityEngine.Color", new[] { 0d, 0d, 1d, 1d }, "blue"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseColorYellow, "UnityEngine.Color", new[] { 1d, (double)0.9215686f, (double)0.01568628f, 1d }, "yellow"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseColorCyan, "UnityEngine.Color", new[] { 0d, 1d, 1d, 1d }, "cyan"));
        rules.Add(new UnityConstantConstructionRule(DiagnosticIds.UseColorMagenta, "UnityEngine.Color", new[] { 1d, 0d, 1d, 1d }, "magenta"));
    }
}

internal sealed class UnityConstantConstructionRule : ExpressionQuickFixRule
{
    private readonly string _metadataName;
    private readonly double[] _values;
    private readonly string _memberName;
    private readonly string _qualifiedReplacement;

    internal UnityConstantConstructionRule(
        string diagnosticId,
        string metadataName,
        double[] values,
        string memberName)
        : base(
            diagnosticId,
            SyntaxKind.ObjectCreationExpression,
            "Use " + metadataName.Substring(metadataName.LastIndexOf('.') + 1) + "." + memberName,
            "Use " + metadataName.Substring(metadataName.LastIndexOf('.') + 1) + "." + memberName + " for this canonical value",
            "Unity's named value is clearer than reconstructing the same canonical value.",
            "Use " + metadataName.Substring(metadataName.LastIndexOf('.') + 1) + "." + memberName)
    {
        _metadataName = metadataName;
        _values = values;
        _memberName = memberName;
        _qualifiedReplacement = metadataName + "." + memberName;
    }

    internal override bool TryGetReplacement(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExpressionSyntax replacement)
    {
        replacement = null!;
        if (expression is not ObjectCreationExpressionSyntax creation ||
            creation.ArgumentList is null ||
            creation.Initializer is not null ||
            !ExpressionRuleHelpers.IsExactType(
                semanticModel.GetTypeInfo(creation, cancellationToken).Type,
                semanticModel.Compilation,
                _metadataName) ||
            !ExpressionRuleHelpers.ArgumentsAreConstants(
                creation.ArgumentList.Arguments,
                _values,
                semanticModel,
                cancellationToken))
        {
            return false;
        }

        var type = UnitySymbolCache.GetTypeByMetadataName(semanticModel.Compilation, _metadataName);
        if (type is null || !type.GetMembers(_memberName).Any(member => member.IsStatic))
        {
            return false;
        }

        replacement = ExpressionRuleHelpers.QualifiedExpression(_qualifiedReplacement);
        return true;
    }
}

internal enum UnityInvocationKind
{
    QuaternionEulerZero,
    MathfClamp01,
    MathfSqrt,
}

internal sealed class UnityInvocationRule : ExpressionQuickFixRule
{
    private readonly UnityInvocationKind _kind;

    internal UnityInvocationRule(
        string diagnosticId,
        UnityInvocationKind kind,
        string title,
        string message,
        string description,
        string fixTitle)
        : base(diagnosticId, SyntaxKind.InvocationExpression, title, message, description, fixTitle)
    {
        _kind = kind;
    }

    internal override bool TryGetReplacement(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExpressionSyntax replacement)
    {
        replacement = null!;
        if (expression is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        switch (_kind)
        {
            case UnityInvocationKind.QuaternionEulerZero:
                if (!ExpressionRuleHelpers.IsMethod(
                        method,
                        semanticModel.Compilation,
                        "UnityEngine.Quaternion",
                        "Euler") ||
                    !ExpressionRuleHelpers.ArgumentsAreConstants(
                        invocation.ArgumentList.Arguments,
                        new[] { 0d, 0d, 0d },
                        semanticModel,
                        cancellationToken))
                {
                    return false;
                }

                var quaternion = UnitySymbolCache.GetTypeByMetadataName(
                    semanticModel.Compilation,
                    "UnityEngine.Quaternion");
                if (quaternion is null || !quaternion.GetMembers("identity").Any(member => member.IsStatic))
                {
                    return false;
                }

                replacement = ExpressionRuleHelpers.QualifiedExpression("UnityEngine.Quaternion.identity");
                return true;

            case UnityInvocationKind.MathfClamp01:
                if (!ExpressionRuleHelpers.IsMethod(method, semanticModel.Compilation, "UnityEngine.Mathf", "Clamp") ||
                    invocation.ArgumentList.Arguments.Count != 3 ||
                    invocation.ArgumentList.Arguments.Any(argument => argument.NameColon is not null) ||
                    !ExpressionRuleHelpers.IsNumericConstant(
                        invocation.ArgumentList.Arguments[1].Expression,
                        0d,
                        semanticModel,
                        cancellationToken) ||
                    !ExpressionRuleHelpers.IsNumericConstant(
                        invocation.ArgumentList.Arguments[2].Expression,
                        1d,
                        semanticModel,
                        cancellationToken) ||
                    !HasMathfMethod(semanticModel.Compilation, "Clamp01"))
                {
                    return false;
                }

                replacement = SyntaxFactory.InvocationExpression(
                    ExpressionRuleHelpers.QualifiedExpression("UnityEngine.Mathf.Clamp01"),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(invocation.ArgumentList.Arguments[0])));
                return true;

            case UnityInvocationKind.MathfSqrt:
                if (!ExpressionRuleHelpers.IsMethod(method, semanticModel.Compilation, "UnityEngine.Mathf", "Pow") ||
                    invocation.ArgumentList.Arguments.Count != 2 ||
                    invocation.ArgumentList.Arguments.Any(argument => argument.NameColon is not null) ||
                    !ExpressionRuleHelpers.IsNumericConstant(
                        invocation.ArgumentList.Arguments[1].Expression,
                        0.5d,
                        semanticModel,
                        cancellationToken) ||
                    !HasMathfMethod(semanticModel.Compilation, "Sqrt"))
                {
                    return false;
                }

                replacement = SyntaxFactory.InvocationExpression(
                    ExpressionRuleHelpers.QualifiedExpression("UnityEngine.Mathf.Sqrt"),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(invocation.ArgumentList.Arguments[0])));
                return true;

            default:
                return false;
        }
    }

    private static bool HasMathfMethod(Compilation compilation, string name) =>
        UnitySymbolCache.GetTypeByMetadataName(compilation, "UnityEngine.Mathf")
            ?.GetMembers(name).OfType<IMethodSymbol>().Any() == true;
}

internal sealed class MathfCastRule : ExpressionQuickFixRule
{
    private readonly string _sourceMethod;
    private readonly string _targetMethod;

    internal MathfCastRule(string diagnosticId, string sourceMethod, string targetMethod)
        : base(
            diagnosticId,
            SyntaxKind.CastExpression,
            "Use Mathf." + targetMethod,
            "Use Mathf." + targetMethod + " instead of casting Mathf." + sourceMethod,
            "The specialized integer operation avoids an intermediate floating-point result and states the intent directly.",
            "Use Mathf." + targetMethod)
    {
        _sourceMethod = sourceMethod;
        _targetMethod = targetMethod;
    }

    internal override bool TryGetReplacement(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExpressionSyntax replacement)
    {
        replacement = null!;
        if (expression is not CastExpressionSyntax cast ||
            semanticModel.GetTypeInfo(cast.Type, cancellationToken).Type?.SpecialType != SpecialType.System_Int32 ||
            cast.Expression is not InvocationExpressionSyntax invocation ||
            invocation.ArgumentList.Arguments.Count != 1 ||
            invocation.ArgumentList.Arguments[0].NameColon is not null)
        {
            return false;
        }

        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        var mathf = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "UnityEngine.Mathf");
        if (!ExpressionRuleHelpers.IsMethod(method, semanticModel.Compilation, "UnityEngine.Mathf", _sourceMethod) ||
            mathf?.GetMembers(_targetMethod).OfType<IMethodSymbol>().Any() != true)
        {
            return false;
        }

        replacement = SyntaxFactory.InvocationExpression(
            ExpressionRuleHelpers.QualifiedExpression("UnityEngine.Mathf." + _targetMethod),
            invocation.ArgumentList);
        return true;
    }
}

internal sealed class EmptyArrayRule : ExpressionQuickFixRule
{
    internal EmptyArrayRule()
        : base(
            DiagnosticIds.UseArrayEmpty,
            SyntaxKind.ArrayCreationExpression,
            "Reuse an empty array",
            "Use Array.Empty<T>() instead of allocating an empty array",
            "Array.Empty<T>() reuses a cached empty array instead of allocating another object.",
            "Use Array.Empty<T>()")
    {
    }

    internal override bool TryGetReplacement(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExpressionSyntax replacement)
    {
        replacement = null!;
        if (expression is not ArrayCreationExpressionSyntax creation ||
            creation.Type.RankSpecifiers.Count != 1 ||
            creation.Type.RankSpecifiers[0].Sizes.Count != 1 ||
            !HasArrayEmpty(semanticModel.Compilation))
        {
            return false;
        }

        var size = creation.Type.RankSpecifiers[0].Sizes[0];
        var isExplicitZero = size is not OmittedArraySizeExpressionSyntax &&
                             ExpressionRuleHelpers.IsNumericConstant(size, 0d, semanticModel, cancellationToken);
        var isEmptyInitializer = size is OmittedArraySizeExpressionSyntax &&
                                 creation.Initializer?.Expressions.Count == 0;
        if (!isExplicitZero && !isEmptyInitializer)
        {
            return false;
        }

        replacement = ExpressionRuleHelpers.QualifiedExpression(
            "System.Array.Empty<" + creation.Type.ElementType.WithoutTrivia().ToFullString() + ">()");
        return true;
    }

    private static bool HasArrayEmpty(Compilation compilation) =>
        UnitySymbolCache.GetTypeByMetadataName(compilation, "System.Array")
            ?.GetMembers("Empty").OfType<IMethodSymbol>().Any() == true;
}

internal sealed class LinqWhereFusionRule : ExpressionQuickFixRule
{
    private readonly string _terminalMethod;

    internal LinqWhereFusionRule(string diagnosticId, string terminalMethod)
        : base(
            diagnosticId,
            SyntaxKind.InvocationExpression,
            "Fuse Where with " + terminalMethod,
            "Pass the predicate directly to " + terminalMethod,
            "Using the predicate overload avoids creating and traversing a separate Where iterator.",
            "Use " + terminalMethod + " with the predicate")
    {
        _terminalMethod = terminalMethod;
    }

    internal override bool TryGetReplacement(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExpressionSyntax replacement)
    {
        replacement = null!;
        if (expression is not InvocationExpressionSyntax invocation ||
            invocation.ArgumentList.Arguments.Count != 0 ||
            invocation.Expression is not MemberAccessExpressionSyntax terminalAccess ||
            terminalAccess.Expression is not InvocationExpressionSyntax whereInvocation ||
            whereInvocation.ArgumentList.Arguments.Count != 1 ||
            whereInvocation.ArgumentList.Arguments[0].NameColon is not null ||
            whereInvocation.Expression is not MemberAccessExpressionSyntax whereAccess)
        {
            return false;
        }

        var terminal = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        var where = semanticModel.GetSymbolInfo(whereInvocation, cancellationToken).Symbol as IMethodSymbol;
        if (!ExpressionRuleHelpers.IsMethod(terminal, semanticModel.Compilation, "System.Linq.Enumerable", _terminalMethod) ||
            !ExpressionRuleHelpers.IsMethod(where, semanticModel.Compilation, "System.Linq.Enumerable", "Where") ||
            terminal?.ReducedFrom is null ||
            where?.ReducedFrom is null ||
            where.ReducedFrom.Parameters.Length != 2 ||
            where.ReducedFrom.Parameters[1].Type is not INamedTypeSymbol predicateType ||
            predicateType.DelegateInvokeMethod?.Parameters.Length != 1)
        {
            return false;
        }

        replacement = invocation
            .WithExpression(terminalAccess.WithExpression(whereAccess.Expression))
            .WithArgumentList(whereInvocation.ArgumentList);
        return true;
    }
}

internal sealed class DictionaryContainsKeyRule : ExpressionQuickFixRule
{
    internal DictionaryContainsKeyRule()
        : base(
            DiagnosticIds.UseDictionaryContainsKey,
            SyntaxKind.InvocationExpression,
            "Use Dictionary.ContainsKey",
            "Use ContainsKey instead of searching the Keys collection",
            "Dictionary.ContainsKey performs the key lookup directly without routing through its key collection.",
            "Use ContainsKey")
    {
    }

    internal override bool TryGetReplacement(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExpressionSyntax replacement)
    {
        replacement = null!;
        if (expression is not InvocationExpressionSyntax invocation ||
            invocation.ArgumentList.Arguments.Count != 1 ||
            invocation.ArgumentList.Arguments[0].NameColon is not null ||
            invocation.Expression is not MemberAccessExpressionSyntax containsAccess ||
            containsAccess.Name.Identifier.ValueText != "Contains" ||
            containsAccess.Expression is not MemberAccessExpressionSyntax keysAccess ||
            keysAccess.Name.Identifier.ValueText != "Keys")
        {
            return false;
        }

        var dictionaryType = semanticModel.GetTypeInfo(keysAccess.Expression, cancellationToken).Type;
        var keysProperty = semanticModel.GetSymbolInfo(keysAccess, cancellationToken).Symbol as IPropertySymbol;
        var containsMethod = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        if (!ExpressionRuleHelpers.IsOriginalDefinition(
                dictionaryType,
                semanticModel.Compilation,
                "System.Collections.Generic.Dictionary`2") ||
            keysProperty?.Name != "Keys" ||
            containsMethod?.Name != "Contains" ||
            dictionaryType is not INamedTypeSymbol namedDictionary ||
            !namedDictionary.GetMembers("ContainsKey").OfType<IMethodSymbol>().Any())
        {
            return false;
        }

        replacement = invocation.WithExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                keysAccess.Expression,
                SyntaxFactory.IdentifierName("ContainsKey")));
        return true;
    }
}

internal sealed class ListIndexerRule : ExpressionQuickFixRule
{
    internal ListIndexerRule()
        : base(
            DiagnosticIds.UseListIndexer,
            SyntaxKind.InvocationExpression,
            "Use the List indexer",
            "Use the List indexer instead of Enumerable.ElementAt",
            "The List indexer expresses direct random access and avoids extension-method dispatch.",
            "Use the List indexer")
    {
    }

    internal override bool TryGetReplacement(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExpressionSyntax replacement)
    {
        replacement = null!;
        if (expression is not InvocationExpressionSyntax invocation ||
            invocation.ArgumentList.Arguments.Count != 1 ||
            invocation.ArgumentList.Arguments[0].NameColon is not null ||
            invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return false;
        }

        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        var receiverType = semanticModel.GetTypeInfo(access.Expression, cancellationToken).Type;
        if (!ExpressionRuleHelpers.IsMethod(method, semanticModel.Compilation, "System.Linq.Enumerable", "ElementAt") ||
            method?.ReducedFrom is null ||
            !ExpressionRuleHelpers.IsOriginalDefinition(
                receiverType,
                semanticModel.Compilation,
                "System.Collections.Generic.List`1"))
        {
            return false;
        }

        replacement = SyntaxFactory.ElementAccessExpression(
            access.Expression,
            SyntaxFactory.BracketedArgumentList(invocation.ArgumentList.Arguments));
        return true;
    }
}

internal enum CollectionQueryKind
{
    ListCount,
    ArrayCount,
    ArrayAny,
    ListAny,
}

internal sealed class CollectionQueryRule : ExpressionQuickFixRule
{
    private readonly CollectionQueryKind _kind;

    internal CollectionQueryRule(string diagnosticId, CollectionQueryKind kind)
        : base(
            diagnosticId,
            SyntaxKind.InvocationExpression,
            GetTitle(kind),
            GetMessage(kind),
            "A collection property avoids the general-purpose LINQ query path.",
            GetTitle(kind))
    {
        _kind = kind;
    }

    internal override bool TryGetReplacement(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExpressionSyntax replacement)
    {
        replacement = null!;
        if (expression is not InvocationExpressionSyntax invocation ||
            invocation.ArgumentList.Arguments.Count != 0 ||
            invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return false;
        }

        var methodName = _kind is CollectionQueryKind.ListCount or CollectionQueryKind.ArrayCount
            ? "Count"
            : "Any";
        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        var receiverType = semanticModel.GetTypeInfo(access.Expression, cancellationToken).Type;
        var expectsArray = _kind is CollectionQueryKind.ArrayCount or CollectionQueryKind.ArrayAny;
        var correctReceiver = expectsArray
            ? receiverType is IArrayTypeSymbol { Rank: 1 }
            : ExpressionRuleHelpers.IsOriginalDefinition(
                receiverType,
                semanticModel.Compilation,
                "System.Collections.Generic.List`1");
        if (!correctReceiver ||
            !ExpressionRuleHelpers.IsMethod(method, semanticModel.Compilation, "System.Linq.Enumerable", methodName) ||
            method?.ReducedFrom is null)
        {
            return false;
        }

        var propertyName = expectsArray ? "Length" : "Count";
        var property = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            access.Expression,
            SyntaxFactory.IdentifierName(propertyName));
        if (methodName == "Count")
        {
            replacement = property;
            return true;
        }

        ExpressionSyntax comparison = SyntaxFactory.BinaryExpression(
            SyntaxKind.NotEqualsExpression,
            property,
            SyntaxFactory.LiteralExpression(
                SyntaxKind.NumericLiteralExpression,
                SyntaxFactory.Literal(0)));
        // A binary comparison does not bind like the invocation it replaces, so a
        // parent expression (!x.Any(), flag == x.Any(), ...) needs parentheses.
        replacement = expression.Parent is ExpressionSyntax and not ParenthesizedExpressionSyntax
            ? SyntaxFactory.ParenthesizedExpression(comparison)
            : comparison;
        return true;
    }

    private static string GetTitle(CollectionQueryKind kind) => kind switch
    {
        CollectionQueryKind.ListCount => "Use List.Count",
        CollectionQueryKind.ArrayCount => "Use Array.Length",
        CollectionQueryKind.ArrayAny => "Use Array.Length for emptiness",
        _ => "Use List.Count for emptiness",
    };

    private static string GetMessage(CollectionQueryKind kind) => kind switch
    {
        CollectionQueryKind.ListCount => "Use the List.Count property instead of Enumerable.Count",
        CollectionQueryKind.ArrayCount => "Use the array Length property instead of Enumerable.Count",
        CollectionQueryKind.ArrayAny => "Check array Length instead of calling Enumerable.Any",
        _ => "Check List.Count instead of calling Enumerable.Any",
    };
}

internal enum StringBuilderRuleKind
{
    AppendCharacter,
    AppendLineWithoutEmptyString,
}

internal sealed class StringBuilderRule : ExpressionQuickFixRule
{
    private readonly StringBuilderRuleKind _kind;

    internal StringBuilderRule(string diagnosticId, StringBuilderRuleKind kind)
        : base(
            diagnosticId,
            SyntaxKind.InvocationExpression,
            kind == StringBuilderRuleKind.AppendCharacter
                ? "Append a character"
                : "Append an empty line directly",
            kind == StringBuilderRuleKind.AppendCharacter
                ? "Use the character overload of StringBuilder.Append"
                : "Call StringBuilder.AppendLine without an empty string",
            kind == StringBuilderRuleKind.AppendCharacter
                ? "The character overload avoids processing a one-character string."
                : "The parameterless overload expresses an empty line without a redundant string argument.",
            kind == StringBuilderRuleKind.AppendCharacter
                ? "Use Append(char)"
                : "Use AppendLine()")
    {
        _kind = kind;
    }

    internal override bool TryGetReplacement(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExpressionSyntax replacement)
    {
        replacement = null!;
        if (expression is not InvocationExpressionSyntax invocation ||
            invocation.ArgumentList.Arguments.Count != 1 ||
            invocation.ArgumentList.Arguments[0].NameColon is not null)
        {
            return false;
        }

        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        var expectedMethod = _kind == StringBuilderRuleKind.AppendCharacter ? "Append" : "AppendLine";
        if (!ExpressionRuleHelpers.IsMethod(
                method,
                semanticModel.Compilation,
                "System.Text.StringBuilder",
                expectedMethod) ||
            method?.Parameters.Length != 1 ||
            method.Parameters[0].Type.SpecialType != SpecialType.System_String)
        {
            return false;
        }

        var constant = semanticModel.GetConstantValue(
            invocation.ArgumentList.Arguments[0].Expression,
            cancellationToken);
        if (!constant.HasValue || constant.Value is not string text)
        {
            return false;
        }

        if (_kind == StringBuilderRuleKind.AppendCharacter)
        {
            if (text.Length != 1)
            {
                return false;
            }

            var argument = invocation.ArgumentList.Arguments[0].WithExpression(
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.CharacterLiteralExpression,
                    SyntaxFactory.Literal(text[0])));
            replacement = invocation.WithArgumentList(
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(argument)));
            return true;
        }

        if (text.Length != 0)
        {
            return false;
        }

        replacement = invocation.WithArgumentList(SyntaxFactory.ArgumentList());
        return true;
    }
}

internal sealed class DefaultValueConstructionRule : ExpressionQuickFixRule
{
    private readonly string _metadataName;
    private readonly string _qualifiedReplacement;
    private readonly string _memberName;

    internal DefaultValueConstructionRule(
        string diagnosticId,
        string metadataName,
        string qualifiedReplacement,
        string title,
        string message)
        : base(
            diagnosticId,
            SyntaxKind.ObjectCreationExpression,
            title,
            message,
            "The named empty value communicates intent and avoids an unnecessary constructor expression.",
            title)
    {
        _metadataName = metadataName;
        _qualifiedReplacement = qualifiedReplacement;
        _memberName = qualifiedReplacement.Substring(qualifiedReplacement.LastIndexOf('.') + 1);
    }

    internal override bool TryGetReplacement(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExpressionSyntax replacement)
    {
        replacement = null!;
        if (expression is not ObjectCreationExpressionSyntax creation ||
            creation.ArgumentList is not { Arguments.Count: 0 } ||
            creation.Initializer is not null ||
            !ExpressionRuleHelpers.IsExactType(
                semanticModel.GetTypeInfo(creation, cancellationToken).Type,
                semanticModel.Compilation,
                _metadataName))
        {
            return false;
        }

        var type = UnitySymbolCache.GetTypeByMetadataName(semanticModel.Compilation, _metadataName);
        if (type is null || !type.GetMembers(_memberName).Any(member => member.IsStatic))
        {
            return false;
        }

        replacement = ExpressionRuleHelpers.QualifiedExpression(_qualifiedReplacement);
        return true;
    }
}

internal sealed class EnumerableEmptyToArrayRule : ExpressionQuickFixRule
{
    internal EnumerableEmptyToArrayRule()
        : base(
            DiagnosticIds.UseArrayEmptyForEnumerableEmpty,
            SyntaxKind.InvocationExpression,
            "Use Array.Empty<T>() directly",
            "Use Array.Empty<T>() instead of materializing Enumerable.Empty<T>()",
            "Array.Empty<T>() directly returns the cached empty array without a LINQ materialization call.",
            "Use Array.Empty<T>()")
    {
    }

    internal override bool TryGetReplacement(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExpressionSyntax replacement)
    {
        replacement = null!;
        if (expression is not InvocationExpressionSyntax invocation ||
            invocation.ArgumentList.Arguments.Count != 0 ||
            invocation.Expression is not MemberAccessExpressionSyntax access ||
            access.Expression is not InvocationExpressionSyntax emptyInvocation ||
            emptyInvocation.ArgumentList.Arguments.Count != 0)
        {
            return false;
        }

        var toArray = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        var empty = semanticModel.GetSymbolInfo(emptyInvocation, cancellationToken).Symbol as IMethodSymbol;
        var returnType = semanticModel.GetTypeInfo(invocation, cancellationToken).Type as IArrayTypeSymbol;
        if (!ExpressionRuleHelpers.IsMethod(toArray, semanticModel.Compilation, "System.Linq.Enumerable", "ToArray") ||
            !ExpressionRuleHelpers.IsMethod(empty, semanticModel.Compilation, "System.Linq.Enumerable", "Empty") ||
            returnType is null ||
            UnitySymbolCache.GetTypeByMetadataName(semanticModel.Compilation, "System.Array")
                ?.GetMembers("Empty").OfType<IMethodSymbol>().Any() != true)
        {
            return false;
        }

        replacement = ExpressionRuleHelpers.QualifiedExpression(
            "System.Array.Empty<" +
            returnType.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) +
            ">()");
        return true;
    }
}
