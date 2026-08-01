using UnityBestPractices.Analyzers;
using Xunit;

public sealed class OmniSharpRefactoringTitleTests
{
    [Theory]
    [InlineData("Inline method", "Inline method")]
    [InlineData("メソッドをインライン化", "Inline メソッドをインライン化")]
    public void InlineAddsTheRoutingPrefixOnlyWhenNeeded(string title, string expected) =>
        Assert.Equal(expected, OmniSharpRefactoringTitle.Inline(title));

    [Theory]
    [InlineData("Extract Move parameter right", "Extract Move parameter right")]
    [InlineData("پارامتر را به راست انتقال دهید", "Extract پارامتر را به راست انتقال دهید")]
    public void ExtractAddsTheRoutingPrefixOnlyWhenNeeded(string title, string expected) =>
        Assert.Equal(expected, OmniSharpRefactoringTitle.Extract(title));
}
