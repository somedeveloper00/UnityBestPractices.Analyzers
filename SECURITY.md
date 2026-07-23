# Security policy

## Supported versions

Security fixes are applied to the latest released minor version. Users should upgrade to the newest GitHub release.

## Reporting a vulnerability

Do not open a public issue for a vulnerability that could expose user source code, execute code during analysis, or compromise a release artifact. Use GitHub's private vulnerability reporting feature on this repository. Include affected versions, impact, reproduction steps, and any proposed mitigation.

Maintainers will acknowledge a report when available, investigate it privately, and coordinate disclosure after a fix is ready. Analyzer diagnostics and false positives that do not create a security boundary issue should use the public issue templates.

The analyzer runs inside compiler and IDE processes. Treat third-party analyzer DLLs as executable code and verify release checksums before installation.
