# Unity Best Practices Analyzer

[English](README.md) | [日本語](README.ja.md) | [فارسی](README.fa.md)

A complementary Roslyn analyzer with 70 opt-in quick fixes for Unity and high-performance C# practices not already covered by `Microsoft.Unity.Analyzers`. Every diagnostic has `Info` severity, so Rider and Visual Studio can offer the quick fix while builds produce no errors or warnings and the Unity Console stays clean.

## Quick fixes

| ID | Recognizes | Fix |
|---|---|---|
| `UBP0001` | A serializable public field on a `MonoBehaviour` or `ScriptableObject` | Adds `[UnityEngine.SerializeField]` and makes the field private |
| `UBP0002` | `yield return 0` or a boxed Boolean in a Unity coroutine | Replaces the value with `null` to wait one frame without boxing |
| `UBP0003` | `vector.magnitude < 10f` and equivalent comparisons against a positive constant | Uses `vector.sqrMagnitude < (10f * 10f)` |
| `UBP0004` | A Unity `IJob*` struct without Burst enabled | Adds `[Unity.Burst.BurstCompile]` |
| `UBP0005` | A `NativeArray<T>` job field that is provably read-only inside the job | Adds `[Unity.Collections.ReadOnly]` |
| `UBP0006` | A small temporary managed array assigned to an explicit `Span<T>` or `ReadOnlySpan<T>` | Replaces the array allocation with `stackalloc` |
| `UBP0007` | A struct element copied, mutated, then assigned to the same collection slot | Uses a `ref` local and removes the copy-back assignment |
| `UBP0008` | Two or more `Camera.main` accesses in the same block | Caches the camera in a collision-free local variable |
| `UBP0009` | A parameterless `List<T>` followed by at least five consecutive `Add` calls | Initializes the list with the known minimum capacity |
| `UBP0010` | `Mathf.Pow(value, 2f)` where `value` is a float local or parameter | Replaces the general power call with `value * value` |
| `UBP0011` | A default-cleared `NativeArray<T>` immediately overwritten by a canonical full-range loop | Adds `NativeArrayOptions.UninitializedMemory` |

### Additional quick fixes

The expanded rules use a syntax-kind-indexed registry. Adding another expression optimization requires a rule declaration and matcher, while diagnostic registration, suggestion severity, quick-fix registration, formatting, and fix-all support are shared.

