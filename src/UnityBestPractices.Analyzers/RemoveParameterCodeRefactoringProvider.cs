using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

namespace UnityBestPractices.Analyzers;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(RemoveParameterCodeRefactoringProvider)), Shared]
public sealed class RemoveParameterCodeRefactoringProvider : CodeRefactoringProvider
{
    public const string Title = "Remove parameter";

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return;
        }

        var parameter = MoveParameterCodeRefactoringProvider.FindParameter(root, context.Span);
        if (parameter is null ||
            !MoveParameterCodeRefactoringProvider.TryGetDeclaration(
                parameter,
                semanticModel,
                context.CancellationToken,
                out var symbol,
                out var parameters))
        {
            return;
        }

        var parameterIndex = parameters.IndexOf(parameter);
        var symbolParameters = MoveParameterCodeRefactoringProvider.GetParameters(symbol);
        if (parameterIndex < 0 ||
            symbolParameters.Length != parameters.Count ||
            symbolParameters[parameterIndex].IsThis ||
            symbol is IPropertySymbol && symbolParameters.Length == 1)
        {
            return;
        }

        context.RegisterRefactoring(
            CodeAction.Create(
                Title,
                cancellationToken => RemoveParameterAsync(
                    context.Document.Project.Solution,
                    symbol,
                    parameterIndex,
                    cancellationToken),
                Title));
    }

    private static async Task<Solution> RemoveParameterAsync(
        Solution solution,
        ISymbol symbol,
        int parameterIndex,
        CancellationToken cancellationToken)
    {
        var referencedSymbols = (await SymbolFinder.FindReferencesAsync(
                symbol,
                solution,
                cancellationToken).ConfigureAwait(false))
            .ToImmutableArray();
        var definitions = referencedSymbols
            .Select(reference => reference.Definition)
            .Concat(new[] { symbol })
            .Distinct(SymbolEqualityComparer.Default)
            .ToImmutableArray();
        var declarationSpans = new Dictionary<DocumentId, HashSet<TextSpan>>();
        var referenceSpans = new Dictionary<DocumentId, HashSet<TextSpan>>();

        foreach (var definition in definitions)
        {
            var parameters = MoveParameterCodeRefactoringProvider.GetParameters(definition);
            if (parameterIndex >= parameters.Length ||
                parameters[parameterIndex].IsThis ||
                definition is IPropertySymbol && parameters.Length == 1)
            {
                return solution;
            }

            foreach (var syntaxReference in definition.DeclaringSyntaxReferences)
            {
                var document = solution.GetDocument(syntaxReference.SyntaxTree);
                if (document is null)
                {
                    continue;
                }

                if (document.Project.Language != LanguageNames.CSharp)
                {
                    return solution;
                }

                MoveParameterCodeRefactoringProvider.AddSpan(
                    declarationSpans,
                    document.Id,
                    syntaxReference.Span);
            }
        }

        foreach (var referencedSymbol in referencedSymbols)
        {
            foreach (var location in referencedSymbol.Locations.Where(location => !location.IsImplicit))
            {
                var document = solution.GetDocument(location.Document.Id);
                if (document is null)
                {
                    continue;
                }

                if (document.Project.Language != LanguageNames.CSharp)
                {
                    return solution;
                }

                MoveParameterCodeRefactoringProvider.AddSpan(
                    referenceSpans,
                    document.Id,
                    location.Location.SourceSpan);
            }
        }

        var documentIds = declarationSpans.Keys.Concat(referenceSpans.Keys).Distinct().ToImmutableArray();
        var changedSolution = solution;
        foreach (var documentId in documentIds)
        {
            var document = solution.GetDocument(documentId);
            if (document is null)
            {
                continue;
            }

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (root is null || semanticModel is null)
            {
                return solution;
            }

            var declarations = MoveParameterCodeRefactoringProvider.FindDeclarationNodes(
                root,
                declarationSpans.TryGetValue(documentId, out var declarationSet)
                    ? declarationSet
                    : Enumerable.Empty<TextSpan>());
            var calls = CreateCallEdits(
                root,
                semanticModel,
                referenceSpans.TryGetValue(documentId, out var referenceSet)
                    ? referenceSet
                    : Enumerable.Empty<TextSpan>(),
                parameterIndex,
                cancellationToken);
            if (calls is null)
            {
                return solution;
            }

            var edits = declarations.Concat(calls.Keys).Distinct().ToImmutableArray();
            var updatedRoot = root.ReplaceNodes(
                edits,
                (original, rewritten) => calls.ContainsKey(original)
                    ? RemoveArguments(rewritten, calls[original])
                    : RemoveDeclarationParameter(rewritten, parameterIndex));
            changedSolution = changedSolution.WithDocumentSyntaxRoot(documentId, updatedRoot);
        }

        return changedSolution;
    }

    private static Dictionary<SyntaxNode, ImmutableArray<int>>? CreateCallEdits(
        SyntaxNode root,
        SemanticModel semanticModel,
        IEnumerable<TextSpan> spans,
        int parameterIndex,
        CancellationToken cancellationToken)
    {
        var edits = new Dictionary<SyntaxNode, ImmutableArray<int>>();
        foreach (var span in spans)
        {
            var call = MoveParameterCodeRefactoringProvider.FindCall(root, span);
            if (call is null)
            {
                var node = root.FindNode(span, getInnermostNodeForTie: true);
                if (node.FirstAncestorOrSelf<CrefSyntax>() is null &&
                    !node.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().Any(
                        invocation =>
                            invocation.Expression is IdentifierNameSyntax name &&
                            name.Identifier.ValueText == "nameof" &&
                            invocation.ArgumentList.Span.Contains(span)))
                {
                    return null;
                }

                continue;
            }

            if (edits.ContainsKey(call))
            {
                continue;
            }

            if (!MoveParameterCodeRefactoringProvider.TryGetCallEdit(
                    call,
                    semanticModel,
                    cancellationToken,
                    out var callEdit))
            {
                return null;
            }

            var indexes = callEdit.Bindings
                .Select((binding, index) => new { binding.Ordinal, Index = index })
                .Where(item => item.Ordinal == parameterIndex)
                .Select(item => item.Index)
                .ToImmutableArray();
            if (!indexes.IsEmpty)
            {
                edits.Add(call, indexes);
            }
        }

        return edits;
    }

    private static SyntaxNode RemoveDeclarationParameter(SyntaxNode declaration, int parameterIndex) =>
        declaration switch
        {
            MethodDeclarationSyntax method => method.WithParameterList(
                method.ParameterList.WithParameters(method.ParameterList.Parameters.RemoveAt(parameterIndex))),
            ConstructorDeclarationSyntax constructor => constructor.WithParameterList(
                constructor.ParameterList.WithParameters(
                    constructor.ParameterList.Parameters.RemoveAt(parameterIndex))),
            LocalFunctionStatementSyntax localFunction => localFunction.WithParameterList(
                localFunction.ParameterList.WithParameters(
                    localFunction.ParameterList.Parameters.RemoveAt(parameterIndex))),
            IndexerDeclarationSyntax indexer => indexer.WithParameterList(
                indexer.ParameterList.WithParameters(indexer.ParameterList.Parameters.RemoveAt(parameterIndex))),
            _ => declaration,
        };

    private static SyntaxNode RemoveArguments(SyntaxNode call, ImmutableArray<int> indexes)
    {
        var arguments = MoveParameterCodeRefactoringProvider.GetArguments(call);
        if (arguments is null)
        {
            return call;
        }

        var updatedArguments = arguments.Value;
        foreach (var index in indexes.OrderByDescending(index => index))
        {
            updatedArguments = updatedArguments.RemoveAt(index);
        }

        return (call switch
        {
            InvocationExpressionSyntax invocation => invocation.WithArgumentList(
                invocation.ArgumentList.WithArguments(updatedArguments)),
            ObjectCreationExpressionSyntax creation when creation.ArgumentList is not null =>
                creation.WithArgumentList(creation.ArgumentList.WithArguments(updatedArguments)),
            ConstructorInitializerSyntax initializer => initializer.WithArgumentList(
                initializer.ArgumentList.WithArguments(updatedArguments)),
            ElementAccessExpressionSyntax elementAccess => elementAccess.WithArgumentList(
                elementAccess.ArgumentList.WithArguments(updatedArguments)),
            _ => call,
        }).WithAdditionalAnnotations(Formatter.Annotation);
    }
}
