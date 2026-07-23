# Unity Best Practices Analyzers

This package installs `UnityBestPractices.Analyzers.dll` as a Unity Roslyn
analyzer. The committed `.meta` file assigns the case-sensitive
`RoslynAnalyzer` label, disables platform loading, and disables Unity reference
validation for the IDE-hosted code-fix dependencies.

After installation:

1. Regenerate the C# project files.
2. Restart Rider or Visual Studio so its Roslyn host reloads the analyzer.
3. Ensure Roslyn analyzers are enabled in the IDE.

See the repository README for rule safety classifications, configuration, and
manual installation details.
