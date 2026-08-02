# Unity Best Practices Analyzers

> Catch Unity C# performance and correctness issues before entering play mode.

[Project overview](https://github.com/somedeveloper00/UnityBestPractices.Analyzers#readme) · [Rule catalog](https://github.com/somedeveloper00/UnityBestPractices.Analyzers/blob/master/docs/rules/index.md) · [Configuration](https://github.com/somedeveloper00/UnityBestPractices.Analyzers/blob/master/docs/configuration.md) · [Report an issue](https://github.com/somedeveloper00/UnityBestPractices.Analyzers/issues)

## What this package does

This package installs `UnityBestPractices.Analyzers.dll` as a Unity Roslyn analyzer for Rider, Visual Studio, and VS Code on Windows, macOS, and Linux. The included `.meta` file:

- assigns the case-sensitive `RoslynAnalyzer` label;
- disables platform loading; and
- disables Unity reference validation for IDE-hosted code-fix dependencies.

## After installation

1. Regenerate the C# project files.
2. Restart Rider, Visual Studio, or VS Code so its Roslyn host reloads the analyzer.
3. Ensure Roslyn analyzers are enabled in the IDE.

> [!NOTE]
> Suggestions default to `Info`, so they do not add build warnings, errors, or Unity Console noise.

If suggestions do not appear, see the repository's [manual installation and troubleshooting steps](https://github.com/somedeveloper00/UnityBestPractices.Analyzers#manual-dll). Review the [safety classifications](https://github.com/somedeveloper00/UnityBestPractices.Analyzers#safety-model) before applying review-required fixes.
