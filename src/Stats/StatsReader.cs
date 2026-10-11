using System.Collections.Generic;
using System.Text;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;

namespace StatsUI
{
    // Reads the ratios of each stat row from the leading role's attributes into the stats part of the page data.
    internal static class StatsReader
    {
        // The attribute ids from this one up are ratios (MoveSpeed_Ratio 10001 and above).
        private const int FirstRatioId = 10000;

        private static string lastDump;

        private const int SatietyStat = 1;

        // The passive Satiation, whose effect row holds the Satiety values where Well-Fed starts and ends.
        private const int SatiationPassive = 10009;

        // The threshold of Well-Fed from the config of the game, or null with one warning when the config has no row.
        private static StatsLogic.Threshold? WellFedThreshold()
        {
            StatsLogic.Threshold? threshold = null;
            try
            {
                var config = ConfigManager.Instance;
                var buff = config?.Get_Config_Buff(SatiationPassive);
                var effect = buff != null ? config.Get_Config_Effect(buff.EffectConfigId) : null;
                if (effect != null)
                    threshold = StatsLogic.WellFedThreshold(new[] { effect.IntValue0, effect.IntValue1, effect.IntValue2, effect.IntValue3 });
            }
            catch (System.Exception e)
            {
                Plugin.WarnOnce("Stats UI: the config of Well-Fed cannot be read (" + e.Message + "); the threshold line shows nothing.");
                return null;
            }
            if (threshold == null)
                Plugin.WarnOnce("Stats UI: the config of Well-Fed has no threshold; the threshold line shows nothing.");
            return threshold;
        }

        internal static string Json()
        {
            var role = GameWorld.Current?._AgentManager?.GetLeadingRole();
            var attrs = role != null ? AgentTools.GetAgentComponent<AttributeComponent>(role) : null;
            var dict = attrs?.AttrDict;
            if (dict == null)
            {
                Plugin.WarnOnce("Stats UI: the attributes of the leading role cannot be read; the gain marks show nothing.");
                return PageJson.EmptyStats;
            }
            // The parts of the loss ratios that end with a buff; with no buff list, the whole ratio is the rest.
            var entries = BuffsReader.Entries(out _, out _) ?? new List<BuffsLogic.Entry>();
            var rows = new List<PageJson.StatRead>();
            for (int i = 0; i < StatsLogic.Rows.Count; i++)
            {
                var row = StatsLogic.Rows[i];
                var lossRatio = RawLoss(dict, row.Loss);
                rows.Add(new PageJson.StatRead
                {
                    Gain = Ratio(attrs, dict, row.Gain),
                    Loss = Ratio(attrs, dict, row.Loss),
                    LossRest = BuffsLogic.LossRest(lossRatio.Raw, entries, i),
                    LossMin = lossRatio.Min,
                    LossMax = lossRatio.Max,
                    NaturalGain = Ratio(attrs, dict, row.NaturalGain),
                    NaturalLoss = Ratio(attrs, dict, row.NaturalLoss),
                    GainCap = Cap(dict, row.Gain),
                    Threshold = row.Stat == SatietyStat ? WellFedThreshold() : null,
                    Food = FoodRatios(attrs, dict, row),
                });
            }
            if (Plugin.Verbose.Value)
            {
                // A stat sync comes with each natural change of a stat, so only a new set of ratios logs.
                string dump = DumpRatios(dict);
                if (dump != lastDump) Plugin.Log.LogDebug("Stats UI stats: ratios " + dump);
                lastDump = dump;
            }
            return PageJson.StatsJson(rows);
        }

        // The total of a ratio as a part of 1: (base + strengthening) within the limits of the attribute, / 1000.
        // GetTotalValue_Float and not GetTotalValue, which gives a ValueTuple that the interop reads wrongly. A ratio
        // that the character does not have is 0.
        private static double Ratio(AttributeComponent attrs, Il2CppSystem.Collections.Generic.Dictionary<AttrName, Attr> dict, int id)
        {
            var name = (AttrName)id;
            return dict.ContainsKey(name) ? attrs.GetTotalValue_Float(name) : 0;
        }

        // A loss ratio before the limits of its attribute, with those limits: GetTotalValue_Float gives the limited
        // value, and the parts of the buffs that the coming loss takes away are raw.
        private static (double Raw, double Min, double Max) RawLoss(Il2CppSystem.Collections.Generic.Dictionary<AttrName, Attr> dict, int id)
        {
            var name = (AttrName)id;
            if (!dict.ContainsKey(name)) return StatsLogic.NoLossRatio;
            var attr = dict[name];
            return StatsLogic.LossRatio(attr.BaseValue, attr.StrengtheningValue, attr.ConditionValue, attr.Min, attr.Max);
        }

        // The gain ratio of each food type of a row that is not zero, with the name of the food type in the display
        // language: FoodType_Name_<k> is a localization key of the config (GetLocalTxt), not a constant text. The ids
        // of a row are in the order of the food types 1-11.
        private static List<(string, double)> FoodRatios(AttributeComponent attrs, Il2CppSystem.Collections.Generic.Dictionary<AttrName, Attr> dict, StatsLogic.Row row)
        {
            var food = new List<(string, double)>();
            for (int k = 0; k < row.Food.Count; k++)
            {
                double ratio = Ratio(attrs, dict, row.Food[k]);
                if (ratio != 0) food.Add((BuffsReader.FoodName(k + 1), ratio));
            }
            return food;
        }

        // The attribute maximum of a gain ratio as a part of 1. A ratio that the character does not have has no cap.
        private static double Cap(Il2CppSystem.Collections.Generic.Dictionary<AttrName, Attr> dict, int id)
        {
            var name = (AttrName)id;
            return dict.ContainsKey(name) ? dict[name].Max / 1000.0 : double.PositiveInfinity;
        }

        // Each ratio entry with a value that is not zero, as id=base/strengthening/condition, so the log shows which
        // ids the buffs set.
        private static string DumpRatios(Il2CppSystem.Collections.Generic.Dictionary<AttrName, Attr> dict)
        {
            var sb = new StringBuilder();
            var it = dict.GetEnumerator();
            while (it.MoveNext())
            {
                int id = (int)it.Current.Key;
                var attr = it.Current.Value;
                if (id < FirstRatioId || attr == null) continue;
                if (attr.BaseValue == 0 && attr.StrengtheningValue == 0 && attr.ConditionValue == 0) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(id).Append('=').Append(attr.BaseValue).Append('/').Append(attr.StrengtheningValue)
                    .Append('/').Append(attr.ConditionValue).Append(" max ").Append(attr.Max);
            }
            return sb.Length > 0 ? sb.ToString() : "none";
        }
    }
}
