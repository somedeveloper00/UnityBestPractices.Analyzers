using System.Globalization;
using System.Linq;
using UnityBestPractices.Analyzers;
using Xunit;

public sealed class FixTitleLocalizerTests
{
    private static readonly CultureInfo[] SupportedCultures =
    {
        CultureInfo.GetCultureInfo("ja-JP"),
        CultureInfo.GetCultureInfo("fa-IR"),
        CultureInfo.GetCultureInfo("ru-RU"),
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
        var suggestions = new[]
        {
            (FixTitleLocalizer.ConvertStringLiteralToNameof, ConvertStringLiteralToNameofCodeRefactoringProvider.Title),
            (FixTitleLocalizer.InlineMethod, InlineMethodCodeRefactoringProvider.Title),
            (FixTitleLocalizer.MoveParameterLeft, MoveParameterCodeRefactoringProvider.MoveLeftTitle),
            (FixTitleLocalizer.MoveParameterRight, MoveParameterCodeRefactoringProvider.MoveRightTitle),
            (FixTitleLocalizer.MoveStatementUp, MoveStatementCodeRefactoringProvider.MoveUpTitle),
            (FixTitleLocalizer.MoveStatementDown, MoveStatementCodeRefactoringProvider.MoveDownTitle),
            (FixTitleLocalizer.RemoveParameter, RemoveParameterCodeRefactoringProvider.Title),
            (FixTitleLocalizer.RemoveDoubleEmptyLines, RemoveDoubleEmptyLinesCodeRefactoringProvider.Title),
            (FixTitleLocalizer.RemoveSymbol, RemoveSymbolCodeRefactoringProvider.Title),
        };

        foreach (var (key, englishTitle) in suggestions)
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
        var suggestions = new[]
        {
            (FixTitleLocalizer.ConvertStringLiteralToNameof, ConvertStringLiteralToNameofCodeRefactoringProvider.Title, "文字列リテラルを nameof に置換"),
            (FixTitleLocalizer.InlineMethod, InlineMethodCodeRefactoringProvider.Title, "メソッドをインライン化"),
            (FixTitleLocalizer.MoveParameterLeft, MoveParameterCodeRefactoringProvider.MoveLeftTitle, "パラメーターを左へ移動"),
            (FixTitleLocalizer.MoveParameterRight, MoveParameterCodeRefactoringProvider.MoveRightTitle, "パラメーターを右へ移動"),
            (FixTitleLocalizer.MoveStatementUp, MoveStatementCodeRefactoringProvider.MoveUpTitle, "ステートメントを上へ移動"),
            (FixTitleLocalizer.MoveStatementDown, MoveStatementCodeRefactoringProvider.MoveDownTitle, "ステートメントを下へ移動"),
            (FixTitleLocalizer.RemoveParameter, RemoveParameterCodeRefactoringProvider.Title, "パラメーターを削除"),
            (FixTitleLocalizer.RemoveDoubleEmptyLines, RemoveDoubleEmptyLinesCodeRefactoringProvider.Title, "連続する空行を削除"),
            (FixTitleLocalizer.RemoveSymbol, RemoveSymbolCodeRefactoringProvider.Title, "シンボルとすべての使用箇所を削除"),
        };

        var culture = CultureInfo.GetCultureInfo("ja-JP");
        foreach (var (key, englishTitle, japaneseTitle) in suggestions)
        {
            Assert.Equal(japaneseTitle, FixTitleLocalizer.Get(key, englishTitle, culture));
        }
    }
}
