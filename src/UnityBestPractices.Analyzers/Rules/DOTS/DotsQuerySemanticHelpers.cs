using UnityBestPractices.Analyzers;
using UnityBestPractices.Analyzers.Infrastructure;
// Shared semantic helpers for DOTS query and job conversions.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityBestPractices.Analyzers.Rules.Dots;

internal static class DotsQuerySemanticHelpers
{
    private static readonly ImmutableHashSet<string> SupportedFilters = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "WithAll",
        "WithAny",
        "WithNone",
        "WithChangeFilter",
        "WithEntityQueryOptions");

    internal static bool IsSupportedFilterName(string name) => SupportedFilters.Contains(name);

    internal static bool IsUnityEntitiesMethod(IMethodSymbol? method, string name) =>
        method is not null &&
        method.Name == name &&
        IsUnityEntitiesNamespace(method.ContainingNamespace);

    internal static bool IsUnityEntitiesSystemApiMethod(IMethodSymbol? method, string name) =>
        IsUnityEntitiesMethod(method, name) && method!.ContainingType.Name == "SystemAPI";

    internal static bool IsEntitiesBuilderExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var symbol = semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol;
        var type = semanticModel.GetTypeInfo(expression, cancellationToken).Type;
        return symbol?.Name == "Entities" &&
               type is not null &&
               IsUnityEntitiesNamespace(type.ContainingNamespace);
    }

    internal static bool TryCreateFilter(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax access,
        out DotsQueryFilter filter,
        string? normalizedName = null)
    {
        filter = null!;
        var name = normalizedName ?? access.Name.Identifier.ValueText;
        if (name == "WithEntityQueryOptions")
        {
            if (invocation.ArgumentList.Arguments.Count != 1)
            {
                return false;
            }

            filter = new DotsQueryFilter(
                name,
                ImmutableArray<string>.Empty,
                invocation.ArgumentList.Arguments[0].Expression.WithoutTrivia().ToFullString());
            return true;
        }

        if (invocation.ArgumentList.Arguments.Count != 0 ||
            access.Name is not GenericNameSyntax genericName ||
            genericName.TypeArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        filter = new DotsQueryFilter(
            name,
            genericName.TypeArgumentList.Arguments
                .Select(type => type.WithoutTrivia().ToFullString())
                .ToImmutableArray(),
            argument: null);
        return true;
    }

    internal static bool TryCreateJobData(
        CSharpSyntaxNode body,
        ImmutableArray<ISymbol> allowedParameters,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out BlockSyntax jobBody,
        out ImmutableArray<DotsJobField> jobFields)
    {
        jobBody = CreateBlock(body);
        jobFields = ImmutableArray<DotsJobField>.Empty;
        // Generic method names are GenericNameSyntax, so the identifier-based
        // unsupported-SystemAPI check below does not see calls such as
        // GetComponentRW<T>. HasComponent<T> and Exists are the only invocations
        // explicitly lowered by this conversion.
        if (body.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Select(invocation =>
                semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol)
            .Any(method =>
                method is not null &&
                method.Name != "HasComponent" &&
                method.Name != "Exists" &&
                method.ContainingType.ToDisplayString() == "Unity.Entities.SystemAPI"))
        {
            return false;
        }

        if (body.DescendantNodesAndSelf().Any(node =>
                node is AnonymousFunctionExpressionSyntax ||
                node is LocalFunctionStatementSyntax ||
                node is ThisExpressionSyntax ||
                node is BaseExpressionSyntax))
        {
            return false;
        }

        var systemTimeAccesses = body.DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(access => TryGetSystemTimeMember(
                access,
                semanticModel,
                cancellationToken,
                out _))
            .ToImmutableArray();
        var replacedTimeMemberTokens = systemTimeAccesses
            .Select(access => access.Name.Identifier)
            .ToImmutableHashSet();
        var usedNames = body.DescendantTokens()
            .Where(token => token.IsKind(SyntaxKind.IdentifierToken))
            .Where(token => !replacedTimeMemberTokens.Contains(token))
            .Select(token => token.ValueText)
            .Concat(allowedParameters.Select(parameter => parameter.Name))
            .ToImmutableHashSet(StringComparer.Ordinal);
        var fields = ImmutableArray.CreateBuilder<DotsJobField>();
        var synthesizedLocalNames = ImmutableHashSet.Create<string>(StringComparer.Ordinal);
        var capturedFieldNames = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
        var replacements = new Dictionary<SyntaxNode, string>();
        var bodySpan = body.Span;

        var componentLookupType = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "Unity.Entities.ComponentLookup`1");
        var entityStorageInfoLookupType = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "Unity.Entities.EntityStorageInfoLookup");
        var hasComponentCalls = body.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Select(invocation => TryGetHasComponentCall(
                    invocation,
                    semanticModel,
                    cancellationToken,
                    out var componentType,
                    out var entityExpression)
                ? new HasComponentCall(invocation, componentType, entityExpression)
                : null)
            .Where(call => call is not null)
            .Cast<HasComponentCall>()
            .ToImmutableArray();

        // A SystemAPI.HasComponent-shaped expression which did not bind to the one supported
        // overload must not silently become an ordinary capture or survive in the generated job.
        if (body.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any(invocation =>
                LooksLikeHasComponent(invocation, semanticModel, cancellationToken) &&
                !hasComponentCalls.Any(call => call.Invocation == invocation)) ||
            (hasComponentCalls.Length != 0 && componentLookupType is null))
        {
            return false;
        }

        var existsCalls = body.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => TryGetExistsCall(
                invocation,
                semanticModel,
                cancellationToken,
                out _))
            .ToImmutableArray();
        if (body.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any(invocation =>
                LooksLikeSystemApiCall(invocation, "Exists", semanticModel, cancellationToken) &&
                !existsCalls.Contains(invocation)) ||
            (existsCalls.Length != 0 && entityStorageInfoLookupType is null))
        {
            return false;
        }

        foreach (var identifier in body.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
            if (symbol is ILocalSymbol or IParameterSymbol)
            {
                if (allowedParameters.Any(parameter =>
                        SymbolEqualityComparer.Default.Equals(parameter, symbol)) ||
                    symbol.Locations.Any(location =>
                        location.IsInSource && bodySpan.Contains(location.SourceSpan)))
                {
                    continue;
                }

                var captureType = symbol switch
                {
                    ILocalSymbol local => local.Type,
                    IParameterSymbol parameter => parameter.Type,
                    _ => null,
                };
                if (captureType is null ||
                    !captureType.IsUnmanagedType ||
                    symbol is IParameterSymbol { RefKind: not RefKind.None } ||
                    IsWrittenByReference(identifier))
                {
                    return false;
                }

                if (!capturedFieldNames.TryGetValue(symbol, out var fieldName))
                {
                    fieldName = CreateUniqueJobFieldName(symbol.Name, usedNames);
                    usedNames = usedNames.Add(fieldName);
                    capturedFieldNames.Add(symbol, fieldName);
                    fields.Add(new DotsJobField(
                        fieldName,
                        captureType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        EscapeIdentifier(symbol.Name),
                        symbol));
                }

                replacements.Add(identifier, fieldName);
                continue;
            }

            if (symbol is IFieldSymbol { IsStatic: false } or
                IPropertySymbol { IsStatic: false })
            {
                var isMemberAccessName =
                    identifier.Parent is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name == identifier;
                var isObjectInitializerMember =
                    identifier.Parent is AssignmentExpressionSyntax assignment &&
                    assignment.Left == identifier &&
                    assignment.Parent is InitializerExpressionSyntax;
                if (!isMemberAccessName && !isObjectInitializerMember)
                {
                    var captureType = symbol switch
                    {
                        IFieldSymbol capturedField => capturedField.Type,
                        IPropertySymbol capturedProperty => capturedProperty.Type,
                        _ => throw new InvalidOperationException(),
                    };
                    if (!captureType.IsUnmanagedType || IsWrittenByReference(identifier))
                    {
                        return false;
                    }

                    if (!capturedFieldNames.TryGetValue(symbol, out var fieldName))
                    {
                        fieldName = CreateUniqueJobFieldName(symbol.Name, usedNames);
                        usedNames = usedNames.Add(fieldName);
                        capturedFieldNames.Add(symbol, fieldName);
                        fields.Add(new DotsJobField(
                            fieldName,
                            captureType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            "this." + EscapeIdentifier(symbol.Name),
                            symbol));
                    }

                    replacements.Add(identifier, fieldName);
                }

                continue;
            }

            if (symbol is IMethodSymbol { IsStatic: false })
            {
                var isMemberAccessName =
                    identifier.Parent is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name == identifier;
                if (!isMemberAccessName)
                {
                    return false;
                }
            }
        }

        foreach (var access in systemTimeAccesses)
        {
            TryGetSystemTimeMember(
                access,
                semanticModel,
                cancellationToken,
                out var timeMember);
            var existingField = fields.FirstOrDefault(field =>
                field.Initializer == "Unity.Entities.SystemAPI.Time." + timeMember);
            var fieldName = existingField?.Name;
            if (fieldName is null)
            {
                fieldName = CreateUniqueJobFieldName(timeMember, usedNames);
                usedNames = usedNames.Add(fieldName);
                fields.Add(new DotsJobField(
                    fieldName,
                    timeMember == "ElapsedTime" ? "double" : "float",
                    "Unity.Entities.SystemAPI.Time." + timeMember));
            }

            replacements.Add(access, fieldName);
        }

        foreach (var group in hasComponentCalls.GroupBy<HasComponentCall, ITypeSymbol>(
                     call => call.ComponentType,
                     SymbolEqualityComparer.Default))
        {
            var componentType = group.Key;
            var componentName = componentType.Name;
            var fieldName = CreateUniqueJobFieldName(componentName + "Lookup", usedNames);
            usedNames = usedNames.Add(fieldName);

            var localName = TryFindCompatibleLookupLocal(
                body,
                componentType,
                semanticModel,
                cancellationToken);
            string? declaration = null;
            if (localName is null)
            {
                var localNames = GetContainingMemberNames(body)
                    .Concat(usedNames)
                    .Concat(synthesizedLocalNames)
                    .ToImmutableHashSet(StringComparer.Ordinal);
                localName = CreateUniqueLocalName(
                    char.ToLowerInvariant(componentName[0]) + componentName.Substring(1) + "Lookup",
                    localNames);
                synthesizedLocalNames = synthesizedLocalNames.Add(localName);
                declaration =
                    "var " + localName + " = Unity.Entities.SystemAPI.GetComponentLookup<" +
                    componentType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">(true);";
            }

            fields.Add(new DotsJobField(
                fieldName,
                "Unity.Entities.ComponentLookup<" +
                componentType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">",
                EscapeIdentifier(localName),
                preJobDeclaration: declaration)
            {
                IsReadOnly = true,
            });

            foreach (var call in group)
            {
                var nestedReplacements = replacements.Keys
                    .Where(node => call.EntityExpression.Span.Contains(node.Span))
                    .ToImmutableArray();
                var entityExpression = call.EntityExpression.ReplaceNodes(
                    nestedReplacements,
                    (original, _) => SyntaxFactory.IdentifierName(replacements[original])
                        .WithTriviaFrom(original));
                replacements.Add(
                    call.Invocation,
                    fieldName + ".HasComponent(" + entityExpression.WithoutTrivia().ToFullString() + ")");
            }
        }

        if (!existsCalls.IsEmpty)
        {
            var fieldName = CreateUniqueJobFieldName("EntityStorageInfoLookup", usedNames);
            usedNames = usedNames.Add(fieldName);
            fields.Add(new DotsJobField(
                fieldName,
                "Unity.Entities.EntityStorageInfoLookup",
                "Unity.Entities.SystemAPI.GetEntityStorageInfoLookup()")
            {
                IsReadOnly = true,
            });

            foreach (var call in existsCalls)
            {
                var entityExpression = call.ArgumentList.Arguments[0].Expression;
                var nestedReplacements = replacements.Keys
                    .Where(node => entityExpression.Span.Contains(node.Span))
                    .ToImmutableArray();
                entityExpression = entityExpression.ReplaceNodes(
                    nestedReplacements,
                    (original, _) => SyntaxFactory.IdentifierName(replacements[original])
                        .WithTriviaFrom(original));
                replacements.Add(
                    call,
                    fieldName + ".Exists(" + entityExpression.WithoutTrivia().ToFullString() + ")");
            }
        }

        var unsupportedSystemApiAccess = body.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier =>
            {
                var symbol = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
                if (symbol?.ContainingType?.ToDisplayString() != "Unity.Entities.SystemAPI")
                {
                    return false;
                }

                return !identifier.AncestorsAndSelf()
                    .OfType<MemberAccessExpressionSyntax>()
                    .Any(access => replacements.ContainsKey(access)) &&
                    !identifier.AncestorsAndSelf()
                        .OfType<InvocationExpressionSyntax>()
                        .Any(invocation => replacements.ContainsKey(invocation));
            });
        if (unsupportedSystemApiAccess)
        {
            return false;
        }

        jobBody = CreateBlock(
            body.ReplaceNodes(
                replacements.Keys.Where(node => !replacements.Keys.Any(parent =>
                    parent != node && parent.Span.Contains(node.Span))),
                (original, _) => (original is InvocationExpressionSyntax
                        ? SyntaxFactory.ParseExpression(replacements[original])
                        : SyntaxFactory.IdentifierName(replacements[original]))
                    .WithTriviaFrom(original)));
        jobFields = fields.ToImmutable();
        return true;
    }

    internal static bool HasUnsupportedCaptures(
        CSharpSyntaxNode body,
        ImmutableArray<ISymbol> allowedParameters,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (body.DescendantNodesAndSelf().Any(node =>
                node is AnonymousFunctionExpressionSyntax ||
                node is LocalFunctionStatementSyntax ||
                node is ThisExpressionSyntax ||
                node is BaseExpressionSyntax))
        {
            return true;
        }

        var bodySpan = body.Span;
        foreach (var identifier in body.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
            if (symbol is ILocalSymbol or IParameterSymbol)
            {
                if (allowedParameters.Any(parameter => SymbolEqualityComparer.Default.Equals(parameter, symbol)))
                {
                    continue;
                }

                if (!symbol.Locations.Any(location =>
                        location.IsInSource && bodySpan.Contains(location.SourceSpan)))
                {
                    return true;
                }
            }

            if (symbol is IFieldSymbol { IsStatic: false } or IPropertySymbol { IsStatic: false } or IMethodSymbol { IsStatic: false })
            {
                var isMemberAccessName =
                    identifier.Parent is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name == identifier;
                var isObjectInitializerMember =
                    identifier.Parent is AssignmentExpressionSyntax assignment &&
                    assignment.Left == identifier &&
                    assignment.Parent is InitializerExpressionSyntax;
                if (!isMemberAccessName && !isObjectInitializerMember)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed class HasComponentCall
    {
        internal HasComponentCall(
            InvocationExpressionSyntax invocation,
            ITypeSymbol componentType,
            ExpressionSyntax entityExpression)
        {
            Invocation = invocation;
            ComponentType = componentType;
            EntityExpression = entityExpression;
        }

        internal InvocationExpressionSyntax Invocation { get; }

        internal ITypeSymbol ComponentType { get; }

        internal ExpressionSyntax EntityExpression { get; }
    }

    private static bool TryGetHasComponentCall(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ITypeSymbol componentType,
        out ExpressionSyntax entityExpression)
    {
        componentType = null!;
        entityExpression = null!;
        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        if (method is null ||
            method.Name != "HasComponent" ||
            method.ContainingType?.ToDisplayString() != "Unity.Entities.SystemAPI" ||
            !method.IsStatic ||
            method.TypeArguments.Length != 1 ||
            method.Parameters.Length != 1 ||
            method.Parameters[0].RefKind != RefKind.None ||
            method.Parameters[0].Type.ToDisplayString() != "Unity.Entities.Entity" ||
            method.ReturnType.SpecialType != SpecialType.System_Boolean ||
            invocation.ArgumentList.Arguments.Count != 1 ||
            !invocation.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.None))
        {
            return false;
        }

        componentType = method.TypeArguments[0];
        if (componentType.TypeKind is TypeKind.Error or TypeKind.TypeParameter)
        {
            return false;
        }

        entityExpression = invocation.ArgumentList.Arguments[0].Expression;
        if (entityExpression.ContainsDiagnostics ||
            entityExpression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(identifier =>
                semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol
                    ?.ContainingType?.ToDisplayString() == "Unity.Entities.SystemAPI"))
        {
            return false;
        }

        return true;
    }

    private static bool TryGetExistsCall(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExpressionSyntax entityExpression)
    {
        entityExpression = null!;
        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        if (method is null ||
            method.Name != "Exists" ||
            method.ContainingType?.ToDisplayString() != "Unity.Entities.SystemAPI" ||
            !method.IsStatic ||
            method.Parameters.Length != 1 ||
            method.Parameters[0].RefKind != RefKind.None ||
            method.Parameters[0].Type.ToDisplayString() != "Unity.Entities.Entity" ||
            method.ReturnType.SpecialType != SpecialType.System_Boolean ||
            invocation.ArgumentList.Arguments.Count != 1 ||
            !invocation.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.None))
        {
            return false;
        }

        entityExpression = invocation.ArgumentList.Arguments[0].Expression;
        return !entityExpression.ContainsDiagnostics;
    }

    private static bool LooksLikeSystemApiCall(
        InvocationExpressionSyntax invocation,
        string methodName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var name = invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax { Name: IdentifierNameSyntax identifier } =>
                identifier.Identifier.ValueText,
            _ => string.Empty,
        };
        if (name != methodName)
        {
            return false;
        }

        var symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellationToken);
        if (symbolInfo.Symbol?.ContainingType?.ToDisplayString() == "Unity.Entities.SystemAPI" ||
            symbolInfo.CandidateSymbols.Any(symbol =>
                symbol.ContainingType?.ToDisplayString() == "Unity.Entities.SystemAPI"))
        {
            return true;
        }

        return invocation.Expression is MemberAccessExpressionSyntax member &&
               member.Expression.ToString().EndsWith("SystemAPI", StringComparison.Ordinal);
    }

    private static bool LooksLikeHasComponent(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var name = invocation.Expression switch
        {
            GenericNameSyntax generic => generic.Identifier.ValueText,
            MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } =>
                generic.Identifier.ValueText,
            _ => string.Empty,
        };
        if (name != "HasComponent")
        {
            return false;
        }

        var symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellationToken);
        if (symbolInfo.Symbol?.ContainingType?.ToDisplayString() == "Unity.Entities.SystemAPI" ||
            symbolInfo.CandidateSymbols.Any(symbol =>
                symbol.ContainingType?.ToDisplayString() == "Unity.Entities.SystemAPI"))
        {
            return true;
        }

        return invocation.Expression is MemberAccessExpressionSyntax member &&
               member.Expression.ToString().EndsWith("SystemAPI", StringComparison.Ordinal);
    }

    private static string? TryFindCompatibleLookupLocal(
        CSharpSyntaxNode body,
        ITypeSymbol componentType,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var lookupDefinition = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "Unity.Entities.ComponentLookup`1");
        foreach (var local in semanticModel.LookupSymbols(body.SpanStart).OfType<ILocalSymbol>())
        {
            if (local.Type is not INamedTypeSymbol namedType ||
                lookupDefinition is null ||
                !SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, lookupDefinition) ||
                namedType.TypeArguments.Length != 1 ||
                !SymbolEqualityComparer.Default.Equals(namedType.TypeArguments[0], componentType))
            {
                continue;
            }

            var declarator = local.DeclaringSyntaxReferences.FirstOrDefault()
                ?.GetSyntax(cancellationToken) as VariableDeclaratorSyntax;
            if (declarator?.Initializer?.Value is not InvocationExpressionSyntax initializer ||
                semanticModel.GetSymbolInfo(initializer, cancellationToken).Symbol is not IMethodSymbol method ||
                method.Name != "GetComponentLookup" ||
                method.ContainingType?.ToDisplayString() != "Unity.Entities.SystemAPI" ||
                method.TypeArguments.Length != 1 ||
                !SymbolEqualityComparer.Default.Equals(method.TypeArguments[0], componentType) ||
                initializer.ArgumentList.Arguments.Count != 1 ||
                semanticModel.GetConstantValue(
                    initializer.ArgumentList.Arguments[0].Expression,
                    cancellationToken) is not { HasValue: true, Value: true })
            {
                continue;
            }

            return local.Name;
        }

        return null;
    }

    private static IEnumerable<string> GetContainingMemberNames(CSharpSyntaxNode body) =>
        body.FirstAncestorOrSelf<MemberDeclarationSyntax>()?
            .DescendantTokens()
            .Where(token => token.IsKind(SyntaxKind.IdentifierToken))
            .Select(token => token.ValueText) ?? Enumerable.Empty<string>();

    private static string CreateUniqueLocalName(string baseName, ImmutableHashSet<string> usedNames)
    {
        var candidate = baseName;
        for (var suffix = 2; usedNames.Contains(candidate); suffix++)
        {
            candidate = baseName + suffix;
        }

        return candidate;
    }

    private static bool TryGetSystemTimeMember(
        MemberAccessExpressionSyntax access,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out string member)
    {
        member = access.Name.Identifier.ValueText;
        if ((member != "ElapsedTime" && member != "DeltaTime") ||
            access.Expression is not MemberAccessExpressionSyntax timeAccess ||
            timeAccess.Name.Identifier.ValueText != "Time")
        {
            return false;
        }

        var receiver = semanticModel.GetSymbolInfo(
            timeAccess.Expression,
            cancellationToken).Symbol as INamedTypeSymbol;
        return receiver?.ToDisplayString() == "Unity.Entities.SystemAPI";
    }

    private static bool IsWrittenByReference(IdentifierNameSyntax identifier)
    {
        for (SyntaxNode? current = identifier; current is ExpressionSyntax; current = current.Parent)
        {
            if (current.Parent is AssignmentExpressionSyntax assignment &&
                assignment.Left == current)
            {
                return true;
            }

            if (current.Parent is PrefixUnaryExpressionSyntax prefix &&
                (prefix.IsKind(SyntaxKind.PreIncrementExpression) ||
                 prefix.IsKind(SyntaxKind.PreDecrementExpression)))
            {
                return true;
            }

            if (current.Parent is PostfixUnaryExpressionSyntax postfix &&
                (postfix.IsKind(SyntaxKind.PostIncrementExpression) ||
                 postfix.IsKind(SyntaxKind.PostDecrementExpression)))
            {
                return true;
            }

            // Captured unmanaged values are copied into fields on both the original
            // lambda job and the generated IJobEntity. Passing that field by `ref`
            // (or `in`) therefore preserves the original behavior and is required by
            // APIs such as CollisionWorld queries and NativeContainer helpers. An
            // `out` argument is different: it definitely replaces the capture and is
            // intentionally kept unsupported with the other direct writes above.
            if (current.Parent is ArgumentSyntax argument &&
                argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword))
            {
                return true;
            }

            if (current.Parent is RefExpressionSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static string CreateUniqueJobFieldName(
        string sourceName,
        ImmutableHashSet<string> usedNames)
    {
        var baseName = sourceName.Length == 0
            ? "Value"
            : char.ToUpperInvariant(sourceName[0]) + sourceName.Substring(1);
        var candidate = baseName;
        for (var suffix = 2; usedNames.Contains(candidate); suffix++)
        {
            candidate = baseName + suffix;
        }

        return candidate;
    }

    private static string EscapeIdentifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;

    internal static BlockSyntax CreateBlock(CSharpSyntaxNode body) => body switch
    {
        BlockSyntax block => block,
        ExpressionSyntax expression => SyntaxFactory.Block(SyntaxFactory.ExpressionStatement(expression)),
        StatementSyntax statement => statement as BlockSyntax ?? SyntaxFactory.Block(statement),
        _ => SyntaxFactory.Block(),
    };

    internal static string CreateUniqueNestedTypeName(TypeDeclarationSyntax containingType, string baseName)
    {
        var existingNames = containingType.Members
            .SelectMany(member => member switch
            {
                BaseTypeDeclarationSyntax type => new[] { type.Identifier.ValueText },
                _ => Array.Empty<string>(),
            })
            .ToImmutableHashSet(StringComparer.Ordinal);
        var candidate = baseName;
        for (var suffix = 2; existingNames.Contains(candidate); suffix++)
        {
            candidate = baseName + suffix;
        }

        return candidate;
    }

    private static bool IsUnityEntitiesNamespace(INamespaceSymbol? namespaceSymbol)
    {
        var name = namespaceSymbol?.ToDisplayString();
        return name == "Unity.Entities" || name?.StartsWith("Unity.Entities.", StringComparison.Ordinal) == true;
    }
}
