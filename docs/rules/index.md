# Rule index

This index is generated from the analyzer's central diagnostic catalog. Run `dotnet run --project tests/UnityBestPractices.Analyzers.Tests -- --generate-rule-docs .` after changing rule metadata.

| ID | Title | Category | Severity | Safety | Fix All |
| --- | --- | --- | --- | --- | --- |
| [UBP0001](UBP0001.md) | Encapsulate serialized field | `Unity.API.Design` | Info | ReviewRequired | No |
| [UBP0002](UBP0002.md) | Yield null for the next frame | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0003](UBP0003.md) | Use squared magnitude for a distance check | `Unity.Performance.Review` | Info | ReviewRequired | No |
| [UBP0004](UBP0004.md) | Burst compile Unity job | `Unity.Performance.Review` | Info | ReviewRequired | No |
| [UBP0005](UBP0005.md) | Mark job input as read-only | `Unity.Performance.Review` | Info | ReviewRequired | No |
| [UBP0006](UBP0006.md) | Stack allocate small temporary buffer | `CSharp.Performance` | Info | ReviewRequired | No |
| [UBP0007](UBP0007.md) | Mutate struct element by reference | `CSharp.Performance` | Info | ReviewRequired | No |
| [UBP0008](UBP0008.md) | Cache repeated Camera.main lookup | `Unity.Performance.Review` | Info | ReviewRequired | No |
| [UBP0009](UBP0009.md) | Preallocate list capacity | `CSharp.Performance` | Info | Safe | Yes |
| [UBP0010](UBP0010.md) | Multiply to square a scalar | `Unity.Performance.Review` | Info | ReviewRequired | No |
| [UBP0011](UBP0011.md) | Skip redundant NativeArray clearing | `Unity.Performance.Review` | Info | ReviewRequired | No |
| [UBP0012](UBP0012.md) | Use Vector2.zero | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0013](UBP0013.md) | Use Vector2.one | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0014](UBP0014.md) | Use Vector2.up | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0015](UBP0015.md) | Use Vector2.down | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0016](UBP0016.md) | Use Vector2.left | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0017](UBP0017.md) | Use Vector2.right | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0018](UBP0018.md) | Use Vector3.zero | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0019](UBP0019.md) | Use Vector3.one | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0020](UBP0020.md) | Use Vector3.up | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0021](UBP0021.md) | Use Vector3.down | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0022](UBP0022.md) | Use Vector3.left | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0023](UBP0023.md) | Use Vector3.right | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0024](UBP0024.md) | Use Vector3.forward | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0025](UBP0025.md) | Use Vector3.back | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0026](UBP0026.md) | Use Quaternion.identity | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0027](UBP0027.md) | Use Quaternion.identity | `Unity.Performance.Review` | Info | ReviewRequired | No |
| [UBP0028](UBP0028.md) | Use Color.clear | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0029](UBP0029.md) | Use Color.black | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0030](UBP0030.md) | Use Color.white | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0031](UBP0031.md) | Use Color.red | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0032](UBP0032.md) | Use Color.green | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0033](UBP0033.md) | Use Color.blue | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0034](UBP0034.md) | Use Color.yellow | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0035](UBP0035.md) | Use Color.cyan | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0036](UBP0036.md) | Use Color.magenta | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0037](UBP0037.md) | Use Mathf.Clamp01 | `Unity.Performance.Safe` | Info | Safe | Yes |
| [UBP0038](UBP0038.md) | Use Mathf.Sqrt | `Unity.Performance.Review` | Info | ReviewRequired | No |
| [UBP0039](UBP0039.md) | Use Mathf.FloorToInt | `Unity.Performance.Review` | Info | ReviewRequired | No |
| [UBP0040](UBP0040.md) | Use Mathf.CeilToInt | `Unity.Performance.Review` | Info | ReviewRequired | No |
| [UBP0041](UBP0041.md) | Use Mathf.RoundToInt | `Unity.Performance.Review` | Info | ReviewRequired | No |
| [UBP0042](UBP0042.md) | Reuse an empty array | `CSharp.Performance` | Info | ReviewRequired | No |
| [UBP0043](UBP0043.md) | Fuse Where with Any | `CSharp.Performance` | Info | Safe | Yes |
| [UBP0044](UBP0044.md) | Fuse Where with Count | `CSharp.Performance` | Info | Safe | Yes |
| [UBP0045](UBP0045.md) | Fuse Where with First | `CSharp.Performance` | Info | Safe | Yes |
| [UBP0046](UBP0046.md) | Fuse Where with FirstOrDefault | `CSharp.Performance` | Info | Safe | Yes |
| [UBP0047](UBP0047.md) | Use Dictionary.ContainsKey | `CSharp.Performance` | Info | Safe | Yes |
| [UBP0048](UBP0048.md) | Use the List indexer | `CSharp.Performance` | Info | Safe | Yes |
| [UBP0049](UBP0049.md) | Use List.Count | `CSharp.Performance` | Info | Safe | Yes |
| [UBP0050](UBP0050.md) | Use Array.Length | `CSharp.Performance` | Info | Safe | Yes |
| [UBP0051](UBP0051.md) | Use Array.Length for emptiness | `CSharp.Performance` | Info | Safe | Yes |
| [UBP0052](UBP0052.md) | Use List.Count for emptiness | `CSharp.Performance` | Info | Safe | Yes |
| [UBP0053](UBP0053.md) | Append a character | `CSharp.Performance` | Info | Safe | Yes |
| [UBP0054](UBP0054.md) | Append an empty line directly | `CSharp.Performance` | Info | Safe | Yes |
| [UBP0055](UBP0055.md) | Use CancellationToken.None | `CSharp.Performance` | Info | Safe | Yes |
| [UBP0056](UBP0056.md) | Use Guid.Empty | `CSharp.Performance` | Info | Safe | Yes |
| [UBP0057](UBP0057.md) | Use Array.Empty<T>() directly | `CSharp.Performance` | Info | ReviewRequired | No |
| [UBP0058](UBP0058.md) | Convert Entities.ForEach to SystemAPI.Query | `Unity.DOTS.Migration` | Info | ReviewRequired | No |
| [UBP0059](UBP0059.md) | Convert Entities.ForEach to IJobEntity.Run | `Unity.DOTS.Migration` | Info | ReviewRequired | No |
| [UBP0060](UBP0060.md) | Convert Entities.ForEach to IJobEntity.Schedule | `Unity.DOTS.Migration` | Info | ReviewRequired | No |
| [UBP0061](UBP0061.md) | Convert Entities.ForEach to IJobEntity.ScheduleParallel | `Unity.DOTS.Migration` | Info | ReviewRequired | No |
| [UBP0062](UBP0062.md) | Convert SystemAPI.Query to IJobEntity.Run | `Unity.DOTS.Migration` | Info | ReviewRequired | No |
| [UBP0063](UBP0063.md) | Convert SystemAPI.Query to IJobEntity.Schedule | `Unity.DOTS.Migration` | Info | ReviewRequired | No |
| [UBP0064](UBP0064.md) | Convert SystemAPI.Query to IJobEntity.ScheduleParallel | `Unity.DOTS.Migration` | Info | ReviewRequired | No |
| [UBP0065](UBP0065.md) | Switch IJobEntity from Run to Schedule | `Unity.DOTS.Migration` | Info | ReviewRequired | No |
| [UBP0066](UBP0066.md) | Switch IJobEntity from Run to ScheduleParallel | `Unity.DOTS.Migration` | Info | ReviewRequired | No |
| [UBP0067](UBP0067.md) | Switch IJobEntity from Schedule to Run | `Unity.DOTS.Migration` | Info | ReviewRequired | No |
| [UBP0068](UBP0068.md) | Switch IJobEntity from Schedule to ScheduleParallel | `Unity.DOTS.Migration` | Info | ReviewRequired | No |
| [UBP0069](UBP0069.md) | Switch IJobEntity from ScheduleParallel to Run | `Unity.DOTS.Migration` | Info | ReviewRequired | No |
| [UBP0070](UBP0070.md) | Switch IJobEntity from ScheduleParallel to Schedule | `Unity.DOTS.Migration` | Info | ReviewRequired | No |
| [UBP0071](UBP0071.md) | Preserve scheduled JobHandle | `Unity.Correctness` | Info | Safe | No |
| [UBP0072](UBP0072.md) | Dispose persistent native container | `Unity.Correctness` | Info | Safe | No |
| [UBP0073](UBP0073.md) | Do not let temporary native memory escape | `Unity.Correctness` | Info | Safe | No |
| [UBP0074](UBP0074.md) | Cache shader property ID | `Unity.Performance.Safe` | Info | Safe | No |
| [UBP0075](UBP0075.md) | Match the folder namespace | `CSharp.CodeStyle` | Info | ReviewRequired | No |
