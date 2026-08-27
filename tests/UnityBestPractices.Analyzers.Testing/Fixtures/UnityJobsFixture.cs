namespace UnityBestPractices.Analyzers.Testing;

public static class UnityJobsFixture
{
    public const string Source = """
        namespace Unity.Jobs
        {
            public struct JobHandle { }
            public interface IJob { void Execute(); }
            public static class IJobExtensions
            {
                public static JobHandle Schedule<T>(this T job) where T : struct, IJob => default;
                public static JobHandle ScheduleParallel<T>(this T job) where T : struct, IJob => default;
            }
        }
        """;
}
