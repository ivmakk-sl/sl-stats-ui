using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SlShared.Json;

namespace StatsUI
{
    // The JSON text that the plugin sends to the page script, built by hand: netstandard2.1 has no
    // System.Text.Json, and the game's own Newtonsoft.Json is not usable from mod code. No game or BepInEx type
    // here. The page side of this shape is PageData in src/Web/page/types.ts.
    public static class PageJson
    {
        // The config switches of the mod, read once at load.
        public sealed class Switches
        {
            public bool WeightReadout;
            public bool TimeStripes;
            public bool ExtendedTooltips;
            // The text of the game at the bottom of the stat tooltip.
            public bool StatTooltipGameText;
        }

        // The JSON of a part that has nothing to show yet.
        public const string EmptyStats = "[]";
        public const string EmptyBuffs = "[]";
        public const string EmptyWeight = "null";

        // The data of setData: the switches, the mod texts, and the JSON of each part (stats, buffs, weight).
        public static string DataJson(Switches switches, string textsJson, string statsJson, string buffsJson, string weightJson)
        {
            var sb = new StringBuilder(256 + statsJson.Length + buffsJson.Length + textsJson.Length);
            sb.Append("{\"switches\":{\"weightReadout\":").Append(Bool(switches.WeightReadout))
                .Append(",\"timeStripes\":").Append(Bool(switches.TimeStripes))
                .Append(",\"extendedTooltips\":").Append(Bool(switches.ExtendedTooltips))
                .Append(",\"statTooltipGameText\":").Append(Bool(switches.StatTooltipGameText))
                .Append("},\"texts\":").Append(textsJson)
                .Append(",\"stats\":").Append(statsJson)
                .Append(",\"buffs\":").Append(buffsJson)
                .Append(",\"weight\":").Append(weightJson)
                .Append('}');
            return sb.ToString();
        }

