using SlShared.I18n;
using Xunit;

namespace StatsUI.Tests;

// The mod texts of the mod, read from the i18n files through the library. The game-facing part (the display
// language and the warnings to the log) is the library's GameTexts, used by src/ModTexts.cs, and cannot be
// unit-tested.
public class ModTextsTests
{
    private const int English = 1;
    private const int Community = 2;

    private static I18nTexts Texts() => I18nTexts.Load(typeof(ModTextsTests).Assembly, "StatsUI");

    [Fact]
    public void ADisplayLanguageWithNoFileFallsBackToEnglish()
    {
        var set = Texts().For(7, null, null);

        Assert.Equal("en", set.Language);
        Assert.Equal(Texts().For(English, null, null).ToDictionary(), set.ToDictionary());
    }

    [Fact]
    public void ACommunityPackWithNoFileShowsEnglishWithNoWarning()
    {
        var texts = Texts();
        var set = texts.For(Community, "tr-TR", null);

        Assert.Equal("en", set.Language);
        Assert.Equal(Texts().For(English, null, null).ToDictionary(), set.ToDictionary());
        Assert.Empty(texts.Warnings);
    }
}
