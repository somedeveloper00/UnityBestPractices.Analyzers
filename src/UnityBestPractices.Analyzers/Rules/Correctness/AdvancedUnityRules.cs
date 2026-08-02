using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace UnityBestPractices.Analyzers;

internal static class AdvancedUnityRules
{
    private static readonly RuleMetadata DiscardedJobHandle = Create(
        DiagnosticIds.DiscardedScheduledJobHandle,
        "Preserve scheduled JobHandle",
        "Preserve the JobHandle returned by '{0}'",
        "A scheduled Unity job handle must be preserved or combined so callers can propagate dependencies and complete work safely.",
        "Assign the scheduled JobHandle",
        RuleCategories.UnityCorrectness,
        RuleSafety.Safe,
        hasCodeFix: true,
        supportsFixAll: false,
        "Unity.Jobs.JobHandle");

    private static readonly RuleMetadata UndisposedPersistentContainer = Create(
        DiagnosticIds.UndisposedPersistentNativeContainer,
        "Dispose persistent NativeArray",
        "Persistent NativeArray '{0}' is never disposed",
        "A locally owned persistent NativeArray must be disposed or transfer ownership before the method exits.",
        string.Empty,
        RuleCategories.UnityCorrectness,
        RuleSafety.Safe,
        hasCodeFix: false,
        supportsFixAll: false,
        "Unity.Collections.NativeArray`1",
        "Unity.Collections.Allocator");

    private static readonly RuleMetadata TemporaryAllocatorEscape = Create(
        DiagnosticIds.InvalidTemporaryAllocatorEscape,
        "Do not let a temporary NativeArray escape",
        "A NativeArray allocated with '{0}' escapes its valid local lifetime",
        "A NativeArray allocated with Allocator.Temp or Allocator.TempJob cannot safely be returned, stored in a field, or captured by a longer-lived delegate.",
        string.Empty,
        RuleCategories.UnityCorrectness,
        RuleSafety.Safe,
        hasCodeFix: false,
        supportsFixAll: false,
        "Unity.Collections.NativeArray`1",
        "Unity.Collections.Allocator");

    private static readonly RuleMetadata ShaderPropertyId = Create(
        DiagnosticIds.CacheShaderPropertyId,
        "Cache shader property ID",
        "Cache the repeated shader property ID for '{0}'",
        "Caching repeated Shader.PropertyToID calls avoids repeated property-name hashing while preserving the integer property identity.",
        "Cache Shader.PropertyToID result",
        RuleCategories.UnityPerformanceSafe,
        RuleSafety.Safe,
        hasCodeFix: true,
        supportsFixAll: false,
        "UnityEngine.Shader");

    private static readonly RuleMetadata CombineLocalPositionAndRotation = Create(
        DiagnosticIds.CombineLocalPositionAndRotation,
        "Set local position and rotation together",
        "Set local position and rotation with one Transform call",
        "SetLocalPositionAndRotation updates both local transform values in one native call.",
        "Use SetLocalPositionAndRotation",
        RuleCategories.UnityPerformanceReview,
        RuleSafety.ReviewRequired,
        hasCodeFix: true,
        supportsFixAll: false,
        "UnityEngine.Transform");

    internal static ImmutableArray<RuleMetadata> Metadata { get; } = ImmutableArray.Create(
        DiscardedJobHandle,
        UndisposedPersistentContainer,
        TemporaryAllocatorEscape,
        ShaderPropertyId,
        CombineLocalPositionAndRotation);

    internal static ImmutableArray<DiagnosticDescriptor> Descriptors { get; } =
        Metadata.Select(metadata => metadata.Descriptor).ToImmutableArray();

