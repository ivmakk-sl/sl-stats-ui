using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;
using SlShared.ItemPool;

namespace StatsUI
{
    // Reads the cure map from the config once after each save load: each usable item with the effect of its use
    // action (Config_Item.UseAction, Config_Action.EffectConfigID), and each buff with the tree and IntValue0 of its
    // effect. It copies the raw fields into the rows of CuresLogic and keeps no rule of its own.
    internal static class CuresReader
    {
        private static CuresLogic.Map map;

        // A new leading role (a save load): the next read walks the config again.
        internal static void Forget() => map = null;

        // The map of the config, or CuresLogic.Map.Empty with one warning when the config cannot be read.
        internal static CuresLogic.Map Map()
        {
            if (map != null) return map;
            try
            {
                var config = ConfigManager.Instance;
                if (config == null) return CuresLogic.Map.Empty;
                map = CuresLogic.Build(Items(config), Buffs(config));
                Plugin.Debug($"Stats UI cures: {map.DebuffCount} debuffs with a cure or a suppressor, {map.ItemCount} items");
            }
            catch (Exception e)
            {
                map = CuresLogic.Map.Empty;
                Plugin.WarnOnce("Stats UI: the cures of the config cannot be read (" + e.Message + "); the tooltips show no cure and no suppressor.");
            }
            return map;
        }

        // True while the buff list of the last read holds a debuff with a cure or a suppressor and both switches are on
        // (CuresLogic.NeedsPool). The tick reads the item pool again after an item change only while it is true.
        internal static bool NeedsPool { get; private set; }

        // The version of the item pool store that the last cures part holds, so the tick marks the part again only
        // when a later pool read moved the store.
        internal static int PoolVersion { get; private set; }

        // The cures part of the page data. It reads the item totals (a pool read when an item changed) only while
        // NeedsPool holds.
        internal static string Json()
        {
            bool on = Plugin.CuresInTooltips.Value && Plugin.ExtendedTooltips.Value;
            var map = Map();
            var buffIds = BuffIds();
            NeedsPool = CuresLogic.NeedsPool(map, buffIds, Plugin.CuresInTooltips.Value, Plugin.ExtendedTooltips.Value);
            if (!NeedsPool) return PageJson.CuresJson(on, null, null);

            var totals = ItemPool.Read();
            PoolVersion = ItemPool.Store.Version;
            // A failed read (no battle world yet, an unreadable bag mirror) gives no totals, so no lists show until the
            // tick reads the pool again: an unread pool never looks like an empty home.
            if (totals.Failed) return PageJson.EmptyCures;
            var linesByDebuff = new Dictionary<int, CuresLogic.Lines>();
            var items = new Dictionary<int, (string Name, string Icon)>();
            foreach (int id in buffIds)
            {
                if (!map.HasLinks(id) || linesByDebuff.ContainsKey(id)) continue;
                var lines = CuresLogic.LinesOf(map, id, totals.TotalOf);
                linesByDebuff[id] = lines;
                foreach (var line in lines.Cures) if (!items.ContainsKey(line.ItemId)) items[line.ItemId] = ItemInfo(line.ItemId);
                foreach (var line in lines.Suppressors) if (!items.ContainsKey(line.ItemId)) items[line.ItemId] = ItemInfo(line.ItemId);
            }
            if (Plugin.Verbose.Value)
                foreach (var pair in linesByDebuff)
                    Plugin.Log.LogDebug($"Stats UI cures: debuff={pair.Key} cures={LinesText(pair.Value.Cures)} suppressors={LinesText(pair.Value.Suppressors)} store={ItemPool.Store.Version}{(totals.Failed ? " pool failed" : "")}");
            return PageJson.CuresJson(on, linesByDebuff, items);
        }

        private static string LinesText(List<CuresLogic.Line> lines)
        {
            var parts = new List<string>();
            foreach (var line in lines) parts.Add(line.ItemId + "x" + line.Total);
            return parts.Count == 0 ? "-" : string.Join(",", parts);
        }

        // The config ids of the buff list of the leading role (the list of the HUD), in its order.
        private static List<int> BuffIds()
        {
            var ids = new List<int>();
            var list = ReduxUISystem.Instance?.GetData<State_Data_Buff>(Il2CppType.Of<State_Data_Buff>())?.LeadingRoleBuffList;
            if (list == null) return ids;
            var it = list.GetEnumerator();
            while (it.MoveNext())
                if (it.Current != null) ids.Add(it.Current.BuffConfigId);
            return ids;
        }

        // The name of an item in the display language and its web icon (empty for none). Read at each build, never
        // cached across a refresh: the display language can change while the game runs.
        internal static (string Name, string Icon) ItemInfo(int itemId)
        {
            var config = ConfigManager.Instance;
            var item = config?.Get_Config_Item(itemId);
            if (item == null) return (itemId.ToString(), "");
            return (config.GetLocalTxt(item.ItemName), item.WebIcon ?? "");
        }

        private static List<CuresLogic.ItemIn> Items(ConfigManager config)
        {
            var rows = new List<CuresLogic.ItemIn>();
            var it = config._Config_Item_Dict.GetEnumerator();
            while (it.MoveNext())
            {
                var item = it.Current.Value;
                if (item == null || item.CantUse || item.UseAction <= 0) continue;
                var action = config.Get_Config_Action(item.UseAction);
                var effect = action != null ? config.Get_Config_Effect(action.EffectConfigID) : null;
                if (effect == null) continue;
                rows.Add(new CuresLogic.ItemIn
                {
                    ItemId = item.ID,
                    TargetTypes = Ints(effect.EBuffTargetType),
                    TargetIds = Ints(effect.EBuffTargetID),
                    AddBuffs = Ints(effect.EAddBuff),
                    DelBuffs = Ints(effect.EDelBuff),
                    EndBuff1 = effect.ItemEndBuff01,
                    EndBuff2 = effect.ItemEndBuff02,
                });
            }
            return rows;
        }

        private static List<CuresLogic.BuffIn> Buffs(ConfigManager config)
        {
            var rows = new List<CuresLogic.BuffIn>();
            var it = config._Config_Buff_Dict.GetEnumerator();
            while (it.MoveNext())
            {
                var buff = it.Current.Value;
                if (buff == null) continue;
                var effect = buff.EffectConfigId > 0 ? config.Get_Config_Effect(buff.EffectConfigId) : null;
                rows.Add(new CuresLogic.BuffIn
                {
                    BuffId = buff.ID,
                    IsGood = buff.IsGood,
                    Tags = Strings(buff.TagList),
                    Tree = effect?.ConfigName,
                    IntValue0 = effect != null ? effect.IntValue0 : 0,
                });
            }
            return rows;
        }

        private static int[] Ints(Il2CppSystem.Collections.Generic.List<int> list)
        {
            if (list == null) return new int[0];
            var result = new int[list.Count];
            for (int i = 0; i < result.Length; i++) result[i] = list[i];
            return result;
        }

        private static string[] Strings(Il2CppSystem.Collections.Generic.List<string> list)
        {
            if (list == null) return new string[0];
            var result = new string[list.Count];
            for (int i = 0; i < result.Length; i++) result[i] = list[i];
            return result;
        }
    }
}
