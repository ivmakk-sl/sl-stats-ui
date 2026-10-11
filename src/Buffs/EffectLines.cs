using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SlShared.I18n;

namespace StatsUI
{
    // The effect lines of the tooltip of a buff badge, from the fields of its Config_Effect row by name: the plain
    // fields of the plain trees (PlainTrees), and the IntValue<n> / FloatValue<n> fields of each code tree in the tree
    // table below. A buff of another tree gives no line. No game or BepInEx type here.
    public static class EffectLines
    {
        // The effect trees whose values are the plain Buff* fields of the effect row: their Build methods share the
        // AE_BuffAdd5Attr nodes of Buff/Buff_Default (Interval, Max, AddRotio, DelRatio, Start, End). The symptom trees
        // of Diarrhea and Vomiting add only a forced action (Update_Trigger1 with IntValue0 and FloatValue0), which gives
        // no line (game 1.1.18317).
        public static readonly HashSet<string> PlainTrees = new HashSet<string>
        {
            "Buff/Buff_Default", "Buff/SymptomForcedAction", "Buff/SymptomForcedToiletAction",
        };

        public enum Kind { Interval, Start, End, Gain, Loss, NaturalGain, NaturalLoss, Max, Food, Speed, InteractSpeed, EndBuff }

        // The condition of a line: always, only while the character rests, or only in a crisis.
        public enum When { Always, Resting, Crisis }

        // The stats in the order of the game (AttrName 1-5). Health is the stat that the game shows as Fitness, and
        // Vitality is Life.
        public const int Satiety = 1, Morale = 2, Stamina = 3, Fitness = 4, Life = 5;

        private static readonly string[] StatFields = { "Satiety", "Morale", "Stamina", "Health", "Vitality" };

        // The food types of the game (SubCategory 1-11 of a food).
        private const int FoodTypes = 11;

        public struct Line
        {
            public Kind Kind;
            // The stat of the line, 0 for none.
            public int Stat;
            // The change times the layers: a value for Interval, Start, End, and Max, a part of 1 for Gain, Loss,
            // Food, and Speed.
            public double Amount;
            // The interval of an Interval line in game hours.
            public double Hours;
            // The food type of a Food line, or the config id of the buff of an EndBuff line.
            public int Ref;
            public When When;
        }

        // True when the line is a penalty: it takes a stat or a gain, or it adds to a loss. A smaller loss is no
        // penalty, and the end of a buff into another buff is neither.
        public static bool IsBad(Line line)
        {
            switch (line.Kind)
            {
                case Kind.EndBuff: return false;
                case Kind.Loss:
                case Kind.NaturalLoss: return line.Amount > 0;
                default: return line.Amount < 0;
            }
        }

        // The lines with the penalties last; each group keeps its order.
        public static List<Line> Ordered(IEnumerable<Line> lines)
        {
            var list = lines.ToList();
            return list.Where(l => !IsBad(l)).Concat(list.Where(IsBad)).ToList();
        }

        // The lines of a Buff/Buff_Default effect row in a fixed order: by kind, then by stat. Null fields give none.
        // coldReduce lowers the interval values of a cold debuff, as the game's text (BuildBuffDesc) does.
        public static List<Line> From(IReadOnlyDictionary<string, double> fields, int layers, double coldReduce = 0)
        {
            var lines = new List<Line>();
            if (fields == null) return lines;
            double interval = Field(fields, "BuffInterval");
            if (interval > 0)
                AddStats(lines, fields, Kind.Interval, "Interval", layers * (1 - coldReduce), interval / 3600);
            AddStats(lines, fields, Kind.Start, "Start", layers);
            AddStats(lines, fields, Kind.End, "End", layers);
            AddStats(lines, fields, Kind.Gain, "Add", layers);
            AddStats(lines, fields, Kind.Loss, "Del", layers);
            AddStats(lines, fields, Kind.Max, "Max", layers);
            for (int k = 1; k <= FoodTypes; k++)
                Add(lines, Kind.Food, Satiety, Field(fields, "BuffSatietyFood" + k + "Add_Ratio") * layers, 0, k);
            for (int k = 1; k <= FoodTypes; k++)
                Add(lines, Kind.Food, Morale, Field(fields, "EF_BuffMoraleFood" + k + "Add_Ratio") * layers, 0, k);
            Add(lines, Kind.Speed, 0, Field(fields, "BuffSpeed") * layers, 0, 0);
            foreach (string name in new[] { "BuffEndBuff01", "BuffEndBuff02" })
            {
                int id = (int)Field(fields, name);
                if (id > 0) lines.Add(new Line { Kind = Kind.EndBuff, Ref = id });
            }
            return lines;
        }

