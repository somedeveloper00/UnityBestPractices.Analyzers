namespace UnityBestPractices.Analyzers.Testing;

public static class FixtureSources
{
    public static string Unity => string.Join(
        System.Environment.NewLine,
        UnityEngineFixture.Source,
        BurstFixture.Source,
        UnityJobsFixture.Source,
        UnityEntitiesFixture.Source,
        UnityCollectionsFixture.Source);
}
