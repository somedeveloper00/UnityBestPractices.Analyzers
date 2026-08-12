<div align="center">

# Unity Best Practices Analyzer

**Actionable Unity C# performance guidance, directly in Rider, Visual Studio, and VS Code on Windows, macOS, and Linux.**

Roslyn diagnostics · conservative quick fixes · Burst, Jobs, DOTS/ECS · Unity 2021.3+

</div>

[![Build](https://img.shields.io/github/actions/workflow/status/somedeveloper00/UnityBestPractices.Analyzers/ci.yml?branch=master&label=build)](https://github.com/somedeveloper00/UnityBestPractices.Analyzers/actions/workflows/ci.yml)
[![Tests](https://img.shields.io/github/actions/workflow/status/somedeveloper00/UnityBestPractices.Analyzers/ci.yml?branch=master&label=tests)](https://github.com/somedeveloper00/UnityBestPractices.Analyzers/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/somedeveloper00/UnityBestPractices.Analyzers)](https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

<div align="center">

[English](README.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [Polski](README.pl.md) · [فارسی](README.fa.md) · [Русский](README.ru.md)

</div>

**Find Unity C# performance problems before play mode.** Unity Best Practices Analyzer is a cross-platform Roslyn analyzer and code-fix package for Unity 2021.3+, JetBrains Rider, Visual Studio, and VS Code. It provides 78 low-noise diagnostics and 76 opt-in quick fixes for Unity, Burst, Jobs, DOTS/ECS, and high-performance C# practices that are not already covered by `Microsoft.Unity.Analyzers`.

Every diagnostic defaults to `Info`: your IDE can show a useful suggestion without introducing build warnings, errors, or Unity Console noise. Quick-fix labels follow the IDE's system UI language, including English, German, Japanese, Polish, Persian, and Russian.

**Start here:** [install with Unity Package Manager](#unity-package-manager) · [configure rules](#configuration) · [browse all quick fixes](#quick-fixes) · [check Unity compatibility](#compatibility)

> [!TIP]
> **New here?** Download the latest `.tgz`, add it in Unity with **Window > Package Manager > + > Add package from tarball**, regenerate project files, and restart your IDE. See the [full installation guide](#installation) if Unity does not retain the analyzer label.

## At a glance

| What you need | Where to go |
| --- | --- |
| Install the analyzer | [Unity Package Manager](#unity-package-manager), [NuGet](#nuget), or [manual DLL](#manual-dll) |
| See every diagnostic | [Generated rule index](docs/rules/index.md) (`UBP0001`–`UBP0078`) |
| Choose a safe preset | [`config/ubp-safe.editorconfig`](config/ubp-safe.editorconfig) and the [configuration guide](docs/configuration.md) |
| Understand fix safety | [Safety model](#safety-model) and [rule safety decisions](docs/safety.md) |
| Use IDE refactorings | [Parameter and inline-method refactorings](#parameter-refactorings) |
| Build or contribute | [Build and test](#build-and-test), [contributing guide](CONTRIBUTING.md), and [integration fixtures](tests/UnityIntegration/README.md) |

<details>
<summary><strong>Documentation map</strong> — useful for readers, search engines, and coding assistants</summary>

- **Product overview:** this README
- **Rules:** [`docs/rules/index.md`](docs/rules/index.md), with one stable page per diagnostic ID
- **Configuration:** [`docs/configuration.md`](docs/configuration.md) and ready-to-copy [`config/`](config) presets
- **Safety guarantees:** [`docs/safety.md`](docs/safety.md)
- **Release history:** [`CHANGELOG.md`](CHANGELOG.md)
- **Development:** [`CONTRIBUTING.md`](CONTRIBUTING.md) and [`tests/UnityIntegration/README.md`](tests/UnityIntegration/README.md)
- **Package-specific help:** [`packaging/upm/README.md`](packaging/upm/README.md)
- **Machine-readable entry point:** [`llms.txt`](llms.txt)

</details>

## Why use this analyzer?

- Improve common Unity C# hot paths with conservative suggestions for allocations, `Camera.main`, `NativeArray`, `Mathf`, `List<T>`, LINQ, and `StringBuilder`.
- Modernize Entities 1.x code with review-required `Entities.ForEach`, `SystemAPI.Query`, and `IJobEntity` migration actions.
- Keep fixes trustworthy: every rule declares whether it is safe to apply automatically or needs code review.
- Install as a Unity UPM package, a NuGet analyzer, or a manually imported Roslyn analyzer DLL.

## Safety model

Every rule has an explicit classification in the [generated rule index](docs/rules/index.md).
The rationale for every review-required rule is recorded in [rule safety decisions](docs/safety.md).

| Classification | Meaning | Fix All |
| --- | --- | --- |
| `Safe` | The code action is expected to preserve observable behavior under its exact documented preconditions. | Available only when the implementation is safe across the requested scope. |
| `ReviewRequired` | Accessibility, floating-point behavior, allocation lifetime, threading, synchronization, serialization, or ECS scheduling can change. | Never exposed through the global batch Fix All provider. |
| `Experimental` | The rule is opt-in while its compatibility envelope is established. | Not supported. |

`UBP0001` remains review-required. Its diagnostic can identify a public serialized field, but its fix is offered only after solution-wide reference analysis proves that no reference outside the declaring type or its nested types would become inaccessible. DOTS migrations (`UBP0058`–`UBP0070`) are also review-required: changing `Run`, `Schedule`, or `ScheduleParallel` can change execution timing, synchronization, dependency propagation, thread safety, and scheduling behavior.

## Rule categories

- `Unity.Performance.Safe` and `CSharp.Performance` contain conservative allocation and API optimizations.
- `Unity.Performance.Review` contains performance transformations that require runtime review.
- `Unity.Correctness` covers job dependencies and native-container lifetime.
- `Unity.DOTS.Migration` covers Entities 1.x query and execution-mode migrations.
- `Unity.API.Design` covers Unity-facing API and serialization design.
- `CSharp.CodeStyle` covers project-consistency suggestions inferred from nearby source files.

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

### Correctness and caching

| ID | Recognizes | Fix |
|---|---|---|
| `UBP0071` | A returned `Unity.Jobs.JobHandle` discarded from a supported `Schedule` call | Assigns the handle to a collision-free local so it can be propagated or combined |
| `UBP0072` | A locally owned `NativeArray<T>` allocated with `Allocator.Persistent` and undisposed on every analyzed exit | Diagnostic only; disposal and ownership must be chosen by the developer |
| `UBP0073` | Only a `NativeArray<T>` allocated with `Temp` or `TempJob` and returned, stored in a field, or captured by an escaping delegate | Diagnostic only; the correct lifetime or ownership depends on the application |
| `UBP0074` | Repeated constant `Shader.PropertyToID` calls in one type | Adds a uniquely named static readonly ID field and replaces repeated calls |
| `UBP0075` | A namespace-less type file whose neighbors have a clear most-common namespace | Wraps the file's types in the neighboring namespace |
| `UBP0076` | Adjacent local position and rotation assignments on the same Transform | Replaces both assignments with `SetLocalPositionAndRotation` |
| `UBP0077` | A `SystemAPI.Query(...).WithEntityAccess()` loop whose entity result is unused | Removes `WithEntityAccess()` and the unused entity tuple element |
| `UBP0078` | Obsolete generic or `System.Type` Unity object lookup calls | Uses `FindFirstObjectByType` or the ordered `FindObjectsByType` equivalent |

The analyzer resolves Unity symbols semantically. It ignores unrelated types with similar member names, unsupported field types, non-Unity iterators, dynamic distance thresholds, generated code, and Unity/package versions where the required symbols are absent.

Performance transformations have conservative safety limits. Stack allocation is offered only for primitive or enum element types, only outside loops, and only when the total allocation is at most 1 KiB; the fix inserts `Span.Clear()` when necessary to preserve managed-array zero initialization. Ref-local conversion requires a real ref-returning access path, an unchanged receiver/index, a detected mutation, and no use of the copied local after its matching write-back. Read-only job fields are suggested only when every in-job use is a recognized read. List capacity is inferred only from uninterrupted `Add` statements. Squaring is limited to side-effect-free scalar identifiers. Uninitialized native memory is offered only when the next statement is a canonical loop that assigns every index without reading the array's previous contents.

DOTS extraction is offered only for semantic Unity Entities calls using direct `IComponentData` parameters, supported `WithAll`/`WithAny`/`WithNone`/`WithChangeFilter`/query-option filters, and bodies without captured locals, nested lambdas, or system-instance access. `ref` parameters become `RefRW<T>`, `in` or value parameters become `RefRO<T>`, entity parameters become `WithEntityAccess()`, and query filters become the equivalent IJobEntity attributes. Existing `SystemAPI.Query` loops must access wrappers through `ValueRW` or `ValueRO` so extraction can preserve access intent.

## Parameter refactorings

Put the caret on a method, constructor, local-function, or indexer parameter and invoke the IDE quick-action command to use **Move parameter left** or **Move parameter right**. The refactoring updates related interface/implementation declarations and all semantically matched C# call sites in the solution, including named arguments, optional arguments, constructor initializers, and reduced extension-method calls.

Put the caret on a method call and invoke **Inline method** to replace it with the method implementation. Expression calls preserve precedence and are offered when substituting their arguments cannot duplicate, discard, reorder, or reinterpret values. Standalone `void` calls can inline complete block bodies, including parameters, locals, multiple statements, and early returns; generated parameter locals preserve argument evaluation and conversions. Implicit instance calls are supported inside the declaring type.

Putting the caret on a method group used as a delegate replaces it with a lambda containing the method implementation. This supports common event and listener subscriptions, including parameterized, generic, and async handlers. Event unsubscriptions are intentionally left unchanged because a newly created lambda would not identify the originally subscribed delegate.

Put the caret on a string literal whose value exactly matches an accessible
field, property, method, type, parameter, or local name and use
**Replace string literal with nameof**. The refactoring verifies that the
replacement is the same compile-time string constant before offering the
action, including when `nameof` is shadowed or the identifier must be escaped.

Call sites are discovered with Roslyn's solution-wide `SymbolFinder.FindReferencesAsync` API, and each argument is associated with its parameter through Roslyn `IOperation` bindings. The refactoring therefore distinguishes overloads and does not use textual name matching. Moves that would displace an extension `this` parameter, a `params` parameter, or place a required parameter after a parameter with a default are not offered.

Use **Remove parameter** from the same caret position to remove the parameter from related declarations and remove every bound argument from matching call sites. This also handles named, optional, and expanded `params` arguments. Extension receivers and the sole parameter of an indexer cannot be removed because doing so would produce an invalid declaration or call shape.

## Statement and declaration movement refactoring

Put the caret anywhere on a statement or declaration and use **Move statement up**
or **Move statement down** to exchange it with its adjacent sibling. The actions
support one-line statements, braced blocks, switch sections, accessors, enum
members, methods, properties, fields, nested types, namespaces, and top-level
type declarations. Comments and other trivia attached to the moved syntax travel
with it, and an action is omitted when there is no sibling in that direction.

When VS Code uses the legacy OmniSharp language server, the two directions are
exposed as distinct code-action kinds and can be invoked directly from
`keybindings.json`:

```jsonc
{
    "key": "ctrl+alt+up",
    "command": "editor.action.codeAction",
    "args": { "kind": "refactor.inline", "apply": "first" },
    "when": "editorTextFocus && editorLangId == csharp"
},
{
    "key": "ctrl+alt+down",
    "command": "editor.action.codeAction",
    "args": { "kind": "refactor.extract", "apply": "first" },
    "when": "editorTextFocus && editorLangId == csharp"
}
```

The same routing is available for the directional parameter refactorings. These
bindings move the parameter under the caret and update its call sites:

```jsonc
{
    "key": "ctrl+alt+left",
    "command": "editor.action.codeAction",
    "args": { "kind": "refactor.inline", "apply": "first" },
    "when": "editorTextFocus && editorLangId == csharp"
},
{
    "key": "ctrl+alt+right",
    "command": "editor.action.codeAction",
    "args": { "kind": "refactor.extract", "apply": "first" },
    "when": "editorTextFocus && editorLangId == csharp"
}
```

**Inline method** is also exposed as `refactor.inline`, including when its label
is localized. The analyzer's diagnostic quick fixes can be applied from a
shortcut as well:

```jsonc
{
    "key": "ctrl+alt+enter",
    "command": "editor.action.codeAction",
    "args": { "kind": "quickfix", "apply": "first" },
    "when": "editorTextFocus && editorLangId == csharp"
}
```

That binding applies the first quick fix at the caret, regardless of its UBP
diagnostic ID. Use `"apply": "never"` to show the matching fixes instead.
Non-directional refactorings remain available from the standard `refactor`
picker because OmniSharp provides no additional title-derived kinds with which
to address them individually.

This routing depends on OmniSharp's title-based classification. Configure the
C# extension with `"dotnet.server.useOmnisharp": true`; the modern Roslyn LSP
reports third-party refactorings under the generic `refactor` kind. Because the
inline and extract kinds are shared with other providers, use `"apply":
"never"` instead if you prefer a picker whenever more than one matching action
is available.

Use **Remove double empty lines** to collapse every run of consecutive empty
lines in the current document to one. Lines containing only spaces or tabs are
treated as empty, while the document's existing line-ending style is preserved.

## Deliberately out of scope

This package was checked against the current [`Microsoft.Unity.Analyzers` catalog](https://github.com/microsoft/Microsoft.Unity.Analyzers/tree/main/doc) (`UNT0001` through `UNT0043`). It intentionally does not duplicate existing rules such as empty Unity messages, `CompareTag`, `TryGetComponent`, non-allocating physics APIs, cached yield instructions, transform position/rotation APIs, mesh-array loop access, or `Animator.StringToHash`.

The rules follow the official guidance for [Burst-compiled jobs](https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/compilation-burstcompile.html), [read-only NativeContainers](https://docs.unity3d.com/Manual/job-system-native-container.html), [`SystemAPI.Query`](https://docs.unity.cn/Packages/com.unity.entities%401.0/api/Unity.Entities.SystemAPI.Query.html), [`IJobEntity` scheduling](https://docs.unity.cn/Packages/com.unity.entities%401.0/api/Unity.Entities.IJobEntityExtensions.ScheduleParallel.html), [`Camera.main` caching](https://docs.unity3d.com/ScriptReference/Camera-main.html), [`NativeArrayOptions.UninitializedMemory`](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArrayOptions.UninitializedMemory.html), [bounded `stackalloc`](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/stackalloc), and [C# ref-based copy avoidance](https://learn.microsoft.com/dotnet/csharp/advanced-topics/performance/).

## Installation

### Unity Package Manager

Download `com.somedeveloper.unity-best-practices-analyzers-<version>.tgz` from the matching [GitHub release](https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases), then choose **Window > Package Manager > + > Add package from tarball**. The package includes the analyzer DLL and its `.meta` file with the `RoslynAnalyzer` label and disabled reference validation.

If a Unity version does not retain imported labels, select `Packages/Unity Best Practices Analyzers/Editor/Analyzers/UnityBestPractices.Analyzers.dll`, disable **Auto Reference**, **Validate References**, and all platforms, assign the exact label `RoslynAnalyzer`, and apply the import settings.

### NuGet

Stable releases contain `UnityBestPractices.Analyzers.<version>.nupkg` and a symbol package. Until NuGet.org publishing is enabled, download the package from GitHub Releases and add its directory as a local package source:

```sh
# macOS/Linux (use C:\path\to\downloaded-packages on Windows)
dotnet nuget add source ~/Downloads -n UnityBestPracticesLocal
dotnet add package UnityBestPractices.Analyzers --version 0.4.0 --source UnityBestPracticesLocal
```

The `.nupkg` places the DLL under `analyzers/dotnet/cs` and includes the README, translations, changelog, license, repository commit metadata, portable symbols, and Source Link data. NuGet.org publishing is intentionally disabled unless the repository is configured with a `NUGET_API_KEY` secret.

### Manual DLL

1. Download the standard-named `UnityBestPractices.Analyzers.dll` release asset.
2. Copy it below the Unity project's `Assets` directory.
3. In the Plugin Inspector, disable **Auto Reference**, **Validate References**, **Any Platform**, **Editor**, and **Standalone**, then add the exact `RoslynAnalyzer` asset label.
4. Regenerate the C# project and restart Rider, Visual Studio, or VS Code. In Rider, enable Roslyn analyzers under **Settings > Editor > Inspection Settings > Roslyn Analyzers**.
5. Put the caret on the suggestion and invoke the IDE quick-action command (`Alt+Enter` in Rider).

Unity performs analyzer loading for compilation while the IDE discovers the code-fix provider from the same assembly. `Microsoft.CodeAnalysis.Workspaces` and `System.Composition` are IDE-host dependencies, so Unity asset-reference validation must remain disabled.

## Configuration

Use standard `dotnet_diagnostic.UBPxxxx.severity` and category severity settings to promote, suppress, or disable rules. Conservative options configure the stack allocation ceiling, list preallocation threshold, DOTS migrations, and review-required rules. See [configuration](docs/configuration.md) and the ready-to-copy [`config`](config) presets.

```ini
[*.cs]
dotnet_diagnostic.UBP0009.severity = warning
dotnet_diagnostic.UBP0058.severity = none
ubp_max_stackalloc_bytes = 512
ubp_enable_review_required = false
```

Missing or invalid option values use conservative defaults and never crash analysis.

## Compatibility

The analyzer targets `netstandard2.0` and Roslyn 3.8, which is the conservative Unity analyzer-host baseline and is compatible with projects using Unity's .NET Standard 2.1 player profile. It does not reference Unity assemblies at runtime; all Unity and package APIs are resolved semantically. Missing packages, generated code, look-alike APIs, and incomplete syntax are excluded safely.

| Surface | Tested fixture |
| --- | --- |
| Oldest base Unity family | Unity 2021.3 LTS |
| Oldest DOTS family | Unity 2022.3, Entities 1.0.11, Collections 2.1.4, Burst 1.8.2 |
| Current LTS family | Unity 6.3 LTS manifest fixture |
| Development host | Windows, macOS, and Linux (.NET 8) |

The current-LTS DOTS matrix is not claimed until Unity publishes and the project tests a verified package set for that editor. See the [integration fixture process](tests/UnityIntegration/README.md).

## Build and test

The commands below are shell-independent and can be run from Terminal on macOS or Linux, or PowerShell on Windows:

```sh
dotnet restore UnityBestPractices.sln
dotnet build UnityBestPractices.sln -c Release --no-restore
dotnet run --project tests/UnityBestPractices.Analyzers.Tests -c Release --no-build
dotnet test tests/UnityBestPractices.Analyzers.Tests.Xunit -c Release --no-build
dotnet run --project tests/UnityBestPractices.Analyzers.PerformanceTests -c Release --no-build
dotnet pack src/UnityBestPractices.Analyzers -c Release --no-build -o artifacts/packages
```

The dependency-light harness verifies the full 78-rule catalog, at least five positive quick-fix cases per fix and ten for complicated fixes, semantic negative cases, solution-wide accessibility, all DOTS query targets, and document/project/solution Fix All. The xUnit layer uses `Microsoft.CodeAnalysis.Testing` for structured Roslyn integration tests. Broad performance checks cover non-matching files, repeated Unity patterns, large DOTS files, malformed syntax, many documents, incremental edits, diagnostics, elapsed time, and allocations.

## Release process

Project, assembly, file, informational, NuGet, UPM, and Git tag versions use the same SemVer value. To release:

1. Update `<Version>` in the analyzer project and `CHANGELOG.md`.
2. Merge a green CI build.
3. Create and push the matching tag, for example `v0.4.0`.

The tag workflow rejects mismatched versions, rebuilds and retests the tagged commit, validates both package formats, and creates or updates the same GitHub release. Assets include the default-named DLL, `.nupkg`, `.snupkg`, UPM `.tgz`, and `SHA256SUMS`. Rerunning a tag cannot select a different version.

## Contributing and policy

Read [CONTRIBUTING.md](CONTRIBUTING.md) before proposing a rule. New diagnostics need documented semantic guards and false-positive risks and must not duplicate the current `Microsoft.Unity.Analyzers` catalog. Security reports follow [SECURITY.md](SECURITY.md); project changes are recorded in [CHANGELOG.md](CHANGELOG.md).
