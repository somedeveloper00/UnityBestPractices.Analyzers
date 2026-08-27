# Internal dependency direction

The analyzer is intentionally shipped as one `netstandard2.0` assembly. Unity's analyzer
loading behaviour is sensitive to additional dependencies, so production code must not be
split into more assemblies until dependency loading has been explicitly supported and tested
in every supported Unity editor.

Within that assembly, dependencies flow inward through these layers:

1. **Exported entry points and public catalog** — the diagnostic analyzer, code-fix and
   refactoring providers, `DiagnosticCatalog`, `DiagnosticIds`, and rule metadata remain in
   `UnityBestPractices.Analyzers`. This preserves the consumer-facing API and MEF type names.
2. **Infrastructure** — configuration, Roslyn helpers, symbol caches, fix registration, and
   fix-all implementation live in `UnityBestPractices.Analyzers.Infrastructure`. Infrastructure
   may use the public catalog, but it does not depend on a rule family.
3. **Rule families** — core, correctness, expression, and DOTS coordination lives in
   `UnityBestPractices.Analyzers.Rules.Core`, `.Correctness`, `.Expressions`, and `.Dots`.
   Entry points may compose these modules; families should not call unrelated families.
4. **Rule-specific matchers and transformations** — implementation details remain internal
   to their family. They may depend on shared infrastructure and the public catalog.

Rule-family production code must never depend on tests, packaging/engineering code, or the
unrelated root-level refactoring providers. New cross-family behaviour should be composed by
an exported entry point or promoted to a narrowly scoped infrastructure abstraction rather
than creating a sideways dependency.

Architecture tests enforce namespace placement, implementation visibility, export placement,
and forbidden source dependencies. When moving code, migrate one family at a time and run the
full test suite; the export/discovery assertions protect analyzer and MEF discovery while the
namespace assertions protect the internal boundaries.