        private static void AddStats(List<Line> lines, IReadOnlyDictionary<string, double> fields, Kind kind, string suffix, double layers, double hours = 0)
        {
            for (int i = 0; i < StatFields.Length; i++)
                Add(lines, kind, i + 1, Field(fields, "Buff" + StatFields[i] + suffix) * layers, hours, 0);
        }

        // Adds a line of a nonzero change. A change of Fitness gives no line: the HUD does not show that stat, and the
        // game ignores each change of it (AttributeComponent.CostAttrValue returns at once for attribute 4).
        private static void Add(List<Line> lines, Kind kind, int stat, double amount, double hours, int reference, When when = When.Always)
        {
            if (amount != 0 && stat != Fitness)
                lines.Add(new Line { Kind = kind, Stat = stat, Amount = amount, Hours = hours, Ref = reference, When = when });
        }

        private static double Field(IReadOnlyDictionary<string, double> fields, string name) =>
            fields.TryGetValue(name, out var v) ? v : 0;

        // One node of a code tree: the line kind and stat that the field FloatValue<Index> (or IntValue<Index> for an
        // int node) of the effect row gives.
        private struct Node
        {
            public Kind Kind;
            public int Stat;
            public bool Int;
            public int Index;
            public When When;

            public Node(Kind kind, int stat, int index, bool isInt = false, When when = When.Always)
            {
                Kind = kind; Stat = stat; Index = index; Int = isInt; When = when;
            }
        }

