using System;
using System.Collections.Generic;

namespace StatsUI
{
    // The buff entries of the stat rows from the buff list of the leading role, by the rule of the game's HUD reducer
    // (Reducer_Web_CoreUI1, the build of buffsJson): a buff with a config, not hidden, and with a stat row goes into
    // that row, in the order of the list. So the n-th entry of a config id in a row is its n-th badge there. No game or
    // BepInEx type here.
    public static class BuffsLogic
    {
        // One buff of State_Data_Buff.LeadingRoleBuffList with the fields of its Config_Buff.
        public sealed class BuffIn
        {
            public int ConfigId;
            public int Layers;
            // MaxLayerCount of the config: the cap of the layers.
            public int MaxLayers;
            // An absolute game time in game seconds, -1 for no end.
            public int EndTime;
            // False when the game has no Config_Buff for the id.
            public bool Found;
            public bool Hidden;
            public bool IsGood;
            public int DisplayType;
            // BuffDuring of the config in game seconds, -1 for no end, and the duration factors of the game.
            public double During;
            public double DuringFix;
            public double DuringAdd;
            // True for a buff with the tag ColdDebuff.
            public bool Cold;
            // The tree of its Config_Effect row (ConfigName) and the number fields of that row by name, or null for no row.
            public string Tree;
            public IReadOnlyDictionary<string, double> Fields;
            // True for a suppressed debuff (BuffComponent.IsBuffSuppressed): the game stops its effects while a buff with
            // the tag DebuffSuppressor is active. SuppressedBy is the game's name of that buff, or null.
            public bool Suppressed;
            public string SuppressedBy;
        }

        // The loss of one stat from one buff, as positive amounts: the loss of each interval, the interval in game
        // seconds (0 for none), and the loss at its end; and its part of the loss ratio of the stat (Buff<Stat>Del,
        // negative for less loss).
        public struct Loss
        {
            public double Each;
            public double Every;
            public double AtEnd;
            public double Ratio;
        }

        public sealed class Entry
        {
            public int ConfigId;
            public int Row;
            public bool IsGood;
            public int Layers;
            public int MaxLayers;
            public int EndTime;
            // The full time in game seconds, -1 for none.
            public double FullTime;
            // The loss of each stat row, or null when the mod cannot read the losses of the buff or the game suppresses it.
            public Loss[] Losses;
            // The game's reduction of the interval values of a cold debuff, 0 for another buff.
            public double ColdReduce;
            // The fields of its effect row when the row uses a plain tree (EffectLines.PlainTrees), for the losses and
            // the effect lines; else null.
            public IReadOnlyDictionary<string, double> Effect;
            // The code tree of its effect row and the fields of that row, for the effect lines; else null.
            public string Tree;
            public IReadOnlyDictionary<string, double> TreeFields;
            // The game's name of the buff that suppresses this debuff ("" when the mod finds no name), or null when it is
            // not suppressed. A suppressed debuff has no losses and no effect lines: the game applies none of its effects.
            public string SuppressedBy;
        }

        // The stat of each stat row in the names of the Config_Effect fields: Life is the stat Vitality.
        private static readonly string[] StatFields = { "Satiety", "Morale", "Stamina", "Vitality" };

        // The losses of each stat row from the fields of a Buff/Buff_Default effect row, times the layers. A gain
        // gives no loss. coldReduce is the game's reduction of the interval values of a cold debuff
        // (GetColdDebuffReduceRatioFromBuffList), as BuildBuffDesc applies it. Null for no fields.
        public static Loss[] Losses(IReadOnlyDictionary<string, double> fields, int layers, double coldReduce)
        {
            if (fields == null) return null;
            double every = Field(fields, "BuffInterval");
            var losses = new Loss[StatFields.Length];
            for (int i = 0; i < StatFields.Length; i++)
            {
                string stat = StatFields[i];
                double each = every > 0 ? -Field(fields, "Buff" + stat + "Interval") * (1 - coldReduce) : 0;
                losses[i] = new Loss
                {
                    Each = Math.Max(0, each) * layers,
                    Every = every > 0 ? every : 0,
                    AtEnd = Math.Max(0, -Field(fields, "Buff" + stat + "End")) * layers,
                    Ratio = Field(fields, "Buff" + stat + "Del") * layers,
                };
            }
            return losses;
        }

        private static double Field(IReadOnlyDictionary<string, double> fields, string name) =>
            fields.TryGetValue(name, out var v) ? v : 0;

        // The loss ratio of a stat row without the parts of the buffs with an end: the page applies each such part
        // only before the end of its buff. total is the raw ratio of the stat (3000N), before the limits of the attribute.
        public static double LossRest(double total, IEnumerable<Entry> entries, int row)
        {
            double rest = total;
            foreach (var e in entries)
                if (e.EndTime >= 0 && e.Losses != null) rest -= e.Losses[row].Ratio;
            return rest;
        }

        // The stat row of a BuffDisplayType: 1 Satiety, 2 Morale, 3 Stamina, 4 and 5 Life; -1 for none.
        public static int RowOf(int displayType, int rowCount)
        {
            int row = displayType - (displayType >= 5 ? 2 : 1);
            return row >= 0 && row < rowCount ? row : -1;
        }

        // The full time as the game computes the duration of a new buff (BuffComponent): BuffDuring x (1 + the
        // duration fix) + the duration add, or -1 for a buff with no end.
        public static double FullTime(double during, double fix, double add)
        {
            if (during == -1) return -1;
            return during * (1 + fix) + add;
        }

        public static List<Entry> Entries(IEnumerable<BuffIn> list, int rowCount, double coldReduce = 0)
        {
            var entries = new List<Entry>();
            foreach (var buff in list)
            {
                if (!buff.Found || buff.Hidden) continue;
                int row = RowOf(buff.DisplayType, rowCount);
                if (row < 0) continue;
                double full = FullTime(buff.During, buff.DuringFix, buff.DuringAdd);
                double cold = buff.Cold ? coldReduce : 0;
                bool plain = buff.Tree != null && EffectLines.PlainTrees.Contains(buff.Tree);
                var effect = plain && !buff.Suppressed ? buff.Fields : null;
                bool tree = !plain && !buff.Suppressed;
                entries.Add(new Entry
                {
                    ConfigId = buff.ConfigId,
                    Row = row,
                    IsGood = buff.IsGood,
                    Layers = buff.Layers,
                    MaxLayers = buff.MaxLayers,
                    // A buff with no full time has no end: the game gives such a buff (Comfortable, Well-Fed) again
                    // each game hour, with an end time of that hour.
                    EndTime = full < 0 ? -1 : buff.EndTime,
                    FullTime = full,
                    Losses = Losses(effect, buff.Layers, cold),
                    ColdReduce = cold,
                    Effect = effect,
                    Tree = tree ? buff.Tree : null,
                    TreeFields = tree ? buff.Fields : null,
                    SuppressedBy = buff.Suppressed ? buff.SuppressedBy ?? "" : null,
                });
            }
            return entries;
        }
    }
}
