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

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(RemoveSymbolCodeRefactoringProvider)), Shared]
public sealed class RemoveSymbolCodeRefactoringProvider : CodeRefactoringProvider
{
    public const string Title = "Remove symbol and all usages";

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return;
        }

        var token = root.FindToken(context.Span.Start);
        if (!token.Span.IntersectsWith(context.Span) && !context.Span.IsEmpty)
        {
            return;
        }

        var node = token.Parent;
        var symbol = node is null ? null : semanticModel.GetSymbolInfo(node, context.CancellationToken).Symbol;
        symbol ??= node?.AncestorsAndSelf()
            .Select(candidate => semanticModel.GetDeclaredSymbol(candidate, context.CancellationToken))
            .FirstOrDefault(candidate => candidate is not null);
        if (!IsSupported(symbol) || symbol!.DeclaringSyntaxReferences.IsEmpty)
        {
            return;
        }

        context.RegisterRefactoring(CodeAction.Create(
            FixTitleLocalizer.Get(FixTitleLocalizer.RemoveSymbol, Title),
            cancellationToken => RemoveAsync(context.Document.Project.Solution, symbol, cancellationToken),
            Title));
    }

    private static bool IsSupported(ISymbol? symbol) => symbol switch
    {
        ILocalSymbol => true,
        IFieldSymbol field => !field.IsImplicitlyDeclared,
        IMethodSymbol method => method.MethodKind == MethodKind.Ordinary && !method.IsImplicitlyDeclared,
        INamedTypeSymbol type => type.TypeKind == TypeKind.Class && !type.IsImplicitlyDeclared,
        _ => false,
    };

    private static async Task<Solution> RemoveAsync(
        Solution solution,
        ISymbol symbol,
        CancellationToken cancellationToken)
    {
        var referencedSymbols = (await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken)
            .ConfigureAwait(false)).ToImmutableArray();
        var declarations = referencedSymbols.Select(item => item.Definition)
            .Concat(new[] { symbol })
            .Distinct(SymbolEqualityComparer.Default)
            .SelectMany(item => item.DeclaringSyntaxReferences)
            .ToImmutableArray();
        var removals = new Dictionary<DocumentId, HashSet<TextSpan>>();

        foreach (var declaration in declarations)
        {
            var document = solution.GetDocument(declaration.SyntaxTree);
            if (document is not null)
            {
                Add(removals, document.Id, declaration.Span);
            }
        }

        foreach (var referencedSymbol in referencedSymbols)
        {
            foreach (var location in referencedSymbol.Locations.Where(item => !item.IsImplicit))
            {
                Add(removals, location.Document.Id, location.Location.SourceSpan);
            }
        }

        var changedSolution = solution;
        foreach (var pair in removals)
        {
            var document = solution.GetDocument(pair.Key);
            var root = document is null
                ? null
                : await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                continue;
            }

            var originalText = await document!.GetTextAsync(cancellationToken).ConfigureAwait(false);

            var nodes = pair.Value.Select(span => FindRemovalNode(root, span))
                .Where(candidate => candidate is not null)
                .Cast<SyntaxNode>()
                .Distinct()
                .ToImmutableArray();
            var outermostNodes = nodes.Where(candidate =>
                    !candidate.Ancestors().Any(nodes.Contains))
                .ToImmutableArray();
            var updatedRoot = root.RemoveNodes(outermostNodes, SyntaxRemoveOptions.KeepExteriorTrivia)
                ?.WithAdditionalAnnotations(Formatter.Annotation);
            if (updatedRoot is not null)
            {
                var updatedText = RemoveNewlyEmptyLines(
                    originalText,
                    updatedRoot.GetText(),
                    outermostNodes.Select(node => node.Span));
                changedSolution = changedSolution.WithDocumentText(pair.Key, updatedText);
            }
        }

        return changedSolution;
    }

    private static SourceText RemoveNewlyEmptyLines(
        SourceText originalText,
        SourceText updatedText,
        IEnumerable<TextSpan> removedSpans)
    {
        // KeepExteriorTrivia deliberately preserves comments and existing spacing. It also leaves the
        // indentation and line break behind when an entire declaration or usage occupied a line.
        // Map lines touched by removed nodes into the updated text so multiline removals do not make
        // unrelated line numbers appear to have become empty.
        var candidateLineNumbers = new HashSet<int>();
        var removedLineCount = 0;
        foreach (var span in removedSpans.OrderBy(span => span.Start))
        {
            var firstLine = originalText.Lines.GetLineFromPosition(span.Start).LineNumber;
            var lastPosition = span.IsEmpty ? span.Start : span.End - 1;
            var lastLine = originalText.Lines.GetLineFromPosition(lastPosition).LineNumber;
            candidateLineNumbers.Add(firstLine - removedLineCount);
            removedLineCount += lastLine - firstLine;
        }

        var changes = candidateLineNumbers
            .Where(lineNumber => lineNumber >= 0 && lineNumber < updatedText.Lines.Count)
            .Select(lineNumber => updatedText.Lines[lineNumber])
            .Where(line => IsEmpty(updatedText, line.Span))
            .Select(line => line.SpanIncludingLineBreak)
            .Distinct()
            .Select(span => new TextChange(span, string.Empty))
            .ToImmutableArray();
        return changes.IsEmpty ? updatedText : updatedText.WithChanges(changes);
    }

    private static bool IsEmpty(SourceText text, TextSpan span)
    {
        for (var index = span.Start; index < span.End; index++)
        {
            if (!char.IsWhiteSpace(text[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static SyntaxNode? FindRemovalNode(SyntaxNode root, TextSpan span)
    {
        var node = root.FindNode(span, getInnermostNodeForTie: true);
        var declarator = node.AncestorsAndSelf().OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault(candidate => candidate.Identifier.Span.IntersectsWith(span));
        if (declarator is not null)
        {
            if (declarator.Parent is VariableDeclarationSyntax declaration && declaration.Variables.Count > 1)
            {
                return declarator;
            }

            return declarator.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>() ??
                (SyntaxNode?)declarator.FirstAncestorOrSelf<FieldDeclarationSyntax>();
        }

        var declarationNode = node.AncestorsAndSelf().FirstOrDefault(candidate => candidate switch
        {
            MethodDeclarationSyntax method => method.Identifier.Span.IntersectsWith(span),
            ClassDeclarationSyntax type => type.Identifier.Span.IntersectsWith(span),
            _ => false,
        });
        if (declarationNode is not null)
        {
            return declarationNode;
        }

        // Removing the complete statement/member also removes side effects that depended on the symbol;
        // it never invents a replacement value or leaves a syntactically incomplete expression behind.
        return node.FirstAncestorOrSelf<StatementSyntax>() ??
            (SyntaxNode?)node.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().FirstOrDefault();
    }

    private static void Add(Dictionary<DocumentId, HashSet<TextSpan>> spans, DocumentId documentId, TextSpan span)
    {
        if (!spans.TryGetValue(documentId, out var documentSpans))
        {
            documentSpans = new HashSet<TextSpan>();
            spans.Add(documentId, documentSpans);
        }

        documentSpans.Add(span);
    }
}
