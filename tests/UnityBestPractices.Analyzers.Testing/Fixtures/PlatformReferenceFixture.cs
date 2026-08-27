namespace UnityBestPractices.Analyzers.Testing;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

public static class PlatformReferenceFixture
{
    public static IEnumerable<MetadataReference> Discover()
    {
        var trustedAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("The runtime did not provide platform assemblies.");
        return trustedAssemblies
            .Split(System.IO.Path.PathSeparator)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
    }
}
