using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace UnityBestPractices.Analyzers.Tests.Xunit;

public sealed class ArchitectureTests
{
    private const string RootNamespace = "UnityBestPractices.Analyzers";

    [Fact]
    public void ExportedEntryPointsRemainPublicInRootNamespace()
    {
        var entryPoints = ProductionTypes()
            .Where(type => IsConcreteSubclass(type, typeof(DiagnosticAnalyzer))
                || IsConcreteSubclass(type, typeof(CodeFixProvider))
                || IsConcreteSubclass(type, typeof(CodeRefactoringProvider)))
            .ToArray();

        Assert.NotEmpty(entryPoints);
        Assert.All(entryPoints, type =>
        {
            Assert.True(type.IsPublic, $"{type.FullName} must remain public for discovery.");
            Assert.Equal(RootNamespace, type.Namespace);
        });
    }

    [Fact]
    public void DirectoryNamespacesAndRuleVisibilityMatchArchitecture()
    {
        var expectedFamilies = new HashSet<string>(StringComparer.Ordinal)
        {
            RootNamespace + ".Rules.Core",
            RootNamespace + ".Rules.Correctness",
            RootNamespace + ".Rules.Expressions",
            RootNamespace + ".Rules.Dots",
        };

        var familyTypes = ProductionTypes()
            .Where(type => type.Namespace != null && type.Namespace.StartsWith(RootNamespace + ".Rules.", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(expectedFamilies.OrderBy(value => value), familyTypes.Select(type => type.Namespace!).Distinct().OrderBy(value => value));
        Assert.All(familyTypes, type => Assert.True(type.IsNotPublic || type.IsNestedAssembly || type.IsNestedPrivate, $"{type.FullName} is a rule implementation and must be internal."));

        Assert.Contains(ProductionTypes(), type => type.Namespace == RootNamespace + ".Infrastructure");
    }

    [Fact]
    public void RuleFamiliesDoNotReferenceForbiddenLayers()
    {
        var root = FindRepositoryRoot();
        var rules = Path.Combine(root, "src", "UnityBestPractices.Analyzers", "Rules");
        var forbiddenFragments = new[]
        {
            "UnityBestPractices.Analyzers.Tests",
            "UnityBestPractices.Analyzers.Packaging",
            "CodeRefactoringProvider",
        };

        foreach (var file in Directory.EnumerateFiles(rules, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            Assert.All(forbiddenFragments, fragment =>
                Assert.DoesNotContain(fragment, source, StringComparison.Ordinal));
        }
    }

    private static Type[] ProductionTypes() => typeof(UnityBestPracticesAnalyzer).Assembly.GetTypes();

    private static bool IsConcreteSubclass(Type candidate, Type baseType) =>
        !candidate.IsAbstract && baseType.IsAssignableFrom(candidate);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "UnityBestPractices.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
