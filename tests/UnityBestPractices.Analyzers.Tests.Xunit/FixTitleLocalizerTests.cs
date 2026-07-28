using System.Globalization;
using UnityBestPractices.Analyzers;
using Xunit;

public sealed class FixTitleLocalizerTests
{
    [Fact]
    public void UsesSystemCultureWhenAnalyzerHostUICultureIsEnglish()
    {
        var title = FixTitleLocalizer.Get(
            DiagnosticIds.YieldNull,
            "Yield null",
            CultureInfo.GetCultureInfo("en-US"),
            CultureInfo.GetCultureInfo("en-US"),
            CultureInfo.GetCultureInfo("ja-JP"));

        Assert.Equal("null を yield する", title);
    }

    [Fact]
    public void UsesCurrentCultureWhenAnalyzerHostUICultureIsEnglish()
    {
        var title = FixTitleLocalizer.Get(
            DiagnosticIds.YieldNull,
            "Yield null",
            CultureInfo.GetCultureInfo("en-US"),
            CultureInfo.GetCultureInfo("ja-JP"),
            CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("null を yield する", title);
    }

    [Fact]
    public void PreservesAnExplicitLocalizedIdeCulture()
    {
        var title = FixTitleLocalizer.Get(
            DiagnosticIds.YieldNull,
            "Yield null",
            CultureInfo.GetCultureInfo("ru-RU"),
            CultureInfo.GetCultureInfo("ja-JP"),
            CultureInfo.GetCultureInfo("ja-JP"));

        Assert.Equal("Вернуть null через yield", title);
    }
}
