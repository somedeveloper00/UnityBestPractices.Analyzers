using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace UnityBestPractices.Analyzers;

internal sealed class UnitySymbolCache
{
    private static readonly ConditionalWeakTable<Compilation, UnitySymbolCache> Caches = new();

    private readonly Compilation _compilation;
    private readonly ConcurrentDictionary<string, INamedTypeSymbol> _resolved = new();
    private readonly ConcurrentDictionary<string, byte> _missing = new();

    private UnitySymbolCache(Compilation compilation)
    {
        _compilation = compilation;
    }

    internal static UnitySymbolCache For(Compilation compilation) =>
        Caches.GetValue(compilation, current => new UnitySymbolCache(current));

    internal static INamedTypeSymbol? GetTypeByMetadataName(
        Compilation compilation,
        string metadataName) =>
        For(compilation).GetTypeByMetadataName(metadataName);

    internal INamedTypeSymbol? GetTypeByMetadataName(string metadataName)
    {
        if (_resolved.TryGetValue(metadataName, out var resolved))
        {
            return resolved;
        }

        if (_missing.ContainsKey(metadataName))
        {
            return null;
        }

        resolved = _compilation.GetTypeByMetadataName(metadataName);
        if (resolved is null)
        {
            _missing.TryAdd(metadataName, 0);
            return null;
        }

        _resolved.TryAdd(metadataName, resolved);
        return resolved;
    }
}
