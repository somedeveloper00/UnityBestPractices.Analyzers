using System.Threading.Tasks;
using Xunit;

public sealed class FixtureIntegrityTests
{
    [Fact]
    public async Task SharedUnityAndDotsFixturesCompileWithoutErrors()
    {
        var result = await AnalyzerTestHost.ValidateUnityFixturesAsync();
        Assert.True(result.Succeeded, string.Join(System.Environment.NewLine, result.Diagnostics));
    }
}
