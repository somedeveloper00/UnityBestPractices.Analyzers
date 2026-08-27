using System;
using System.Runtime.CompilerServices;

internal static class FixtureBootstrap
{
    [ModuleInitializer]
    internal static void ValidateSharedFixtures()
    {
        var result = AnalyzerTestHost.ValidateUnityFixturesAsync().GetAwaiter().GetResult();
        if (!result.Succeeded)
            throw new InvalidOperationException(
                "Shared Unity fixtures failed to compile:" + Environment.NewLine +
                string.Join(Environment.NewLine, result.Diagnostics));
    }
}
