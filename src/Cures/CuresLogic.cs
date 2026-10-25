using System;
using System.Collections.Generic;

namespace StatsUI
{
    // The cures and the suppressors of each debuff, from the raw fields of the item effects and the buffs of the
    // config. The mod keeps no list of its own: a game update that adds a med changes the map with no mod change. No
    // game or BepInEx type here.
    public static class CuresLogic
    {
        // EBuffTargetType of an effect row for the leading role (AE_SelectTargetAddOrDelBuff.Run). Type 2 picks
        // other agents by EBuffTargetID.
        public const int PlayerTarget = 1;
        // Config_Buff.TagList: the tag of a buff that suppresses each debuff with SuppressibleTag while it is active
        // (BuffComponent.SetDebuffSuppression).
        public const string SuppressorTag = "DebuffSuppressor";
        public const string SuppressibleTag = "SuppressibleDebuff";
        // The ConfigName of the effect of a counter buff: it gives back the changes of the buff of its IntValue0
        // while that buff is active (Fever Reducing for Disease Outbreak).
        public const string CounterTree = "Buff/Buff_CondOnOtherBuff";

        // One usable item with the fields of the effect of its use action, and the two buffs at the end of its use
        // (Config_Effect.ItemEndBuff01 and ItemEndBuff02 of the same effect, 0 for none).
        public sealed class ItemIn
        {
            public int ItemId;
            public int[] TargetTypes;
            public int[] TargetIds;
            public int[] AddBuffs;
            public int[] DelBuffs;
            public int EndBuff1;
            public int EndBuff2;
        }

        // One buff of Config_Buff with the ConfigName and IntValue0 of its effect.
        public sealed class BuffIn
        {
            public int BuffId;
            public bool IsGood;
            public string[] Tags;
            public string Tree;
            public int IntValue0;
        }

        // The item ids of the cures and of the suppressors of each debuff with a link, each list in the order of the
        // item id.
        public sealed class Map
        {
            private static readonly IReadOnlyList<int> None = new int[0];
            private readonly Dictionary<int, List<int>> cures;
            private readonly Dictionary<int, List<int>> suppressors;

            internal Map(Dictionary<int, List<int>> cures, Dictionary<int, List<int>> suppressors)
            {
                this.cures = cures;
                this.suppressors = suppressors;
                var debuffs = new HashSet<int>(cures.Keys);
                debuffs.UnionWith(suppressors.Keys);
                DebuffCount = debuffs.Count;
                var items = new HashSet<int>();
                foreach (var list in cures.Values) items.UnionWith(list);
                foreach (var list in suppressors.Values) items.UnionWith(list);
                ItemCount = items.Count;
            }

            public static readonly Map Empty = new Map(new Dictionary<int, List<int>>(), new Dictionary<int, List<int>>());

            // The count of the debuffs with a cure or a suppressor, and of the items in any list, for the log.
            public int DebuffCount { get; }
            public int ItemCount { get; }

            public IReadOnlyList<int> CuresOf(int debuffId) =>
                cures.TryGetValue(debuffId, out var list) ? list : None;

            public IReadOnlyList<int> SuppressorsOf(int debuffId) =>
                suppressors.TryGetValue(debuffId, out var list) ? list : None;

            public bool HasLinks(int debuffId) => cures.ContainsKey(debuffId) || suppressors.ContainsKey(debuffId);
        }

