# Analyzer configuration

Unity Best Practices Analyzer uses standard `.editorconfig` severity settings and a small set of conservative analyzer options. Copy one of the files in [`config`](../config) to the root of a Unity project, rename it to `.editorconfig`, and merge it with any existing configuration.

## Severity

All rules are enabled as `suggestion`/`Info` by default. The IDE can still offer a quick fix, but command-line and Unity builds do not produce a warning or error.

```ini
[*.cs]
# Promote one rule.
dotnet_diagnostic.UBP0009.severity = warning

# Treat another rule as an error.
dotnet_diagnostic.UBP0002.severity = error

# Disable one rule.
dotnet_diagnostic.UBP0008.severity = none

# Configure an entire category.
dotnet_analyzer_diagnostic.category-Unity.Performance.Safe.severity = suggestion
```

Rules can be configured as `none`, `silent`, `suggestion`, `warning`, or `error`. A rule-specific `dotnet_diagnostic.UBPxxxx.severity` setting takes precedence over a category or global setting according to Roslyn's standard configuration rules.

## Options

| Key | Default | Valid values | Meaning |
| --- | --- | --- | --- |
| `ubp_max_stackalloc_bytes` | `1024` | `16` through `1048576` | Maximum statically known byte size considered by UBP0006. |
| `ubp_minimum_list_adds` | `5` | `2` through `1000` | Minimum consecutive `List<T>.Add` calls considered by UBP0009. |
| `ubp_enable_dots_migration` | `true` | `true` or `false` | Enables UBP0058–UBP0070. |
| `ubp_enable_review_required` | `true` | `true` or `false` | Enables every rule classified as ReviewRequired. |

Options declared for a particular source-file section override values in the global analyzer configuration. Missing and invalid values use the documented defaults; malformed configuration never throws from the analyzer.

```ini
[Assets/Scripts/Gameplay/**/*.cs]
ubp_max_stackalloc_bytes = 512
ubp_minimum_list_adds = 8
ubp_enable_dots_migration = false
```

## Presets

- [`ubp-safe.editorconfig`](../config/ubp-safe.editorconfig) enables only safe transformations.
- [`ubp-performance.editorconfig`](../config/ubp-performance.editorconfig) enables performance rules, including review-required performance transformations.
- [`ubp-dots-migration.editorconfig`](../config/ubp-dots-migration.editorconfig) enables only review-required DOTS migration rules.
- [`ubp-all.editorconfig`](../config/ubp-all.editorconfig) enables the complete catalog.

Review-required fixes deliberately do not support Fix All. In particular, changing `Run`, `Schedule`, or `ScheduleParallel` may change synchronization, dependency propagation, execution timing, and thread-safety requirements.