        // The mod texts of the display language by name.
        public static string TextsJson(IEnumerable<KeyValuePair<string, string>> texts)
        {
            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var pair in texts)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.AppendStr(pair.Key).Append(':').AppendStr(pair.Value);
            }
            return sb.Append('}').ToString();
        }

        // The weight part: the number of the bag button, the fill of the weight bar and the place of its line (parts
        // of the weight limit), and the tone (good, near, over). All weights in grams.
        public static string WeightJson(long carried, long limit, long line)
        {
            var sb = new StringBuilder(80);
            sb.Append("{\"text\":").AppendStr(WeightLogic.Text(carried))
                .Append(",\"fill\":").Append(Frac(WeightLogic.Fill(carried, limit)))
                .Append(",\"line\":").Append(Frac(WeightLogic.Fill(line, limit)))
                .Append(",\"tone\":").AppendStr(WeightLogic.ToneOf(carried, line).ToString().ToLowerInvariant())
                .Append('}');
            return sb.ToString();
        }

        // The ratios of one stat row as the game gives them (parts of 1), and the attribute maximum of its gain ratio.
        public sealed class StatRead
        {
            public double Gain;
            public double Loss;
            public double NaturalGain;
            public double NaturalLoss;
            public double GainCap;
            // The raw loss ratio (before the limits) without the parts of the buffs with an end (BuffsLogic.LossRest),
            // and the limits of the loss ratio, which the page applies to the sum at each loss.
            public double LossRest;
            public double LossMin;
            public double LossMax;
            // The threshold line of the row, on the Satiety row only.
            public StatsLogic.Threshold? Threshold;
            // The gain ratio of each food type that has one, on eat only, with the name of the food type.
            public IReadOnlyList<(string Name, double Ratio)> Food;
        }

        // The stats part: one entry for each stat row in the order of the HUD, with the gain mark (null at x1) and the
        // ratios.
        public static string StatsJson(IReadOnlyList<StatRead> rows)
        {
            var sb = new StringBuilder(128 * rows.Count);
            sb.Append('[');
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (i > 0) sb.Append(',');
                var mark = StatsLogic.GainMark(row.Gain, row.GainCap);
                sb.Append("{\"mark\":");
                if (mark.HasValue)
                    sb.Append("{\"text\":").AppendStr(mark.Value.Text)
                        .Append(",\"tone\":").AppendStr(mark.Value.Tone.ToString().ToLowerInvariant()).Append('}');
                else sb.Append("null");
                sb.Append(",\"gain\":").Append(Frac(row.Gain))
                    .Append(",\"loss\":").Append(Frac(row.Loss))
                    .Append(",\"naturalGain\":").Append(Frac(row.NaturalGain))
                    .Append(",\"naturalLoss\":").Append(Frac(row.NaturalLoss))
                    .Append(",\"lossRest\":").Append(Frac(row.LossRest))
                    .Append(",\"lossMin\":").Append(Frac(row.LossMin))
                    .Append(",\"lossMax\":").Append(Frac(row.LossMax))
                    .Append(",\"threshold\":");
                if (row.Threshold.HasValue)
                    sb.Append("{\"start\":").Append(JsonText.Num(row.Threshold.Value.Start))
                        .Append(",\"end\":").Append(JsonText.Num(row.Threshold.Value.End)).Append('}');
                else sb.Append("null");
                sb.Append(",\"food\":[");
                for (int k = 0; row.Food != null && k < row.Food.Count; k++)
                {
                    if (k > 0) sb.Append(',');
                    sb.Append("{\"name\":").AppendStr(row.Food[k].Name).Append(",\"ratio\":").Append(Frac(row.Food[k].Ratio)).Append('}');
                }
                sb.Append("]}");
            }
            return sb.Append(']').ToString();
        }

        internal static string Bool(bool b) => b ? "true" : "false";

        // A part of 1 with four decimals, enough for a length on the page.
        internal static string Frac(double x) => Math.Round(x, 4).ToString("0.####", CultureInfo.InvariantCulture);

        // The result of a command when the root page has no page script: a new root page, or one that
        // the game built again after a browser crash.
        public const string NoScript = "no script";

        // The push of the data when the page script is already in the root page. now is the game time in game seconds
        // (State_Data_Player.TotalSeconds), the anchor of the page clock; it goes outside the data, so a push that only
        // the clock would change never goes.
        public static string SetDataCommand(string json, int now) =>
            "window.__statsui?window.__statsui.setData(" + json + "," + JsonText.Num(now) + "):'" + NoScript + "'";

        // The check, when the data did not change, that the root page still has the page script and the CoreUI1 frame
        // still has the observer, with the game time for the page clock. It answers "ok <runs> <passes> <micros>" (the
        // observer runs, the apply passes, and the time of the passes in microseconds since the last check), or
        // another text that makes the plugin push again.
        public static string CheckCommand(int now) =>
            "window.__statsui?window.__statsui.check(" + JsonText.Num(now) + "):'" + NoScript + "'";

        // The page script, then the push of the data: sent only when a command gave NoScript.
        public static string SetDataWithScriptCommand(string script, string json, int now) =>
            script + ";window.__statsui.setData(" + json + "," + JsonText.Num(now) + ");";

        // The buffs part: each buff and debuff of the stat rows, in the order of the game's badges, with its row, its
        // end time in game seconds (-1 for no end), its full time (-1 for none), the text of the game, and for a
        // suppressed debuff the mod text suppressedBy filled with the name of the suppressing buff (else null).
        public static string BuffsJson(IReadOnlyList<BuffsLogic.Entry> entries, Func<BuffsLogic.Entry, string> desc,
            Func<BuffsLogic.Entry, IReadOnlyList<(string Label, string Value, bool Bad)>> lines = null, string suppressedBy = "")
        {
            var sb = new StringBuilder(160 * entries.Count + 2);
            sb.Append('[');
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":").Append(JsonText.Num(e.ConfigId))
                    .Append(",\"row\":").Append(JsonText.Num(e.Row))
                    .Append(",\"good\":").Append(Bool(e.IsGood))
                    .Append(",\"layers\":").Append(JsonText.Num(e.Layers))
                    .Append(",\"maxLayers\":").Append(JsonText.Num(e.MaxLayers))
                    .Append(",\"end\":").Append(JsonText.Num(e.EndTime))
                    .Append(",\"full\":").Append(Frac(e.FullTime))
                    .Append(",\"desc\":").AppendStr(desc(e) ?? "")
                    .Append(",\"suppressed\":");
                if (e.SuppressedBy != null) sb.AppendStr(SlShared.I18n.I18nTexts.Fill(suppressedBy ?? "", ("buff", e.SuppressedBy)));
                else sb.Append("null");
                sb.Append(",\"losses\":");
                AppendLosses(sb, e.Losses);
                sb.Append(",\"lines\":[");
                var list = lines?.Invoke(e);
                for (int k = 0; list != null && k < list.Count; k++)
                {
                    if (k > 0) sb.Append(',');
                    sb.Append("{\"label\":").AppendStr(list[k].Label).Append(",\"value\":").AppendStr(list[k].Value)
                        .Append(",\"bad\":").Append(Bool(list[k].Bad)).Append('}');
                }
                sb.Append("]}");
            }
            return sb.Append(']').ToString();
        }

        // The loss of each stat row of a buff, or null.
        private static void AppendLosses(StringBuilder sb, BuffsLogic.Loss[] losses)
        {
            if (losses == null)
            {
                sb.Append("null");
                return;
            }
            sb.Append('[');
            for (int i = 0; i < losses.Length; i++)
            {
                var l = losses[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"each\":").Append(Frac(l.Each))
                    .Append(",\"every\":").Append(Frac(l.Every))
                    .Append(",\"atEnd\":").Append(Frac(l.AtEnd))
                    .Append(",\"ratio\":").Append(Frac(l.Ratio))
                    .Append('}');
            }
            sb.Append(']');
        }
    }
}
