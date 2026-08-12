# Contributing

Thank you for improving Unity Best Practices Analyzer. Changes should be conservative, semantically guarded, and compatible with Unity's Roslyn 3.8 host.

## Development setup

Install the .NET SDK selected by `global.json`, then run:

```powershell
dotnet restore UnityBestPractices.sln
dotnet build UnityBestPractices.sln -c Release --no-restore
dotnet run --project tests/UnityBestPractices.Analyzers.Tests -c Release --no-build
dotnet test tests/UnityBestPractices.Analyzers.Tests.Xunit -c Release --no-build
dotnet run --project tests/UnityBestPractices.Analyzers.PerformanceTests -c Release --no-build
```

## Rule changes

Every diagnostic must have a unique stable ID, central `RuleMetadata`, an `Info` default severity, a help link, a rule page, negative semantic tests, and a documented safety classification. Do not reuse or renumber IDs. A safe rule may support Fix All only when all documented preconditions preserve observable behavior. Review-required and experimental transformations must be applied one occurrence at a time.

Keep the dependency-light regression harness at the existing coverage floor: five distinct positive quick-fix cases for every rule and ten for complicated transformations. Add focused xUnit tests under a file named for the diagnostic when changing symbol, solution, or Fix All behavior.

### Quick fixes

- Register every fix through `CodeFixRegistration.Register`. The equivalence key must be the diagnostic ID; titles come from `RuleMetadata.FixTitle` via `FixTitleLocalizer`.
- `UnityBestPracticesCodeFixProvider.FixableDiagnosticIds` is catalog-driven (`HasCodeFix`). Do not maintain a separate ID list.
- Prefer structured `SyntaxFactory` rewrites over `ParseExpression` / `ParseStatement` / `ParseMemberDeclaration` for multi-statement or type-member generation. Revalidate matchers on apply.
- Put apply logic next to the rule family under `Rules/` (for example `LegacyCoreCodeFixes`, expression rules, DOTS builders). Keep the code-fix provider as thin dispatch.
- Catalog/provider parity, equivalence keys, and Safe Fix All exceptions are enforced by `CodeFixCatalogInvariantTests`.

After changing catalog metadata, regenerate rule pages:

```powershell
dotnet run --project tests/UnityBestPractices.Analyzers.Tests -- --generate-rule-docs .
```

## Pull requests

Keep changes focused and explain false-positive controls, behavior changes, compatibility impact, and validation commands. Update `CHANGELOG.md`. New diagnostics should begin with a design note that discusses expected false positives and semantic guards.

By contributing, you agree that your contribution is licensed under the repository's MIT License.
