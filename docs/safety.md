# Rule safety decisions

The central catalog is authoritative. This document records why rules currently classified as `ReviewRequired` do not participate in the global Roslyn batch Fix All provider.

| Rules | Decision and risk |
| --- | --- |
| UBP0001 | Changing a public field to private changes API accessibility. The diagnostic remains available, but solution-wide symbol reference analysis withholds the fix when a reference outside the declaring or nested type would become inaccessible. Unity serialization should still be reviewed. |
| UBP0003 | Squared-distance comparison can differ for NaN, infinity, overflow, rounding, or code that depends on the original square-root operation. |
| UBP0004 | Burst compilation changes generated code, supported managed operations, debugging, floating-point behavior, and platform execution characteristics. |
| UBP0005 | `[ReadOnly]` affects Unity job safety and scheduling. The analyzer proves recognized reads but cannot prove every package/runtime behavior. |
| UBP0006 | `stackalloc` changes allocation lifetime and consumes bounded stack space. The rule has a conservative byte limit but still requires review for call depth and platform stack constraints. |
| UBP0007 | Ref mutation changes aliasing and copy behavior. The matcher requires a real ref-return path and matching write-back, but surrounding code may observe aliases. |
| UBP0008 | Caching `Camera.main` assumes repeated accesses in a block should observe the same camera. Scene or tag changes between accesses could make that observably different. |
| UBP0010 | Multiplication and `Mathf.Pow` can differ in floating-point corner cases and platform implementation details. |
| UBP0011 | Uninitialized native memory changes allocation initialization and becomes unsafe if the analyzer's full-overwrite precondition is invalidated by later edits. |
| UBP0027 | Replacing `Quaternion.Euler(0, 0, 0)` can differ in floating-point representation or code relying on a particular construction path. |
| UBP0038 | `Mathf.Sqrt` and `Mathf.Pow(value, 0.5f)` can differ for negative, NaN, infinity, and rounding cases. |
| UBP0039 | `Mathf.FloorToInt` can differ from cast-after-floor behavior at overflow and non-finite boundaries. |
| UBP0040 | `Mathf.CeilToInt` can differ from cast-after-ceil behavior at overflow and non-finite boundaries. |
| UBP0041 | `Mathf.RoundToInt` can differ from cast-after-round behavior at overflow and non-finite boundaries. |
| UBP0042 | Replacing a new empty array with `Array.Empty<T>()` changes object identity even though contents are equivalent. |
| UBP0057 | Replacing `Enumerable.Empty<T>().ToArray()` with `Array.Empty<T>()` changes allocation and object identity. |
| UBP0058 | `Entities.ForEach(...).Run()` to `SystemAPI.Query` retains main-thread iteration but changes query construction and source shape; filters, enabled-state behavior, and package semantics require review. |
| UBP0059 | Extracting `Entities.ForEach` to `IJobEntity.Run` changes generated query/job structure even though execution remains immediate. |
| UBP0060 | Extracting to `IJobEntity.Schedule` changes timing, dependency propagation, synchronization, and thread-safety obligations. |
| UBP0061 | Extracting to `IJobEntity.ScheduleParallel` additionally introduces parallel execution and component access constraints. |
| UBP0062 | Extracting `SystemAPI.Query` to `IJobEntity.Run` changes query/job representation and generated-code behavior. |
| UBP0063 | Extracting `SystemAPI.Query` to `IJobEntity.Schedule` changes execution timing and dependency ownership. |
| UBP0064 | Extracting `SystemAPI.Query` to `IJobEntity.ScheduleParallel` changes timing, parallelism, dependencies, and thread-safety requirements. |
| UBP0065 | `IJobEntity.Run` to `Schedule` changes immediate execution to deferred scheduled work. |
| UBP0066 | `IJobEntity.Run` to `ScheduleParallel` changes immediate execution to deferred parallel work. |
| UBP0067 | `IJobEntity.Schedule` to `Run` introduces an immediate synchronization/execution point. |
| UBP0068 | `IJobEntity.Schedule` to `ScheduleParallel` changes worker concurrency and component access requirements. |
| UBP0069 | `IJobEntity.ScheduleParallel` to `Run` removes parallelism and introduces immediate execution. |
| UBP0070 | `IJobEntity.ScheduleParallel` to `Schedule` changes parallel scheduling to a single scheduled job. |

Safe rules may still omit Fix All when one code action rewrites multiple locations or generates names that could conflict with independently batched actions. UBP0071 and UBP0074 use that conservative policy.
