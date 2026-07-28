# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.4.18] - 2026-07-28

### Changed

- Expanded **Replace string literal with nameof** regression coverage for aliases,
  events, local functions, query range variables, enum members, contextual
  keywords, static contexts, nested interpolations, selection positions, and
  unavailable refactoring scenarios.

## [0.4.17] - 2026-07-28

### Added

- Added **Replace string literal with nameof**, an opt-in refactoring for string
  literals whose value exactly matches an accessible symbol name. The action
  verifies that the generated `nameof(...)` remains the same string constant
  before offering the edit.

### Fixed

- **Inline method** now preserves argument comments and withholds transformations
  that would remove implicit parameter or return conversions.
- **Replace string literal with nameof** is no longer offered for language
  versions earlier than C# 6.

## [0.4.16] - 2026-07-27

### Fixed

- **Remove parameter** now tolerates method-group references in event
  subscriptions and unsubscriptions, leaving those references unchanged while
  removing the declaration parameter.

## [0.4.15] - 2026-07-27

### Changed

- **Remove parameter** now performs the requested declaration and call-site
  edits even when the removed parameter is still referenced in the declaration
  body, leaving any resulting compiler errors for the user to resolve.

### Fixed

- **Move statement up** and **Move statement down** now reorder complete
  `if`/`else if` branches, including their conditions and bodies.

## [0.4.14] - 2026-07-27

### Added

- Added a solution-wide **Remove parameter** refactoring that uses Roslyn's
  parameter bindings and reference finder to update related declarations and
  remove corresponding call-site arguments, including expanded `params`
  arguments.
- Added **Move statement up** and **Move statement down** refactorings for
  statements, braced blocks, declarations, accessors, switch sections, and enum
  members, preserving attached comments and trivia.
- Added a conservative **Inline method** refactoring for expression-bodied methods
  and methods consisting of a single return statement.

### Fixed

- **Remove parameter** now avoids transformations when the selected parameter is
  still used by a declaration body or the target has a method-group reference.

## [0.4.13] - 2026-07-27

### Fixed

- UBP0071 now recognizes only scheduling extension methods declared in the
  `Unity.Jobs` namespace hierarchy or by `Unity.Entities.IJobEntityExtensions`,
  avoiding false positives for instance methods and lookalike namespaces.

## [0.4.12] - 2026-07-27

### Added

- Added **Move parameter left** and **Move parameter right** refactorings. They
  use Roslyn's semantic reference finder and operation bindings to update
  related declarations and C# call sites across the solution.

## [0.4.11] - 2026-07-27

### Fixed

- UBP0058 now converts synchronous `WithStructuralChanges()` component loops
  by preserving component access through `RefRW<T>`/`RefRO<T>` aliases and
  deferring `EntityManager.RemoveComponent` calls through a temporary
  `EntityCommandBuffer`.

## [0.4.10] - 2026-07-26

### Fixed

- UBP0007 now recognizes `DynamicBuffer<T>` copy-modify-write-back patterns
  whose final assignment uses the ref-returning `ElementAt(int)`, including
  loops that safely read another buffered element before the write-back.

## [0.4.9] - 2026-07-26

### Fixed

- UBP0058 now rewrites indexed writes to migrated `DynamicBuffer<T>` query
  values through `ElementAt`, while leaving indexed reads unchanged.

## [0.4.8] - 2026-07-26

### Added

- Added UBP0075 to suggest the clear most-common namespace used by neighboring
  C# files in the same folder, with a review-required quick fix that wraps
  namespace-less type declarations.
- Extended UBP0007 to replace `NativeArray<T>` copy-modify-write-back patterns
  with a `ref` local through `AsSpan()` when that mutable API is available.

### Fixed

- UBP0058–UBP0061 now migrate `Entities.ForEach` lambdas with
  `DynamicBuffer<T>` parameters. The synchronous UBP0058 fix also preserves
  `entityInQueryIndex` as a packed loop counter.

## [0.4.7] - 2026-07-26

### Fixed

- UBP0059–UBP0061 job extraction now fully qualifies captured field types, so
  nested unmanaged types such as `EntityCommandBuffer.ParallelWriter` compile
  after being moved into the generated `IJobEntity`.
- Generated `SystemAPI.Time` fields no longer receive an unnecessary numeric
  suffix when the replaced time member is their only name collision.

## [0.4.6] - 2026-07-26

### Fixed

- DOTS `Entities.ForEach` job migrations now support the special
  `entityInQueryIndex` parameter, read-only unmanaged captures such as
  `EntityCommandBuffer.ParallelWriter`, and `SystemAPI.Time.ElapsedTime` /
  `DeltaTime` by generating initialized `IJobEntity` fields.

