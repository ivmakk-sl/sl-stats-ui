using SlShared.I18n;
using Xunit;

namespace StatsUI.Tests;

// The mod texts of the mod, read from the i18n files through the library. The game-facing part (the display
// language and the warnings to the log) is in src/ModTexts.cs and cannot be unit-tested.
public class ModTextsTests
{
    private const int English = 1;

    private static I18nTexts Texts() => I18nTexts.Load(typeof(ModTextsTests).Assembly, "StatsUI");

    [Fact]
    public void ADisplayLanguageWithNoFileFallsBackToEnglish()
    {
        var set = Texts().For(7, null, "Klingon");

        Assert.Equal("en", set.Language);
        Assert.Equal(Texts().For(English, null).ToDictionary(), set.ToDictionary());
    }

    [Fact]
    public void ADisplayLanguageWithNoFileWarnsOnceWithItsNumberAndName()
    {
        var texts = Texts();
        texts.For(7, null, "Klingon");

        Assert.Contains(texts.Warnings, w => w.Contains("7") && w.Contains("Klingon"));
    }
}
