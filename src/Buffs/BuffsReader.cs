using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;

namespace StatsUI
{
    // Reads the buff list of the leading role (State_Data_Buff.LeadingRoleBuffList, the list of the HUD) with the
    // config of each buff into the buffs part of the page data, and the game time that the page clock starts from.
    internal static class BuffsReader
    {
        private static State_Data_Buff BuffState() =>
            ReduxUISystem.Instance?.GetData<State_Data_Buff>(Il2CppType.Of<State_Data_Buff>());

        // The game time in game seconds, from the clock of the game. Not State_Data_Player.TotalSeconds, the copy that
        // the game's buff tooltip reads: it is 0 just after a save load and moves only in steps of game minutes. 0
        // before a save loads.
        internal static int GameNow()
        {
            var time = GameWorld.Current?._GameTimeManager;
            return time != null ? time.GetTotalSeconds() : 0;
        }

        internal static string Json()
        {
            var entries = Entries(out var state, out var configs);
            if (entries == null) return PageJson.EmptyBuffs;
            double heavyCarryEase = HeavyCarryEase();
            if (Plugin.Verbose.Value)
            {
                Plugin.Log.LogDebug(FormattableString.Invariant($"Stats UI talent: heavyCarryEase={heavyCarryEase}"));
                int now = GameNow();
                foreach (var e in entries)
                    Plugin.Log.LogDebug(FormattableString.Invariant($"Stats UI buff: id={e.ConfigId} row={e.Row} layers={e.Layers} end={e.EndTime} full={e.FullTime} now={now} losses={LossesText(e.Losses)}"));
            }
            var texts = ModTexts.Current();
            return PageJson.BuffsJson(entries, e => Desc(configs[e.ConfigId], state), e => Lines(e, texts, heavyCarryEase), texts["suppressedBy"]);
        }

        // The effect tree of Effortless Strength, whose talent ratio lowers the penalty of Buff/BagOverweight
        // (AE_AddOverweightPenalty).
        private const string HeavyCarryEaseTree = "Buff/AE_HeavyCarryEase";

        // The talent ratio of Effortless Strength of the leading role, or 0 when the game cannot give it.
        private static double HeavyCarryEase()
        {
            try
            {
                var role = GameWorld.Current?._AgentManager?.GetLeadingRole();
                var buffs = role != null ? AgentTools.GetAgentComponent<BuffComponent>(role) : null;
                if (buffs != null) return buffs.GetTalentEffectRatio(HeavyCarryEaseTree);
                Plugin.WarnOnce("Stats UI: the buffs of the leading role cannot be read; the Encumbered and Limited Mobility lines show no talent factor.");
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Stats UI: the talent ratio of Effortless Strength cannot be read: " + e.Message);
            }
            return 0;
        }

        // The buff entries of the stat rows with their losses, or null with one warning when the list cannot be read.
        internal static List<BuffsLogic.Entry> Entries(out State_Data_Buff state, out Dictionary<int, Config_Buff> configs)
        {
            state = BuffState();
            configs = new Dictionary<int, Config_Buff>();
            var list = state?.LeadingRoleBuffList;
            var config = ConfigManager.Instance;
            if (list == null || config == null)
            {
                Plugin.WarnOnce("Stats UI: the buff list of the leading role cannot be read; the time stripes show nothing.");
                return null;
            }
            var world = GameWorld.Current;
            long roleId = world?._AgentManager?.GetLeadingRoleId() ?? 0;
            var effects = world?._BTEffectManager;

            var input = new List<BuffsLogic.BuffIn>();
            var it = list.GetEnumerator();
            while (it.MoveNext())
            {
                var buff = it.Current;
                if (buff == null) continue;
                var c = config.Get_Config_Buff(buff.BuffConfigId);
                if (c != null) configs[buff.BuffConfigId] = c;
                else Plugin.WarnOnce("Stats UI: a buff of the leading role has no config row; it is left out of the stat rows.");
                var row = c != null ? EffectRow(config, c.EffectConfigId) : (null, null);
                input.Add(new BuffsLogic.BuffIn
                {
                    ConfigId = buff.BuffConfigId,
                    Layers = buff.LayerCount,
                    MaxLayers = c != null ? c.MaxLayerCount : 0,
                    EndTime = buff.TimeEndTime,
                    Found = c != null,
                    Hidden = c != null && c.IsHide,
                    IsGood = c != null && c.IsGood,
                    DisplayType = c != null ? c.BuffDisplayType : 0,
                    During = c != null ? c.BuffDuring : -1,
                    DuringFix = c != null ? DuringFactor(() => effects.TriggerBuffDuringFix(roleId, c)) : 0,
                    DuringAdd = c != null ? DuringFactor(() => effects.TriggerBuffDuringAdd(roleId, c)) : 0,
                    Tree = row.Tree,
                    Fields = row.Fields,
                    Cold = c != null && c.TagList != null && c.TagList.Contains(ColdTag),
                });
            }
            MarkSuppressed(input, configs);
            return BuffsLogic.Entries(input, StatsLogic.Rows.Count, ColdReduce(state));
        }