        // The code trees whose fields the mod knows, from the Build method of each tree (game/decompiled/HotUpdate/
        // GameCore.HotUpdate.Battle.Logic/E_Buff_<tree>.c, game 1.1.18293): each AE_AddStrengtheningAttr node names an
        // attribute (NameAttr) and the index of its value field. The attribute ids: 2000N gain ratio (20003
        // StaminaAdd_Ratio, 20004 HealthAdd_Ratio), 3000N loss ratio, 4000N natural gain (40004 HealthAutoAdd_Ratio, 40005
        // VitalityAutoAdd_Ratio), 5000N natural loss, 103 MaxStamina, 105 MaxVitality, 10001 MoveSpeed_Ratio, 10002
        // FurnitureInteractSpeed_Ratio. The rows of Disease-Proof, Athlete, and Sweet Dreams are from game 1.1.18317. An
        // AE_AddCostAttr node changes a stat on the timer of IntValue0 (Action_UpdateTrigger with
        // Action_TestTimeCondition). Buff/BagOverweight has its own branch in FromTree.
        private static readonly Dictionary<string, Node[]> Trees = new Dictionary<string, Node[]>
        {
            // Well-Fed.
            ["Buff/Buff_1300_Satiated"] = new[]
            {
                new Node(Kind.NaturalLoss, Satiety, 0), new Node(Kind.NaturalGain, Morale, 1),
                new Node(Kind.NaturalGain, Stamina, 2), new Node(Kind.InteractSpeed, 0, 3),
            },
            // Energetic, Early Bedtime: two gain ratios behind AE_TestOwnerState 16 (resting, by the text of the buffs).
            ["Buff/Buff_306510"] = new[]
            {
                new Node(Kind.NaturalLoss, Satiety, 2), new Node(Kind.Gain, Morale, 0, when: When.Resting),
                new Node(Kind.Gain, Stamina, 1, when: When.Resting),
            },
            // Fatigued.
            ["Buff/Child_Fatigue"] = new[]
            {
                new Node(Kind.Gain, Satiety, 0), new Node(Kind.Gain, Morale, 1), new Node(Kind.Gain, Fitness, 2), new Node(Kind.Gain, Life, 3),
            },
            // Exhausted.
            ["Buff/Child_ExtremeFatigue"] = new[]
            {
                new Node(Kind.Gain, Satiety, 0), new Node(Kind.Gain, Morale, 1), new Node(Kind.Gain, Fitness, 2), new Node(Kind.Gain, Life, 3),
                new Node(Kind.Interval, Life, 4),
            },
            // Critical.
            ["Buff/Child_Dying"] = new[]
            {
                new Node(Kind.Max, Stamina, 0, isInt: true), new Node(Kind.Gain, Satiety, 0), new Node(Kind.Gain, Morale, 1),
                new Node(Kind.Gain, Stamina, 2), new Node(Kind.Gain, Fitness, 3),
            },
            // Starving.
            ["Buff/Child_ExtremeHunger"] = new[]
            {
                new Node(Kind.Interval, Fitness, 0), new Node(Kind.Interval, Morale, 1), new Node(Kind.Interval, Life, 2),
            },
            // Depressed.
            ["Buff/Child_Dismay"] = new[] { new Node(Kind.Gain, Satiety, 0), new Node(Kind.Gain, Stamina, 1) },
            // Weakened.
            ["Buff/Child_Subhealth"] = new[] { new Node(Kind.Max, Life, 0, isInt: true), new Node(Kind.Max, Stamina, 1, isInt: true) },
            // Overweight: 30001 SatietyDel_Ratio and 10001 MoveSpeed_Ratio. Its AE_AddDependencyValue_Float node (Morale on
            // eat from the Satiety of the food, FloatValue1) gives no line.
            ["Buff/Child_Obesity"] = new[] { new Node(Kind.Loss, Satiety, 2), new Node(Kind.Speed, 0, 0) },
            // Disease-Proof: 40004 and 40005. Its AE_AddImmuneBuffType node (IntValue0) gives no line.
            ["Buff/Buff_306410_DiseaseImmunity"] = new[] { new Node(Kind.NaturalGain, Fitness, 0), new Node(Kind.NaturalGain, Life, 1) },
            // Athlete: 20004, 20003, and 105 MaxVitality. Its node of 305 TargetVitality (IntValue1) gives no line: the
            // effect of a Target attribute is not known.
            ["Buff/Life_Movement"] = new[]
            {
                new Node(Kind.Gain, Fitness, 0), new Node(Kind.Gain, Stamina, 1), new Node(Kind.Max, Life, 0, isInt: true),
            },
            // Sweet Dreams: not an AE_AddStrengtheningAttr node but AE_AddConditionAttr_Float on Battle_BeforeCostAttr,
            // which adds FloatValue0 to 20003 StaminaAdd_Ratio when AE_TestCostActionType finds a Rest action (2).
            ["Buff/QuickRest"] = new[] { new Node(Kind.Gain, Stamina, 0, when: When.Resting) },
        };

        // The tree of Calm in Crisis and Steady Focus: AE_AddAttackerBuff adds the buff IntValue0 in a crisis.
        private const string CrisisTree = "Buff/Buff_305310";

        // The tree of Encumbered and Limited Mobility: AE_AddOverweightPenalty adds FloatValue0 x (1 - the talent ratio
        // of Effortless Strength, clamped to 0-1) to MoveSpeed_Ratio once, with no layer count. Limited Mobility has
        // FloatValue0 -0.5 while its game text says -30%; the line shows the value that the game applies.
        private const string OverweightTree = "Buff/BagOverweight";