| ID | Recognizes | Fix |
|---|---|---|
| `UBP0012` | `new Vector2(0f, 0f)` | Uses `UnityEngine.Vector2.zero` |
| `UBP0013` | `new Vector2(1f, 1f)` | Uses `UnityEngine.Vector2.one` |
| `UBP0014` | `new Vector2(0f, 1f)` | Uses `UnityEngine.Vector2.up` |
| `UBP0015` | `new Vector2(0f, -1f)` | Uses `UnityEngine.Vector2.down` |
| `UBP0016` | `new Vector2(-1f, 0f)` | Uses `UnityEngine.Vector2.left` |
| `UBP0017` | `new Vector2(1f, 0f)` | Uses `UnityEngine.Vector2.right` |
| `UBP0018` | `new Vector3(0f, 0f, 0f)` | Uses `UnityEngine.Vector3.zero` |
| `UBP0019` | `new Vector3(1f, 1f, 1f)` | Uses `UnityEngine.Vector3.one` |
| `UBP0020` | `new Vector3(0f, 1f, 0f)` | Uses `UnityEngine.Vector3.up` |
| `UBP0021` | `new Vector3(0f, -1f, 0f)` | Uses `UnityEngine.Vector3.down` |
| `UBP0022` | `new Vector3(-1f, 0f, 0f)` | Uses `UnityEngine.Vector3.left` |
| `UBP0023` | `new Vector3(1f, 0f, 0f)` | Uses `UnityEngine.Vector3.right` |
| `UBP0024` | `new Vector3(0f, 0f, 1f)` | Uses `UnityEngine.Vector3.forward` |
| `UBP0025` | `new Vector3(0f, 0f, -1f)` | Uses `UnityEngine.Vector3.back` |
| `UBP0026` | `new Quaternion(0f, 0f, 0f, 1f)` | Uses `UnityEngine.Quaternion.identity` |
| `UBP0027` | `Quaternion.Euler(0f, 0f, 0f)` | Uses `UnityEngine.Quaternion.identity` |
| `UBP0028` | `new Color(0f, 0f, 0f, 0f)` | Uses `UnityEngine.Color.clear` |
| `UBP0029` | `new Color(0f, 0f, 0f, 1f)` | Uses `UnityEngine.Color.black` |
| `UBP0030` | `new Color(1f, 1f, 1f, 1f)` | Uses `UnityEngine.Color.white` |
| `UBP0031` | `new Color(1f, 0f, 0f, 1f)` | Uses `UnityEngine.Color.red` |
| `UBP0032` | `new Color(0f, 1f, 0f, 1f)` | Uses `UnityEngine.Color.green` |
| `UBP0033` | `new Color(0f, 0f, 1f, 1f)` | Uses `UnityEngine.Color.blue` |
| `UBP0034` | Construction of Unity's standard yellow RGBA value | Uses `UnityEngine.Color.yellow` |
| `UBP0035` | `new Color(0f, 1f, 1f, 1f)` | Uses `UnityEngine.Color.cyan` |
| `UBP0036` | `new Color(1f, 0f, 1f, 1f)` | Uses `UnityEngine.Color.magenta` |
| `UBP0037` | `Mathf.Clamp(value, 0f, 1f)` | Uses `UnityEngine.Mathf.Clamp01(value)` |
| `UBP0038` | `Mathf.Pow(value, 0.5f)` | Uses `UnityEngine.Mathf.Sqrt(value)` |
| `UBP0039` | `(int)Mathf.Floor(value)` | Uses `UnityEngine.Mathf.FloorToInt(value)` |
| `UBP0040` | `(int)Mathf.Ceil(value)` | Uses `UnityEngine.Mathf.CeilToInt(value)` |
| `UBP0041` | `(int)Mathf.Round(value)` | Uses `UnityEngine.Mathf.RoundToInt(value)` |
| `UBP0042` | `new T[0]` or an explicitly typed empty array initializer | Uses the cached `System.Array.Empty<T>()` instance |
| `UBP0043` | `source.Where(predicate).Any()` with a non-indexed predicate | Uses `source.Any(predicate)` |
| `UBP0044` | `source.Where(predicate).Count()` with a non-indexed predicate | Uses `source.Count(predicate)` |
| `UBP0045` | `source.Where(predicate).First()` with a non-indexed predicate | Uses `source.First(predicate)` |
| `UBP0046` | `source.Where(predicate).FirstOrDefault()` with a non-indexed predicate | Uses `source.FirstOrDefault(predicate)` |
| `UBP0047` | `dictionary.Keys.Contains(key)` | Uses `dictionary.ContainsKey(key)` |
| `UBP0048` | `list.ElementAt(index)` on a concrete `List<T>` | Uses `list[index]` |
| `UBP0049` | `list.Count()` on a concrete `List<T>` | Uses the `list.Count` property |
| `UBP0050` | `array.Count()` on a one-dimensional array | Uses the `array.Length` property |
| `UBP0051` | `array.Any()` on a one-dimensional array | Uses `array.Length != 0` |
| `UBP0052` | `list.Any()` on a concrete `List<T>` | Uses `list.Count != 0` |
| `UBP0053` | `StringBuilder.Append("x")` with a one-character constant | Uses the character overload `Append('x')` |
| `UBP0054` | `StringBuilder.AppendLine("")` | Uses parameterless `AppendLine()` |
| `UBP0055` | `new CancellationToken()` | Uses `System.Threading.CancellationToken.None` |
| `UBP0056` | `new Guid()` | Uses `System.Guid.Empty` |
| `UBP0057` | `Enumerable.Empty<T>().ToArray()` | Uses the cached `System.Array.Empty<T>()` instance directly |

