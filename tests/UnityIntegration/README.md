# Unity integration fixtures

These package manifests prepare local smoke tests without redistributing Unity assemblies or requiring a public-CI Unity license.

| Fixture | Purpose | Coverage status |
| --- | --- | --- |
| `2021.3` | Oldest claimed editor family; base Unity and C# rules | Manifest fixture and local Editor smoke test |
| `dots-2022.3` | Oldest supported Entities 1.x family | Entities 1.0.11, Collections 2.1.4, Burst 1.8.2 symbol and compilation fixture |
| `6000.3` | Current Unity 6.3 LTS family | Manifest fixture and local Editor smoke test |

Unity identifies Unity 6.3 as the current LTS release on its [release support page](https://unity.com/releases/unity-6/support). Unity's 2022.3 documentation lists Entities 1.0.11 as released for that editor, and the Unity 6.0 documentation lists Entities 1.4.3 and Collections 2.6.3 as released. The current-LTS fixture intentionally does not pin DOTS packages until Unity publishes an equivalent verified 6000.3 package matrix.

## Local process

1. Copy the chosen fixture to a temporary directory.
2. Add the assembled UPM package to `Packages/manifest.json` as a local `file:` dependency.
3. Open the fixture with the matching installed Unity editor.
4. Confirm the analyzer DLL imports with the `RoslynAnalyzer` label and no Unity Console errors.
5. Add representative source under `Assets`, verify expected suggestions in the IDE, apply each tested fix, and request script recompilation.
6. For the DOTS fixture, compile examples using `SystemAPI.Query`, `IJobEntity`, `NativeArray`, Burst, and all three execution modes.

Public CI validates the manifests and package layout. An optional Unity job may be enabled by configuring the Unity license secrets documented in the CI workflow.

No compatibility is claimed for DOTS package shapes that fail the analyzer's documented symbol guards.
