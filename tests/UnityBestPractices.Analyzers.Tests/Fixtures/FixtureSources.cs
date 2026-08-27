internal static class FixtureSources
{
    internal static string Unity => string.Join(
        System.Environment.NewLine,
        UnityEngineFixture.Source,
        BurstFixture.Source,
        UnityJobsFixture.Source,
        UnityEntitiesFixture.Source,
        UnityCollectionsFixture.Source);
}
