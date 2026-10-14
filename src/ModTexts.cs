using SlShared.I18n;

namespace StatsUI
{
    // The mod texts (src/i18n/) through the i18n library, which reads the display language. No other code of the mod
    // reads the language.
    internal static class ModTexts
    {
        private static readonly GameTexts texts =
            new GameTexts(typeof(ModTexts).Assembly, "StatsUI", warning => Plugin.Log.LogWarning("Stats UI: " + warning));
        private static LanguageChoice logged;

        // The mod texts in the display language. The language can change while the game runs, so the texts are read
        // again at each push.
        internal static I18nTextSet Current()
        {
            var set = texts.Current();
            if (!texts.Choice.Equals(logged))
            {
                logged = texts.Choice;
                Plugin.Debug("Stats UI texts: " + logged);
            }
            return set;
        }
    }
}
