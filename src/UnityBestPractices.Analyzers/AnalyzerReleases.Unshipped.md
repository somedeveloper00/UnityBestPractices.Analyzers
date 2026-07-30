; Unshipped analyzer release

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
UBP0071 | Unity.Correctness | Info | Preserve a discarded scheduled JobHandle
UBP0072 | Unity.Correctness | Info | Detect a narrowly provable undisposed persistent NativeArray
UBP0073 | Unity.Correctness | Info | Detect a temporary allocator lifetime escape
UBP0074 | Unity.Performance.Safe | Info | Cache repeated Shader.PropertyToID calls
UBP0076 | Unity.Performance.Safe | Info | Combine adjacent local position and rotation assignments
UBP0077 | Unity.Performance.Safe | Info | Remove unused SystemAPI.Query entity access

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
UBP0001 | Unity.API.Design | Info | Unity.BestPractices | Hidden | Enable IDE discovery; classify API change as review-required
UBP0002 | Unity.Performance.Safe | Info | Unity.BestPractices | Hidden | Enable IDE quick-fix discovery
UBP0003 | Unity.Performance.Review | Info | Unity.BestPractices | Hidden | Enable IDE discovery; classify floating-point rewrite as review-required
UBP0004 | Unity.Performance.Review | Info | Unity.BestPractices | Hidden | Enable IDE discovery; classify Burst behavior as review-required
UBP0005 | Unity.Performance.Review | Info | Unity.BestPractices | Hidden | Enable IDE discovery; classify job access change as review-required
UBP0006 | CSharp.Performance | Info | Unity.BestPractices | Hidden | Enable IDE discovery; classify allocation-lifetime change as review-required
UBP0007 | CSharp.Performance | Info | Unity.BestPractices | Hidden | Enable IDE discovery; classify aliasing change as review-required
UBP0008 | Unity.Performance.Review | Info | Unity.BestPractices | Hidden | Enable IDE discovery; classify lookup caching as review-required
UBP0009 | CSharp.Performance | Info | Unity.BestPractices | Hidden | Enable IDE quick-fix discovery
UBP0010 | Unity.Performance.Review | Info | Unity.BestPractices | Hidden | Enable IDE discovery; classify floating-point rewrite as review-required
UBP0011 | Unity.Performance.Review | Info | Unity.BestPractices | Hidden | Enable IDE discovery; classify initialization change as review-required
