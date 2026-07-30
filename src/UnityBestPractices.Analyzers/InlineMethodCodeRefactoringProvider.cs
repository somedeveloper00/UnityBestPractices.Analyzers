using System.Collections.Generic;
using System.Collections.Immutable;
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
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace UnityBestPractices.Analyzers;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(InlineMethodCodeRefactoringProvider)), Shared]
public sealed class InlineMethodCodeRefactoringProvider : CodeRefactoringProvider
{
    public const string Title = "Inline method";

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var invocation = FindInvocation(root, context.Span);
        if (semanticModel is null)
        {
            return;
        }

        SyntaxNode nodeToReplace;
        SyntaxNode replacement;
        if (invocation is not null)
        {
            if (!TryCreateReplacement(
                    invocation,
                    semanticModel,
                    context.CancellationToken,
                    out nodeToReplace,
                    out replacement))
            {
                return;
            }
        }
        else
        {
            var methodGroup = FindMethodGroup(root, context.Span);
            if (methodGroup is null ||
                !TryCreateLambdaReplacement(
                    methodGroup,
                    semanticModel,
                    context.CancellationToken,
                    out replacement))
            {
                return;
            }

            nodeToReplace = methodGroup;
        }

        context.RegisterRefactoring(CodeAction.Create(
            FixTitleLocalizer.Get(FixTitleLocalizer.InlineMethod, Title),
            cancellationToken => InlineAsync(context.Document, nodeToReplace, replacement, cancellationToken),
            Title));
    }

    private static InvocationExpressionSyntax? FindInvocation(SyntaxNode? root, TextSpan span)
    {
        if (root is null || root.FullSpan.IsEmpty || span.Start >= root.FullSpan.End)
        {
            return null;
        }

        var position = System.Math.Min(span.Start, root.FullSpan.End - 1);
        return root.FindToken(position).Parent?
            .AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(invocation =>
                span.IsEmpty
                    ? invocation.Expression.FullSpan.Contains(position) ||
                      position == invocation.Expression.Span.End
                    : invocation.Expression.FullSpan.IntersectsWith(span));
    }

    private static ExpressionSyntax? FindMethodGroup(SyntaxNode? root, TextSpan span)
    {
        if (root is null || root.FullSpan.IsEmpty || span.Start >= root.FullSpan.End)
        {
            return null;
        }

        var position = System.Math.Min(span.Start, root.FullSpan.End - 1);
        var tokens = span.IsEmpty && position > 0
            ? new[] { root.FindToken(position), root.FindToken(position - 1) }
            : new[] { root.FindToken(position) };
        var name = tokens
            .SelectMany(token => token.Parent?.AncestorsAndSelf() ?? Enumerable.Empty<SyntaxNode>())
            .OfType<SimpleNameSyntax>()
            .FirstOrDefault(candidate =>
                span.IsEmpty
                    ? candidate.FullSpan.Contains(position) || position == candidate.Span.End
                    : candidate.FullSpan.IntersectsWith(span));
        if (name is null)
        {
            return null;
        }

        var expression = name.Parent is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name == name
                ? (ExpressionSyntax)memberAccess
                : name;
        return expression.Parent is InvocationExpressionSyntax invocation &&
            invocation.Expression == expression
                ? null
                : expression;
    }

    private static bool TryCreateReplacement(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out SyntaxNode nodeToReplace,
        out SyntaxNode replacement)
    {
        nodeToReplace = null!;
        replacement = null!;
        if (semanticModel.GetOperation(invocation, cancellationToken) is not IInvocationOperation operation)
        {
            return false;
        }

        var method = operation.TargetMethod;
        if (method.MethodKind != MethodKind.Ordinary || method.IsAsync ||
            method.ReturnsByRef || method.ReturnsByRefReadonly ||
            method.Parameters.Any(parameter => parameter.RefKind != RefKind.None || parameter.IsParams) ||
            method.DeclaringSyntaxReferences.Length != 1)
        {
            return false;
        }

        var declaration = method.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) as MethodDeclarationSyntax;
        if (TryCreateVoidStatementReplacement(
                invocation,
                operation,
                method,
                declaration,
                semanticModel,
                cancellationToken,
                out nodeToReplace,
                out replacement))
        {
            return true;
        }

        if (method.IsGenericMethod)
        {
            return false;
        }

        if (!method.IsStatic &&
            (semanticModel.GetEnclosingSymbol(invocation.SpanStart, cancellationToken)?.ContainingType is not INamedTypeSymbol containingType ||
             !SymbolEqualityComparer.Default.Equals(containingType, method.ContainingType) ||
             operation.Instance is not IInstanceReferenceOperation { IsImplicit: true } &&
             invocation.Expression is not MemberAccessExpressionSyntax
             {
                 Expression: ThisExpressionSyntax
             }))
        {
            return false;
        }

        var bodyExpression = declaration?.ExpressionBody?.Expression ??
            (declaration?.Body?.Statements.Count == 1 && declaration.Body.Statements[0] is ReturnStatementSyntax returnStatement
                ? returnStatement.Expression
                : null);
        if (bodyExpression is null)
        {
            return false;
        }

        var declarationModel = semanticModel.Compilation.GetSemanticModel(declaration!.SyntaxTree);
        var bodyType = declarationModel.GetTypeInfo(bodyExpression, cancellationToken).Type;
        if (!SymbolEqualityComparer.Default.Equals(bodyType, method.ReturnType))
        {
            // Preserve the implicit conversion performed at the return boundary.
            return false;
        }

        var identifiers = bodyExpression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().ToImmutableArray();
        var parameterUses = new List<(IdentifierNameSyntax Syntax, IParameterSymbol Symbol)>();
        foreach (var identifier in identifiers)
        {
            var symbol = declarationModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
            if (symbol is IParameterSymbol parameter && SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, method))
            {
                parameterUses.Add((identifier, parameter));
            }
            else if (!CanPreserveExpressionSymbol(identifier, symbol, method))
            {
                return false;
            }
        }

        if (method.Parameters.Any(parameter => parameterUses.Count(use =>
                SymbolEqualityComparer.Default.Equals(use.Symbol, parameter)) != 1))
        {
            return false;
        }

        var arguments = operation.Arguments
            .Where(argument => !argument.IsImplicit && argument.Parameter is not null)
            .OrderBy(argument => argument.Syntax.SpanStart)
            .ToImmutableArray();
        if (arguments.Length != method.Parameters.Length)
        {
            return false;
        }

        if (arguments.Any(argument => !argument.InConversion.IsIdentity))
        {
            // Inlining would otherwise remove the conversion performed when the
            // value crosses the method parameter boundary.
            return false;
        }

        var argumentByParameter = new Dictionary<IParameterSymbol, ExpressionSyntax>(
            SymbolEqualityComparer.Default);
        foreach (var argument in arguments)
        {
            var argumentSyntax = (ArgumentSyntax)argument.Syntax;
            var sourceType = semanticModel.GetTypeInfo(
                argumentSyntax.Expression,
                cancellationToken).Type;
            if (!SymbolEqualityComparer.Default.Equals(sourceType, argument.Parameter!.Type))
            {
                return false;
            }

            argumentByParameter.Add(
                argument.Parameter!,
                argumentSyntax.Expression);
        }

        // Substitution must not reorder evaluation of argument expressions.
        var useOrdinals = parameterUses.Select(use => use.Symbol.Ordinal).ToImmutableArray();
        var argumentOrdinals = arguments.Select(argument => argument.Parameter!.Ordinal).ToImmutableArray();
        if (!useOrdinals.SequenceEqual(argumentOrdinals))
        {
            return false;
        }

        var substitutedExpression = (ExpressionSyntax)new InlineExpressionRewriter(
                declarationModel,
                argumentByParameter,
                method,
                cancellationToken)
            .Visit(bodyExpression)!;
        replacement = SyntaxFactory.ParenthesizedExpression(substitutedExpression.WithoutTrivia())
            .WithTriviaFrom(invocation)
            .WithAdditionalAnnotations(Formatter.Annotation);

        var argumentExpressions = argumentByParameter.Values.ToImmutableArray();
        var orphanedTrivia = invocation.DescendantTrivia(descendIntoTrivia: true)
            .Where(trivia =>
                invocation.Span.Contains(trivia.Span) &&
                !argumentExpressions.Any(expression => expression.FullSpan.Contains(trivia.Span)))
            .ToImmutableArray();
        if (orphanedTrivia.Any(IsComment))
        {
            replacement = replacement.WithLeadingTrivia(
                invocation.GetLeadingTrivia().AddRange(orphanedTrivia));
        }

        nodeToReplace = invocation;
        return true;
    }

    private static bool CanPreserveExpressionSymbol(
        IdentifierNameSyntax identifier,
        ISymbol? symbol,
        IMethodSymbol method)
    {
        if (symbol is null)
        {
            return identifier.Identifier.ValueText == "nameof" &&
                identifier.Parent is InvocationExpressionSyntax { Expression: var expression } &&
                expression == identifier;
        }

        // Names which are already qualified retain their receiver, while type and
        // namespace names have compilation-wide meaning. Unqualified members of the
        // declaring type are made explicit by InlineExpressionRewriter so that a
        // caller local with the same name cannot capture them.
        if (identifier.Parent is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name == identifier)
        {
            return true;
        }

        if (symbol is INamedTypeSymbol || symbol is INamespaceSymbol)
        {
            return true;
        }

        return symbol is IFieldSymbol or IPropertySymbol or IEventSymbol or IMethodSymbol &&
            SymbolEqualityComparer.Default.Equals(symbol.ContainingType, method.ContainingType);
    }

    private static bool TryCreateVoidStatementReplacement(
        InvocationExpressionSyntax invocation,
        IInvocationOperation operation,
        IMethodSymbol method,
        MethodDeclarationSyntax? declaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out SyntaxNode nodeToReplace,
        out SyntaxNode replacement)
    {
        nodeToReplace = null!;
        replacement = null!;
        if (!method.ReturnsVoid ||
            invocation.Parent is not ExpressionStatementSyntax invocationStatement ||
            declaration is null ||
            declaration.Body is null && declaration.ExpressionBody is null ||
            semanticModel.GetEnclosingSymbol(invocation.SpanStart, cancellationToken)?.ContainingType is not INamedTypeSymbol containingType ||
            !SymbolEqualityComparer.Default.Equals(containingType, method.ContainingType))
        {
            return false;
        }

        if (!method.IsStatic)
        {
            // An explicitly supplied receiver may be evaluated or may differ from
            // the current instance. Only inline an implicit call on this instance,
            // and only while still inside the method's declaring type.
            if (operation.Instance is not IInstanceReferenceOperation { IsImplicit: true } &&
                !(invocation.Expression is MemberAccessExpressionSyntax
                {
                    Expression: ThisExpressionSyntax
                }))
            {
                return false;
            }
        }

        var declarationModel = semanticModel.Compilation.GetSemanticModel(declaration.SyntaxTree);
        var declaredMethod = declarationModel.GetDeclaredSymbol(declaration, cancellationToken);
        if (declaredMethod is null ||
            !TryCreateArgumentLocals(
                invocation,
                operation,
                method,
                declaredMethod,
                cancellationToken,
                out var argumentLocals,
                out var parameterReplacements,
                out var usedNames))
        {
            return false;
        }

        SyntaxNode sourceBody = declaration.Body ??
            (SyntaxNode)declaration.ExpressionBody!.Expression;
        var rewriter = new MethodBodyRewriter(
            declarationModel,
            parameterReplacements,
            CreateRenamedSymbols(sourceBody, declarationModel, usedNames, cancellationToken),
            CreateTypeParameterReplacements(declaredMethod, method),
            usedNames,
            rewriteReturns: true,
            cancellationToken);
        var rewrittenBody = rewriter.Visit(sourceBody)!;
        var bodyStatements = rewrittenBody is BlockSyntax block
            ? block.Statements
            : SyntaxFactory.SingletonList<StatementSyntax>(
                SyntaxFactory.ExpressionStatement((ExpressionSyntax)rewrittenBody));
        var statements = argumentLocals.AddRange(bodyStatements);
        if (rewriter.ReplacedReturn)
        {
            statements = statements.Add(SyntaxFactory.LabeledStatement(
                rewriter.ReturnLabel,
                SyntaxFactory.EmptyStatement()));
        }

        nodeToReplace = invocationStatement;
        replacement = SyntaxFactory.Block(statements)
            .WithTriviaFrom(invocationStatement)
            .WithAdditionalAnnotations(Formatter.Annotation);
        return true;
    }

    private static bool TryCreateLambdaReplacement(
        ExpressionSyntax methodGroup,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out SyntaxNode replacement)
    {
        replacement = null!;
        if (semanticModel.GetSymbolInfo(methodGroup, cancellationToken).Symbol is not IMethodSymbol method ||
            method.MethodKind != MethodKind.Ordinary ||
            method.ReturnsByRef ||
            method.ReturnsByRefReadonly ||
            method.ReducedFrom is not null ||
            method.DeclaringSyntaxReferences.Length == 0 ||
            semanticModel.GetTypeInfo(methodGroup, cancellationToken).ConvertedType is not INamedTypeSymbol delegateType ||
            delegateType.TypeKind != TypeKind.Delegate ||
            delegateType.DelegateInvokeMethod is not IMethodSymbol delegateInvoke ||
            delegateInvoke.Parameters.Length != method.Parameters.Length ||
            method.Parameters.Any(parameter => parameter.RefKind != RefKind.None) ||
            delegateInvoke.Parameters.Any(parameter => parameter.RefKind != RefKind.None) ||
            method.Parameters.Where((parameter, index) =>
                    !SymbolEqualityComparer.Default.Equals(
                        parameter.Type,
                        delegateInvoke.Parameters[index].Type))
                .Any() ||
            IsRemoval(methodGroup))
        {
            return false;
        }

        var declaration = method.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(cancellationToken))
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(candidate => candidate.Body is not null || candidate.ExpressionBody is not null);
        if (declaration is null ||
            declaration.Body?.DescendantNodes().OfType<YieldStatementSyntax>().Any() == true ||
            semanticModel.GetEnclosingSymbol(methodGroup.SpanStart, cancellationToken)?.ContainingType is not INamedTypeSymbol containingType ||
            !SymbolEqualityComparer.Default.Equals(containingType, method.ContainingType) ||
            !method.IsStatic && !HasStableImplicitReceiver(methodGroup))
        {
            return false;
        }

        var declarationModel = semanticModel.Compilation.GetSemanticModel(declaration.SyntaxTree);
        var declaredMethod = declarationModel.GetDeclaredSymbol(declaration, cancellationToken);
        if (declaredMethod is null)
        {
            return false;
        }

        var usedNames = CollectUsedNames(methodGroup.SyntaxTree.GetRoot(cancellationToken));
        var parameterReplacements = new Dictionary<IParameterSymbol, ExpressionSyntax>(
            SymbolEqualityComparer.Default);
        var lambdaParameters = new List<ParameterSyntax>(declaredMethod.Parameters.Length);
        foreach (var parameter in declaredMethod.Parameters)
        {
            var name = CreateUniqueName("__inline" + UppercaseFirst(parameter.Name), usedNames);
            lambdaParameters.Add(SyntaxFactory.Parameter(SyntaxFactory.Identifier(name)));
            parameterReplacements.Add(parameter, SyntaxFactory.IdentifierName(name));
        }

        var sourceBody = declaration.Body;
        SyntaxNode bodyNode = sourceBody ?? (SyntaxNode)declaration.ExpressionBody!.Expression;
        var rewriter = new MethodBodyRewriter(
            declarationModel,
            parameterReplacements,
            CreateRenamedSymbols(bodyNode, declarationModel, usedNames, cancellationToken),
            CreateTypeParameterReplacements(declaredMethod, method),
            usedNames,
            rewriteReturns: false,
            cancellationToken);
        var rewrittenBody = (CSharpSyntaxNode)rewriter.Visit(bodyNode)!;
        var lambda = SyntaxFactory.ParenthesizedLambdaExpression(
                SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(lambdaParameters)),
                rewrittenBody)
            .WithTriviaFrom(methodGroup)
            .WithAdditionalAnnotations(Formatter.Annotation);
        if (method.IsAsync)
        {
            lambda = lambda.WithAsyncKeyword(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
        }

        replacement = lambda;
        return true;
    }

    private static bool TryCreateArgumentLocals(
        InvocationExpressionSyntax invocation,
        IInvocationOperation operation,
        IMethodSymbol method,
        IMethodSymbol declaredMethod,
        CancellationToken cancellationToken,
        out SyntaxList<StatementSyntax> locals,
        out Dictionary<IParameterSymbol, ExpressionSyntax> replacements,
        out HashSet<string> usedNames)
    {
        locals = default;
        replacements = new Dictionary<IParameterSymbol, ExpressionSyntax>(
            SymbolEqualityComparer.Default);
        usedNames = CollectUsedNames(invocation.SyntaxTree.GetRoot(cancellationToken));
        var arguments = operation.Arguments
            .Where(argument => !argument.IsImplicit && argument.Parameter is not null)
            .OrderBy(argument => argument.Syntax.SpanStart)
            .ToImmutableArray();
        if (arguments.Length != method.Parameters.Length ||
            declaredMethod.Parameters.Length != method.Parameters.Length)
        {
            return false;
        }

        var declaredParameterByOrdinal = declaredMethod.Parameters.ToDictionary(
            parameter => parameter.Ordinal);
        foreach (var argument in arguments)
        {
            if (argument.Syntax is not ArgumentSyntax argumentSyntax)
            {
                return false;
            }

            var parameter = argument.Parameter!;
            var declaredParameter = declaredParameterByOrdinal[parameter.Ordinal];
            var name = CreateUniqueName(
                "__inline" + UppercaseFirst(parameter.Name),
                usedNames);
            var type = SyntaxFactory.ParseTypeName(
                parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            var declaration = SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(type)
                    .WithVariables(SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(name))
                            .WithInitializer(SyntaxFactory.EqualsValueClause(
                                argumentSyntax.Expression)))));
            locals = locals.Add(declaration);
            replacements.Add(declaredParameter, SyntaxFactory.IdentifierName(name));
        }

        return true;
    }

    private static Dictionary<ITypeParameterSymbol, TypeSyntax> CreateTypeParameterReplacements(
        IMethodSymbol declaredMethod,
        IMethodSymbol targetMethod)
    {
        var result = new Dictionary<ITypeParameterSymbol, TypeSyntax>(
            SymbolEqualityComparer.Default);
        if (!targetMethod.IsGenericMethod ||
            declaredMethod.TypeParameters.Length != targetMethod.TypeArguments.Length)
        {
            return result;
        }

        for (var index = 0; index < declaredMethod.TypeParameters.Length; index++)
        {
            result.Add(
                declaredMethod.TypeParameters[index],
                SyntaxFactory.ParseTypeName(
                    targetMethod.TypeArguments[index].ToDisplayString(
                        SymbolDisplayFormat.FullyQualifiedFormat)));
        }

        return result;
    }

    private static Dictionary<ISymbol, string> CreateRenamedSymbols(
        SyntaxNode body,
        SemanticModel semanticModel,
        HashSet<string> usedNames,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
        foreach (var node in body.DescendantNodesAndSelf())
        {
            ISymbol? symbol = null;
            string? name = null;
            switch (node)
            {
                case VariableDeclaratorSyntax variable:
                    symbol = semanticModel.GetDeclaredSymbol(variable, cancellationToken);
                    name = variable.Identifier.ValueText;
                    break;
                case ForEachStatementSyntax forEach:
                    symbol = semanticModel.GetDeclaredSymbol(forEach, cancellationToken);
                    name = forEach.Identifier.ValueText;
                    break;
                case CatchDeclarationSyntax catchDeclaration when catchDeclaration.Identifier != default:
                    symbol = semanticModel.GetDeclaredSymbol(catchDeclaration, cancellationToken);
                    name = catchDeclaration.Identifier.ValueText;
                    break;
                case SingleVariableDesignationSyntax designation:
                    symbol = semanticModel.GetDeclaredSymbol(designation, cancellationToken);
                    name = designation.Identifier.ValueText;
                    break;
                case LocalFunctionStatementSyntax localFunction:
                    symbol = semanticModel.GetDeclaredSymbol(localFunction, cancellationToken);
                    name = localFunction.Identifier.ValueText;
                    break;
                case ParameterSyntax parameter:
                    symbol = semanticModel.GetDeclaredSymbol(parameter, cancellationToken);
                    name = parameter.Identifier.ValueText;
                    break;
                case LabeledStatementSyntax labeled:
                    symbol = semanticModel.GetDeclaredSymbol(labeled, cancellationToken);
                    name = labeled.Identifier.ValueText;
                    break;
            }

            if (symbol is not null && name is not null && !result.ContainsKey(symbol))
            {
                result.Add(
                    symbol,
                    CreateUniqueName("__inline" + UppercaseFirst(name), usedNames));
            }
        }

        return result;
    }

    private static HashSet<string> CollectUsedNames(SyntaxNode root) =>
        new HashSet<string>(
            root.DescendantTokens()
                .Where(token => token.IsKind(SyntaxKind.IdentifierToken))
                .Select(token => token.ValueText),
            System.StringComparer.Ordinal);

    private static string CreateUniqueName(string baseName, HashSet<string> usedNames)
    {
        var name = baseName;
        for (var suffix = 1; !usedNames.Add(name); suffix++)
        {
            name = baseName + suffix;
        }

        return name;
    }

    private static string UppercaseFirst(string value) =>
        value.Length == 0
            ? "Value"
            : char.ToUpperInvariant(value[0]) + value.Substring(1);

    private static bool HasStableImplicitReceiver(ExpressionSyntax methodGroup) =>
        methodGroup is IdentifierNameSyntax ||
        methodGroup is GenericNameSyntax ||
        methodGroup is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax };

    private static bool IsRemoval(ExpressionSyntax methodGroup) =>
        methodGroup.Ancestors()
            .OfType<AssignmentExpressionSyntax>()
            .Any(assignment =>
                assignment.IsKind(SyntaxKind.SubtractAssignmentExpression) &&
                assignment.Right.Span.Contains(methodGroup.Span));

    private static bool IsComment(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
        trivia.IsKind(SyntaxKind.MultiLineCommentTrivia);

    private sealed class InlineExpressionRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _semanticModel;
        private readonly Dictionary<IParameterSymbol, ExpressionSyntax> _arguments;
        private readonly IMethodSymbol _method;
        private readonly CancellationToken _cancellationToken;

        public InlineExpressionRewriter(
            SemanticModel semanticModel,
            Dictionary<IParameterSymbol, ExpressionSyntax> arguments,
            IMethodSymbol method,
            CancellationToken cancellationToken)
        {
            _semanticModel = semanticModel;
            _arguments = arguments;
            _method = method;
            _cancellationToken = cancellationToken;
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            var symbol = _semanticModel.GetSymbolInfo(node, _cancellationToken).Symbol;
            if (symbol is IParameterSymbol parameter && _arguments.TryGetValue(parameter, out var argument))
            {
                return SyntaxFactory.ParenthesizedExpression(argument.WithoutTrivia())
                    .WithTriviaFrom(node);
            }

            if (node.Parent is not MemberAccessExpressionSyntax { Name: var name } || name != node)
            {
                if (symbol is IFieldSymbol or IPropertySymbol or IEventSymbol or IMethodSymbol &&
                    SymbolEqualityComparer.Default.Equals(symbol.ContainingType, _method.ContainingType))
                {
                    ExpressionSyntax receiver = symbol.IsStatic
                        ? SyntaxFactory.ParseExpression(_method.ContainingType.ToDisplayString(
                            SymbolDisplayFormat.FullyQualifiedFormat))
                        : SyntaxFactory.ThisExpression();
                    return SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            receiver,
                            node.WithoutTrivia())
                        .WithTriviaFrom(node);
                }
            }

            return base.VisitIdentifierName(node);
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" } &&
                _semanticModel.GetConstantValue(node, _cancellationToken) is
                    { HasValue: true, Value: string value })
            {
                return SyntaxFactory.LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        SyntaxFactory.Literal(value))
                    .WithTriviaFrom(node);
            }

            return base.VisitInvocationExpression(node);
        }
    }

    private sealed class MethodBodyRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _semanticModel;
        private readonly Dictionary<IParameterSymbol, ExpressionSyntax> _parameterReplacements;
        private readonly Dictionary<ISymbol, string> _renamedSymbols;
        private readonly Dictionary<ITypeParameterSymbol, TypeSyntax> _typeParameterReplacements;
        private readonly bool _rewriteReturns;
        private readonly CancellationToken _cancellationToken;
        private readonly Dictionary<string, string> _renamedLabels;
        private int _nestedFunctionDepth;

        public MethodBodyRewriter(
            SemanticModel semanticModel,
            Dictionary<IParameterSymbol, ExpressionSyntax> parameterReplacements,
            Dictionary<ISymbol, string> renamedSymbols,
            Dictionary<ITypeParameterSymbol, TypeSyntax> typeParameterReplacements,
            HashSet<string> usedNames,
            bool rewriteReturns,
            CancellationToken cancellationToken)
        {
            _semanticModel = semanticModel;
            _parameterReplacements = parameterReplacements;
            _renamedSymbols = renamedSymbols;
            _typeParameterReplacements = typeParameterReplacements;
            _rewriteReturns = rewriteReturns;
            _cancellationToken = cancellationToken;
            _renamedLabels = renamedSymbols
                .Where(pair => pair.Key.Kind == SymbolKind.Label)
                .ToDictionary(pair => pair.Key.Name, pair => pair.Value);
            ReturnLabel = SyntaxFactory.Identifier(
                CreateUniqueName("__inlineReturn", usedNames));
        }

        public bool ReplacedReturn { get; private set; }

        public SyntaxToken ReturnLabel { get; }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" } &&
                _semanticModel.GetSymbolInfo(node, _cancellationToken).Symbol is null &&
                _semanticModel.GetConstantValue(node, _cancellationToken) is
                    { HasValue: true, Value: string value })
            {
                return SyntaxFactory.LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        SyntaxFactory.Literal(value))
                    .WithTriviaFrom(node);
            }

            return base.VisitInvocationExpression(node);
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            var symbol = _semanticModel.GetSymbolInfo(node, _cancellationToken).Symbol;
            if (symbol is IParameterSymbol parameter &&
                _parameterReplacements.TryGetValue(parameter, out var replacement))
            {
                return replacement.WithTriviaFrom(node);
            }

            if (symbol is ITypeParameterSymbol typeParameter &&
                _typeParameterReplacements.TryGetValue(typeParameter, out var type))
            {
                return type.WithTriviaFrom(node);
            }

            if (symbol is not null && _renamedSymbols.TryGetValue(symbol, out var name))
            {
                return node.WithIdentifier(Rename(node.Identifier, name));
            }

            return base.VisitIdentifierName(node);
        }

        public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node) =>
            RenameVisitedDeclaration(
                node,
                (VariableDeclaratorSyntax)base.VisitVariableDeclarator(node)!,
                node.Identifier,
                (current, identifier) => current.WithIdentifier(identifier));

        public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node) =>
            RenameVisitedDeclaration(
                node,
                (ForEachStatementSyntax)base.VisitForEachStatement(node)!,
                node.Identifier,
                (current, identifier) => current.WithIdentifier(identifier));

        public override SyntaxNode? VisitCatchDeclaration(CatchDeclarationSyntax node) =>
            RenameVisitedDeclaration(
                node,
                (CatchDeclarationSyntax)base.VisitCatchDeclaration(node)!,
                node.Identifier,
                (current, identifier) => current.WithIdentifier(identifier));

        public override SyntaxNode? VisitSingleVariableDesignation(SingleVariableDesignationSyntax node) =>
            RenameVisitedDeclaration(
                node,
                (SingleVariableDesignationSyntax)base.VisitSingleVariableDesignation(node)!,
                node.Identifier,
                (current, identifier) => current.WithIdentifier(identifier));

        public override SyntaxNode? VisitLabeledStatement(LabeledStatementSyntax node) =>
            RenameVisitedDeclaration(
                node,
                (LabeledStatementSyntax)base.VisitLabeledStatement(node)!,
                node.Identifier,
                (current, identifier) => current.WithIdentifier(identifier));

        public override SyntaxNode? VisitGotoStatement(GotoStatementSyntax node)
        {
            var visited = (GotoStatementSyntax)base.VisitGotoStatement(node)!;
            if (visited.Expression is IdentifierNameSyntax identifier &&
                _renamedLabels.TryGetValue(identifier.Identifier.ValueText, out var name))
            {
                visited = visited.WithExpression(identifier.WithIdentifier(
                    Rename(identifier.Identifier, name)));
            }

            return visited;
        }

        public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            var visited = (LocalFunctionStatementSyntax)VisitNestedFunction(
                node,
                base.VisitLocalFunctionStatement)!;
            return RenameVisitedDeclaration(
                node,
                visited,
                node.Identifier,
                (current, identifier) => current.WithIdentifier(identifier));
        }

        public override SyntaxNode? VisitParameter(ParameterSyntax node) =>
            RenameVisitedDeclaration(
                node,
                (ParameterSyntax)base.VisitParameter(node)!,
                node.Identifier,
                (current, identifier) => current.WithIdentifier(identifier));

        public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
        {
            if (_rewriteReturns && _nestedFunctionDepth == 0)
            {
                ReplacedReturn = true;
                return SyntaxFactory.GotoStatement(
                        SyntaxKind.GotoStatement,
                        SyntaxFactory.IdentifierName(ReturnLabel))
                    .WithTriviaFrom(node);
            }

            return base.VisitReturnStatement(node);
        }

        public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) =>
            VisitNestedFunction(node, base.VisitSimpleLambdaExpression);

        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) =>
            VisitNestedFunction(node, base.VisitParenthesizedLambdaExpression);

        public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node) =>
            VisitNestedFunction(node, base.VisitAnonymousMethodExpression);

        private SyntaxNode? VisitNestedFunction<TNode>(
            TNode node,
            System.Func<TNode, SyntaxNode?> visit)
            where TNode : SyntaxNode
        {
            _nestedFunctionDepth++;
            var result = visit(node);
            _nestedFunctionDepth--;
            return result;
        }

        private TNode RenameVisitedDeclaration<TNode>(
            TNode original,
            TNode visited,
            SyntaxToken identifier,
            System.Func<TNode, SyntaxToken, TNode> replace)
            where TNode : SyntaxNode
        {
            var symbol = _semanticModel.GetDeclaredSymbol(original, _cancellationToken);
            return symbol is not null && _renamedSymbols.TryGetValue(symbol, out var name)
                ? replace(visited, Rename(identifier, name))
                : visited;
        }

        private static SyntaxToken Rename(SyntaxToken token, string name) =>
            SyntaxFactory.Identifier(
                token.LeadingTrivia,
                SyntaxKind.IdentifierToken,
                name,
                name,
                token.TrailingTrivia);

    }

    private static async Task<Document> InlineAsync(
        Document document,
        SyntaxNode nodeToReplace,
        SyntaxNode replacement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return root is null
            ? document
            : document.WithSyntaxRoot(root.ReplaceNode(nodeToReplace, replacement));
    }
}
