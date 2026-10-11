using System;
using System.Globalization;

namespace StatsUI
{
    // The weight readout of the bag button from the carried weight, the weight limit, and the Encumbered line, all in
    // grams. No game or BepInEx type here.
    public static class WeightLogic
    {
        public enum Tone { Good, Near, Over }

        // Near the Encumbered line starts at this part of it.
        private const double NearFrom = 0.85;

        // Kilograms with one decimal, rounded up as the storage window rounds the carried weight (fmtWeightUp of
        // BackpackUI.html), with no unit: 8601 -> "8.7".
        public static string Text(long grams)
        {
            long tenths = (Math.Max(0, grams) + 99) / 100;
            return (tenths / 10).ToString(CultureInfo.InvariantCulture) + "." + (tenths % 10).ToString(CultureInfo.InvariantCulture);
        }

        // A weight as a part of the weight limit, within 0 and 1: the fill of the weight bar, and the place of the
        // Encumbered line on it.
        public static double Fill(long grams, long limit)
        {
            if (limit <= 0) return 0;
            return Math.Min(1, Math.Max(0, (double)grams / limit));
        }

        // Green below 85% of the Encumbered line, amber from there up to the line, red above it.
        public static Tone ToneOf(long carried, long line)
        {
            if (carried > line) return Tone.Over;
            if (carried >= line * NearFrom) return Tone.Near;
            return Tone.Good;
        }
    }
}