        // The map of the config. A cure removes a debuff from the player; a suppressor gives the player a buff with
        // SuppressorTag (for each debuff with SuppressibleTag) or a counter buff (for the debuff of its IntValue0).
        // The effect lists pair by index as the game loop does: it runs while the index is below the length of both
        // target lists, and at a target of the player it adds and removes the entry of that index when there is one.
        public static Map Build(IEnumerable<ItemIn> items, IEnumerable<BuffIn> buffs)
        {
            var buffById = new Dictionary<int, BuffIn>();
            var suppressible = new List<int>();
            foreach (var b in buffs)
            {
                if (b == null) continue;
                buffById[b.BuffId] = b;
                if (!b.IsGood && HasTag(b, SuppressibleTag)) suppressible.Add(b.BuffId);
            }

            var cures = new Dictionary<int, List<int>>();
            var suppressors = new Dictionary<int, List<int>>();
            var sorted = new List<ItemIn>(items);
            sorted.RemoveAll(i => i == null);
            sorted.Sort((a, b) => a.ItemId.CompareTo(b.ItemId));
            foreach (var item in sorted)
            {
                var added = new List<int>();
                int steps = Math.Min(Length(item.TargetTypes), Length(item.TargetIds));
                for (int i = 0; i < steps; i++)
                {
                    if (item.TargetTypes[i] != PlayerTarget) continue;
                    if (i < Length(item.AddBuffs) && item.AddBuffs[i] > 0) added.Add(item.AddBuffs[i]);
                    if (i < Length(item.DelBuffs) && item.DelBuffs[i] > 0 && IsDebuff(buffById, item.DelBuffs[i]))
                        AddOnce(cures, item.DelBuffs[i], item.ItemId);
                }
                if (item.EndBuff1 > 0) added.Add(item.EndBuff1);
                if (item.EndBuff2 > 0) added.Add(item.EndBuff2);

                foreach (int id in added)
                {
                    if (!buffById.TryGetValue(id, out var buff)) continue;
                    if (HasTag(buff, SuppressorTag))
                        foreach (int debuff in suppressible) AddOnce(suppressors, debuff, item.ItemId);
                    if (buff.Tree == CounterTree && buff.IntValue0 > 0 && IsDebuff(buffById, buff.IntValue0))
                        AddOnce(suppressors, buff.IntValue0, item.ItemId);
                }
            }
            return new Map(cures, suppressors);
        }

        // The most item lines in the tooltip of one debuff, the cures first.
        public const int MaxLines = 10;

        // One item line: the item and its item total.
        public struct Line
        {
            public int ItemId;
            public int Total;
        }

        // The item lines of one debuff, each block in the order of the tooltip.
        public sealed class Lines
        {
            public readonly List<Line> Cures = new List<Line>();
            public readonly List<Line> Suppressors = new List<Line>();
        }

        // The item lines of one debuff with the item totals: in each block the items with a total above 0 first, then
        // the others, each group by item id, and at most MaxLines lines with the cures first.
        public static Lines LinesOf(Map map, int debuffId, Func<int, int> totalOf)
        {
            var lines = new Lines();
            int left = Fill(lines.Cures, map.CuresOf(debuffId), totalOf, MaxLines);
            Fill(lines.Suppressors, map.SuppressorsOf(debuffId), totalOf, left);
            return lines;
        }

        // True while the item totals are needed: both switches are on and a debuff of the buff list has a cure or a
        // suppressor. Else an item change only sets the dirty bit of the item pool, and no read follows.
        public static bool NeedsPool(Map map, IEnumerable<int> buffIds, bool cures, bool extended)
        {
            if (!cures || !extended || map == null || buffIds == null) return false;
            foreach (int id in buffIds)
                if (map.HasLinks(id)) return true;
            return false;
        }

        // The ids come in the order of the item id, so a stable split keeps that order in each group.
        private static int Fill(List<Line> into, IReadOnlyList<int> itemIds, Func<int, int> totalOf, int left)
        {
            var none = new List<Line>();
            foreach (int id in itemIds)
            {
                int total = totalOf(id);
                if (total > 0) into.Add(new Line { ItemId = id, Total = total });
                else none.Add(new Line { ItemId = id, Total = 0 });
            }
            into.AddRange(none);
            if (into.Count > left) into.RemoveRange(left, into.Count - left);
            return left - into.Count;
        }

        private static int Length(int[] list) => list == null ? 0 : list.Length;

        private static bool HasTag(BuffIn buff, string tag) => buff.Tags != null && Array.IndexOf(buff.Tags, tag) >= 0;

        private static bool IsDebuff(Dictionary<int, BuffIn> buffById, int id) =>
            buffById.TryGetValue(id, out var buff) && !buff.IsGood;

        // The items come in the order of the item id, so each list stays in that order.
        private static void AddOnce(Dictionary<int, List<int>> map, int debuffId, int itemId)
        {
            if (!map.TryGetValue(debuffId, out var list)) map[debuffId] = list = new List<int>();
            if (list.Count == 0 || list[list.Count - 1] != itemId) list.Add(itemId);
        }
    }
}
