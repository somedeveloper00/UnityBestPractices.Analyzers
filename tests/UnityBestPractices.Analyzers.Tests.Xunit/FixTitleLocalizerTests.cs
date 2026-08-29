using System.Globalization;
using System.Linq;
using UnityBestPractices.Analyzers;
using Xunit;

public sealed class FixTitleLocalizerTests
{
    private static readonly (string Key, string EnglishTitle, string JapaneseTitle)[] RefactoringSuggestions =
    {
        (FixTitleLocalizer.ConvertStringLiteralToNameof, ConvertStringLiteralToNameofCodeRefactoringProvider.Title, "文字列リテラルを nameof に置換"),
        (FixTitleLocalizer.ConvertLocalToField, ConvertLocalToFieldCodeRefactoringProvider.Title, "ローカル変数をフィールドに変換"),
        (FixTitleLocalizer.ConvertSystemBaseToISystem, ConvertSystemBaseToISystemCodeRefactoringProvider.Title, "SystemBase を ISystem に変換"),
        (FixTitleLocalizer.InlineMethod, InlineMethodCodeRefactoringProvider.Title, "メソッドをインライン化"),
        (FixTitleLocalizer.MoveParameterLeft, MoveParameterCodeRefactoringProvider.MoveLeftTitle, "パラメーターを左へ移動"),
        (FixTitleLocalizer.MoveParameterRight, MoveParameterCodeRefactoringProvider.MoveRightTitle, "パラメーターを右へ移動"),
        (FixTitleLocalizer.MoveStatementUp, MoveStatementCodeRefactoringProvider.MoveUpTitle, "ステートメントを上へ移動"),
        (FixTitleLocalizer.MoveStatementDown, MoveStatementCodeRefactoringProvider.MoveDownTitle, "ステートメントを下へ移動"),
        (FixTitleLocalizer.MoveStatementLeft, MoveStatementCodeRefactoringProvider.MoveLeftTitle, "ステートメントを左へ移動"),
        (FixTitleLocalizer.MoveStatementRight, MoveStatementCodeRefactoringProvider.MoveRightTitle, "ステートメントを右へ移動"),
        (FixTitleLocalizer.RemoveParameter, RemoveParameterCodeRefactoringProvider.Title, "パラメーターを削除"),
        (FixTitleLocalizer.RemoveDoubleEmptyLines, RemoveDoubleEmptyLinesCodeRefactoringProvider.Title, "連続する空行を削除"),
        (FixTitleLocalizer.RemoveEmptyBrackets, RemoveEmptyBracketsCodeRefactoringProvider.Title, "空の括弧を削除"),
        (FixTitleLocalizer.RemoveSymbol, RemoveSymbolCodeRefactoringProvider.Title, "シンボルとすべての使用箇所を削除"),
    };

    private static readonly CultureInfo[] SupportedCultures =
    {
        CultureInfo.GetCultureInfo("ja-JP"),
        CultureInfo.GetCultureInfo("fa-IR"),
        CultureInfo.GetCultureInfo("ru-RU"),
        CultureInfo.GetCultureInfo("de-DE"),
        CultureInfo.GetCultureInfo("pl-PL"),
    };

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

    [Fact]
    public void EveryQuickFixHasEverySupportedLocalization()
    {
        foreach (var rule in DiagnosticCatalog.All.Where(rule => rule.HasCodeFix))
        {
            foreach (var culture in SupportedCultures)
            {
                Assert.NotEqual(rule.FixTitle, FixTitleLocalizer.Get(rule.DiagnosticId, rule.FixTitle, culture));
            }
        }
    }

    [Fact]
    public void EveryRefactoringSuggestionHasEverySupportedLocalization()
    {
        foreach (var (key, englishTitle, _) in RefactoringSuggestions)
        {
            foreach (var culture in SupportedCultures)
            {
                Assert.NotEqual(englishTitle, FixTitleLocalizer.Get(key, englishTitle, culture));
            }
        }
    }

    [Fact]
    public void JapaneseRefactoringSuggestionsUseJapaneseDisplayTitles()
    {
        var culture = CultureInfo.GetCultureInfo("ja-JP");
        foreach (var (key, englishTitle, japaneseTitle) in RefactoringSuggestions)
        {
            Assert.Equal(japaneseTitle, FixTitleLocalizer.Get(key, englishTitle, culture));
        }
    }

    [Theory]
    [InlineData("de-DE", "null mit yield zurückgeben")]
    [InlineData("pl-PL", "Zwróć null za pomocą yield")]
    public void NewCulturesUseLocalizedDisplayTitles(string cultureName, string expectedTitle)
    {
        Assert.Equal(
            expectedTitle,
            FixTitleLocalizer.Get(
                DiagnosticIds.YieldNull,
                "Yield null",
                CultureInfo.GetCultureInfo(cultureName)));
    }
}
