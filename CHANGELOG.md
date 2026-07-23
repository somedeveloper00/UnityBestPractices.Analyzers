# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/compare/v0.3...HEAD
[0.3.0]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.3
[0.2.0]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.2
[0.1.0]: https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases/tag/v0.1
