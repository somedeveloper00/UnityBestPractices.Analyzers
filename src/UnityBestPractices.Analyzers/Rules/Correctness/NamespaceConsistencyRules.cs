using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;

namespace UnityBestPractices.Analyzers;

internal static class NamespaceConsistencyRules
{
    internal static ImmutableArray<CodeFixHandler> Handlers { get; } = ImmutableArray.Create(
        new CodeFixHandler(
            DiagnosticCatalog.Get(DiagnosticIds.MatchFolderNamespace),
            AddNamespaceAsync));

    private const string NamespaceProperty = "Namespace";

    internal static DiagnosticDescriptor Descriptor =>
        DiagnosticCatalog.Get(DiagnosticIds.MatchFolderNamespace).Descriptor;

    internal static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        var files = context.Compilation.SyntaxTrees
            .Select(tree => TryCreateFileInfo(tree, context.CancellationToken))
            .Where(info => info is not null)
            .Cast<FileInfo>()
            .ToImmutableArray();

        foreach (var folder in files.GroupBy(file => file.Directory, StringComparer.Ordinal))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var namespaceExamples = folder
                .Where(file => file.DeclaredNamespace is not null)
                .ToImmutableArray();
            if (namespaceExamples.IsEmpty)
            {
                continue;
            }

            var namespaceCounts = namespaceExamples
                .GroupBy(file => file.DeclaredNamespace!, StringComparer.Ordinal)
                .Select(group => new
                {
                    Namespace = group.Key,
                    Count = group.Count(),
                })
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Namespace, StringComparer.Ordinal)
                .ToImmutableArray();
            if (namespaceCounts.Length > 1 &&
                namespaceCounts[0].Count == namespaceCounts[1].Count)
            {
                continue;
            }

            var expectedNamespace = namespaceCounts[0].Namespace;
            foreach (var candidate in folder.Where(file => file.CanReceiveNamespace))
            {
                if (!AnalyzerConfiguration
                        .For(context.Options.AnalyzerConfigOptionsProvider, candidate.Tree)
                        .IsRuleEnabled(DiagnosticIds.MatchFolderNamespace))
                {
                    continue;
                }

                var properties = ImmutableDictionary<string, string?>.Empty
                    .Add(NamespaceProperty, expectedNamespace);
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        candidate.Root.Members[0].GetLocation(),
                        properties,
                        expectedNamespace));
            }
        }
    }

    internal static async Task<Document> AddNamespaceAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        if (!diagnostic.Properties.TryGetValue(NamespaceProperty, out var namespaceName) ||
            string.IsNullOrWhiteSpace(namespaceName))
        {
            return document;
        }

        var targetNamespace = namespaceName!;
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax compilationUnit ||
            !CanReceiveNamespace(compilationUnit))
        {
            return document;
        }

        var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(
                SyntaxFactory.ParseName(targetNamespace))
            .WithMembers(compilationUnit.Members)
            .WithAdditionalAnnotations(Formatter.Annotation);
        var updatedRoot = compilationUnit
            .WithMembers(
                SyntaxFactory.SingletonList<MemberDeclarationSyntax>(namespaceDeclaration))
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(updatedRoot);
    }

    private static FileInfo? TryCreateFileInfo(
        SyntaxTree tree,
        CancellationToken cancellationToken)
    {
        var path = tree.FilePath;
        if (string.IsNullOrWhiteSpace(path) ||
            !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var separatorIndex = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        var directory = separatorIndex < 0
            ? "."
            : separatorIndex == 0
                ? path.Substring(0, 1)
                : path.Substring(0, separatorIndex);
        var fileName = path.Substring(separatorIndex + 1);
        if (IsGeneratedFileName(fileName) ||
            tree.GetRoot(cancellationToken) is not CompilationUnitSyntax root)
        {
            return null;
        }

        return new FileInfo(
            tree,
            root,
            directory,
            TryGetDeclaredNamespace(root),
            CanReceiveNamespace(root));
    }

    private static string? TryGetDeclaredNamespace(CompilationUnitSyntax root)
    {
        if (root.Members.Count != 1 ||
            root.Members[0] is not NamespaceDeclarationSyntax declaration ||
            declaration.Name.IsMissing)
        {
            return null;
        }

        return declaration.Name.ToString();
    }

    private static bool CanReceiveNamespace(CompilationUnitSyntax root) =>
        root.Members.Count != 0 &&
        root.Members.All(member =>
            member is BaseTypeDeclarationSyntax ||
            member is DelegateDeclarationSyntax);

    private static bool IsGeneratedFileName(string fileName) =>
        fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".AssemblyAttributes.cs", StringComparison.OrdinalIgnoreCase);

    private sealed class FileInfo
    {
        internal FileInfo(
            SyntaxTree tree,
            CompilationUnitSyntax root,
            string directory,
            string? declaredNamespace,
            bool canReceiveNamespace)
        {
            Tree = tree;
            Root = root;
            Directory = directory;
            DeclaredNamespace = declaredNamespace;
            CanReceiveNamespace = canReceiveNamespace;
        }

        internal SyntaxTree Tree { get; }

        internal CompilationUnitSyntax Root { get; }

        internal string Directory { get; }

        internal string? DeclaredNamespace { get; }

        internal bool CanReceiveNamespace { get; }
    }
}
