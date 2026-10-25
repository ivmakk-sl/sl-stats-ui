using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using SlShared.ItemPool;
using UnityEngine;

namespace StatsUI
{
    // The per-frame schedule of the sends to the HUD page. Each game hook only marks the part that it changes; this
    // tick reads the marked parts and pushes the data at most once per frame, or checks about once each real second
    // that the page still has the page script. PushSchedule decides between a push, a check, and nothing, and holds
    // the only retry.
    //
    // WebUILayer.OnUpdate is the per-frame hook of the web UI layer (as in Item Totals and Project Cook). It has no
    // pause check, which the weight readout needs: the storage window pauses the world while the player moves items
    // in and out of the bag.
    [HarmonyPatch(typeof(WebUILayer), "OnUpdate")]
    internal static class PageTick
    {
        private static readonly PushSchedule schedule = new PushSchedule();
        private static readonly DataParts parts = new DataParts();
        private static PageJson.Switches switches;
        // The leading role of the last tick: a new one (a save load) marks each part.
        private static IntPtr lastRole = IntPtr.Zero;
        // The marked parts of the last build, for the Verbose push line.
        private static string reason = "";

        internal static void Mark(DataParts.Part part) => parts.Mark(part);

        internal static void ForgetLastPush() => schedule.ForgetLastPush();

        // The last pushed data, or null before the first push.
        internal static string LastJson => schedule.LastJson;

        // The counts of one check that was ok, logged as sums once each real minute.
        internal static void OnCheckCounts(int runs, int passes, int passMicros)
        {
            var sum = schedule.ObserverSum(Time.realtimeSinceStartup, runs, passes, passMicros);
            if (!sum.HasValue) return;
            var (r, p, micros) = sum.Value;
            double average = p > 0 ? micros / 1000.0 / p : 0;
            Plugin.Debug(FormattableString.Invariant(
                $"Stats UI observer: {r} runs, {p} passes in 60 s, {micros / 1000.0:F1} ms in passes, {average:F2} ms each"));
        }

        // Nothing here may throw into the game's frame. A failed build keeps its marks in DataParts, and a failed send
        // sends again one real second later.
        private static void Postfix()
        {
            try
            {
                // Before a save loads there is no leading role, and no CoreUI1 frame to send to.
                var role = GameWorld.Current?._AgentManager?.GetLeadingRole();
                if (role == null) return;
                if (role.Pointer != lastRole)
                {
                    lastRole = role.Pointer;
                    CuresReader.Forget();
                    foreach (DataParts.Part part in Enum.GetValues(typeof(DataParts.Part))) parts.Mark(part);
                }
                // The item totals of the cures: a pool read after an item change, only while a debuff of the buff list
                // needs them (CuresReader.NeedsPool), at most once for each frame.
                if (CuresReader.NeedsPool)
                {
                    if (ItemPool.Store.IsDirty) ItemPool.Refresh();
                    if (ItemPool.Store.Version != CuresReader.PoolVersion) parts.Mark(DataParts.Part.Cures);
                }
                // A push with no build this tick sends the last data again.
                reason = "send again";
                var step = schedule.Tick(Time.realtimeSinceStartup, parts.AnyDirty, Build);
                if (step.Kind == PushSchedule.Kind.Push) PageScript.SetData(step.Json, reason);
                else if (step.Kind == PushSchedule.Kind.Check) PageScript.Check();
            }
            catch (Exception e)
            {
                schedule.ForgetLastPush();
                Plugin.WarnOnce("Stats UI: push failed: " + e.Message);
            }
        }

        private static string Build()
        {
            if (switches == null)
                switches = new PageJson.Switches
                {
                    WeightReadout = Plugin.WeightReadout.Value,
                    TimeStripes = Plugin.TimeStripes.Value,
                    ExtendedTooltips = Plugin.ExtendedTooltips.Value,
                    StatTooltipGameText = Plugin.StatTooltipGameText.Value,
                    CuresInTooltips = Plugin.CuresInTooltips.Value,
                };
            var marked = new List<string>();
            foreach (DataParts.Part part in Enum.GetValues(typeof(DataParts.Part)))
                if (parts.IsMarked(part)) marked.Add(part.ToString().ToLowerInvariant());
            var json = parts.Read(ReadPart);
            reason = "changed " + string.Join(",", marked);
            return PageJson.DataJson(switches,
                json[(int)DataParts.Part.Texts],
                json[(int)DataParts.Part.Stats],
                json[(int)DataParts.Part.Buffs],
                json[(int)DataParts.Part.Weight],
                json[(int)DataParts.Part.Cures]);
        }

        private static string ReadPart(DataParts.Part part)
        {
            switch (part)
            {
                case DataParts.Part.Texts: return PageJson.TextsJson(ModTexts.Current().ToDictionary());
                case DataParts.Part.Stats: return StatsReader.Json();
                case DataParts.Part.Buffs: return BuffsReader.Json();
                case DataParts.Part.Cures: return CuresReader.Json();
                default: return WeightReader.Json();
            }
        }
    }
}
