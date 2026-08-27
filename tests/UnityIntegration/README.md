# Unity integration fixtures

> Reproducible smoke-test projects for every claimed Unity compatibility family.

[← Project overview](../../README.md) · [Compatibility matrix](../../README.md#compatibility) · [Contributing](../../CONTRIBUTING.md)

These package manifests prepare local smoke tests without redistributing Unity assemblies or requiring a public-CI Unity license.

| Fixture | Purpose | Coverage status |
| --- | --- | --- |
| `2021.3` | Oldest claimed editor family; base Unity and C# rules | Manifest fixture and local Editor smoke test |
| `dots-2022.3` | Oldest supported Entities 1.x family | Entities 1.0.11, Collections 2.1.4, Burst 1.8.2 symbol and compilation fixture |
| `6000.3` | Current Unity 6.3 LTS family | Manifest fixture and local Editor smoke test |

## Ownership and supported versions

The analyzer maintainers own these fixtures, their package pins, and the EditMode smoke tests. Changes to `packaging/upm`, analyzer loading metadata, supported Unity families, or DOTS symbol handling must update the affected fixture in the same pull request. The licensed matrix currently runs Unity **2021.3.45f1**, **2022.3.62f1** (the `dots-2022.3` fixture), and **6000.3.8f1**. Those exact patch releases are the CI-supported editor versions; the fixture directory names describe the compatibility families.

To upgrade a fixture:

1. Select a supported Unity patch release and update both `ProjectSettings/ProjectVersion.txt` and the matching matrix entry in `.github/workflows/ci.yml`.
2. Review Unity's package compatibility documentation, update only intentional `Packages/manifest.json` pins, and retain the local analyzer-package installation performed by CI.
3. Update or add the Editor smoke tests for API or compiler changes, then run the license-free fixture validator and the fixture's licensed EditMode test.
4. Update this table and version list, and include the upgrade evidence in the pull request.

## CI and release policy

Every CI run assembles the UPM payload and installs it into every fixture without requiring a Unity license. The licensed GameCI matrix runs weekly at 03:17 UTC on Monday, on branch pushes, before tagged releases through the evidence gate below, and when manually dispatched. Pull requests from forks can complete the required license-free job when secrets are unavailable; the summary reports that the licensed entries were not executed rather than implying success.

A release commit must have a successful licensed matrix run, or be a descendant of a commit with a successful scheduled matrix run completed within the previous **7 days**. Release managers must link that run in the release notes. Results older than seven days are stale: rerun the matrix manually on the release commit before creating the tag. A failed or credential-unavailable run is never acceptable release evidence.

Unity identifies Unity 6.3 as the current LTS release on its [release support page](https://unity.com/releases/unity-6/support). Unity's 2022.3 documentation lists Entities 1.0.11 as released for that editor, and the Unity 6.0 documentation lists Entities 1.4.3 and Collections 2.6.3 as released. The current-LTS fixture intentionally does not pin DOTS packages until Unity publishes an equivalent verified 6000.3 package matrix.

## Run a local smoke test

1. Copy the chosen fixture to a temporary directory.
2. Add the assembled UPM package to `Packages/manifest.json` as a local `file:` dependency.
3. Open the fixture with the matching installed Unity editor.
4. Confirm the analyzer DLL imports with the `RoslynAnalyzer` label and no Unity Console errors.
5. Add representative source under `Assets`, verify expected suggestions in the IDE, apply each tested fix, and request script recompilation.
6. For the DOTS fixture, compile examples using `SystemAPI.Query`, `IJobEntity`, `NativeArray`, Burst, and all three execution modes.

Public CI validates the manifests and package layout. An optional Unity job may be enabled by configuring the Unity license secrets documented in the CI workflow.

> [!IMPORTANT]
> No compatibility is claimed for DOTS package shapes that fail the analyzer's documented symbol guards.