    internal static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation ||
            invocation.Parent is not ExpressionStatementSyntax)
        {
            return;
        }

        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken)
            .Symbol as IMethodSymbol;
        var jobHandle = UnitySymbolCache.GetTypeByMetadataName(
            context.SemanticModel.Compilation,
            "Unity.Jobs.JobHandle");
        if (method is null ||
            jobHandle is null ||
            !SymbolEqualityComparer.Default.Equals(method.ReturnType, jobHandle) ||
            (method.Name != "Schedule" && method.Name != "ScheduleParallel") ||
            !IsSupportedSchedulingMethod(method))
        {
            return;
        }

        Report(
            context,
            DiscardedJobHandle,
            invocation.GetLocation(),
            method.Name);
    }

    internal static void AnalyzeLocalDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not LocalDeclarationStatementSyntax declaration ||
            declaration.UsingKeyword != default ||
            declaration.Declaration.Variables.Count != 1 ||
            declaration.Declaration.Variables[0] is not { Initializer.Value: { } initializer } variable ||
            !TryGetAllocatorKind(
                initializer,
                context.SemanticModel,
                context.CancellationToken,
                out var allocatorKind) ||
            allocatorKind != "Persistent" ||
            context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is not ILocalSymbol local)
        {
            return;
        }

        if (!IsDefinitelyUndisposedOnEveryExit(declaration, local, context))
        {
            return;
        }

        Report(
            context,
            UndisposedPersistentContainer,
            variable.Identifier.GetLocation(),
            variable.Identifier.ValueText);
    }

    private static bool IsDefinitelyUndisposedOnEveryExit(
        LocalDeclarationStatementSyntax declaration,
        ILocalSymbol local,
        SyntaxNodeAnalysisContext context)
    {
        var executable = declaration.Ancestors()
            .FirstOrDefault(node =>
                node is BaseMethodDeclarationSyntax ||
                node is LocalFunctionStatementSyntax);
        if (executable is null)
        {
            return false;
        }

        ControlFlowGraph graph;
        try
        {
            if (executable is LocalFunctionStatementSyntax localFunction)
            {
                var containingMethod = localFunction.Ancestors().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault();
                var localFunctionSymbol = context.SemanticModel.GetDeclaredSymbol(
                    localFunction,
                    context.CancellationToken) as IMethodSymbol;
                if (containingMethod is null || localFunctionSymbol is null)
                {
                    return false;
                }

                var containingGraph = ControlFlowGraph.Create(
                    containingMethod,
                    context.SemanticModel,
                    context.CancellationToken);
                graph = containingGraph.GetLocalFunctionControlFlowGraphInScope(
                    localFunctionSymbol,
                    context.CancellationToken);
            }
            else
            {
                graph = ControlFlowGraph.Create(
                    executable,
                    context.SemanticModel,
                    context.CancellationToken);
            }
        }
        catch (ArgumentException)
        {
            // Broken or unsupported operation trees are not a safe basis for a leak diagnostic.
            return false;
        }

        // Each bit records whether a path reaching the block still owns the allocation.
        // Reporting requires the exit to contain only the owned bit: if even one path
        // disposes or transfers ownership, the analysis deliberately remains silent.
        const int disposed = 1;
        const int owned = 2;
        var states = new int[graph.Blocks.Length];
        var declarationSeen = false;
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var block in graph.Blocks)
            {
                var state = states[block.Ordinal];
                foreach (var operation in block.Operations)
                {
                    if (declaration.Span.Contains(operation.Syntax.Span))
                    {
                        state |= owned;
                        declarationSeen = true;
                    }

                    state = ApplyOwnershipEffects(operation, state, local, context);
                }

                if (block.BranchValue is { } branchValue)
                {
                    state = ApplyOwnershipEffects(branchValue, state, local, context);
                    if ((block.FallThroughSuccessor?.Semantics == ControlFlowBranchSemantics.Return ||
                         block.ConditionalSuccessor?.Semantics == ControlFlowBranchSemantics.Return) &&
                        ReferencesLocal(branchValue, local))
                    {
                        state = (state & ~owned) | disposed;
                    }
                }

                changed |= Propagate(block.FallThroughSuccessor, state, states);
                changed |= Propagate(block.ConditionalSuccessor, state, states);
            }
        }

        return declarationSeen && states[graph.Blocks[graph.Blocks.Length - 1].Ordinal] == owned;

        static bool Propagate(ControlFlowBranch? branch, int state, int[] targetStates)
        {
            if (branch?.Destination is not { } destination || state == 0)
            {
                return false;
            }

            var combined = targetStates[destination.Ordinal] | state;
            if (combined == targetStates[destination.Ordinal])
            {
                return false;
            }

            targetStates[destination.Ordinal] = combined;
            return true;
        }

        static int ApplyOwnershipEffects(
            IOperation operation,
            int state,
            ILocalSymbol local,
            SyntaxNodeAnalysisContext context)
        {
            foreach (var child in operation.Children)
            {
                state = ApplyOwnershipEffects(child, state, local, context);
            }

            if ((state & owned) == 0)
            {
                return state;
            }

            var transfers = operation switch
            {
                IInvocationOperation invocation => IsDispose(invocation, local) ||
                    IsConfiguredOwnershipSink(invocation, local, context),
                IReturnOperation returned => ReferencesLocal(returned.ReturnedValue, local),
                ILocalReferenceOperation reference =>
                    SymbolEqualityComparer.Default.Equals(reference.Local, local) &&
                    IsUsingResource(reference.Syntax, local, context),
                ISimpleAssignmentOperation assignment =>
                    assignment.Target.Type is not null &&
                    (assignment.Target is IFieldReferenceOperation || assignment.Target is IPropertyReferenceOperation) &&
                    ReferencesLocal(assignment.Value, local),
                _ => operation.Syntax is UsingStatementSyntax usingStatement &&
                    usingStatement.Expression is { } expression &&
                    SymbolEqualityComparer.Default.Equals(
                        context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol,
                        local),
            };
            return transfers ? (state & ~owned) | disposed : state;
        }
    }

    private static bool IsDispose(IInvocationOperation invocation, ILocalSymbol local) =>
        invocation.TargetMethod.Name == "Dispose" &&
        invocation.Arguments.Length == 0 &&
        ReferencesLocal(invocation.Instance, local);

    private static bool IsUsingResource(
        SyntaxNode syntax,
        ILocalSymbol local,
        SyntaxNodeAnalysisContext context)
    {
        var usingStatement = syntax.FirstAncestorOrSelf<UsingStatementSyntax>();
        return usingStatement?.Expression is { } expression &&
            expression.Span.Contains(syntax.Span) &&
            SymbolEqualityComparer.Default.Equals(
                context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol,
                local);
    }

    private static bool IsConfiguredOwnershipSink(
        IInvocationOperation invocation,
        ILocalSymbol local,
        SyntaxNodeAnalysisContext context)
    {
        if (!invocation.Arguments.Any(argument => ReferencesLocal(argument.Value, local)))
        {
            return false;
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
        if (!options.TryGetValue("ubp_0072_ownership_transfer_methods", out var configured))
        {
            return false;
        }

        var documentationId = invocation.TargetMethod.OriginalDefinition.GetDocumentationCommentId();
        return configured.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Any(value => value == invocation.TargetMethod.Name || value == documentationId);
    }

    private static bool ReferencesLocal(IOperation? operation, ILocalSymbol local) =>
        operation is not null && operation.DescendantsAndSelf()
            .OfType<ILocalReferenceOperation>()
            .Any(reference => SymbolEqualityComparer.Default.Equals(reference.Local, local));

    internal static void AnalyzeReturn(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ReturnStatementSyntax { Expression: { } expression })
        {
            return;
        }

        if (TryGetTemporaryEscape(
                expression,
                context.SemanticModel,
                context.CancellationToken,
                out var allocatorKind))
        {
            Report(context, TemporaryAllocatorEscape, expression.GetLocation(), allocatorKind);
        }
    }

    internal static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not AssignmentExpressionSyntax assignment)
        {
            return;
        }

        var target = context.SemanticModel.GetSymbolInfo(
            assignment.Left,
            context.CancellationToken).Symbol;
        if (target is not IFieldSymbol && target is not IPropertySymbol)
        {
            return;
        }

        if (TryGetTemporaryEscape(
                assignment.Right,
                context.SemanticModel,
                context.CancellationToken,
                out var allocatorKind))
        {
            // TempJob allocations are specifically intended to survive long enough for a
            // scheduled job. Allocator.Temp allocations are thread-local and must never
            // receive the same job-field exemption.
            if (allocatorKind == "TempJob" &&
                target is IFieldSymbol { ContainingType: { } containingType } &&
                UnitySymbolCache.IsUnityJobType(containingType))
            {
                return;
            }

            Report(context, TemporaryAllocatorEscape, assignment.Right.GetLocation(), allocatorKind);
        }
    }

    internal static void AnalyzeExpressionStatement(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ExpressionStatementSyntax first ||
            first.Expression is not AssignmentExpressionSyntax firstAssignment ||
            !TryGetTransformPropertyAssignment(
                firstAssignment,
                context,
                "localPosition",
                out var receiver,
                out var receiverSymbol) ||
            first.Parent is not BlockSyntax block)
        {
            return;
        }

        var index = block.Statements.IndexOf(first);
        if (index < 0 || index + 1 >= block.Statements.Count ||
            block.Statements[index + 1] is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax secondAssignment } second ||
            !TryGetTransformPropertyAssignment(
                secondAssignment,
                context,
                "localRotation",
                out var secondReceiver,
                out var secondReceiverSymbol) ||
            !SyntaxFactory.AreEquivalent(receiver, secondReceiver) ||
            !SymbolEqualityComparer.Default.Equals(receiverSymbol, secondReceiverSymbol) ||
            first.GetTrailingTrivia().Concat(second.GetLeadingTrivia()).Any(trivia =>
                !trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia)))
        {
            return;
        }

        Report(context, CombineLocalPositionAndRotation, first.GetLocation());
    }

    private static bool TryGetTransformPropertyAssignment(
        AssignmentExpressionSyntax assignment,
        SyntaxNodeAnalysisContext context,
        string propertyName,
        out ExpressionSyntax receiver,
        out ISymbol receiverSymbol)
    {
        receiver = null!;
        receiverSymbol = null!;
        if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) ||
            assignment.Left is not MemberAccessExpressionSyntax memberAccess ||
            memberAccess.Name.Identifier.ValueText != propertyName ||
            context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol is not IPropertySymbol property ||
            property.ContainingType?.ToDisplayString() != "UnityEngine.Transform" ||
            !property.ContainingType.GetMembers("SetLocalPositionAndRotation").OfType<IMethodSymbol>().Any(method => method.Parameters.Length == 2) ||
            context.SemanticModel.GetSymbolInfo(memberAccess.Expression, context.CancellationToken).Symbol is not { } symbol ||
            symbol is not ILocalSymbol and not IParameterSymbol)
        {
            return false;
        }

        receiver = memberAccess.Expression;
        receiverSymbol = symbol;
        return true;
    }

    internal static async Task<Document> CombineLocalPositionAndRotationAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var first = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<ExpressionStatementSyntax>();
        if (first?.Expression is not AssignmentExpressionSyntax
            {
                Left: MemberAccessExpressionSyntax positionAccess,
                Right: var position
            } || first.Parent is not BlockSyntax block)
        {
            return document;
        }

        var index = block.Statements.IndexOf(first);
        if (index < 0 || index + 1 >= block.Statements.Count ||
            block.Statements[index + 1] is not ExpressionStatementSyntax
            {
                Expression: AssignmentExpressionSyntax { Right: var rotation }
            } second)
        {
            return document;
        }

        if (position is ObjectCreationExpressionSyntax { ArgumentList: { } argumentList } creation)
        {
            position = SyntaxFactory.ImplicitObjectCreationExpression(creation.NewKeyword, argumentList, creation.Initializer)
                .WithTriviaFrom(position);
        }

        var invocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                positionAccess.Expression.WithoutTrivia(),
                SyntaxFactory.IdentifierName("SetLocalPositionAndRotation")),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
            {
                SyntaxFactory.Argument(position.WithoutTrivia()),
                SyntaxFactory.Argument(rotation.WithoutTrivia())
            })));
        var replacement = SyntaxFactory.ExpressionStatement(invocation)
            .WithLeadingTrivia(first.GetLeadingTrivia())
            .WithTrailingTrivia(second.GetTrailingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);
        var statements = block.Statements
            .RemoveAt(index)
            .RemoveAt(index)
            .Insert(index, replacement);
        return document.WithSyntaxRoot(root.ReplaceNode(block, block.WithStatements(statements)));
    }

    internal static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not TypeDeclarationSyntax declaration)
        {
            return;
        }

        var groups = declaration.DescendantNodes(
                descendIntoChildren: node => node == declaration || node is not TypeDeclarationSyntax)
            .OfType<InvocationExpressionSyntax>()
            .Select(invocation => (
                Invocation: invocation,
                PropertyName: TryGetShaderPropertyName(
                    invocation,
                    context.SemanticModel,
                    context.CancellationToken)))
            .Where(item => item.PropertyName is not null)
            .GroupBy(item => item.PropertyName!, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var matches = group.OrderBy(item => item.Invocation.SpanStart).ToArray();
            if (matches.Length >= 2)
            {
                Report(
                    context,
                    ShaderPropertyId,
                    matches[0].Invocation.GetLocation(),
                    group.Key);
            }
        }
    }

    internal static async Task<Document> AssignJobHandleAsync(
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
        var statement = invocation?.FirstAncestorOrSelf<ExpressionStatementSyntax>();
        if (invocation is null || statement is null)
        {
            return document;
        }

        var name = GetUniqueLocalName(semanticModel, statement, "jobHandle");
        var replacement = SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"))
                    .AddVariables(
                        SyntaxFactory.VariableDeclarator(name)
                            .WithInitializer(
                                SyntaxFactory.EqualsValueClause(invocation.WithoutTrivia()))))
            .WithTriviaFrom(statement)
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(statement, replacement));
    }

    internal static async Task<Document> CacheShaderPropertyIdAsync(
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
        var declaration = invocation?.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        var propertyName = invocation is null
            ? null
            : TryGetShaderPropertyName(invocation, semanticModel, cancellationToken);
        if (invocation is null || declaration is null || propertyName is null)
        {
            return document;
        }

        var matchingCalls = declaration.DescendantNodes(
                descendIntoChildren: node => node == declaration || node is not TypeDeclarationSyntax)
            .OfType<InvocationExpressionSyntax>()
            .Where(candidate =>
                string.Equals(
                    TryGetShaderPropertyName(candidate, semanticModel, cancellationToken),
                    propertyName,
                    StringComparison.Ordinal))
            .ToArray();
        if (matchingCalls.Length < 2)
        {
            return document;
        }

        var fieldName = GetUniqueMemberName(
            semanticModel.GetDeclaredSymbol(declaration, cancellationToken) as INamedTypeSymbol,
            CreatePropertyIdName(propertyName));
        var replacedDeclaration = declaration.ReplaceNodes(
            matchingCalls,
            (original, _) => SyntaxFactory.IdentifierName(fieldName).WithTriviaFrom(original));
        var literal = matchingCalls[0].ArgumentList.Arguments[0].Expression.ToString();
        var field = SyntaxFactory.ParseMemberDeclaration(
                $"private static readonly int {fieldName} = UnityEngine.Shader.PropertyToID({literal});")
            ?.WithAdditionalAnnotations(Formatter.Annotation);
        if (field is null)
        {
            return document;
        }

        replacedDeclaration = replacedDeclaration
            .WithMembers(replacedDeclaration.Members.Insert(0, field))
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(declaration, replacedDeclaration));
    }

    private static RuleMetadata Create(
        string diagnosticId,
        string title,
        string message,
        string description,
        string fixTitle,
        string category,
        RuleSafety safety,
        bool hasCodeFix,
        bool supportsFixAll,
        params string[] requiredSymbols) =>
        new(
            diagnosticId,
            title,
            message,
            description,
            fixTitle,
            category,
            DiagnosticSeverity.Info,
            safety,
            hasCodeFix,
            supportsFixAll,
            requiredSymbols.ToImmutableArray(),
            diagnosticId switch
            {
                DiagnosticIds.DiscardedScheduledJobHandle =>
                    "Unity 2021.3 with the Unity Job System",
                DiagnosticIds.UndisposedPersistentNativeContainer or
                DiagnosticIds.InvalidTemporaryAllocatorEscape =>
                    "Unity 2021.3 with Collections 1.x",
                _ => "Unity 2021.3",
            });

    private static bool IsSupportedSchedulingMethod(IMethodSymbol method)
    {
        var containingNamespace = method.ContainingNamespace.ToDisplayString();
        return method.IsExtensionMethod &&
               (IsNamespaceOrChild(containingNamespace, "Unity.Jobs") ||
               containingNamespace == "Unity.Entities" &&
               method.ContainingType.Name == "IJobEntityExtensions");
    }

    private static bool IsNamespaceOrChild(string candidate, string expected) =>
        string.Equals(candidate, expected, StringComparison.Ordinal) ||
        candidate.StartsWith(expected + ".", StringComparison.Ordinal);

    private static bool TryGetTemporaryEscape(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out string allocatorKind)
    {
        return TryGetTemporaryEscape(
            expression,
            semanticModel,
            cancellationToken,
            new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default),
            out allocatorKind);
    }

    private static bool TryGetTemporaryEscape(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        HashSet<ILocalSymbol> visitedLocals,
        out string allocatorKind)
    {
        if (TryGetAllocatorKind(expression, semanticModel, cancellationToken, out allocatorKind) &&
            (allocatorKind == "Temp" || allocatorKind == "TempJob"))
        {
            return true;
        }

        if (expression is AnonymousFunctionExpressionSyntax lambda)
        {
            return TryGetCapturedTemporaryAllocator(
                lambda,
                semanticModel,
                cancellationToken,
                visitedLocals,
                out allocatorKind);
        }

        if (expression is not IdentifierNameSyntax identifier ||
            semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol is not ILocalSymbol local ||
            !visitedLocals.Add(local) ||
            local.RefKind != RefKind.None ||
            local.DeclaringSyntaxReferences.Length != 1 ||
            local.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) is not VariableDeclaratorSyntax
            {
                Initializer.Value: { } initializer
            } declarator ||
            initializer is RefExpressionSyntax ||
            !HasUnambiguousUnwrittenValue(
                local,
                declarator,
                expression,
                semanticModel,
                cancellationToken))
        {
            allocatorKind = string.Empty;
            return false;
        }

        return TryGetTemporaryEscape(
            initializer,
            semanticModel,
            cancellationToken,
            visitedLocals,
            out allocatorKind);
    }

    private static bool TryGetCapturedTemporaryAllocator(
        AnonymousFunctionExpressionSyntax lambda,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        HashSet<ILocalSymbol> visitedLocals,
        out string allocatorKind)
    {
        foreach (var identifier in lambda.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol is not ILocalSymbol local ||
                local.DeclaringSyntaxReferences.Any(reference =>
                    lambda.Span.Contains(reference.Span)) ||
                !TryGetTemporaryEscape(
                    identifier,
                    semanticModel,
                    cancellationToken,
                    new HashSet<ILocalSymbol>(visitedLocals, SymbolEqualityComparer.Default),
                    out allocatorKind))
            {
                continue;
            }

            return true;
        }

        allocatorKind = string.Empty;
        return false;
    }

    private static bool HasUnambiguousUnwrittenValue(
        ILocalSymbol local,
        VariableDeclaratorSyntax declarator,
        ExpressionSyntax use,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var declarationStatement = declarator.AncestorsAndSelf()
            .OfType<StatementSyntax>()
            .FirstOrDefault(statement => statement.Parent is BlockSyntax);
        var useStatement = use.AncestorsAndSelf()
            .OfType<StatementSyntax>()
            .FirstOrDefault(statement => statement.Parent is BlockSyntax);
        if (declarationStatement?.Parent is not BlockSyntax declarationBlock ||
            useStatement?.Parent is not BlockSyntax useBlock ||
            declarationBlock != useBlock ||
            declarator.SpanStart >= use.SpanStart)
        {
            return false;
        }

        return !declarationBlock.DescendantNodes()
            .Where(node => node.SpanStart > declarator.Span.End && node.SpanStart < use.SpanStart)
            .Any(node => node switch
            {
                AssignmentExpressionSyntax assignment => IsSymbol(assignment.Left, local),
                PrefixUnaryExpressionSyntax prefix when
                    prefix.IsKind(SyntaxKind.PreIncrementExpression) ||
                    prefix.IsKind(SyntaxKind.PreDecrementExpression) => IsSymbol(prefix.Operand, local),
                PostfixUnaryExpressionSyntax postfix when
                    postfix.IsKind(SyntaxKind.PostIncrementExpression) ||
                    postfix.IsKind(SyntaxKind.PostDecrementExpression) => IsSymbol(postfix.Operand, local),
                ArgumentSyntax { RefKindKeyword.RawKind: not 0 } argument => IsSymbol(argument.Expression, local),
                RefExpressionSyntax reference => IsSymbol(reference.Expression, local),
                _ => false
            });

        bool IsSymbol(ExpressionSyntax candidate, ISymbol symbol) =>
            SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(candidate, cancellationToken).Symbol,
                symbol);
    }

    private static bool TryGetAllocatorKind(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out string allocatorKind)
    {
        allocatorKind = string.Empty;
        if (expression is not ObjectCreationExpressionSyntax creation ||
            semanticModel.GetTypeInfo(creation, cancellationToken).Type is not INamedTypeSymbol type ||
            type.ConstructedFrom is not { } constructed ||
            !SymbolEqualityComparer.Default.Equals(
                constructed,
                UnitySymbolCache.GetTypeByMetadataName(
                    semanticModel.Compilation,
                    "Unity.Collections.NativeArray`1")) ||
            semanticModel.GetSymbolInfo(creation, cancellationToken).Symbol is not IMethodSymbol constructor)
        {
            return false;
        }

        for (var index = 0; index < creation.ArgumentList?.Arguments.Count; index++)
        {
            var argument = creation.ArgumentList.Arguments[index];
            var parameter = argument.NameColon is null
                ? index < constructor.Parameters.Length ? constructor.Parameters[index] : null
                : constructor.Parameters.FirstOrDefault(candidate =>
                    candidate.Name == argument.NameColon.Name.Identifier.ValueText);
            if (parameter?.Type is not INamedTypeSymbol parameterType ||
                !SymbolEqualityComparer.Default.Equals(
                    parameterType,
                    UnitySymbolCache.GetTypeByMetadataName(
                        semanticModel.Compilation,
                        "Unity.Collections.Allocator")))
            {
                continue;
            }

            if (semanticModel.GetSymbolInfo(argument.Expression, cancellationToken).Symbol is IFieldSymbol
                {
                    ContainingType: { } containingType
                } allocator &&
                SymbolEqualityComparer.Default.Equals(parameterType, containingType))
            {
                allocatorKind = allocator.Name;
                return true;
            }
        }

        return false;
    }

    private static string? TryGetShaderPropertyName(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (invocation.ArgumentList.Arguments.Count != 1 ||
            semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol
            {
                Name: "PropertyToID",
                IsStatic: true
            } method ||
            !SymbolEqualityComparer.Default.Equals(
                method.ContainingType,
                UnitySymbolCache.GetTypeByMetadataName(
                    semanticModel.Compilation,
                    "UnityEngine.Shader")))
        {
            return null;
        }

        var argument = invocation.ArgumentList.Arguments[0].Expression;
        if (argument is not LiteralExpressionSyntax literal ||
            !literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return null;
        }

        var constant = semanticModel.GetConstantValue(argument, cancellationToken);
        return constant.HasValue ? constant.Value as string : null;
    }

    private static string GetUniqueLocalName(
        SemanticModel semanticModel,
        StatementSyntax statement,
        string baseName)
    {
        var scope = statement.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>() as SyntaxNode ??
                    statement.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>() ??
                    statement.Parent;
        var identifiers = scope?.DescendantTokens()
            .Where(token => token.IsKind(SyntaxKind.IdentifierToken))
            .Select(token => token.ValueText)
            .ToImmutableHashSet(StringComparer.Ordinal) ??
            ImmutableHashSet<string>.Empty;
        var name = baseName;
        var suffix = 1;
        while (identifiers.Contains(name) ||
               semanticModel.LookupSymbols(statement.SpanStart, name: name).Length != 0)
        {
            name = baseName + suffix++;
        }

        return name;
    }

    private static string GetUniqueMemberName(INamedTypeSymbol? containingType, string baseName)
    {
        var existingNames = containingType?.GetMembers()
            .Select(member => member.Name)
            .ToImmutableHashSet(StringComparer.Ordinal) ??
            ImmutableHashSet<string>.Empty;
        var name = baseName;
        var suffix = 1;
        while (existingNames.Contains(name))
        {
            name = baseName + suffix++;
        }

        return name;
    }

    private static string CreatePropertyIdName(string propertyName)
    {
        var builder = new StringBuilder();
        var makeUpper = true;
        foreach (var character in propertyName.TrimStart('_'))
        {
            if (!char.IsLetterOrDigit(character))
            {
                makeUpper = true;
                continue;
            }

            builder.Append(makeUpper ? char.ToUpperInvariant(character) : character);
            makeUpper = false;
        }

        if (builder.Length == 0 || char.IsDigit(builder[0]))
        {
            builder.Insert(0, "ShaderProperty");
        }

        return builder.Append("Id").ToString();
    }

    private static void Report(
        SyntaxNodeAnalysisContext context,
        RuleMetadata metadata,
        Location location,
        params object[] arguments)
    {
        if (AnalyzerConfiguration.For(context).IsRuleEnabled(metadata.DiagnosticId))
        {
            context.ReportDiagnostic(Diagnostic.Create(metadata.Descriptor, location, arguments));
        }
    }
}