### DOTS query quick fixes

These quick fixes use the current Entities 1.x query systems. `SystemAPI.Query` is the main-thread `foreach` target; `IJobEntity.Run`, `Schedule`, and `ScheduleParallel` provide immediate, single-scheduled-job, and parallel-scheduled-job variants. Entities 1.x does not define a separate `IJobParallel` query interface—parallel IJobEntity execution uses `ScheduleParallel`.

| ID | Recognizes | Fix |
|---|---|---|
| `UBP0058` | A compatible main-thread `Entities.ForEach(...).Run()` | Converts it to `foreach` over `SystemAPI.Query<RefRW<...>, RefRO<...>>()` |
| `UBP0059` | A compatible `Entities.ForEach` pipeline | Extracts a Burst-enabled `IJobEntity` and invokes `Run()` |
| `UBP0060` | A compatible `Entities.ForEach` pipeline | Extracts a Burst-enabled `IJobEntity` and invokes `Schedule()` |
| `UBP0061` | A compatible `Entities.ForEach` pipeline | Extracts a Burst-enabled `IJobEntity` and invokes `ScheduleParallel()` |
| `UBP0062` | A compatible `SystemAPI.Query` foreach loop | Extracts a Burst-enabled `IJobEntity` and invokes `Run()` |
| `UBP0063` | A compatible `SystemAPI.Query` foreach loop | Extracts a Burst-enabled `IJobEntity` and invokes `Schedule()` |
| `UBP0064` | A compatible `SystemAPI.Query` foreach loop | Extracts a Burst-enabled `IJobEntity` and invokes `ScheduleParallel()` |
| `UBP0065` | A parameterless `IJobEntity.Run()` invocation | Switches execution to `Schedule()` |
| `UBP0066` | A parameterless `IJobEntity.Run()` invocation | Switches execution to `ScheduleParallel()` |
| `UBP0067` | A parameterless `IJobEntity.Schedule()` invocation | Switches execution to `Run()` |
| `UBP0068` | A parameterless `IJobEntity.Schedule()` invocation | Switches execution to `ScheduleParallel()` |
| `UBP0069` | A parameterless `IJobEntity.ScheduleParallel()` invocation | Switches execution to `Run()` |
| `UBP0070` | A parameterless `IJobEntity.ScheduleParallel()` invocation | Switches execution to `Schedule()` |

The analyzer resolves Unity symbols semantically. It ignores unrelated types with similar member names, unsupported field types, non-Unity iterators, dynamic distance thresholds, generated code, and Unity/package versions where the required symbols are absent.

Performance transformations have conservative safety limits. Stack allocation is offered only for primitive or enum element types, only outside loops, and only when the total allocation is at most 1 KiB; the fix inserts `Span.Clear()` when necessary to preserve managed-array zero initialization. Ref-local conversion requires a real ref-returning access path, an unchanged receiver/index, a detected mutation, and no use of the copied local after its matching write-back. Read-only job fields are suggested only when every in-job use is a recognized read. List capacity is inferred only from uninterrupted `Add` statements. Squaring is limited to side-effect-free scalar identifiers. Uninitialized native memory is offered only when the next statement is a canonical loop that assigns every index without reading the array's previous contents.

DOTS extraction is offered only for semantic Unity Entities calls using direct `IComponentData` parameters, supported `WithAll`/`WithAny`/`WithNone`/`WithChangeFilter`/query-option filters, and bodies without captured locals, nested lambdas, or system-instance access. `ref` parameters become `RefRW<T>`, `in` or value parameters become `RefRO<T>`, entity parameters become `WithEntityAccess()`, and query filters become the equivalent IJobEntity attributes. Existing `SystemAPI.Query` loops must access wrappers through `ValueRW` or `ValueRO` so extraction can preserve access intent.

## Deliberately out of scope

This package was checked against the current `Microsoft.Unity.Analyzers` catalog (`UNT0001` through `UNT0043`). It intentionally does not duplicate existing rules such as empty Unity messages, `CompareTag`, `TryGetComponent`, non-allocating physics APIs, cached yield instructions, transform position/rotation APIs, mesh-array loop access, or `Animator.StringToHash`.

