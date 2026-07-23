// Metadata-backed expression rule registry.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityBestPractices.Analyzers;

internal abstract class ExpressionQuickFixRule
{
    protected ExpressionQuickFixRule(
        string diagnosticId,
        SyntaxKind syntaxKind,
        string title,
        string message,
        string description,
        string fixTitle)
    {
        SyntaxKind = syntaxKind;
        Metadata = RuleMetadataFactory.Create(
            diagnosticId,
            title,
            message,
            description,
            fixTitle);
    }

    internal RuleMetadata Metadata { get; }

    internal DiagnosticDescriptor Descriptor => Metadata.Descriptor;

    internal string DiagnosticId => Descriptor.Id;

    internal SyntaxKind SyntaxKind { get; }

    internal string FixTitle => Metadata.FixTitle;

    internal abstract bool TryGetReplacement(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExpressionSyntax replacement);
}

internal static class ExpressionQuickFixRegistry
{
    private static readonly ImmutableArray<ExpressionQuickFixRule> AllRules =
        ExpressionOptimizationRules.Create();

    private static readonly ImmutableDictionary<int, ImmutableArray<ExpressionQuickFixRule>> RulesBySyntaxKind =
        AllRules
            .GroupBy(rule => (int)rule.SyntaxKind)
            .ToImmutableDictionary(group => group.Key, group => group.ToImmutableArray());

    private static readonly ImmutableDictionary<string, ExpressionQuickFixRule> RulesByDiagnosticId =
        AllRules.ToImmutableDictionary(rule => rule.DiagnosticId, StringComparer.Ordinal);

    internal static ImmutableArray<DiagnosticDescriptor> Descriptors { get; } =
        AllRules.Select(rule => rule.Descriptor).ToImmutableArray();

    internal static ImmutableArray<RuleMetadata> Metadata { get; } =
        AllRules.Select(rule => rule.Metadata).ToImmutableArray();

    internal static ImmutableArray<string> DiagnosticIds { get; } =
        AllRules.Select(rule => rule.DiagnosticId).ToImmutableArray();

    internal static ImmutableArray<SyntaxKind> SyntaxKinds { get; } =
        RulesBySyntaxKind.Keys.Select(value => (SyntaxKind)value).ToImmutableArray();

    internal static ImmutableArray<ExpressionQuickFixRule> ForSyntaxKind(SyntaxKind syntaxKind) =>
        RulesBySyntaxKind.TryGetValue((int)syntaxKind, out var rules)
            ? rules
            : ImmutableArray<ExpressionQuickFixRule>.Empty;

    internal static bool TryGetRule(string diagnosticId, out ExpressionQuickFixRule rule) =>
        RulesByDiagnosticId.TryGetValue(diagnosticId, out rule!);
}

internal static class ExpressionRuleHelpers
{
    internal static bool IsExactType(ITypeSymbol? type, Compilation compilation, string metadataName)
    {
        var expected = UnitySymbolCache.GetTypeByMetadataName(compilation, metadataName);
        return type is not null &&
               expected is not null &&
               SymbolEqualityComparer.Default.Equals(type, expected);
    }

    internal static bool IsOriginalDefinition(
        ITypeSymbol? type,
        Compilation compilation,
        string metadataName)
    {
        var expected = UnitySymbolCache.GetTypeByMetadataName(compilation, metadataName);
        return type is INamedTypeSymbol named &&
               expected is not null &&
               SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, expected);
    }

    internal static bool IsMethod(
        IMethodSymbol? method,
        Compilation compilation,
        string containingTypeMetadataName,
        string methodName)
    {
        var containingType = UnitySymbolCache.GetTypeByMetadataName(
            compilation,
            containingTypeMetadataName);
        return method is not null &&
               containingType is not null &&
               method.Name == methodName &&
               SymbolEqualityComparer.Default.Equals(method.ContainingType, containingType);
    }

    internal static bool ArgumentsAreConstants(
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        IReadOnlyList<double> expected,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (arguments.Count != expected.Count || arguments.Any(argument => argument.NameColon is not null))
        {
            return false;
        }

        for (var index = 0; index < arguments.Count; index++)
        {
            var constant = semanticModel.GetConstantValue(arguments[index].Expression, cancellationToken);
            if (!constant.HasValue || constant.Value is null)
            {
                return false;
            }

            double actual;
            try
            {
                actual = Convert.ToDouble(constant.Value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return false;
            }

            if (actual != expected[index])
            {
                return false;
            }
        }

        return true;
    }

    internal static bool IsNumericConstant(
        ExpressionSyntax expression,
        double expected,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var constant = semanticModel.GetConstantValue(expression, cancellationToken);
        if (!constant.HasValue || constant.Value is null)
        {
            return false;
        }

        try
        {
            return Convert.ToDouble(constant.Value, System.Globalization.CultureInfo.InvariantCulture) == expected;
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal static ExpressionSyntax QualifiedExpression(string text) =>
        SyntaxFactory.ParseExpression(text);
}