        // The tag of a cold debuff, whose interval values the game lowers (BuildBuffDesc).
        private const string ColdTag = "ColdDebuff";
        // The tags of Config_Buff.TagList that the game's suppression reads (BuffComponent.IsSuppressibleDebuff,
        // GetDebuffSuppressorCount): a debuff that a suppressor stops, and a buff that stops them (Analgesia).
        private const string SuppressibleTag = "SuppressibleDebuff";
        private const string SuppressorTag = "DebuffSuppressor";

        private static bool HasTag(Dictionary<int, Config_Buff> configs, int id, string tag) =>
            configs.TryGetValue(id, out var c) && c.TagList != null && c.TagList.Contains(tag);

        // Marks each suppressible debuff that the game suppresses now, with the name of the active suppressor. When the
        // game cannot say, each debuff stays not suppressed, with one warning.
        private static void MarkSuppressed(List<BuffsLogic.BuffIn> input, Dictionary<int, Config_Buff> configs)
        {
            var suppressor = input.FirstOrDefault(b => HasTag(configs, b.ConfigId, SuppressorTag));
            if (suppressor == null || !input.Any(b => HasTag(configs, b.ConfigId, SuppressibleTag))) return;
            try
            {
                var role = GameWorld.Current?._AgentManager?.GetLeadingRole();
                var buffs = role != null ? AgentTools.GetAgentComponent<BuffComponent>(role) : null;
                if (buffs == null)
                {
                    Plugin.WarnOnce("Stats UI: the buffs of the leading role cannot be read; a suppressed debuff shows its losses.");
                    return;
                }
                string name = BuffName(suppressor.ConfigId);
                foreach (var b in input)
                {
                    if (!HasTag(configs, b.ConfigId, SuppressibleTag) || !ReadSuppressed(buffs, b.ConfigId)) continue;
                    b.Suppressed = true;
                    b.SuppressedBy = name;
                    if (Plugin.Verbose.Value)
                        Plugin.Log.LogDebug(FormattableString.Invariant($"Stats UI buffs: suppressed {b.ConfigId} by {suppressor.ConfigId}"));
                }
            }
            catch (Exception e)
            {
                foreach (var b in input) { b.Suppressed = false; b.SuppressedBy = null; }
                Plugin.WarnOnce("Stats UI: the suppression of a debuff cannot be read (" + e.Message + "); a suppressed debuff shows its losses.");
            }
        }

        // A member that game 1.1.18599 added: in its own method, so that a game without it fails only this read.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadSuppressed(BuffComponent buffs, int configId) => buffs.IsBuffSuppressed(configId);

        // The number fields of Config_Effect that an effect row can use: Buff*, EF_Buff*, IntValue*, FloatValue*.
        private static PropertyInfo[] effectProps;

        // The tree and the number fields of each effect row by id. The config does not change while the game runs.
        private static readonly Dictionary<int, (string Tree, IReadOnlyDictionary<string, double> Fields)> effectCache =
            new Dictionary<int, (string, IReadOnlyDictionary<string, double>)>();