The rules follow the official guidance for [Burst-compiled jobs](https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/compilation-burstcompile.html), [read-only NativeContainers](https://docs.unity3d.com/Manual/job-system-native-container.html), [`SystemAPI.Query`](https://docs.unity.cn/Packages/com.unity.entities%401.0/api/Unity.Entities.SystemAPI.Query.html), [`IJobEntity` scheduling](https://docs.unity.cn/Packages/com.unity.entities%401.0/api/Unity.Entities.IJobEntityExtensions.ScheduleParallel.html), [`Camera.main` caching](https://docs.unity3d.com/ScriptReference/Camera-main.html), [`NativeArrayOptions.UninitializedMemory`](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArrayOptions.UninitializedMemory.html), [bounded `stackalloc`](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/stackalloc), and [C# ref-based copy avoidance](https://learn.microsoft.com/dotnet/csharp/advanced-topics/performance/).

## Build and test

```powershell
dotnet run --project tests/UnityBestPractices.Analyzers.Tests
dotnet pack src/UnityBestPractices.Analyzers -c Release -o artifacts
```

The test project is a dependency-light executable test harness. It verifies all 70 descriptors have unique IDs, `Info` suggestion severity, and registered fixes, then compiles every transformation and checks conservative negative cases. DOTS coverage includes all query targets, all six execution-mode switches, filter transfer, entity access, Burst job extraction, exact offered-fix sets, and rejection of captures, raw wrapper access, unsupported query forms, structural-change pipelines, and look-alike non-Unity APIs.

### Automated releases

Every branch push is restored, built in Release configuration, and tested by GitHub Actions. A successful push to the repository's default branch additionally publishes the exact tested analyzer DLL as a GitHub Release asset.

Release tags use the `v0.N` series, while the release asset keeps the standard filename `UnityBestPractices.Analyzers.dll`. GitHub's release-workflow run number begins at 1 and advances only for new default-branch release runs, producing `v0.1`, `v0.2`, `v0.3`, and so on; rerunning an existing workflow keeps its original version. The project version remains `0.1.0` locally, while the workflow supplies the selected three-part assembly/package version during each release build.

## Use in Unity

Unity's player scripting profile supports .NET Standard 2.1. The analyzer DLL intentionally targets `netstandard2.0`, as required by [Unity's Roslyn analyzer guidance](https://docs.unity3d.com/2023.2/Documentation/Manual/roslyn-analyzers.html), and is therefore compatible with projects using the .NET Standard 2.1 player profile. The solution includes a `netstandard2.1` compatibility project that references both public analyzer entry points; every local and CI solution build compiles it to prevent compatibility regressions.

1. Build or pack the analyzer.
2. Copy `UnityBestPractices.Analyzers.dll` from `bin/Release/netstandard2.0` (or from the NuGet package's `analyzers/dotnet/cs` directory) into a folder under the Unity project's `Assets` directory.
3. Select the DLL in Unity's Plugin Inspector. Disable **Auto Reference**, **Validate References**, **Any Platform**, **Editor**, and **Standalone**, then assign the exact asset label `RoslynAnalyzer`.
4. Apply the import settings, regenerate the C# project, and restart Visual Studio or Rider so its Roslyn host reloads the assembly. In Rider, also verify **Settings | Editor | Inspection Settings | Roslyn Analyzers | Enable Roslyn analyzers**.
5. Put the caret on the light dotted suggestion and invoke the IDE's quick-action command (`Alt+Enter` in Rider).

Unity performs analyzer loading for compilation, while the supported IDE discovers the code-fix provider from the same assembly. `Microsoft.CodeAnalysis.Workspaces` and `System.Composition` are IDE-host dependencies of that provider, which is why Unity asset-reference validation must remain disabled. Because all descriptors use `DiagnosticSeverity.Info`, they appear as low-noise suggestions with light-bulb actions rather than compiler warnings or errors.
