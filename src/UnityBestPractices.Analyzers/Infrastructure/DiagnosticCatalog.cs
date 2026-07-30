using System;
using System.Collections.Immutable;
using System.Linq;

namespace UnityBestPractices.Analyzers;

public static class DiagnosticCatalog
{
    internal static readonly ImmutableArray<RuleMetadata> CoreRules = ImmutableArray.Create(
        RuleMetadataFactory.Create(
            DiagnosticIds.EncapsulateSerializedField,
            "Encapsulate serialized field",
            "Keep serialized field '{0}' private",
            "A private field with SerializeField preserves Inspector serialization without exposing mutable state as public API.",
            "Make private and add SerializeField",
            "UnityEngine.MonoBehaviour",
            "UnityEngine.ScriptableObject",
            "UnityEngine.SerializeField"),
        RuleMetadataFactory.Create(
            DiagnosticIds.YieldNull,
            "Yield null for the next frame",
            "Yield null instead of a boxed value to wait for the next frame",
            "Unity coroutines can yield null for one frame without boxing a value type.",
            "Yield null",
            "UnityEngine.MonoBehaviour"),
        RuleMetadataFactory.Create(
            DiagnosticIds.UseSquaredMagnitude,
            "Use squared magnitude for a distance check",
            "Compare sqrMagnitude with the squared threshold",
            "Comparing squared magnitudes avoids the square-root calculation performed by magnitude.",
            "Use squared magnitude",
            "UnityEngine.Vector2",
            "UnityEngine.Vector3"),
        RuleMetadataFactory.Create(
            DiagnosticIds.AddBurstCompile,
            "Burst compile Unity job",
            "Add BurstCompile to job '{0}'",
            "Burst can compile compatible Unity jobs to optimized native code.",
            "Add BurstCompile",
            "Unity.Jobs.IJob",
            "Unity.Burst.BurstCompileAttribute"),
        RuleMetadataFactory.Create(
            DiagnosticIds.MarkNativeArrayReadOnly,
            "Mark job input as read-only",
            "Mark NativeArray field '{0}' as ReadOnly",
            "ReadOnly access allows jobs that consume the same NativeArray to run concurrently.",
            "Mark NativeArray as ReadOnly",
            "Unity.Collections.NativeArray`1",
            "Unity.Collections.ReadOnlyAttribute"),
        RuleMetadataFactory.Create(
            DiagnosticIds.UseStackalloc,
            "Stack allocate small temporary buffer",
            "Use stackalloc for this bounded temporary Span buffer",
            "A small, non-looping stackalloc Span avoids a managed array allocation.",
            "Use stackalloc",
            "System.Span`1"),
        RuleMetadataFactory.Create(
            DiagnosticIds.UseRefLocal,
            "Mutate struct element by reference",
            "Use a ref local instead of copying '{0}' and assigning it back",
            "A ref local avoids copying a struct out of a ref-returning collection and writing it back.",
            "Mutate through a ref local"),
        RuleMetadataFactory.Create(
            DiagnosticIds.CacheCameraMain,
            "Cache repeated Camera.main lookup",
            "Cache Camera.main for the {0} accesses in this block",
            "Caching repeated Camera.main access avoids repeated component lookup overhead.",
            "Cache Camera.main in this block",
            "UnityEngine.Camera"),
        RuleMetadataFactory.Create(
            DiagnosticIds.PreallocateList,
            "Preallocate list capacity",
            "Initialize this list with capacity {0}",
            "Preallocating a known minimum capacity avoids list backing-array growth while adding elements.",
            "Preallocate List capacity",
            "System.Collections.Generic.List`1"),
        RuleMetadataFactory.Create(
            DiagnosticIds.UseMultiplicationForSquare,
            "Multiply to square a scalar",
            "Multiply '{0}' by itself instead of calling Mathf.Pow",
            "Direct multiplication is cheaper than a general-purpose power function for a stable scalar squared.",
            "Multiply the value by itself",
            "UnityEngine.Mathf"),
        RuleMetadataFactory.Create(
            DiagnosticIds.UseUninitializedNativeArray,
            "Skip redundant NativeArray clearing",
            "Use UninitializedMemory because the following loop overwrites every element",
            "Skipping default memory clearing avoids redundant work when every NativeArray element is immediately assigned.",
            "Use uninitialized NativeArray memory",
            "Unity.Collections.NativeArray`1",
            "Unity.Collections.NativeArrayOptions"),
        RuleMetadataFactory.Create(
            DiagnosticIds.MatchFolderNamespace,
            "Match the folder namespace",
            "Place this file in namespace '{0}' to match neighboring files",
            "A namespace-less type file should use the clear most-common namespace declared by neighboring files in the same folder.",
            "Use the folder namespace"),
        RuleMetadataFactory.Create(
            DiagnosticIds.RemoveUnusedEntityAccess,
            "Remove unused entity access",
            "Remove WithEntityAccess because the entity value is unused",
            "Avoiding an unused entity value keeps a SystemAPI.Query iteration tuple as small as necessary.",
            "Remove unused entity access",
            "Unity.Entities.SystemAPI"));

    private static readonly Lazy<ImmutableArray<RuleMetadata>> LazyAll =
        new(
            () => CoreRules
                .AddRange(ExpressionQuickFixRegistry.Metadata)
                .AddRange(DotsQueryRules.Metadata)
                .AddRange(AdvancedUnityRules.Metadata)
                .OrderBy(rule => rule.DiagnosticId, StringComparer.Ordinal)
                .ToImmutableArray());

    private static readonly Lazy<ImmutableDictionary<string, RuleMetadata>> LazyById =
        new(() => All.ToImmutableDictionary(rule => rule.DiagnosticId, StringComparer.Ordinal));

    public static ImmutableArray<RuleMetadata> All => LazyAll.Value;

    public static bool TryGet(string diagnosticId, out RuleMetadata metadata) =>
        LazyById.Value.TryGetValue(diagnosticId, out metadata!);

    public static RuleMetadata Get(string diagnosticId) =>
        TryGet(diagnosticId, out var metadata)
            ? metadata
            : throw new ArgumentOutOfRangeException(nameof(diagnosticId), diagnosticId, "Unknown diagnostic ID.");
}
