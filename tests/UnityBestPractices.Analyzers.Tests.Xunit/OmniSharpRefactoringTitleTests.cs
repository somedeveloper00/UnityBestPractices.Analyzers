using UnityBestPractices.Analyzers;
using Xunit;

public sealed class OmniSharpRefactoringTitleTests
{
    [Theory]
    [InlineData("Inline method", "Inline method", "Inline method")]
    [InlineData("メソッドをインライン化", "Inline method", "メソッドをインライン化")]
    public void InlineAddsTheRoutingPrefixOnlyForEnglishLabels(
        string displayTitle,
        string englishTitle,
        string expected) =>
        Assert.Equal(expected, OmniSharpRefactoringTitle.Inline(displayTitle, englishTitle));

    [Theory]
    [InlineData("Extract Move parameter right", "Move parameter right", "Extract Move parameter right")]
    [InlineData("パラメーターを右へ移動", "Move parameter right", "パラメーターを右へ移動")]
    public void ExtractAddsTheRoutingPrefixOnlyForEnglishLabels(
        string displayTitle,
        string englishTitle,
        string expected) =>
        Assert.Equal(expected, OmniSharpRefactoringTitle.Extract(displayTitle, englishTitle));
}
