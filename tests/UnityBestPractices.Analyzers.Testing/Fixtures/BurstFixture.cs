namespace UnityBestPractices.Analyzers.Testing;

public static class BurstFixture
{
    public const string Source = """
        namespace Unity.Burst
        {
            public sealed class BurstCompileAttribute : System.Attribute { }
        }
        """;
}
