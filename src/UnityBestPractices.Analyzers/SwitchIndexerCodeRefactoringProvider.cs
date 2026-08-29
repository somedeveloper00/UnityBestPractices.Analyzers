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
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using UnityBestPractices.Analyzers.Infrastructure;

namespace UnityBestPractices.Analyzers;

/// <summary>Lets a call site change to any other accessible indexer on its receiver type.</summary>
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(SwitchIndexerCodeRefactoringProvider)), Shared]
public sealed class SwitchIndexerCodeRefactoringProvider : CodeRefactoringProvider
{
    internal const string TitlePrefix = "Use indexer ";

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        var access = FindElementAccess(root, context.Span);
        if (access is null || model.GetOperation(access, context.CancellationToken) is not IPropertyReferenceOperation operation ||
            !operation.Property.IsIndexer || operation.Instance?.Type is not INamedTypeSymbol receiverType)
        {
            return;
        }

        foreach (var indexer in FindIndexers(receiverType, model, access.SpanStart)
                     .Where(candidate => !SymbolEqualityComparer.Default.Equals(candidate, operation.Property)))
        {
            var signature = FormatSignature(indexer, model, access.SpanStart);
            var englishTitle = TitlePrefix + signature;
            context.RegisterRefactoring(CodeAction.Create(
                OmniSharpRefactoringTitle.Inline(
                    FixTitleLocalizer.Get(FixTitleLocalizer.SwitchIndexer, englishTitle),
                    englishTitle),
                cancellationToken => SwitchAsync(context.Document, access, indexer, cancellationToken),
                nameof(SwitchIndexerCodeRefactoringProvider) + ":" + indexer.ToDisplayString()));
        }
    }

    internal static ElementAccessExpressionSyntax? FindElementAccess(SyntaxNode root, TextSpan span)
    {
        var position = Math.Min(span.Start, root.FullSpan.End);
        var access = root.FindToken(position).Parent?.FirstAncestorOrSelf<ElementAccessExpressionSyntax>();
        return access is not null && (span.IsEmpty ? access.FullSpan.Contains(position) : access.FullSpan.IntersectsWith(span))
            ? access
            : null;
    }

    internal static ImmutableArray<IPropertySymbol> FindIndexers(
        INamedTypeSymbol type,
        SemanticModel model,
        int position)
    {
        var result = ImmutableArray.CreateBuilder<IPropertySymbol>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<INamedTypeSymbol>();
        pending.Enqueue(type);
        foreach (var @interface in type.AllInterfaces)
        {
            pending.Enqueue(@interface);
        }

        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            pending.Enqueue(current);
        }

        while (pending.Count > 0)
        {
            foreach (var indexer in pending.Dequeue().GetMembers().OfType<IPropertySymbol>())
            {
                if (!indexer.IsIndexer || indexer.IsStatic || !model.IsAccessible(position, indexer))
                {
                    continue;
                }

                var key = string.Join("|", indexer.Parameters.Select(parameter =>
                    parameter.RefKind + ":" + parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                if (seen.Add(key))
                {
                    result.Add(indexer);
                }
            }
        }

        return result.ToImmutable();
    }

    private static string FormatSignature(IPropertySymbol indexer, SemanticModel model, int position) =>
        "[" + string.Join(", ", indexer.Parameters.Select(parameter =>
            parameter.Type.ToMinimalDisplayString(model, position) + " " + parameter.Name)) + "]";

    private static async Task<Document> SwitchAsync(
        Document document,
        ElementAccessExpressionSyntax access,
        IPropertySymbol indexer,
        CancellationToken cancellationToken)
    {
        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (model is null || root is null)
        {
            return document;
        }

        var oldArguments = access.ArgumentList.Arguments;
        var arguments = new List<ArgumentSyntax>(indexer.Parameters.Length);
        for (var i = 0; i < indexer.Parameters.Length; i++)
        {
            ExpressionSyntax expression;
            if (i < oldArguments.Count && oldArguments[i].NameColon is null &&
                model.ClassifyConversion(oldArguments[i].Expression, indexer.Parameters[i].Type).IsImplicit)
            {
                expression = oldArguments[i].Expression.WithoutTrivia();
            }
            else
            {
                var typeName = indexer.Parameters[i].Type.ToMinimalDisplayString(model, access.SpanStart);
                expression = SyntaxFactory.ParseExpression("default(" + typeName + ")");
            }

            arguments.Add(SyntaxFactory.Argument(expression));
        }

        var replacement = access.WithArgumentList(
            access.ArgumentList.WithArguments(SyntaxFactory.SeparatedList(arguments)))
            .WithTriviaFrom(access);
        return document.WithSyntaxRoot(root.ReplaceNode(access, replacement));
    }
}
