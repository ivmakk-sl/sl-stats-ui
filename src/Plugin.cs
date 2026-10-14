using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace StatsUI
{
    [BepInPlugin(PluginGuid, "Stats UI", "1.0.0")]
    [BepInProcess("SurvivalLog.exe")]
    public sealed class Plugin : BasePlugin
    {
        public const string PluginGuid = "com.ivmakk.survivallog.statsui";

        internal static new ManualLogSource Log;
        internal static Harmony Harmony;
        internal static ConfigEntry<bool> WeightReadout;
        internal static ConfigEntry<bool> TimeStripes;
        internal static ConfigEntry<bool> ExtendedTooltips;
        internal static ConfigEntry<bool> StatTooltipGameText;
        internal static ConfigEntry<bool> Verbose;

        public override void Load()
        {
            Log = base.Log;
            WeightReadout = Config.Bind(
                "Display", "WeightReadout", true,
                "Show the carried weight on the Inventory button of the HUD: the number and the weight bar. Applies at the next game start.");
            TimeStripes = Config.Bind(
                "Display", "TimeStripes", true,
                "Show a time stripe at the bottom of each buff and debuff badge that has an end time. Applies at the next game start.");
            ExtendedTooltips = Config.Bind(
                "Display", "ExtendedTooltips", true,
                "Show the extended tooltips of the stat bars and the buff and debuff badges in place of the game's tooltips. Applies at the next game start.");
            StatTooltipGameText = Config.Bind(
                "Display", "StatTooltipGameText", true,
                "Show the game's text about the stat at the bottom of the extended tooltip of a stat bar. Applies at the next game start.");
            Verbose = Config.Bind(
                "General", "Verbose", false,
                "Log each send to the HUD, the values it sent, and the page-script detail at Debug level. Keep off in normal play.");
            Harmony = new Harmony(PluginGuid);
            var patches = new List<Type>
            {
                typeof(PageTick),
                typeof(WeightOnUpdateOwnerBagWeight),
                typeof(WeightOnUpdateHeavyLoadRatio),
                typeof(WeightOnAddExtraBurden),
                typeof(StatsOnSyncAttr),
                typeof(StatsOnRefreshLeadingRoleBuff),
                typeof(TextsOnApplyLanguageKey),
            };
            // Each patch target is attached on its own, so a target missing after a game update turns
            // off only its own part.
            foreach (var type in patches)
            {
                try { Harmony.CreateClassProcessor(type).Patch(); }
                catch (Exception e) { Log.LogWarning($"patch {type.Name} failed, its target method is missing: {e.Message}"); }
            }

            Log.LogInfo("Stats UI loaded.");
        }

        internal static void Debug(string line)
        {
            if (Verbose.Value) Log.LogDebug(line);
        }

        private static readonly HashSet<string> warned = new HashSet<string>();

        // Logs one warning for each distinct text, because a failure can repeat on each frame.
        internal static void WarnOnce(string line)
        {
            if (warned.Add(line)) Log.LogWarning(line);
        }
    }
}