## [0.4.5] - 2026-07-26

### Fixed

- UBP0051 and UBP0052 fixes now parenthesize the `Length != 0` / `Count != 0` replacement when a parent expression binds tighter, so fixing `!list.Any()` no longer produces code that fails to compile.
- UBP0011 now matches only the `(int length, Allocator allocator)` `NativeArray` constructor; the copy constructors take the same argument count but have no `NativeArrayOptions` overload, so the fix produced a call to a nonexistent constructor.
- Expression-bodied `Entities.ForEach` lambdas and brace-less `SystemAPI.Query` `foreach` bodies no longer crash the analyzer (AD0001) during DOTS migration analysis.
- UBP0006 no longer suggests `stackalloc` for `Span` locals that escape the method (returned as a span-typed value, stored into a `ref`/`out` parameter, or passed by reference), where the fix produced code that fails to compile.
- UBP0007 no longer suggests a ref local when a jump statement between the copy and the write-back could discard the mutations, or when a lambda or local function would have to capture the ref local.

## [0.4.3] - 2026-07-23

### Changed

- Entity-only UBP0058 conversions now use the conventional `SystemAPI.Query<RefRO<T>>().WithEntityAccess()` shape.
- Structural entity-only conversions collect entity IDs through the same `SystemAPI.Query` form before applying structural work to a disposable snapshot.

## [0.4.2] - 2026-07-23

### Fixed

- Enabled UBP0058 for synchronous `Entities.ForEach` lambdas that capture locals or instance state.
- Added conservative conversion support for entity-only `WithAll<T>()` pipelines, including `WithoutBurst()` and entity-only `WithStructuralChanges()` snapshot iteration.
- Kept unsafe job extraction and structural component-reference conversions suppressed.

## [0.4.1] - 2026-07-23

### Fixed

- Made the UBP0001 Roslyn integration test newline expectation portable across Windows and Linux CI runners.

## [0.4.0] - 2026-07-23

### Added

- Production package metadata, Source Link, symbol packages, and package validation.
- Explicit rule safety metadata, rule-aware Fix All behavior, and per-rule documentation.
- Unity Package Manager release assembly and validation.
- EditorConfig presets and conservative analyzer options.
- Structured xUnit Roslyn tests, Unity integration fixtures, and analyzer performance regression workloads.
- UBP0071 for discarded scheduled `JobHandle` values.
- UBP0072 for narrowly provable undisposed persistent `NativeArray` ownership.
- UBP0073 for clear `Allocator.Temp` and `Allocator.TempJob` lifetime escapes.
- UBP0074 for caching repeated constant `Shader.PropertyToID` calls.
- Contribution, security, conduct, issue, and pull-request guidance.

### Changed

- Release versions now use matching SemVer package versions and `vMAJOR.MINOR.PATCH` tags.
- Review-required transformations no longer participate in blanket batch Fix All operations.
- UBP0001 now suppresses its fix when solution-wide symbol references would become inaccessible.
- Rule families, central metadata, and Unity symbol lookup caching now reduce duplication and repeated semantic lookup work.
- CI now validates pull requests, packages, documentation, compatibility, performance, and optional licensed Unity fixtures.

### Fixed

- Release packages now use coherent SemVer tags and package/assembly versions and include symbols, UPM archives, and checksums.

## [0.3.0] - 2026-07-23

### Fixed

- Changed diagnostics from hidden to suggestion severity so Rider can discover quick fixes.

## [0.2.0] - 2026-07-23

### Added

- Japanese and Persian README translations.
- Unity .NET Standard 2.1 consumption compatibility checks.

## [0.1.0] - 2026-07-23

### Added

- Initial 70 Unity, Burst, Jobs, high-performance C#, and DOTS migration quick fixes.

[Unreleased]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/compare/v0.4.18...HEAD
[0.4.18]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.4.18
[0.4.17]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.4.17
[0.4.16]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.4.16
[0.4.15]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.4.15
[0.4.14]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.4.14
[0.4.13]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.4.13
[0.4.12]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.4.12
[0.4.11]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.4.11
[0.4.10]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.4.10
[0.4.9]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.4.9
[0.4.8]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.4.8
[0.4.7]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.4.7
[0.4.6]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.4.6
[0.4.5]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.4.5
[0.4.3]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.4.3
[0.4.2]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.4.2
[0.4.1]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.4.1
[0.4.0]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.4.0
[0.3.0]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.3
[0.2.0]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.2
[0.1.0]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.1
