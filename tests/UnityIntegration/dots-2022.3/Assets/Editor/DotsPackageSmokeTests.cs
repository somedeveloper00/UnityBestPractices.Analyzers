using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

public struct SmokeComponent : IComponentData
{
    public int Value;
}

[BurstCompile]
public partial struct SmokeJob : IJobEntity
{
    public void Execute(ref SmokeComponent component)
    {
        component.Value++;
    }
}

public sealed class DotsPackageSmokeTests
{
    [Test]
    public void RealDotsSymbolsCompileAndLoad()
    {
        using var values = new NativeArray<int>(1, Allocator.Temp);
        Assert.AreEqual(1, values.Length);
    }
}
