# Documentation hub

> A human- and AI-friendly map of the Unity Best Practices Analyzer documentation.

[← Project overview](../README.md) · [Install](../README.md#installation) · [Configure](configuration.md) · [Contribute](../CONTRIBUTING.md)

## Choose a destination

| Goal | Documentation |
| --- | --- |
| Find a diagnostic by ID or category | [Rule index](rules/index.md) |
| Decide whether a fix can be applied automatically | [Safety model and decisions](safety.md) |
| Change severities or analyzer options | [Configuration reference](configuration.md) |
| Copy a recommended `.editorconfig` | [`config/` presets](../config) |
| Verify supported Unity and DOTS versions | [Compatibility matrix](../README.md#compatibility) |
| Run a Unity Editor smoke test | [Unity integration fixtures](../tests/UnityIntegration/README.md) |
| Package the analyzer for Unity | [UPM package notes](../packaging/upm/README.md) |
| Propose a rule or code change | [Contributing guide](../CONTRIBUTING.md) |

## Documentation conventions

- Diagnostic pages use stable IDs (`UBP0001`–`UBP0075`) and live in [`rules/`](rules).
- The [rule index](rules/index.md) is the source of truth for category, default severity, safety classification, and Fix All support.
- Configuration keys and conservative defaults are documented in [configuration](configuration.md).
- Behavior-changing risks are recorded explicitly in [safety decisions](safety.md).

When using an AI coding assistant, point it to this page or [`llms.txt`](../llms.txt) before asking it to configure, explain, or extend the analyzer.
