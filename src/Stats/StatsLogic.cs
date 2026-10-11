using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StatsUI
{
    // The gain mark of a stat bar and the ratio ids of each stat row. A ratio is the value of an attribute of the
    // game as a part of 1 (GetTotalValue_Float: per mille / 1000): 0.5 is +50%, so the multiplier is 1 + ratio. No
    // game or BepInEx type here.
    public static class StatsLogic
    {
        public enum Tone { Good, Bad, Cap }

        public readonly struct Mark
        {
            public readonly string Text;
            public readonly Tone Tone;

            public Mark(string text, Tone tone) { Text = text; Tone = tone; }
        }

        // The attribute ids of one stat row, from the AttrName enum of the game (game/api/HotUpdate/GameCore.HotUpdate/
        // AttrName.cs): the gain ratio <Stat>Add_Ratio 2000N of each gain, the loss ratio <Stat>Del_Ratio 3000N of each
        // loss, the natural gain <Stat>AutoAdd_Ratio 4000N and the natural loss <Stat>AutoDel_Ratio 5000N of the natural
        // change only, and the gain ratio of each food type k (SatietyFood<k>Add_Ratio 60000+k, MoraleFood<k>Add_Ratio
        // 70000+k), on eat only.
        public sealed class Row
        {
            public int Stat;
            public int Gain => 20000 + Stat;
            public int Loss => 30000 + Stat;
            public int NaturalGain => 40000 + Stat;
            public int NaturalLoss => 50000 + Stat;
            public IReadOnlyList<int> Food;
        }

        // The food types of the game (SubCategory 1-11 of a food).
        private const int FoodTypes = 11;

        // The stat rows of the HUD in the order of the game: Satiety, Morale, Stamina, and Life, which is the stat
        // Vitality.
        public static readonly IReadOnlyList<Row> Rows = new[]
        {
            new Row { Stat = 1, Food = Enumerable.Range(60001, FoodTypes).ToArray() },
            new Row { Stat = 2, Food = Enumerable.Range(70001, FoodTypes).ToArray() },
            new Row { Stat = 3, Food = new int[0] },
            new Row { Stat = 5, Food = new int[0] },
        };

        // A multiplier this near to x1 shows no mark.
        private const double NoMarkWithin = 0.005;

        // The gain mark of a gain ratio, or null at x1. cap is the attribute maximum of the gain ratio: a ratio at it
        // shows x3 with the cap tone, which the stat tooltip shows as MAX.
        public static Mark? GainMark(double ratio, double cap)
        {
            if (ratio >= cap - 0.0005) return new Mark("x" + Format(1 + cap), Tone.Cap);
            if (Math.Abs(ratio) < NoMarkWithin) return null;
            return new Mark(MultiplierText(ratio), ratio > 0 ? Tone.Good : Tone.Bad);
        }

        // The Satiety values where Well-Fed starts and where it ends.
        public readonly struct Threshold
        {
            public readonly int Start;
            public readonly int End;

            public Threshold(int start, int end) { Start = start; End = end; }
        }

        // The threshold from IntValue0 and up of the effect row of the passive Satiation (Buff/Passive_Satiated):
        // IntValue1 is the start of Well-Fed, IntValue3 its end. Null for no row or no start value.
        public static Threshold? WellFedThreshold(IReadOnlyList<int> intValues)
        {
            if (intValues == null || intValues.Count < 4 || intValues[1] <= 0) return null;
            return new Threshold(intValues[1], intValues[3]);
        }

        // A loss ratio as parts of 1: the raw sum of its parts (base, strengthening, condition, per mille) and the limits
        // of the attribute. The game limits the sum at each loss, so a buff that ends can bring a ratio back inside.
        public static (double Raw, double Min, double Max) LossRatio(int baseValue, int strengthening, int condition, int min, int max) =>
            ((baseValue + strengthening + condition) / 1000.0, min / 1000.0, max / 1000.0);

        // A stat with no loss ratio entry: the limits of AttributeStandard 30001-30005, which the entry gets when a buff
        // first sets it.
        public static readonly (double Raw, double Min, double Max) NoLossRatio = (0, -0.5, 2);

        // The multiplier of a ratio with at most two decimals and no trailing zero: -0.5 -> "x0.5".
        public static string MultiplierText(double ratio) => "x" + Format(1 + ratio);

        private static string Format(double x) => Math.Round(x, 2).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
