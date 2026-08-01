using System;
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
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace UnityBestPractices.Analyzers;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(MoveParameterCodeRefactoringProvider)), Shared]
public sealed class MoveParameterCodeRefactoringProvider : CodeRefactoringProvider
{
    public const string MoveLeftTitle = "Move parameter left";
    public const string MoveRightTitle = "Move parameter right";

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return;
        }

        var parameter = FindParameter(root, context.Span);
        if (parameter is null ||
            !TryGetDeclaration(parameter, semanticModel, context.CancellationToken, out var symbol, out var parameters))
        {
            return;
        }

        var parameterIndex = parameters.IndexOf(parameter);
        var symbolParameters = GetParameters(symbol);
        if (parameterIndex < 0 || symbolParameters.Length != parameters.Count)
        {
            return;
        }

        if (parameterIndex > 0 && CanSwap(symbolParameters, parameterIndex - 1, parameterIndex))
        {
            context.RegisterRefactoring(
                CodeAction.Create(
                    OmniSharpRefactoringTitle.Inline(
                        FixTitleLocalizer.Get(FixTitleLocalizer.MoveParameterLeft, MoveLeftTitle),
                        MoveLeftTitle),
                    cancellationToken => MoveParameterAsync(
                        context.Document.Project.Solution,
                        symbol,
                        parameterIndex,
                        parameterIndex - 1,
                        cancellationToken),
                    MoveLeftTitle));
        }

        if (parameterIndex < parameters.Count - 1 &&
            CanSwap(symbolParameters, parameterIndex, parameterIndex + 1))
        {
            context.RegisterRefactoring(
                CodeAction.Create(
                    OmniSharpRefactoringTitle.Extract(
                        FixTitleLocalizer.Get(FixTitleLocalizer.MoveParameterRight, MoveRightTitle),
                        MoveRightTitle),
                    cancellationToken => MoveParameterAsync(
                        context.Document.Project.Solution,
                        symbol,
                        parameterIndex,
                        parameterIndex + 1,
                        cancellationToken),
                    MoveRightTitle));
        }
    }

    internal static ParameterSyntax? FindParameter(SyntaxNode root, TextSpan span)
    {
        var position = Math.Min(span.Start, root.FullSpan.End);
        var token = root.FindToken(position);
        var parameter = token.Parent?.FirstAncestorOrSelf<ParameterSyntax>();
        if (parameter is null)
        {
            return null;
        }

        return span.IsEmpty
            ? parameter.FullSpan.Contains(position)
                ? parameter
                : null
            : parameter.FullSpan.IntersectsWith(span)
                ? parameter
                : null;
    }

    internal static bool TryGetDeclaration(
        ParameterSyntax parameter,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ISymbol symbol,
        out SeparatedSyntaxList<ParameterSyntax> parameters)
    {
        symbol = null!;
        parameters = default;
        if (parameter.Parent is not ParameterListSyntax parameterList)
        {
            return false;
        }

        parameters = parameterList.Parameters;
        symbol = parameterList.Parent switch
        {
            MethodDeclarationSyntax method =>
                semanticModel.GetDeclaredSymbol(method, cancellationToken)!,
            ConstructorDeclarationSyntax constructor =>
                semanticModel.GetDeclaredSymbol(constructor, cancellationToken)!,
            LocalFunctionStatementSyntax localFunction =>
                semanticModel.GetDeclaredSymbol(localFunction, cancellationToken)!,
            IndexerDeclarationSyntax indexer =>
                semanticModel.GetDeclaredSymbol(indexer, cancellationToken)!,
            _ => null!,
        };

        return symbol is IMethodSymbol or IPropertySymbol;
    }

    internal static ImmutableArray<IParameterSymbol> GetParameters(ISymbol symbol) =>
        symbol switch
        {
            IMethodSymbol method => method.Parameters,
            IPropertySymbol property when property.IsIndexer => property.Parameters,
            _ => ImmutableArray<IParameterSymbol>.Empty,
        };

    private static bool CanSwap(
        ImmutableArray<IParameterSymbol> parameters,
        int firstIndex,
        int secondIndex)
    {
        var first = parameters[firstIndex];
        var second = parameters[secondIndex];

        // C# requires `this` to remain first, `params` to remain last, and required
        // parameters to precede parameters with defaults.
        return !first.IsThis &&
               !second.IsThis &&
               !first.IsParams &&
               !second.IsParams &&
               first.HasExplicitDefaultValue == second.HasExplicitDefaultValue;
    }

    private static async Task<Solution> MoveParameterAsync(
        Solution solution,
        ISymbol symbol,
        int sourceIndex,
        int destinationIndex,
        CancellationToken cancellationToken)
    {
        var referencedSymbols = (await SymbolFinder.FindReferencesAsync(
                symbol,
                solution,
                cancellationToken).ConfigureAwait(false))
            .ToImmutableArray();

        var declarationSpans = new Dictionary<DocumentId, HashSet<TextSpan>>();
        var referenceSpans = new Dictionary<DocumentId, HashSet<TextSpan>>();
        var definitions = referencedSymbols
            .Select(reference => reference.Definition)
            .Concat(new[] { symbol })
            .Distinct(SymbolEqualityComparer.Default)
            .ToImmutableArray();

        foreach (var definition in definitions)
        {
            var parameters = GetParameters(definition);
            if (parameters.Length <= Math.Max(sourceIndex, destinationIndex) ||
                !CanSwap(
                    parameters,
                    Math.Min(sourceIndex, destinationIndex),
                    Math.Max(sourceIndex, destinationIndex)))
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

                AddSpan(declarationSpans, document.Id, syntaxReference.Span);
            }
        }

        foreach (var referencedSymbol in referencedSymbols)
        {
            foreach (var location in referencedSymbol.Locations)
            {
                if (location.IsImplicit)
                {
                    continue;
                }

                var document = solution.GetDocument(location.Document.Id);
                if (document is null)
                {
                    continue;
                }

                if (document.Project.Language != LanguageNames.CSharp)
                {
                    return solution;
                }

                AddSpan(referenceSpans, document.Id, location.Location.SourceSpan);
            }
        }

        var documentIds = declarationSpans.Keys
            .Concat(referenceSpans.Keys)
            .Distinct()
            .ToImmutableArray();
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

            var declarationNodes = FindDeclarationNodes(
                root,
                declarationSpans.TryGetValue(documentId, out var documentDeclarations)
                    ? documentDeclarations
                    : Enumerable.Empty<TextSpan>());
            var callEdits = CreateCallEdits(
                root,
                semanticModel,
                referenceSpans.TryGetValue(documentId, out var documentReferences)
                    ? documentReferences
                    : Enumerable.Empty<TextSpan>(),
                sourceIndex,
                destinationIndex,
                cancellationToken);

            if (callEdits is null)
            {
                return solution;
            }

            var edits = declarationNodes
                .Concat(callEdits.Keys)
                .Distinct()
                .ToImmutableArray();
            if (edits.IsEmpty)
            {
                continue;
            }

            var updatedRoot = root.ReplaceNodes(
                edits,
                (original, rewritten) =>
                {
                    if (callEdits.TryGetValue(original, out var argumentOrdinals))
                    {
                        return ReorderCall(
                            rewritten,
                            argumentOrdinals,
                            sourceIndex,
                            destinationIndex);
                    }

                    return ReorderDeclaration(rewritten, sourceIndex, destinationIndex);
                });
            changedSolution = changedSolution.WithDocumentSyntaxRoot(documentId, updatedRoot);
        }

        return changedSolution;
    }

    internal static void AddSpan(
        IDictionary<DocumentId, HashSet<TextSpan>> spans,
        DocumentId documentId,
        TextSpan span)
    {
        if (!spans.TryGetValue(documentId, out var documentSpans))
        {
            documentSpans = new HashSet<TextSpan>();
            spans.Add(documentId, documentSpans);
        }

        documentSpans.Add(span);
    }

    internal static ImmutableArray<SyntaxNode> FindDeclarationNodes(
        SyntaxNode root,
        IEnumerable<TextSpan> spans)
    {
        var builder = ImmutableArray.CreateBuilder<SyntaxNode>();
        foreach (var span in spans)
        {
            var node = root.FindNode(span, getInnermostNodeForTie: true);
            var declaration = node.FirstAncestorOrSelf<SyntaxNode>(IsSupportedDeclaration);
            if (declaration is not null)
            {
                builder.Add(declaration);
            }
        }

        return builder.ToImmutable();
    }

    private static bool IsSupportedDeclaration(SyntaxNode node) =>
        node is MethodDeclarationSyntax or
            ConstructorDeclarationSyntax or
            LocalFunctionStatementSyntax or
            IndexerDeclarationSyntax;

    private static Dictionary<SyntaxNode, CallEdit>? CreateCallEdits(
        SyntaxNode root,
        SemanticModel semanticModel,
        IEnumerable<TextSpan> spans,
        int sourceIndex,
        int destinationIndex,
        CancellationToken cancellationToken)
    {
        var edits = new Dictionary<SyntaxNode, CallEdit>();
        foreach (var span in spans)
        {
            var call = FindCall(root, span);
            if (call is null || edits.ContainsKey(call))
            {
                // Method groups, nameof, and documentation references do not have
                // argument lists to update.
                continue;
            }

            if (!TryGetCallEdit(
                    call,
                    semanticModel,
                    cancellationToken,
                    out var callEdit))
            {
                return null;
            }

            if (callEdit.Bindings.Any(
                    binding => binding.Ordinal == sourceIndex || binding.Ordinal == destinationIndex))
            {
                edits.Add(call, callEdit);
            }
        }

        return edits;
    }

    internal static SyntaxNode? FindCall(SyntaxNode root, TextSpan referenceSpan)
    {
        for (var node = root.FindNode(referenceSpan, getInnermostNodeForTie: true);
             node is not null;
             node = node.Parent)
        {
            switch (node)
            {
                case InvocationExpressionSyntax invocation
                    when invocation.Expression.Span.Contains(referenceSpan):
                    return invocation;
                case ObjectCreationExpressionSyntax creation
                    when creation.Type.Span.Contains(referenceSpan):
                    return creation;
                case ConstructorInitializerSyntax initializer
                    when !initializer.ArgumentList.Span.Contains(referenceSpan):
                    return initializer;
                case ElementAccessExpressionSyntax elementAccess
                    when !elementAccess.ArgumentList.Arguments.Any(
                        argument => argument.Span.Contains(referenceSpan)):
                    return elementAccess;
            }
        }

        return null;
    }

    internal static bool TryGetCallEdit(
        SyntaxNode call,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out CallEdit callEdit)
    {
        var arguments = GetArguments(call);
        if (arguments is null)
        {
            callEdit = null!;
            return false;
        }

        if (arguments.Value.Count == 0)
        {
            callEdit = new CallEdit(ImmutableArray<ArgumentBinding>.Empty, 0, 0);
            return true;
        }

        var operation = semanticModel.GetOperation(call, cancellationToken);
        var operationArguments = operation switch
        {
            IInvocationOperation invocation => invocation.Arguments,
            IObjectCreationOperation creation => creation.Arguments,
            IPropertyReferenceOperation property => property.Arguments,
            _ => default,
        };
        // Roslyn binds explicit arguments of a reduced extension call to the
        // original extension parameters (ordinals start at one), even though
        // the receiver is not present in the syntax argument list.
        var invocationSymbol = call is InvocationExpressionSyntax
            ? semanticModel.GetSymbolInfo(call, cancellationToken).Symbol as IMethodSymbol
            : null;
        var positionalOrdinalOffset = invocationSymbol?.ReducedFrom is not null ? 1 : 0;
        var parameterCount = operation switch
        {
            IInvocationOperation invocation when invocation.TargetMethod.ReducedFrom is not null =>
                invocation.TargetMethod.ReducedFrom.Parameters.Length,
            IInvocationOperation invocation => invocation.TargetMethod.Parameters.Length,
            IObjectCreationOperation creation => creation.Constructor?.Parameters.Length ?? 0,
            IPropertyReferenceOperation property => property.Property.Parameters.Length,
            _ => 0,
        };

        if (!operationArguments.IsDefault)
        {
            var bySpan = operationArguments
                .Where(argument => argument.Parameter is not null && argument.Syntax is ArgumentSyntax)
                .GroupBy(argument => argument.Syntax.Span)
                .ToDictionary(
                    group => group.Key,
                    group => new ArgumentBinding(
                        group.First().Parameter!.Ordinal,
                        group.First().Parameter!.Name));
            var builder = ImmutableArray.CreateBuilder<ArgumentBinding>(arguments.Value.Count);
            foreach (var argument in arguments.Value)
            {
                if (!bySpan.TryGetValue(argument.Span, out var binding))
                {
                    // Expanded params arguments may be represented by an
                    // implicit array in IOperation rather than one operation
                    // per ArgumentSyntax. Fall back to the target symbol so
                    // callers can still associate every explicit argument
                    // with the params parameter.
                    return TryGetCallEditFromSymbol(
                        call,
                        arguments.Value,
                        semanticModel,
                        cancellationToken,
                        out callEdit);
                }

                builder.Add(binding);
            }

            callEdit = new CallEdit(builder.ToImmutable(), positionalOrdinalOffset, parameterCount);
            return true;
        }

        return TryGetCallEditFromSymbol(
            call,
            arguments.Value,
            semanticModel,
            cancellationToken,
            out callEdit);
    }

    internal static SeparatedSyntaxList<ArgumentSyntax>? GetArguments(SyntaxNode call) =>
        call switch
        {
            InvocationExpressionSyntax invocation => invocation.ArgumentList.Arguments,
            ObjectCreationExpressionSyntax creation when creation.ArgumentList is not null =>
                creation.ArgumentList.Arguments,
            ConstructorInitializerSyntax initializer => initializer.ArgumentList.Arguments,
            ElementAccessExpressionSyntax elementAccess => elementAccess.ArgumentList.Arguments,
            _ => null,
        };

    private static bool TryGetCallEditFromSymbol(
        SyntaxNode call,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out CallEdit callEdit)
    {
        var target = semanticModel.GetSymbolInfo(call, cancellationToken).Symbol;
        var parameters = GetParameters(target!);
        var offset = target is IMethodSymbol { ReducedFrom: not null } ? 1 : 0;
        if (parameters.IsEmpty)
        {
            callEdit = null!;
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<ArgumentBinding>(arguments.Count);
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (argument.NameColon is not null)
            {
                var name = argument.NameColon.Name.Identifier.ValueText;
                var parameter = parameters.FirstOrDefault(candidate => candidate.Name == name);
                if (parameter is null)
                {
                    callEdit = null!;
                    return false;
                }

                builder.Add(new ArgumentBinding(parameter.Ordinal + offset, parameter.Name));
                continue;
            }

            var ordinal = Math.Min(index, parameters.Length - 1);
            var parameterAtOrdinal = parameters[ordinal];
            builder.Add(new ArgumentBinding(ordinal + offset, parameterAtOrdinal.Name));
        }

        callEdit = new CallEdit(builder.ToImmutable(), offset, parameters.Length + offset);
        return true;
    }

    private static SyntaxNode ReorderDeclaration(
        SyntaxNode declaration,
        int sourceIndex,
        int destinationIndex) =>
        declaration switch
        {
            MethodDeclarationSyntax method => method.WithParameterList(
                method.ParameterList.WithParameters(
                    Swap(method.ParameterList.Parameters, sourceIndex, destinationIndex))),
            ConstructorDeclarationSyntax constructor => constructor.WithParameterList(
                constructor.ParameterList.WithParameters(
                    Swap(constructor.ParameterList.Parameters, sourceIndex, destinationIndex))),
            LocalFunctionStatementSyntax localFunction => localFunction.WithParameterList(
                localFunction.ParameterList.WithParameters(
                    Swap(localFunction.ParameterList.Parameters, sourceIndex, destinationIndex))),
            IndexerDeclarationSyntax indexer => indexer.WithParameterList(
                indexer.ParameterList.WithParameters(
                    Swap(indexer.ParameterList.Parameters, sourceIndex, destinationIndex))),
            _ => declaration,
        };

    private static SyntaxNode ReorderCall(
        SyntaxNode call,
        CallEdit callEdit,
        int sourceIndex,
        int destinationIndex)
    {
        var arguments = GetArguments(call);
        if (arguments is null || arguments.Value.Count != callEdit.Bindings.Length)
        {
            return call;
        }

        var reordered = arguments.Value
            .Select((argument, index) => new
            {
                Argument = argument,
                Binding = callEdit.Bindings[index],
                OriginalIndex = index,
                NewOrdinal = GetNewOrdinal(
                    callEdit.Bindings[index].Ordinal,
                    sourceIndex,
                    destinationIndex),
            })
            .OrderBy(item => item.NewOrdinal)
            .ThenBy(item => item.OriginalIndex)
            .Select((item, index) => AddNameWhenPositionWouldChangeBinding(
                item.Argument,
                item.Binding.Name,
                item.NewOrdinal,
                index,
                callEdit))
            .ToImmutableArray();
        var updatedArguments = SyntaxFactory.SeparatedList(
            reordered,
            arguments.Value.GetSeparators());

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

    private static ArgumentSyntax AddNameWhenPositionWouldChangeBinding(
        ArgumentSyntax argument,
        string parameterName,
        int newOrdinal,
        int argumentIndex,
        CallEdit callEdit)
    {
        if (argument.NameColon is not null || callEdit.ParameterCount == 0)
        {
            return argument;
        }

        var positionalOrdinal = Math.Min(
            argumentIndex + callEdit.OrdinalOffset,
            callEdit.ParameterCount - 1);
        if (positionalOrdinal == newOrdinal)
        {
            return argument;
        }

        var identifier = SyntaxFacts.GetKeywordKind(parameterName) != SyntaxKind.None ||
                         SyntaxFacts.GetContextualKeywordKind(parameterName) != SyntaxKind.None
            ? SyntaxFactory.Identifier("@" + parameterName)
            : SyntaxFactory.Identifier(parameterName);
        var nameColon = SyntaxFactory.NameColon(SyntaxFactory.IdentifierName(identifier))
            .WithColonToken(
                SyntaxFactory.Token(SyntaxKind.ColonToken)
                    .WithTrailingTrivia(SyntaxFactory.Space));
        return argument.WithNameColon(nameColon);
    }

    private static int GetNewOrdinal(int oldOrdinal, int sourceIndex, int destinationIndex)
    {
        if (oldOrdinal == sourceIndex)
        {
            return destinationIndex;
        }

        return oldOrdinal == destinationIndex ? sourceIndex : oldOrdinal;
    }

    private static SeparatedSyntaxList<T> Swap<T>(
        SeparatedSyntaxList<T> list,
        int sourceIndex,
        int destinationIndex)
        where T : SyntaxNode
    {
        var items = list.ToArray();
        (items[sourceIndex], items[destinationIndex]) = (items[destinationIndex], items[sourceIndex]);
        return SyntaxFactory.SeparatedList(items, list.GetSeparators());
    }

    internal sealed class CallEdit
    {
        internal CallEdit(
            ImmutableArray<ArgumentBinding> bindings,
            int ordinalOffset,
            int parameterCount)
        {
            Bindings = bindings;
            OrdinalOffset = ordinalOffset;
            ParameterCount = parameterCount;
        }

        internal ImmutableArray<ArgumentBinding> Bindings { get; }

        internal int OrdinalOffset { get; }

        internal int ParameterCount { get; }
    }

    internal readonly struct ArgumentBinding
    {
        internal ArgumentBinding(int ordinal, string name)
        {
            Ordinal = ordinal;
            Name = name;
        }

        internal int Ordinal { get; }

        internal string Name { get; }
    }
}
