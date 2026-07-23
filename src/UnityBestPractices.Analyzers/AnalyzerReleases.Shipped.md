; Shipped analyzer releases

## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
UBP0001 | Unity.BestPractices | Hidden | Encapsulate serialized field
UBP0002 | Unity.BestPractices | Hidden | Yield null for the next frame
UBP0003 | Unity.BestPractices | Hidden | Use squared magnitude for a distance check
UBP0004 | Unity.BestPractices | Hidden | Burst compile Unity job
UBP0005 | Unity.BestPractices | Hidden | Mark job input as read-only
UBP0006 | Unity.BestPractices | Hidden | Stack allocate small temporary buffer
UBP0007 | Unity.BestPractices | Hidden | Mutate struct element by reference
UBP0008 | Unity.BestPractices | Hidden | Cache repeated Camera.main access
UBP0009 | Unity.BestPractices | Hidden | Preallocate List capacity for known additions
UBP0010 | Unity.BestPractices | Hidden | Multiply a stable float to square it
UBP0011 | Unity.BestPractices | Hidden | Skip clearing a fully overwritten NativeArray

## Release 1.1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
UBP0012 | Unity.BestPractices | Hidden | Use Vector2.zero
UBP0013 | Unity.BestPractices | Hidden | Use Vector2.one
UBP0014 | Unity.BestPractices | Hidden | Use Vector2.up
UBP0015 | Unity.BestPractices | Hidden | Use Vector2.down
UBP0016 | Unity.BestPractices | Hidden | Use Vector2.left
UBP0017 | Unity.BestPractices | Hidden | Use Vector2.right
UBP0018 | Unity.BestPractices | Hidden | Use Vector3.zero
UBP0019 | Unity.BestPractices | Hidden | Use Vector3.one
UBP0020 | Unity.BestPractices | Hidden | Use Vector3.up
UBP0021 | Unity.BestPractices | Hidden | Use Vector3.down
UBP0022 | Unity.BestPractices | Hidden | Use Vector3.left
UBP0023 | Unity.BestPractices | Hidden | Use Vector3.right
UBP0024 | Unity.BestPractices | Hidden | Use Vector3.forward
UBP0025 | Unity.BestPractices | Hidden | Use Vector3.back
UBP0026 | Unity.BestPractices | Hidden | Use Quaternion.identity
UBP0027 | Unity.BestPractices | Hidden | Use Quaternion.identity for zero Euler rotation
UBP0028 | Unity.BestPractices | Hidden | Use Color.clear
UBP0029 | Unity.BestPractices | Hidden | Use Color.black
UBP0030 | Unity.BestPractices | Hidden | Use Color.white
UBP0031 | Unity.BestPractices | Hidden | Use Color.red
UBP0032 | Unity.BestPractices | Hidden | Use Color.green
UBP0033 | Unity.BestPractices | Hidden | Use Color.blue
UBP0034 | Unity.BestPractices | Hidden | Use Color.yellow
UBP0035 | Unity.BestPractices | Hidden | Use Color.cyan
UBP0036 | Unity.BestPractices | Hidden | Use Color.magenta
UBP0037 | Unity.BestPractices | Hidden | Use Mathf.Clamp01
UBP0038 | Unity.BestPractices | Hidden | Use Mathf.Sqrt
UBP0039 | Unity.BestPractices | Hidden | Use Mathf.FloorToInt
UBP0040 | Unity.BestPractices | Hidden | Use Mathf.CeilToInt
UBP0041 | Unity.BestPractices | Hidden | Use Mathf.RoundToInt
UBP0042 | Unity.BestPractices | Hidden | Reuse Array.Empty
UBP0043 | Unity.BestPractices | Hidden | Fuse Where with Any
UBP0044 | Unity.BestPractices | Hidden | Fuse Where with Count
UBP0045 | Unity.BestPractices | Hidden | Fuse Where with First
UBP0046 | Unity.BestPractices | Hidden | Fuse Where with FirstOrDefault
UBP0047 | Unity.BestPractices | Hidden | Use Dictionary.ContainsKey
UBP0048 | Unity.BestPractices | Hidden | Use List indexer
UBP0049 | Unity.BestPractices | Hidden | Use List.Count
UBP0050 | Unity.BestPractices | Hidden | Use Array.Length
UBP0051 | Unity.BestPractices | Hidden | Use Array.Length for emptiness
UBP0052 | Unity.BestPractices | Hidden | Use List.Count for emptiness
UBP0053 | Unity.BestPractices | Hidden | Append a character
UBP0054 | Unity.BestPractices | Hidden | Append an empty line directly
UBP0055 | Unity.BestPractices | Hidden | Use CancellationToken.None
UBP0056 | Unity.BestPractices | Hidden | Use Guid.Empty
UBP0057 | Unity.BestPractices | Hidden | Use Array.Empty for Enumerable.Empty

## Release 1.2.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
UBP0058 | Unity.BestPractices | Hidden | Convert Entities.ForEach to SystemAPI.Query
UBP0059 | Unity.BestPractices | Hidden | Convert Entities.ForEach to IJobEntity.Run
UBP0060 | Unity.BestPractices | Hidden | Convert Entities.ForEach to IJobEntity.Schedule
UBP0061 | Unity.BestPractices | Hidden | Convert Entities.ForEach to IJobEntity.ScheduleParallel
UBP0062 | Unity.BestPractices | Hidden | Convert SystemAPI.Query to IJobEntity.Run
UBP0063 | Unity.BestPractices | Hidden | Convert SystemAPI.Query to IJobEntity.Schedule
UBP0064 | Unity.BestPractices | Hidden | Convert SystemAPI.Query to IJobEntity.ScheduleParallel
UBP0065 | Unity.BestPractices | Hidden | Switch IJobEntity Run to Schedule
UBP0066 | Unity.BestPractices | Hidden | Switch IJobEntity Run to ScheduleParallel
UBP0067 | Unity.BestPractices | Hidden | Switch IJobEntity Schedule to Run
UBP0068 | Unity.BestPractices | Hidden | Switch IJobEntity Schedule to ScheduleParallel
UBP0069 | Unity.BestPractices | Hidden | Switch IJobEntity ScheduleParallel to Run
UBP0070 | Unity.BestPractices | Hidden | Switch IJobEntity ScheduleParallel to Schedule