        // The tree and the nonzero number fields of an effect row by name, or (null, null) for no row.
        private static (string Tree, IReadOnlyDictionary<string, double> Fields) EffectRow(ConfigManager config, int effectId)
        {
            if (effectCache.TryGetValue(effectId, out var cached)) return cached;
            (string, IReadOnlyDictionary<string, double>) row = (null, null);
            try
            {
                var effect = config.Get_Config_Effect(effectId);
                if (effect != null)
                {
                    effectProps ??= typeof(Config_Effect).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p => (p.Name.StartsWith("Buff") || p.Name.StartsWith("EF_Buff") || p.Name.StartsWith("IntValue") || p.Name.StartsWith("FloatValue"))
                            && (p.PropertyType == typeof(float) || p.PropertyType == typeof(int)))
                        .ToArray();
                    var fields = new Dictionary<string, double>();
                    foreach (var p in effectProps)
                    {
                        double v = Convert.ToDouble(p.GetValue(effect));
                        if (v != 0) fields[p.Name] = v;
                    }
                    row = (effect.ConfigName, fields);
                }
                else Plugin.WarnOnce("Stats UI: the config of a buff names no effect row; its coming loss and its effect lines are left out.");
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Stats UI: the effect row of a buff cannot be read (" + e.Message + "); its coming loss and its effect lines are left out.");
            }
            effectCache[effectId] = row;
            return row;
        }

        // The fields of the effect row of a buff by its config id when the row uses a plain tree (EffectLines.PlainTrees),
        // or null.
        private static IReadOnlyDictionary<string, double> DefaultFieldsOf(int buffId)
        {
            var config = ConfigManager.Instance;
            var c = config?.Get_Config_Buff(buffId);
            if (c == null) return null;
            var row = EffectRow(config, c.EffectConfigId);
            return row.Tree != null && EffectLines.PlainTrees.Contains(row.Tree) ? row.Fields : null;
        }

        // The effect lines of an entry as label and value in the display language, the penalties last and marked.
        private static IReadOnlyList<(string Label, string Value, bool Bad)> Lines(BuffsLogic.Entry e, SlShared.I18n.I18nTextSet texts, double heavyCarryEase)
        {
            var result = new List<(string, string, bool)>();
            var lines = e.Tree != null ? EffectLines.FromTree(e.Tree, e.TreeFields, e.Layers, DefaultFieldsOf, heavyCarryEase) : EffectLines.From(e.Effect, e.Layers, e.ColdReduce);
            foreach (var line in EffectLines.Ordered(lines))
            {
                var (label, value) = EffectLines.Text(line, name => texts[name], FoodName, BuffName);
                result.Add((label, value, EffectLines.IsBad(line)));
            }
            return result;
        }

        // The name of a food type in the display language: FoodType_Name_<k> is a localization key of the config.
        internal static string FoodName(int k) => ConfigManager.Instance?.GetLocalTxt("FoodType_Name_" + k) ?? "";

        // The name of a buff in the display language, or its id when the config has no row.
        private static string BuffName(int id)
        {
            var config = ConfigManager.Instance;
            var c = config?.Get_Config_Buff(id);
            return c != null ? config.GetLocalTxt(c.Name) : id.ToString();
        }

        // The game's reduction of the interval values of a cold debuff (Cold-Resistant Constitution), or 0.
        private static double ColdReduce(State_Data_Buff state)
        {
            try { return Reducer_Web_CoreUI1.GetColdDebuffReduceRatioFromBuffList(state); }
            catch (Exception e)
            {
                Plugin.WarnOnce("Stats UI: the cold reduction cannot be read: " + e.Message);
                return 0;
            }
        }

        // The losses of a buff for the Verbose log: each row as each/every/atEnd/ratio, or "none".
        private static string LossesText(BuffsLogic.Loss[] losses)
        {
            if (losses == null) return "none";
            var parts = new string[losses.Length];
            for (int i = 0; i < losses.Length; i++)
                parts[i] = FormattableString.Invariant($"{losses[i].Each}/{losses[i].Every}/{losses[i].AtEnd}/{losses[i].Ratio}");
            return string.Join(" ", parts);
        }

        // A duration factor of the game for a buff that the leading role gets, or 0 when the game cannot give it: the
        // stripe then counts from BuffDuring, and a time left above the full time shows a full stripe.
        private static double DuringFactor(Func<float> read)
        {
            try { return read(); }
            catch (Exception e)
            {
                Plugin.WarnOnce("Stats UI: a duration factor of a buff cannot be read: " + e.Message);
                return 0;
            }
        }

        // The text of the buff as the game's own buff tooltip shows it, with the values of a cold debuff filled in.
        private static string Desc(Config_Buff c, State_Data_Buff state)
        {
            try { return Reducer_Web_CoreUI1.BuildBuffDesc(c, state) ?? ""; }
            catch (Exception e)
            {
                Plugin.WarnOnce("Stats UI: the text of a buff cannot be read: " + e.Message);
                return "";
            }
        }
    }
}
