using GameCore.HotUpdate;
using HarmonyLib;

namespace StatsUI
{
    // A language switch changes the mod texts, the buff part (the text of the game and the effect lines of each buff
    // are in the display language), and the stats part (the names of the food types). ConfigManager.SwitchLanguage
    // returns a UniTask, so the Postfix fires when the method returns, not when the switch ends; the mark only sets a
    // flag, and the push waits for a later frame of PageTick.
    [HarmonyPatch(typeof(ConfigManager), "SwitchLanguage")]
    internal static class TextsOnSwitchLanguage
    {
        private static void Postfix()
        {
            Plugin.Debug("Stats UI texts: language switch");
            PageTick.Mark(DataParts.Part.Texts);
            PageTick.Mark(DataParts.Part.Buffs);
            PageTick.Mark(DataParts.Part.Stats);
        }
    }
}
