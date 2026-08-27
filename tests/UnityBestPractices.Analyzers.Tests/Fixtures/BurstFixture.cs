internal static class BurstFixture
{
    internal const string Source = """
        namespace Unity.Burst
        {
            public sealed class BurstCompileAttribute : System.Attribute { }
        }
        """;
}