        // The lines of a code tree in the order of the kinds, then of the stats. buffEffect gives the plain-tree fields
        // of a buff by its config id, or null. talentRatio is the talent ratio of Effortless Strength
        // (Buff/AE_HeavyCarryEase) of the character. A tree not in the table gives none.
        public static List<Line> FromTree(string tree, IReadOnlyDictionary<string, double> fields, int layers,
            Func<int, IReadOnlyDictionary<string, double>> buffEffect, double talentRatio = 0)
        {
            var lines = new List<Line>();
            if (tree == null || fields == null) return lines;
            if (tree == CrisisTree)
            {
                foreach (var line in From(buffEffect((int)Field(fields, "IntValue0")), layers))
                {
                    var l = line;
                    l.When = When.Crisis;
                    lines.Add(l);
                }
                return lines;
            }
            if (tree == OverweightTree)
            {
                Add(lines, Kind.Speed, 0, Field(fields, "FloatValue0") * (1 - Math.Clamp(talentRatio, 0, 1)), 0, 0);
                return lines;
            }
            if (!Trees.TryGetValue(tree, out var nodes)) return lines;
            double hours = Field(fields, "IntValue0") / 3600;
            foreach (var n in nodes)
            {
                double amount = Field(fields, (n.Int ? "IntValue" : "FloatValue") + n.Index) * layers;
                if (n.Kind == Kind.Interval && hours <= 0) continue;
                Add(lines, n.Kind, n.Stat, amount, n.Kind == Kind.Interval ? hours : 0, 0, n.When);
            }
            Add(lines, Kind.Speed, 0, Field(fields, "BuffSpeed") * layers, 0, 0);
            return lines.OrderBy(l => l.Kind).ThenBy(l => l.Stat).ToList();
        }

        // The names of the mod texts of each stat (AttrName 1-5) and of its maximum.
        private static readonly string[] StatTexts = { "satiety", "morale", "stamina", "fitness", "life" };
        private static readonly string[] MaxTexts = { "maxSatiety", "maxMorale", "maxStamina", "maxFitness", "maxLife" };

        // The label and the value of a line. text gives a mod text by name, foodName the name of a food type, and
        // buffName the name of a buff by its config id, each in the display language.
        public static (string Label, string Value) Text(Line line, Func<string, string> text, Func<int, string> foodName, Func<int, string> buffName)
        {
            var (label, value) = PlainText(line, text, foodName, buffName);
            if (line.When == When.Resting) label = I18nTexts.Fill(text("lineResting"), ("line", label));
            if (line.When == When.Crisis) label = I18nTexts.Fill(text("lineCrisis"), ("line", label));
            return (label, value);
        }

        private static (string Label, string Value) PlainText(Line line, Func<string, string> text, Func<int, string> foodName, Func<int, string> buffName)
        {
            string stat = line.Stat > 0 ? text(StatTexts[line.Stat - 1]) : "";
            switch (line.Kind)
            {
                case Kind.Interval:
                    return line.Hours == 1
                        ? (I18nTexts.Fill(text("lineEachHour"), ("stat", stat)), Signed(line.Amount))
                        : (I18nTexts.Fill(text("lineEvery"), ("stat", stat), ("hours", Number(line.Hours))), Signed(line.Amount));
                case Kind.Start: return (I18nTexts.Fill(text("lineStart"), ("stat", stat)), Signed(line.Amount));
                case Kind.End: return (I18nTexts.Fill(text("lineEnd"), ("stat", stat)), Signed(line.Amount));
                case Kind.Gain: return (I18nTexts.Fill(text("lineGain"), ("stat", stat)), Percent(line.Amount));
                case Kind.Loss: return (I18nTexts.Fill(text("lineLoss"), ("stat", stat)), Percent(line.Amount));
                case Kind.NaturalGain: return (I18nTexts.Fill(text("lineNaturalGain"), ("stat", stat)), Percent(line.Amount));
                case Kind.NaturalLoss: return (I18nTexts.Fill(text("lineNaturalLoss"), ("stat", stat)), Percent(line.Amount));
                case Kind.InteractSpeed: return (text("interactionSpeed"), Percent(line.Amount));
                case Kind.Max: return (text(MaxTexts[line.Stat - 1]), Signed(line.Amount));
                case Kind.Food: return (I18nTexts.Fill(text("lineFood"), ("stat", stat), ("food", foodName(line.Ref))), Percent(line.Amount));
                case Kind.Speed: return (text("movementSpeed"), Percent(line.Amount));
                default: return (text("lineThen"), buffName(line.Ref));
            }
        }

        // A number with at most two decimals and no trailing zero.
        private static string Number(double x) => Math.Round(x, 2).ToString("0.##", CultureInfo.InvariantCulture);

        private static string Signed(double x) => (x > 0 ? "+" : "") + Number(x);

        // A part of 1 as a signed percent: -0.300000012 -> "-30%".
        private static string Percent(double x) => Signed(Math.Round(x * 100, 1)) + "%";
    }
}
