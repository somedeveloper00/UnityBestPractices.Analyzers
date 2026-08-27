using UnityBestPractices.Analyzers.Infrastructure;
using UnityBestPractices.Analyzers.Rules.Core;
using UnityBestPractices.Analyzers.Rules.Correctness;
using UnityBestPractices.Analyzers.Rules.Expressions;
using UnityBestPractices.Analyzers.Rules.Dots;
using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UnityBestPractices.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnityBestPracticesAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor EncapsulateSerializedFieldRule =
        DiagnosticCatalog.Get(DiagnosticIds.EncapsulateSerializedField).Descriptor;
    private static readonly DiagnosticDescriptor YieldNullRule =
        DiagnosticCatalog.Get(DiagnosticIds.YieldNull).Descriptor;
    private static readonly DiagnosticDescriptor SquaredMagnitudeRule =
        DiagnosticCatalog.Get(DiagnosticIds.UseSquaredMagnitude).Descriptor;
    private static readonly DiagnosticDescriptor BurstCompileRule =
        DiagnosticCatalog.Get(DiagnosticIds.AddBurstCompile).Descriptor;
    private static readonly DiagnosticDescriptor ReadOnlyNativeArrayRule =
        DiagnosticCatalog.Get(DiagnosticIds.MarkNativeArrayReadOnly).Descriptor;
    private static readonly DiagnosticDescriptor StackallocRule =
        DiagnosticCatalog.Get(DiagnosticIds.UseStackalloc).Descriptor;
    private static readonly DiagnosticDescriptor RefLocalRule =
        DiagnosticCatalog.Get(DiagnosticIds.UseRefLocal).Descriptor;
    private static readonly DiagnosticDescriptor CacheCameraMainRule =
        DiagnosticCatalog.Get(DiagnosticIds.CacheCameraMain).Descriptor;
    private static readonly DiagnosticDescriptor PreallocateListRule =
        DiagnosticCatalog.Get(DiagnosticIds.PreallocateList).Descriptor;
    private static readonly DiagnosticDescriptor MultiplySquareRule =
        DiagnosticCatalog.Get(DiagnosticIds.UseMultiplicationForSquare).Descriptor;
    private static readonly DiagnosticDescriptor UninitializedNativeArrayRule =
        DiagnosticCatalog.Get(DiagnosticIds.UseUninitializedNativeArray).Descriptor;

    // Catalog is authoritative; keep static field descriptors above for zero-allocation report sites.
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        DiagnosticCatalog.All.Select(metadata => metadata.Descriptor).ToImmutableArray();

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            _ = UnitySymbolCache.For(startContext.Compilation);
            startContext.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
            startContext.RegisterSyntaxNodeAction(AnalyzeYieldStatement, SyntaxKind.YieldReturnStatement);
            startContext.RegisterSyntaxNodeAction(AnalyzeStructDeclaration, SyntaxKind.StructDeclaration);
            startContext.RegisterSyntaxNodeAction(AnalyzeVariableDeclarator, SyntaxKind.VariableDeclarator);
            startContext.RegisterSyntaxNodeAction(AnalyzeLocalDeclaration, SyntaxKind.LocalDeclarationStatement);
            startContext.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
            startContext.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
            startContext.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
            startContext.RegisterSyntaxNodeAction(
                AdvancedUnityRules.AnalyzeReturn,
                SyntaxKind.ReturnStatement);
            startContext.RegisterSyntaxNodeAction(
                AdvancedUnityRules.AnalyzeAssignment,
                SyntaxKind.SimpleAssignmentExpression);
            startContext.RegisterSyntaxNodeAction(
                AdvancedUnityRules.AnalyzeExpressionStatement,
                SyntaxKind.ExpressionStatement);
            startContext.RegisterSyntaxNodeAction(
                AdvancedUnityRules.AnalyzeTypeDeclaration,
                SyntaxKind.ClassDeclaration,
                SyntaxKind.StructDeclaration);
            startContext.RegisterSyntaxNodeAction(
                AnalyzeExpressionQuickFix,
                ExpressionQuickFixRegistry.SyntaxKinds.ToArray());
            startContext.RegisterSyntaxNodeAction(
                DotsQueryRules.AnalyzeExpressionStatement,
                SyntaxKind.ExpressionStatement);
            startContext.RegisterSyntaxNodeAction(
                DotsQueryRules.AnalyzeSystemApiQuery,
                SyntaxKind.ForEachStatement,
                SyntaxKind.ForEachVariableStatement);
            startContext.RegisterSyntaxNodeAction(
                UnusedEntityAccessRule.Analyze,
                SyntaxKind.ForEachVariableStatement);
            startContext.RegisterSyntaxNodeAction(
                AnalyzeRelationalExpression,
                SyntaxKind.LessThanExpression,
                SyntaxKind.LessThanOrEqualExpression,
                SyntaxKind.GreaterThanExpression,
                SyntaxKind.GreaterThanOrEqualExpression);
            startContext.RegisterCompilationEndAction(NamespaceConsistencyRules.AnalyzeCompilation);
        });
    }

    private static void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context)
    {
        var declaration = (FieldDeclarationSyntax)context.Node;
        if (IsEncapsulatableSerializedField(declaration, context.SemanticModel, context.CancellationToken))
        {
            var variable = declaration.Declaration.Variables[0];
            Report(
                context,
                EncapsulateSerializedFieldRule,
                variable.Identifier.GetLocation(),
                variable.Identifier.ValueText);
        }

        if (IsReadOnlyNativeArrayCandidate(declaration, context.SemanticModel, context.CancellationToken))
        {
            var variable = declaration.Declaration.Variables[0];
            Report(
                context,
                ReadOnlyNativeArrayRule,
                variable.Identifier.GetLocation(),
                variable.Identifier.ValueText);
        }
    }

    private static void AnalyzeYieldStatement(SyntaxNodeAnalysisContext context)
    {
        var yieldStatement = (YieldStatementSyntax)context.Node;
        if (!IsBoxedNextFrameYield(yieldStatement, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        Report(context, YieldNullRule, yieldStatement.Expression!.GetLocation());
    }

    private static void AnalyzeRelationalExpression(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (!TryGetMagnitudeComparison(
                binary,
                context.SemanticModel,
                context.CancellationToken,
                out _,
                out _))
        {
            return;
        }

        Report(context, SquaredMagnitudeRule, binary.GetLocation());
    }

    private static void AnalyzeStructDeclaration(SyntaxNodeAnalysisContext context)
    {
        var declaration = (StructDeclarationSyntax)context.Node;
        if (!CanAddBurstCompile(declaration, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        Report(context, BurstCompileRule, declaration.Identifier.GetLocation(), declaration.Identifier.ValueText);
    }

    private static void AnalyzeVariableDeclarator(SyntaxNodeAnalysisContext context)
    {
        var declarator = (VariableDeclaratorSyntax)context.Node;
        var configuration = AnalyzerConfiguration.For(context);
        if (!CanUseStackalloc(
                declarator,
                context.SemanticModel,
                context.CancellationToken,
                out _,
                configuration.MaxStackallocBytes))
        {
            return;
        }

        Report(context, StackallocRule, declarator.Initializer!.Value.GetLocation());
    }

    private static void AnalyzeLocalDeclaration(SyntaxNodeAnalysisContext context)
    {
        AdvancedUnityRules.AnalyzeLocalDeclaration(context);
        var declaration = (LocalDeclarationStatementSyntax)context.Node;
        if (!TryGetCopyBackPattern(
                declaration,
                context.SemanticModel,
                context.CancellationToken,
                out var pattern))
        {
            return;
        }

        Report(context, RefLocalRule, pattern.Variable.Identifier.GetLocation(), pattern.Variable.Identifier.ValueText);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        if (!TryGetRepeatedCameraMain(
                memberAccess,
                context.SemanticModel,
                context.CancellationToken,
                out _,
                out var accesses,
                out _,
                out _))
        {
            return;
        }

        Report(context, CacheCameraMainRule, memberAccess.GetLocation(), accesses.Length);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        var configuration = AnalyzerConfiguration.For(context);
        if (TryGetListPreallocation(
                creation,
                context.SemanticModel,
                context.CancellationToken,
                out var addCount,
                configuration.MinimumListAdds))
        {
            Report(context, PreallocateListRule, creation.GetLocation(), addCount);
        }

        if (TryGetNativeArrayInitialization(
                creation,
                context.SemanticModel,
                context.CancellationToken,
                out _))
        {
            Report(context, UninitializedNativeArrayRule, creation.GetLocation());
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        AdvancedUnityRules.AnalyzeInvocation(context);
        var invocation = (InvocationExpressionSyntax)context.Node;
        ModernObjectFindRule.AnalyzeInvocation(context, invocation);
        if (!TryGetMathfSquare(
                invocation,
                context.SemanticModel,
                context.CancellationToken,
                out var value))
        {
            return;
        }

        Report(context, MultiplySquareRule, invocation.GetLocation(), value.ToString());
    }

    private static void AnalyzeExpressionQuickFix(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ExpressionSyntax expression)
        {
            return;
        }

        foreach (var rule in ExpressionQuickFixRegistry.ForSyntaxKind(expression.Kind()))
        {
            if (!rule.TryGetReplacement(
                    expression,
                    context.SemanticModel,
                    context.CancellationToken,
                    out _))
            {
                continue;
            }

            Report(context, rule.Descriptor, expression.GetLocation());
            return;
        }
    }

    private static void Report(
        SyntaxNodeAnalysisContext context,
        DiagnosticDescriptor descriptor,
        Location location,
        params object[] messageArguments)
    {
        if (AnalyzerConfiguration.For(context).IsRuleEnabled(descriptor.Id))
        {
            context.ReportDiagnostic(Diagnostic.Create(descriptor, location, messageArguments));
        }
    }

    internal static bool CanAddBurstCompile(
        StructDeclarationSyntax declaration,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        var jobType = semanticModel.GetDeclaredSymbol(declaration, cancellationToken);
        var burstCompile = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "Unity.Burst.BurstCompileAttribute");
        return jobType is not null &&
               burstCompile is not null &&
               UnitySymbolCache.IsUnityJobType(jobType) &&
               !HasAttribute(jobType, burstCompile);
    }

    internal static bool IsReadOnlyNativeArrayCandidate(
        FieldDeclarationSyntax declaration,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        if (declaration.Declaration.Variables.Count != 1 ||
            declaration.Modifiers.Any(SyntaxKind.StaticKeyword) ||
            declaration.Modifiers.Any(SyntaxKind.ConstKeyword))
        {
            return false;
        }

        var field = semanticModel.GetDeclaredSymbol(declaration.Declaration.Variables[0], cancellationToken) as IFieldSymbol;
        var nativeArray = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "Unity.Collections.NativeArray`1");
        var readOnlyAttribute = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "Unity.Collections.ReadOnlyAttribute");
        if (field?.Type is not INamedTypeSymbol fieldType ||
            nativeArray is null ||
            readOnlyAttribute is null ||
            !SymbolEqualityComparer.Default.Equals(fieldType.OriginalDefinition, nativeArray) ||
            !UnitySymbolCache.IsUnityJobType(field.ContainingType) ||
            HasAttribute(field, readOnlyAttribute))
        {
            return false;
        }

        var typeDeclaration = declaration.FirstAncestorOrSelf<StructDeclarationSyntax>();
        if (typeDeclaration is null)
        {
            return false;
        }

        var references = typeDeclaration.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier => SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                field))
            .ToArray();
        return references.Length != 0 &&
               references.Any(reference => reference.FirstAncestorOrSelf<MethodDeclarationSyntax>()?.Identifier.ValueText == "Execute") &&
               references.All(IsSafeNativeArrayRead);
    }

    internal static bool CanUseStackalloc(
        VariableDeclaratorSyntax declarator,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out ArrayCreationExpressionSyntax arrayCreation,
        int maxStackallocBytes = AnalyzerConfiguration.DefaultMaxStackallocBytes)
    {
        arrayCreation = null!;
        if (declarator.Initializer?.Value is not ArrayCreationExpressionSyntax candidate)
        {
            return false;
        }

        if (declarator.Parent is not VariableDeclarationSyntax variableDeclaration ||
            variableDeclaration.Parent is not LocalDeclarationStatementSyntax localDeclaration)
        {
            return false;
        }

        if (IsInsideLoop(localDeclaration) ||
            ContainsMeaningfulTrivia(candidate.DescendantTrivia(descendIntoTrivia: true)))
        {
            return false;
        }

        var local = semanticModel.GetDeclaredSymbol(declarator, cancellationToken) as ILocalSymbol;
        if (local?.Type is not INamedTypeSymbol localType ||
            !localType.IsGenericType ||
            localType.TypeArguments.Length != 1)
        {
            return false;
        }

        var originalType = localType.OriginalDefinition.ToDisplayString();
        if (originalType != "System.Span<T>" && originalType != "System.ReadOnlySpan<T>")
        {
            return false;
        }

        // Managed arrays are zero-initialized. The fix preserves that behavior by
        // inserting Span.Clear(), which is unavailable on ReadOnlySpan<T>.
        if (candidate.Initializer is null && originalType != "System.Span<T>")
        {
            return false;
        }

        if (candidate.Initializer is null && localDeclaration.Parent is not BlockSyntax)
        {
            return false;
        }

        var elementType = semanticModel.GetTypeInfo(candidate.Type.ElementType, cancellationToken).Type;
        if (elementType is null ||
            !SymbolEqualityComparer.Default.Equals(elementType, localType.TypeArguments[0]) ||
            !TryGetArrayLength(candidate, semanticModel, cancellationToken, out var length) ||
            !TryGetStackElementSize(elementType, out var elementSize) ||
            length <= 0 ||
            (long)length * elementSize > maxStackallocBytes)
        {
            return false;
        }

        if (SpanEscapesDeclaringMethod(local, localDeclaration, semanticModel, cancellationToken))
        {
            return false;
        }

        arrayCreation = candidate;
        return true;
    }

    private static bool SpanEscapesDeclaringMethod(
        ILocalSymbol local,
        LocalDeclarationStatementSyntax declaration,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        var scope = declaration.Ancestors().FirstOrDefault(node =>
            node is BaseMethodDeclarationSyntax ||
            node is LocalFunctionStatementSyntax ||
            node is AnonymousFunctionExpressionSyntax ||
            node is AccessorDeclarationSyntax) ?? declaration.Parent;
        if (scope is null)
        {
            return true;
        }

        return scope.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier => SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                local))
            .Any(reference => IsSpanEscape(reference, semanticModel, cancellationToken));
    }

    private static bool IsSpanEscape(
        IdentifierNameSyntax reference,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        // A stackalloc span cannot leave its method: returning a span-typed value
        // derived from the local, handing the local out by ref, or storing it into a
        // ref or out parameter would no longer compile.
        var returnStatement = reference.FirstAncestorOrSelf<ReturnStatementSyntax>();
        if (returnStatement?.Expression is not null &&
            semanticModel.GetTypeInfo(returnStatement.Expression, cancellationToken).Type?.IsRefLikeType == true)
        {
            return true;
        }

        var argument = reference.FirstAncestorOrSelf<ArgumentSyntax>();
        if (argument is not null &&
            argument.Expression.Span.Contains(reference.Span) &&
            !argument.RefKindKeyword.IsKind(SyntaxKind.None))
        {
            return true;
        }

        for (SyntaxNode? current = reference; current is ExpressionSyntax; current = current.Parent)
        {
            if (current.Parent is RefExpressionSyntax)
            {
                return true;
            }

            if (current.Parent is AssignmentExpressionSyntax assignment &&
                assignment.Right == current &&
                semanticModel.GetTypeInfo(assignment.Right, cancellationToken).Type?.IsRefLikeType == true &&
                semanticModel.GetSymbolInfo(assignment.Left, cancellationToken).Symbol is IParameterSymbol
                {
                    RefKind: not RefKind.None,
                })
            {
                return true;
            }
        }

        return false;
    }

    internal static bool TryGetCopyBackPattern(
        LocalDeclarationStatementSyntax declaration,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out CopyBackPattern pattern)
    {
        pattern = null!;
        if (declaration.Declaration.Variables.Count != 1 ||
            declaration.Modifiers.Count != 0 ||
            declaration.Declaration.Variables[0].Initializer?.Value is not ElementAccessExpressionSyntax elementAccess)
        {
            return false;
        }

        if (elementAccess.Expression is not IdentifierNameSyntax containerIdentifier ||
            elementAccess.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        if (declaration.Parent is not BlockSyntax block ||
            ContainsMeaningfulTrivia(declaration.DescendantTrivia(descendIntoTrivia: true)) ||
            IsInAsyncOrIteratorMethod(declaration))
        {
            return false;
        }

        var variable = declaration.Declaration.Variables[0];
        var local = semanticModel.GetDeclaredSymbol(variable, cancellationToken) as ILocalSymbol;
        if (local is null ||
            !local.Type.IsValueType ||
            local.Type.TypeKind == TypeKind.Enum ||
            local.Type.SpecialType != SpecialType.None ||
            !TryCreateRefTarget(elementAccess, semanticModel, cancellationToken, out var refTarget))
        {
            return false;
        }

        var declarationIndex = block.Statements.IndexOf(declaration);
        if (declarationIndex < 0)
        {
            return false;
        }

        ExpressionStatementSyntax? writeBack = null;
        var writeBackIndex = -1;
        for (var index = declarationIndex + 1; index < block.Statements.Count; index++)
        {
            if (IsMatchingWriteBack(block.Statements[index], elementAccess, local, semanticModel, cancellationToken, out var candidate))
            {
                writeBack = candidate;
                writeBackIndex = index;
                break;
            }
        }

        if (writeBack is null ||
            writeBackIndex == declarationIndex + 1 ||
            ContainsMeaningfulTrivia(writeBack.DescendantTrivia(descendIntoTrivia: true)))
        {
            return false;
        }

        var container = semanticModel.GetSymbolInfo(containerIdentifier, cancellationToken).Symbol;
        var indexExpression = elementAccess.ArgumentList.Arguments[0].Expression;
        ISymbol? indexSymbol = null;
        if (indexExpression is IdentifierNameSyntax indexIdentifier)
        {
            indexSymbol = semanticModel.GetSymbolInfo(indexIdentifier, cancellationToken).Symbol;
        }
        else if (semanticModel.GetConstantValue(indexExpression, cancellationToken).Value is not int)
        {
            return false;
        }

        var intermediateStatements = block.Statements
            .Skip(declarationIndex + 1)
            .Take(writeBackIndex - declarationIndex - 1)
            .ToArray();
        if (container is null ||
            intermediateStatements.Any(PreventsRefLocalRewrite) ||
            HasUnsafeContainerUse(intermediateStatements, container, semanticModel, cancellationToken) ||
            indexSymbol is not null && IsSymbolMutated(intermediateStatements, indexSymbol, semanticModel, cancellationToken) ||
            !MutatesLocal(intermediateStatements, local, semanticModel, cancellationToken))
        {
            return false;
        }

        var hasLocalUseAfterWriteBack = block.Statements
            .Skip(writeBackIndex + 1)
            .SelectMany(statement => statement.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            .Any(identifier => SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                local));
        if (hasLocalUseAfterWriteBack)
        {
            return false;
        }

        pattern = new CopyBackPattern(declaration, variable, refTarget, writeBack!, block);
        return true;
    }

    internal static bool TryGetRepeatedCameraMain(
        MemberAccessExpressionSyntax currentAccess,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out BlockSyntax block,
        out ImmutableArray<MemberAccessExpressionSyntax> accesses,
        out StatementSyntax insertionPoint,
        out string localName)
    {
        block = null!;
        accesses = ImmutableArray<MemberAccessExpressionSyntax>.Empty;
        insertionPoint = null!;
        localName = string.Empty;
        if (!IsCameraMainAccess(currentAccess, semanticModel, cancellationToken))
        {
            return false;
        }

        var containingBlock = currentAccess.FirstAncestorOrSelf<BlockSyntax>();
        if (containingBlock is null || IsInsideNestedExecutable(currentAccess, containingBlock))
        {
            return false;
        }

        var matchingAccesses = containingBlock.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(access =>
                access.FirstAncestorOrSelf<BlockSyntax>() == containingBlock &&
                !IsInsideNestedExecutable(access, containingBlock) &&
                IsCameraMainAccess(access, semanticModel, cancellationToken))
            .OrderBy(access => access.SpanStart)
            .ToImmutableArray();
        if (matchingAccesses.Length < 2 || matchingAccesses[0] != currentAccess)
        {
            return false;
        }

        var firstStatement = currentAccess.FirstAncestorOrSelf<StatementSyntax>();
        if (firstStatement?.Parent != containingBlock)
        {
            return false;
        }

        var usedNames = containingBlock.DescendantTokens()
            .Where(token => token.IsKind(SyntaxKind.IdentifierToken))
            .Select(token => token.ValueText)
            .ToImmutableHashSet(StringComparer.Ordinal);
        var candidateName = "mainCamera";
        for (var suffix = 2; usedNames.Contains(candidateName); suffix++)
        {
            candidateName = "mainCamera" + suffix;
        }

        block = containingBlock;
        accesses = matchingAccesses;
        insertionPoint = firstStatement;
        localName = candidateName;
        return true;
    }

    internal static bool TryGetListPreallocation(
        ObjectCreationExpressionSyntax creation,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out int addCount,
        int minimumListAdds = AnalyzerConfiguration.DefaultMinimumListAdds)
    {
        addCount = 0;
        if (creation.ArgumentList is not { Arguments.Count: 0 } ||
            creation.Initializer is not null ||
            creation.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable } ||
            variable.Parent?.Parent is not LocalDeclarationStatementSyntax declaration ||
            declaration.Parent is not BlockSyntax block)
        {
            return false;
        }

        var local = semanticModel.GetDeclaredSymbol(variable, cancellationToken) as ILocalSymbol;
        var listType = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "System.Collections.Generic.List`1");
        if (local?.Type is not INamedTypeSymbol namedType ||
            listType is null ||
            !SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, listType))
        {
            return false;
        }

        var declarationIndex = block.Statements.IndexOf(declaration);
        for (var index = declarationIndex + 1; index < block.Statements.Count; index++)
        {
            if (!IsListAdd(block.Statements[index], local, listType, semanticModel, cancellationToken))
            {
                break;
            }

            addCount++;
        }

        return addCount >= minimumListAdds;
    }

    internal static bool TryGetMathfSquare(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out ExpressionSyntax value)
    {
        value = null!;
        if (invocation.ArgumentList.Arguments.Count != 2)
        {
            return false;
        }

        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        var mathf = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "UnityEngine.Mathf");
        if (method is null ||
            mathf is null ||
            method.Name != "Pow" ||
            !SymbolEqualityComparer.Default.Equals(method.ContainingType, mathf))
        {
            return false;
        }

        var exponent = semanticModel.GetConstantValue(invocation.ArgumentList.Arguments[1].Expression, cancellationToken);
        var candidate = UnwrapParentheses(invocation.ArgumentList.Arguments[0].Expression);
        var candidateSymbol = semanticModel.GetSymbolInfo(candidate, cancellationToken).Symbol;
        var candidateType = semanticModel.GetTypeInfo(candidate, cancellationToken).Type;
        if (!exponent.HasValue ||
            exponent.Value is not float exponentValue ||
            exponentValue != 2f ||
            candidate is not IdentifierNameSyntax ||
            candidateSymbol is not ILocalSymbol && candidateSymbol is not IParameterSymbol ||
            candidateType?.SpecialType != SpecialType.System_Single)
        {
            return false;
        }

        value = candidate;
        return true;
    }

    internal static bool TryGetNativeArrayInitialization(
        ObjectCreationExpressionSyntax creation,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out ForStatementSyntax initializationLoop)
    {
        initializationLoop = null!;
        if (creation.ArgumentList is not { Arguments.Count: 2 } ||
            creation.Initializer is not null ||
            creation.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable } ||
            variable.Parent?.Parent is not LocalDeclarationStatementSyntax declaration ||
            declaration.Parent is not BlockSyntax block)
        {
            return false;
        }

        var local = semanticModel.GetDeclaredSymbol(variable, cancellationToken) as ILocalSymbol;
        var nativeArray = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "Unity.Collections.NativeArray`1");
        var options = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "Unity.Collections.NativeArrayOptions");
        var constructor = semanticModel.GetSymbolInfo(creation, cancellationToken).Symbol as IMethodSymbol;
        // Only the (int length, Allocator allocator) constructor clears memory that a
        // NativeArrayOptions argument can skip; the copy constructors take the same
        // argument count but have no options overload.
        if (local?.Type is not INamedTypeSymbol localType ||
            nativeArray is null ||
            options is null ||
            !SymbolEqualityComparer.Default.Equals(localType.OriginalDefinition, nativeArray) ||
            constructor is null ||
            constructor.Parameters.Length < 2 ||
            constructor.Parameters[0].Type.SpecialType != SpecialType.System_Int32 ||
            !HasNativeArrayOptionsOverload(localType, options))
        {
            return false;
        }

        var declarationIndex = block.Statements.IndexOf(declaration);
        if (declarationIndex < 0 ||
            declarationIndex + 1 >= block.Statements.Count ||
            block.Statements[declarationIndex + 1] is not ForStatementSyntax loop ||
            !IsFullNativeArrayOverwrite(loop, local, semanticModel, cancellationToken))
        {
            return false;
        }

        initializationLoop = loop;
        return true;
    }

    private static bool IsCameraMainAccess(
        MemberAccessExpressionSyntax access,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        if (access.Name.Identifier.ValueText != "main")
        {
            return false;
        }

        var property = semanticModel.GetSymbolInfo(access, cancellationToken).Symbol as IPropertySymbol;
        var camera = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "UnityEngine.Camera");
        return property is not null &&
               property.IsStatic &&
               camera is not null &&
               SymbolEqualityComparer.Default.Equals(property.ContainingType, camera);
    }

    private static bool IsInsideNestedExecutable(SyntaxNode node, BlockSyntax containingBlock) =>
        node.Ancestors().TakeWhile(ancestor => ancestor != containingBlock).Any(ancestor =>
            ancestor is AnonymousFunctionExpressionSyntax ||
            ancestor is LocalFunctionStatementSyntax ||
            ancestor is TypeDeclarationSyntax);

    private static bool IsListAdd(
        StatementSyntax statement,
        ILocalSymbol local,
        INamedTypeSymbol listType,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        if (statement is not ExpressionStatementSyntax
            {
                Expression: InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax receiver,
                        Name.Identifier.ValueText: "Add",
                    },
                    ArgumentList.Arguments.Count: 1,
                } invocation,
            })
        {
            return false;
        }

        var receiverSymbol = semanticModel.GetSymbolInfo(receiver, cancellationToken).Symbol;
        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        return SymbolEqualityComparer.Default.Equals(receiverSymbol, local) &&
               method is not null &&
               SymbolEqualityComparer.Default.Equals(method.ContainingType.OriginalDefinition, listType);
    }

    private static bool HasNativeArrayOptionsOverload(INamedTypeSymbol nativeArrayType, INamedTypeSymbol options) =>
        nativeArrayType.InstanceConstructors.Any(constructor =>
            constructor.Parameters.Length == 3 &&
            SymbolEqualityComparer.Default.Equals(constructor.Parameters[2].Type, options));

    private static bool IsFullNativeArrayOverwrite(
        ForStatementSyntax loop,
        ILocalSymbol array,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        if (loop.Declaration?.Variables.Count != 1 ||
            loop.Declaration.Variables[0].Initializer?.Value is not ExpressionSyntax initialValue ||
            semanticModel.GetConstantValue(initialValue, cancellationToken).Value is not int initialIndex ||
            initialIndex != 0)
        {
            return false;
        }

        var index = semanticModel.GetDeclaredSymbol(loop.Declaration.Variables[0], cancellationToken) as ILocalSymbol;
        if (index is null ||
            loop.Condition is not BinaryExpressionSyntax
            {
                RawKind: (int)SyntaxKind.LessThanExpression,
                Left: IdentifierNameSyntax conditionIndex,
                Right: MemberAccessExpressionSyntax
                {
                    Expression: IdentifierNameSyntax lengthReceiver,
                    Name.Identifier.ValueText: "Length",
                },
            } ||
            !SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(conditionIndex, cancellationToken).Symbol,
                index) ||
            !SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(lengthReceiver, cancellationToken).Symbol,
                array) ||
            loop.Incrementors.Count != 1 ||
            !IsIncrementOf(loop.Incrementors[0], index, semanticModel, cancellationToken))
        {
            return false;
        }

        var bodyStatement = loop.Statement is BlockSyntax { Statements.Count: 1 } body
            ? body.Statements[0]
            : loop.Statement;
        if (bodyStatement is not ExpressionStatementSyntax
            {
                Expression: AssignmentExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                    Left: ElementAccessExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax targetArray,
                        ArgumentList.Arguments.Count: 1,
                    } target,
                } assignment,
            } ||
            target.ArgumentList.Arguments[0].Expression is not IdentifierNameSyntax targetIndex ||
            !SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(targetArray, cancellationToken).Symbol,
                array) ||
            !SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(targetIndex, cancellationToken).Symbol,
                index))
        {
            return false;
        }

        return !assignment.Right.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                array));
    }

    private static bool IsIncrementOf(
        ExpressionSyntax expression,
        ILocalSymbol index,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        ExpressionSyntax? operand = expression switch
        {
            PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.PostIncrementExpression) => postfix.Operand,
            PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.PreIncrementExpression) => prefix.Operand,
            _ => null,
        };
        return operand is not null &&
               SymbolEqualityComparer.Default.Equals(
                   semanticModel.GetSymbolInfo(operand, cancellationToken).Symbol,
                   index);
    }

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeType) =>
        symbol.GetAttributes().Any(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType));

    private static bool ContainsMeaningfulTrivia(System.Collections.Generic.IEnumerable<SyntaxTrivia> trivia) =>
        trivia.Any(item =>
            !item.IsKind(SyntaxKind.WhitespaceTrivia) &&
            !item.IsKind(SyntaxKind.EndOfLineTrivia));

    private static bool IsSafeNativeArrayRead(IdentifierNameSyntax identifier)
    {
        if (identifier.Parent is ElementAccessExpressionSyntax elementAccess &&
            elementAccess.Expression == identifier)
        {
            return !IsWriteTarget(elementAccess);
        }

        if (identifier.Parent is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Expression == identifier)
        {
            return memberAccess.Parent is not InvocationExpressionSyntax &&
                   !IsWriteTarget(memberAccess);
        }

        return false;
    }

    private static bool IsWriteTarget(ExpressionSyntax expression)
    {
        for (SyntaxNode? current = expression; current is ExpressionSyntax; current = current.Parent)
        {
            if (current.Parent is AssignmentExpressionSyntax assignment && assignment.Left.Span.Contains(expression.Span) ||
                current.Parent is PrefixUnaryExpressionSyntax prefix &&
                (prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression)) ||
                current.Parent is PostfixUnaryExpressionSyntax postfix &&
                (postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression)) ||
                current.Parent is ArgumentSyntax argument && !argument.RefKindKeyword.IsKind(SyntaxKind.None) ||
                current.Parent is RefExpressionSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideLoop(LocalDeclarationStatementSyntax declaration) =>
        declaration.Ancestors().TakeWhile(node =>
                node is not MethodDeclarationSyntax &&
                node is not LocalFunctionStatementSyntax &&
                node is not AnonymousFunctionExpressionSyntax)
            .Any(node =>
                node is ForStatementSyntax ||
                node is ForEachStatementSyntax ||
                node is WhileStatementSyntax ||
                node is DoStatementSyntax);

    private static bool TryGetArrayLength(
        ArrayCreationExpressionSyntax arrayCreation,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out int length)
    {
        length = 0;
        if (arrayCreation.Type.RankSpecifiers.Count != 1 ||
            arrayCreation.Type.RankSpecifiers[0].Sizes.Count != 1)
        {
            return false;
        }

        var size = arrayCreation.Type.RankSpecifiers[0].Sizes[0];
        if (size is OmittedArraySizeExpressionSyntax)
        {
            if (arrayCreation.Initializer is null)
            {
                return false;
            }

            length = arrayCreation.Initializer.Expressions.Count;
            return true;
        }

        var constant = semanticModel.GetConstantValue(size, cancellationToken);
        if (constant.HasValue && constant.Value is int value)
        {
            length = value;
            return true;
        }

        return false;
    }

    private static bool TryGetStackElementSize(ITypeSymbol type, out int size)
    {
        if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol enumType && enumType.EnumUnderlyingType is not null)
        {
            type = enumType.EnumUnderlyingType;
        }

        size = type.SpecialType switch
        {
            SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte => 1,
            SpecialType.System_Char or SpecialType.System_Int16 or SpecialType.System_UInt16 => 2,
            SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Single => 4,
            SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Double => 8,
            SpecialType.System_Decimal => 16,
            _ => 0,
        };
        return size != 0;
    }

    private static bool IsInAsyncOrIteratorMethod(LocalDeclarationStatementSyntax declaration)
    {
        var method = declaration.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        return method is null ||
               method.Modifiers.Any(SyntaxKind.AsyncKeyword) ||
               method.DescendantNodes().OfType<YieldStatementSyntax>().Any();
    }

    private static bool TryCreateRefTarget(
        ElementAccessExpressionSyntax elementAccess,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out ExpressionSyntax refTarget)
    {
        refTarget = null!;
        var receiverType = semanticModel.GetTypeInfo(elementAccess.Expression, cancellationToken).Type;
        if (receiverType is IArrayTypeSymbol)
        {
            refTarget = elementAccess;
            return true;
        }

        if (semanticModel.GetSymbolInfo(elementAccess, cancellationToken).Symbol is IPropertySymbol { ReturnsByRef: true })
        {
            refTarget = elementAccess;
            return true;
        }

        if (receiverType is not INamedTypeSymbol namedType)
        {
            return false;
        }

        var elementAt = namedType.GetMembers("ElementAt")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(method =>
                !method.IsStatic &&
                method.DeclaredAccessibility == Accessibility.Public &&
                method.ReturnsByRef &&
                method.Parameters.Length == 1 &&
                method.Parameters[0].Type.SpecialType == SpecialType.System_Int32);
        if (elementAt is null)
        {
            return TryCreateNativeArraySpanTarget(
                elementAccess,
                namedType,
                semanticModel,
                cancellationToken,
                out refTarget);
        }

        refTarget = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                elementAccess.Expression.WithoutTrivia(),
                SyntaxFactory.IdentifierName("ElementAt")),
            SyntaxFactory.ArgumentList(elementAccess.ArgumentList.Arguments));
        return true;
    }

    private static bool TryCreateNativeArraySpanTarget(
        ElementAccessExpressionSyntax elementAccess,
        INamedTypeSymbol receiverType,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out ExpressionSyntax refTarget)
    {
        refTarget = null!;
        var nativeArrayType = semanticModel.Compilation.GetTypeByMetadataName(
            "Unity.Collections.NativeArray`1");
        var spanType = semanticModel.Compilation.GetTypeByMetadataName("System.Span`1");
        var elementType = semanticModel.GetTypeInfo(elementAccess, cancellationToken).Type;
        if (nativeArrayType is null ||
            spanType is null ||
            elementType is null ||
            !SymbolEqualityComparer.Default.Equals(receiverType.OriginalDefinition, nativeArrayType))
        {
            return false;
        }

        var asSpan = receiverType.GetMembers("AsSpan")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(method =>
                !method.IsStatic &&
                method.DeclaredAccessibility == Accessibility.Public &&
                method.Parameters.Length == 0 &&
                method.ReturnType is INamedTypeSymbol returnedSpan &&
                SymbolEqualityComparer.Default.Equals(returnedSpan.OriginalDefinition, spanType) &&
                returnedSpan.TypeArguments.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(returnedSpan.TypeArguments[0], elementType));
        if (asSpan is null)
        {
            return false;
        }

        var spanInvocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                elementAccess.Expression.WithoutTrivia(),
                SyntaxFactory.IdentifierName("AsSpan")));
        refTarget = SyntaxFactory.ElementAccessExpression(
            spanInvocation,
            SyntaxFactory.BracketedArgumentList(elementAccess.ArgumentList.Arguments));
        return true;
    }

    private static bool IsMatchingWriteBack(
        StatementSyntax statement,
        ElementAccessExpressionSyntax originalElement,
        ILocalSymbol local,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out ExpressionStatementSyntax writeBack)
    {
        writeBack = null!;
        if (statement is not ExpressionStatementSyntax expressionStatement ||
            expressionStatement.Expression is not AssignmentExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                Left: ExpressionSyntax target,
                Right: IdentifierNameSyntax value,
            } ||
            !IsMatchingWriteBackTarget(originalElement, target, semanticModel, cancellationToken) ||
            !SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(value, cancellationToken).Symbol,
                local))
        {
            return false;
        }

        writeBack = expressionStatement;
        return true;
    }

    private static bool IsMatchingWriteBackTarget(
        ElementAccessExpressionSyntax originalElement,
        ExpressionSyntax target,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        if (target is ElementAccessExpressionSyntax targetElement)
        {
            return SyntaxFactory.AreEquivalent(originalElement, targetElement);
        }

        if (target is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Expression: var receiver,
                    Name: IdentifierNameSyntax { Identifier.ValueText: "ElementAt" },
                },
                ArgumentList.Arguments.Count: 1,
            } invocation ||
            originalElement.ArgumentList.Arguments.Count != 1 ||
            !SyntaxFactory.AreEquivalent(originalElement.Expression, receiver) ||
            !SyntaxFactory.AreEquivalent(
                originalElement.ArgumentList.Arguments[0].Expression,
                invocation.ArgumentList.Arguments[0].Expression) ||
            semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method)
        {
            return false;
        }

        return method.ReturnsByRef && !method.ReturnsByRefReadonly;
    }

    private static bool PreventsRefLocalRewrite(StatementSyntax statement) =>
        // A jump can leave the block before the write-back runs, so removing the
        // write-back would persist mutations the original code discarded; lambdas
        // and local functions cannot capture the ref local the fix introduces.
        statement.DescendantNodesAndSelf().Any(node =>
            node is ReturnStatementSyntax ||
            node is BreakStatementSyntax ||
            node is ContinueStatementSyntax ||
            node is GotoStatementSyntax ||
            node is ThrowStatementSyntax ||
            node is ThrowExpressionSyntax ||
            node is YieldStatementSyntax ||
            node is AnonymousFunctionExpressionSyntax ||
            node is LocalFunctionStatementSyntax);

    private static bool HasUnsafeContainerUse(
        StatementSyntax[] statements,
        ISymbol symbol,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        foreach (var identifier in statements
                     .SelectMany(statement => statement.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
                     .Where(identifier => SymbolEqualityComparer.Default.Equals(
                         semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                         symbol)))
        {
            // Reading another value through the same indexer does not move or
            // replace the ref target. Other receiver uses remain conservative:
            // a DynamicBuffer method call, a ref argument, or reassignment can
            // invalidate the element reference before the original write-back.
            if (identifier.Parent is not ElementAccessExpressionSyntax elementAccess ||
                elementAccess.Expression != identifier ||
                IsExpressionMutated(elementAccess))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSymbolMutated(
        StatementSyntax[] statements,
        ISymbol symbol,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken) =>
        statements.SelectMany(statement => statement.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            .Where(identifier => SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                symbol))
            .Any(IsExpressionMutated);

    private static bool IsExpressionMutated(ExpressionSyntax expression)
    {
        for (SyntaxNode? current = expression;
             current is ExpressionSyntax ||
             current is ArgumentSyntax;
             current = current.Parent)
        {
            if (current.Parent is AssignmentExpressionSyntax assignment &&
                assignment.Left.Span.Contains(expression.Span))
            {
                return true;
            }

            if (current.Parent is PrefixUnaryExpressionSyntax prefix &&
                (prefix.IsKind(SyntaxKind.PreIncrementExpression) ||
                 prefix.IsKind(SyntaxKind.PreDecrementExpression)) ||
                current.Parent is PostfixUnaryExpressionSyntax postfix &&
                (postfix.IsKind(SyntaxKind.PostIncrementExpression) ||
                 postfix.IsKind(SyntaxKind.PostDecrementExpression)))
            {
                return true;
            }

            if (current is ArgumentSyntax argument &&
                (argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) ||
                 argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MutatesLocal(
        StatementSyntax[] statements,
        ILocalSymbol local,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        foreach (var identifier in statements.SelectMany(statement =>
                     statement.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()))
        {
            if (!SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                    local))
            {
                continue;
            }

            if (identifier.AncestorsAndSelf().TakeWhile(node => node is not StatementSyntax).Any(node =>
                    node.Parent is AssignmentExpressionSyntax assignment && assignment.Left.Span.Contains(identifier.Span) ||
                    node.Parent is PrefixUnaryExpressionSyntax prefix &&
                    (prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression)) ||
                    node.Parent is PostfixUnaryExpressionSyntax postfix &&
                    (postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression)) ||
                    node is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Expression == identifier &&
                    memberAccess.Parent is InvocationExpressionSyntax))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsEncapsulatableSerializedField(
        FieldDeclarationSyntax declaration,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        if (declaration.Declaration.Variables.Count != 1 ||
            !declaration.Modifiers.Any(SyntaxKind.PublicKeyword) ||
            declaration.Modifiers.Any(SyntaxKind.StaticKeyword) ||
            declaration.Modifiers.Any(SyntaxKind.ConstKeyword) ||
            declaration.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) ||
            declaration.Modifiers.Any(SyntaxKind.FixedKeyword))
        {
            return false;
        }

        var field = semanticModel.GetDeclaredSymbol(declaration.Declaration.Variables[0], cancellationToken) as IFieldSymbol;
        if (field is null ||
            field.DeclaredAccessibility != Accessibility.Public ||
            !IsUnitySerializedOwner(field.ContainingType, semanticModel.Compilation) ||
            !IsUnitySerializableType(field.Type, semanticModel.Compilation) ||
            HasAttribute(field, "UnityEngine.SerializeField") ||
            HasAttribute(field, "System.NonSerializedAttribute"))
        {
            return false;
        }

        return UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "UnityEngine.SerializeField") is not null;
    }

    private static bool HasAttribute(IFieldSymbol field, string metadataName) =>
        field.GetAttributes().Any(attribute =>
            string.Equals(
                attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                metadataName,
                System.StringComparison.Ordinal));

    internal static bool IsBoxedNextFrameYield(
        YieldStatementSyntax yieldStatement,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        if (yieldStatement.Expression is null || !IsBoxedNextFrameConstant(yieldStatement.Expression, semanticModel, cancellationToken))
        {
            return false;
        }

        var methodDeclaration = yieldStatement.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDeclaration is null)
        {
            return false;
        }

        var method = semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken);
        var enumerator = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "System.Collections.IEnumerator");
        var monoBehaviour = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "UnityEngine.MonoBehaviour");
        return method is not null &&
               enumerator is not null &&
               monoBehaviour is not null &&
               SymbolEqualityComparer.Default.Equals(method.ReturnType, enumerator) &&
               IsSameOrDerivedFrom(method.ContainingType, monoBehaviour);
    }

    internal static bool TryGetMagnitudeComparison(
        BinaryExpressionSyntax binary,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out MemberAccessExpressionSyntax magnitude,
        out ExpressionSyntax threshold)
    {
        magnitude = null!;
        threshold = null!;

        if (IsUnityMagnitude(binary.Left, semanticModel, cancellationToken) &&
            IsPositiveFiniteFloatConstant(binary.Right, semanticModel, cancellationToken))
        {
            magnitude = (MemberAccessExpressionSyntax)UnwrapParentheses(binary.Left);
            threshold = UnwrapParentheses(binary.Right);
            return true;
        }

        if (IsUnityMagnitude(binary.Right, semanticModel, cancellationToken) &&
            IsPositiveFiniteFloatConstant(binary.Left, semanticModel, cancellationToken))
        {
            magnitude = (MemberAccessExpressionSyntax)UnwrapParentheses(binary.Right);
            threshold = UnwrapParentheses(binary.Left);
            return true;
        }

        return false;
    }

    private static bool IsBoxedNextFrameConstant(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        var constant = semanticModel.GetConstantValue(expression, cancellationToken);
        if (!constant.HasValue || constant.Value is null)
        {
            return false;
        }

        return constant.Value is bool ||
               constant.Value is sbyte signedByte && signedByte == 0 ||
               constant.Value is byte unsignedByte && unsignedByte == 0 ||
               constant.Value is short shortValue && shortValue == 0 ||
               constant.Value is ushort unsignedShort && unsignedShort == 0 ||
               constant.Value is int integer && integer == 0 ||
               constant.Value is uint unsignedInteger && unsignedInteger == 0 ||
               constant.Value is long longValue && longValue == 0 ||
               constant.Value is ulong unsignedLong && unsignedLong == 0;
    }

    private static bool IsUnityMagnitude(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        expression = UnwrapParentheses(expression);
        if (expression is not MemberAccessExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Name.Identifier.ValueText: "magnitude",
            })
        {
            return false;
        }

        var property = semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol as IPropertySymbol;
        if (property?.Type.SpecialType != SpecialType.System_Single)
        {
            return false;
        }

        var vector2 = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "UnityEngine.Vector2");
        var vector3 = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "UnityEngine.Vector3");
        return SymbolEqualityComparer.Default.Equals(property.ContainingType, vector2) ||
               SymbolEqualityComparer.Default.Equals(property.ContainingType, vector3);
    }

    private static bool IsPositiveFiniteFloatConstant(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        var type = semanticModel.GetTypeInfo(expression, cancellationToken).Type;
        var constant = semanticModel.GetConstantValue(expression, cancellationToken);
        return type?.SpecialType == SpecialType.System_Single &&
               constant.HasValue &&
               constant.Value is float value &&
               value > 0f &&
               !float.IsInfinity(value) &&
               !float.IsNaN(value);
    }

    private static bool IsUnitySerializedOwner(INamedTypeSymbol containingType, Compilation compilation)
    {
        var monoBehaviour = UnitySymbolCache.GetTypeByMetadataName(
            compilation,
            "UnityEngine.MonoBehaviour");
        var scriptableObject = UnitySymbolCache.GetTypeByMetadataName(
            compilation,
            "UnityEngine.ScriptableObject");
        return monoBehaviour is not null && IsSameOrDerivedFrom(containingType, monoBehaviour) ||
               scriptableObject is not null && IsSameOrDerivedFrom(containingType, scriptableObject);
    }

    private static bool IsUnitySerializableType(ITypeSymbol type, Compilation compilation)
    {
        if (type.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        if (type.SpecialType is SpecialType.System_Boolean or
            SpecialType.System_Byte or
            SpecialType.System_SByte or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16 or
            SpecialType.System_Int32 or
            SpecialType.System_UInt32 or
            SpecialType.System_Int64 or
            SpecialType.System_UInt64 or
            SpecialType.System_Single or
            SpecialType.System_Double or
            SpecialType.System_Char or
            SpecialType.System_String)
        {
            return true;
        }

        if (type is IArrayTypeSymbol { Rank: 1 } array)
        {
            return IsUnitySerializableType(array.ElementType, compilation);
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        var unityObject = UnitySymbolCache.GetTypeByMetadataName(compilation, "UnityEngine.Object");
        if (unityObject is not null && IsSameOrDerivedFrom(namedType, unityObject))
        {
            return true;
        }

        if (namedType.IsGenericType &&
            namedType.TypeArguments.Length == 1 &&
            namedType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>")
        {
            return IsUnitySerializableType(namedType.TypeArguments[0], compilation);
        }

        var serializableAttribute = UnitySymbolCache.GetTypeByMetadataName(
            compilation,
            "System.SerializableAttribute");
        return !namedType.IsGenericType &&
               serializableAttribute is not null &&
               namedType.GetAttributes().Any(attribute =>
                   SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, serializableAttribute));
    }

    private static bool IsSameOrDerivedFrom(INamedTypeSymbol type, INamedTypeSymbol expectedBase)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, expectedBase))
            {
                return true;
            }
        }

        return false;
    }

    internal static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }
}

internal sealed class CopyBackPattern
{
    public CopyBackPattern(
        LocalDeclarationStatementSyntax declaration,
        VariableDeclaratorSyntax variable,
        ExpressionSyntax refTarget,
        ExpressionStatementSyntax writeBack,
        BlockSyntax block)
    {
        Declaration = declaration;
        Variable = variable;
        RefTarget = refTarget;
        WriteBack = writeBack;
        Block = block;
    }

    public LocalDeclarationStatementSyntax Declaration { get; }

    public VariableDeclaratorSyntax Variable { get; }

    public ExpressionSyntax RefTarget { get; }

    public ExpressionStatementSyntax WriteBack { get; }

    public BlockSyntax Block { get; }
}
