using System;
using System.Collections.Generic;
using System.Linq;
using StatsUI;
using Xunit;

// The effect lines of a buff from the fields of its Buff/Buff_Default effect row (game 1.1.18293,
// game/config/tables/Effect.tsv).
public class EffectLinesTests
{
    private static List<EffectLines.Line> Lines(Dictionary<string, double> fields, int layers = 1) =>
        EffectLines.From(fields, layers);

    [Fact]
    public void A_change_each_interval_gives_one_line_for_each_stat_in_the_order_of_the_stats()
    {
        // Stimulants.
        var lines = Lines(new Dictionary<string, double>
        {
            ["BuffInterval"] = 3600, ["BuffVitalityInterval"] = -3, ["BuffStaminaInterval"] = 5,
        });

        Assert.Equal(2, lines.Count);
        Assert.Equal((EffectLines.Kind.Interval, EffectLines.Stamina, 5.0, 1.0), (lines[0].Kind, lines[0].Stat, lines[0].Amount, lines[0].Hours));
        Assert.Equal((EffectLines.Kind.Interval, EffectLines.Life, -3.0, 1.0), (lines[1].Kind, lines[1].Stat, lines[1].Amount, lines[1].Hours));
    }

    [Fact]
    public void The_interval_is_in_game_hours_and_an_interval_value_with_no_interval_gives_no_line()
    {
        var bleeding = Lines(new Dictionary<string, double> { ["BuffInterval"] = 7200, ["BuffVitalityInterval"] = -3 });
        var none = Lines(new Dictionary<string, double> { ["BuffVitalityInterval"] = -3 });

        Assert.Equal(2.0, bleeding[0].Hours);
        Assert.Empty(none);
    }

    [Fact]
    public void The_interval_values_of_a_cold_debuff_are_lowered_by_the_cold_reduction_as_the_game_text_is()
    {
        var lines = EffectLines.From(new Dictionary<string, double>
        {
            ["BuffInterval"] = 3600, ["BuffVitalityInterval"] = -4, ["BuffVitalityEnd"] = -20,
        }, 1, 0.25);

        Assert.Equal(-3.0, lines[0].Amount);
        Assert.Equal(-20.0, lines[1].Amount);
    }

    [Fact]
    public void A_cold_debuff_entry_keeps_its_cold_reduction_for_the_effect_lines()
    {
        var input = new[]
        {
            new BuffsLogic.BuffIn { ConfigId = 1, Found = true, DisplayType = 4, During = 3600, Cold = true },
            new BuffsLogic.BuffIn { ConfigId = 2, Found = true, DisplayType = 4, During = 3600 },
        };

        var entries = BuffsLogic.Entries(input, 4, 0.25);

        Assert.Equal(0.25, entries[0].ColdReduce);
        Assert.Equal(0.0, entries[1].ColdReduce);
    }

    [Fact]
    public void A_change_at_the_start_and_at_the_end_each_give_a_line()
    {
        var lines = Lines(new Dictionary<string, double>
        {
            ["BuffMoraleStart"] = 10, ["BuffStaminaEnd"] = -100, ["BuffVitalityEnd"] = -20,
        });

        Assert.Equal(new[] { EffectLines.Kind.Start, EffectLines.Kind.End, EffectLines.Kind.End }, lines.Select(l => l.Kind));
        Assert.Equal(new[] { EffectLines.Morale, EffectLines.Stamina, EffectLines.Life }, lines.Select(l => l.Stat));
        Assert.Equal(new[] { 10.0, -100.0, -20.0 }, lines.Select(l => l.Amount));
    }

    [Fact]
    public void A_gain_ratio_and_a_loss_ratio_give_their_lines_as_parts_of_one()
    {
        var lines = Lines(new Dictionary<string, double> { ["BuffMoraleAdd"] = 0.5, ["BuffSatietyDel"] = -0.5 });

        Assert.Equal((EffectLines.Kind.Gain, EffectLines.Morale, 0.5), (lines[0].Kind, lines[0].Stat, lines[0].Amount));
        Assert.Equal((EffectLines.Kind.Loss, EffectLines.Satiety, -0.5), (lines[1].Kind, lines[1].Stat, lines[1].Amount));
    }

    [Fact]
    public void A_change_of_a_maximum_gives_a_line()
    {
        var line = Assert.Single(Lines(new Dictionary<string, double> { ["BuffStaminaMax"] = -10 }));

        Assert.Equal((EffectLines.Kind.Max, EffectLines.Stamina, -10.0), (line.Kind, line.Stat, line.Amount));
    }

    [Fact]
    public void A_food_type_ratio_gives_a_line_with_its_food_type()
    {
        // Master Butcher: Meat and Fish.
        var lines = Lines(new Dictionary<string, double> { ["BuffSatietyFood4Add_Ratio"] = 0.5, ["BuffSatietyFood2Add_Ratio"] = 0.5 });

        Assert.Equal(new[] { 2, 4 }, lines.Select(l => l.Ref));
        Assert.All(lines, l => Assert.Equal((EffectLines.Kind.Food, EffectLines.Satiety, 0.5), (l.Kind, l.Stat, l.Amount)));
    }

    [Fact]
    public void The_movement_speed_and_an_end_buff_give_their_lines()
    {
        var lines = Lines(new Dictionary<string, double> { ["BuffEndBuff01"] = 2163, ["BuffSpeed"] = -0.2 });

        Assert.Equal((EffectLines.Kind.Speed, -0.2), (lines[0].Kind, lines[0].Amount));
        Assert.Equal((EffectLines.Kind.EndBuff, 2163), (lines[1].Kind, lines[1].Ref));
    }

    [Fact]
    public void The_layers_multiply_each_amount()
    {
        // Floral Fragrance with 16 layers.
        var lines = Lines(new Dictionary<string, double> { ["BuffInterval"] = 3600, ["BuffMoraleInterval"] = 0.05 }, 16);

        Assert.Equal(0.8, lines[0].Amount, 6);
    }

    [Fact]
    public void Zero_fields_and_no_fields_give_no_line()
    {
        Assert.Empty(Lines(new Dictionary<string, double> { ["BuffInterval"] = 3600, ["BuffMoraleAdd"] = 0 }));
        Assert.Empty(EffectLines.From(null, 1));
    }

    [Fact]
    public void The_order_of_the_kinds_is_fixed()
    {
        var lines = Lines(new Dictionary<string, double>
        {
            ["BuffEndBuff01"] = 2163, ["BuffSpeed"] = 0.2, ["BuffSatietyFood1Add_Ratio"] = -0.5, ["BuffMoraleMax"] = 2,
            ["BuffStaminaDel"] = -0.3, ["BuffMoraleAdd"] = 0.5, ["BuffVitalityEnd"] = -20, ["BuffMoraleStart"] = 10,
            ["BuffInterval"] = 3600, ["BuffMoraleInterval"] = 2,
        });

        Assert.Equal(new[]
        {
            EffectLines.Kind.Interval, EffectLines.Kind.Start, EffectLines.Kind.End, EffectLines.Kind.Gain, EffectLines.Kind.Loss,
            EffectLines.Kind.Max, EffectLines.Kind.Food, EffectLines.Kind.Speed, EffectLines.Kind.EndBuff,
        }, lines.Select(l => l.Kind));
    }

    // The English mod texts of the line kinds, as en.json gives them with the game texts of the text keys.
    private static readonly Dictionary<string, string> English = new Dictionary<string, string>
    {
        ["satiety"] = "Satiety", ["morale"] = "Morale", ["stamina"] = "Stamina", ["fitness"] = "Fitness", ["life"] = "Life",
        ["maxSatiety"] = "Max Satiety", ["maxMorale"] = "Max Morale", ["maxStamina"] = "Max Stamina",
        ["maxFitness"] = "Max Fitness", ["maxLife"] = "Max Life", ["movementSpeed"] = "Movement Speed",
        ["lineEachHour"] = "{stat} each hour", ["lineEvery"] = "{stat} every {hours} h", ["lineStart"] = "{stat} at the start",
        ["lineEnd"] = "{stat} at the end", ["lineGain"] = "{stat} gain", ["lineLoss"] = "{stat} loss",
        ["lineFood"] = "{stat} from {food}", ["lineThen"] = "Then",
        ["lineNaturalGain"] = "{stat} natural gain", ["lineNaturalLoss"] = "{stat} natural loss",
        ["interactionSpeed"] = "Interaction speed", ["lineResting"] = "{line} while resting", ["lineCrisis"] = "{line} in a crisis",
    };

    private static (string, string) Text(EffectLines.Line line) =>
        EffectLines.Text(line, name => English[name], k => k == 2 ? "Meat" : "Food " + k, id => id == 2163 ? "Digesting Enoki" : "Buff " + id);

    private static (string, string)[] Texts(Dictionary<string, double> fields, int layers = 1) =>
        Lines(fields, layers).Select(Text).ToArray();

    [Fact]
    public void An_interval_line_names_the_stat_and_the_interval_and_shows_the_signed_change()
    {
        Assert.Equal(new[] { ("Stamina each hour", "+5"), ("Life each hour", "-3") },
            Texts(new Dictionary<string, double> { ["BuffInterval"] = 3600, ["BuffStaminaInterval"] = 5, ["BuffVitalityInterval"] = -3 }));
        Assert.Equal(new[] { ("Life every 2 h", "-3") },
            Texts(new Dictionary<string, double> { ["BuffInterval"] = 7200, ["BuffVitalityInterval"] = -3 }));
        Assert.Equal(new[] { ("Morale each hour", "+0.8") },
            Texts(new Dictionary<string, double> { ["BuffInterval"] = 3600, ["BuffMoraleInterval"] = 0.05 }, 16));
    }

    [Fact]
    public void The_other_line_kinds_show_their_label_and_value()
    {
        Assert.Equal(new[]
        {
            ("Morale at the start", "+10"), ("Life at the end", "-20"), ("Morale gain", "+50%"), ("Satiety loss", "-30%"),
            ("Max Stamina", "-10"), ("Satiety from Meat", "+50%"), ("Movement Speed", "-20%"), ("Then", "Digesting Enoki"),
        }, Texts(new Dictionary<string, double>
        {
            ["BuffMoraleStart"] = 10, ["BuffVitalityEnd"] = -20, ["BuffMoraleAdd"] = 0.5, ["BuffSatietyDel"] = -0.300000012,
            ["BuffStaminaMax"] = -10, ["BuffSatietyFood2Add_Ratio"] = 0.5, ["BuffSpeed"] = -0.200000003, ["BuffEndBuff01"] = 2163,
        }));
    }

    [Fact]
    public void The_plain_trees_are_the_default_tree_and_the_symptom_trees_of_the_forced_actions()
    {
        Assert.Equal(new[] { "Buff/Buff_Default", "Buff/SymptomForcedAction", "Buff/SymptomForcedToiletAction" },
            EffectLines.PlainTrees.OrderBy(t => t, StringComparer.Ordinal));
        foreach (string tree in new[] { "Buff/Buff_CondOnOtherBuff", "Buff/Multitask", "Buff/Buff_306510" })
            Assert.DoesNotContain(tree, EffectLines.PlainTrees);
    }

    [Fact]
    public void Vomiting_shows_its_losses_every_three_hours_as_penalties_and_no_line_for_its_forced_action()
    {
        // Vomiting (game 1.1.18317): the plain fields and the fields of its forced action.
        var lines = Lines(new Dictionary<string, double>
        {
            ["BuffInterval"] = 10800, ["BuffSatietyInterval"] = -3, ["BuffStaminaInterval"] = -3, ["BuffAction"] = 133,
            ["IntValue0"] = 21600, ["FloatValue0"] = 0.5,
        });

        Assert.Equal(new[] { ("Satiety every 3 h", "-3"), ("Stamina every 3 h", "-3") }, lines.Select(Text));
        Assert.All(lines, l => Assert.True(EffectLines.IsBad(l)));
    }

    [Fact]
    public void A_change_of_Fitness_gives_no_line_as_the_HUD_does_not_show_that_stat()
    {
        Assert.Equal(new[] { ("Life each hour", "-1") }, Texts(new Dictionary<string, double>
        {
            ["BuffInterval"] = 3600, ["BuffHealthInterval"] = -1, ["BuffVitalityInterval"] = -1, ["BuffHealthStart"] = 5,
            ["BuffHealthEnd"] = -5, ["BuffHealthAdd"] = 0.5, ["BuffHealthDel"] = 0.5, ["BuffHealthMax"] = 2,
        }));
    }

    // The code trees of game 1.1.18293 (game/decompiled/HotUpdate/GameCore.HotUpdate.Battle.Logic/E_Buff_*.c) with the
    // fields of their effect rows.
    private static (string, string)[] Tree(string tree, Dictionary<string, double> fields, Func<int, IReadOnlyDictionary<string, double>> buffEffect = null) =>
        EffectLines.FromTree(tree, fields, 1, buffEffect ?? (id => null)).Select(Text).ToArray();

    [Fact]
    public void Well_Fed_shows_its_natural_changes_and_the_interaction_speed()
    {
        Assert.Equal(new[]
        {
            ("Morale natural gain", "+20%"), ("Stamina natural gain", "+20%"), ("Satiety natural loss", "+30%"), ("Interaction speed", "+10%"),
        }, Tree("Buff/Buff_1300_Satiated", new Dictionary<string, double>
        {
            ["FloatValue0"] = 0.300000012, ["FloatValue1"] = 0.200000003, ["FloatValue2"] = 0.200000003, ["FloatValue3"] = 0.100000001,
        }));
    }

    [Fact]
    public void Exhausted_shows_its_gain_ratios_and_its_Life_loss_on_the_timer_of_IntValue0()
    {
        Assert.Equal(new[]
        {
            ("Life each hour", "-2"), ("Satiety gain", "-10%"), ("Morale gain", "-10%"), ("Life gain", "-10%"),
            ("Movement Speed", "-30%"),
        }, Tree("Buff/Child_ExtremeFatigue", new Dictionary<string, double>
        {
            ["BuffSpeed"] = -0.300000012, ["IntValue0"] = 3600, ["FloatValue0"] = -0.100000001, ["FloatValue1"] = -0.100000001,
            ["FloatValue2"] = -0.100000001, ["FloatValue3"] = -0.100000001, ["FloatValue4"] = -2,
        }));
    }

    [Fact]
    public void Starving_shows_only_its_nonzero_losses_every_two_hours()
    {
        Assert.Equal(new[] { ("Life every 2 h", "-6"), ("Movement Speed", "-30%") },
            Tree("Buff/Child_ExtremeHunger", new Dictionary<string, double> { ["BuffSpeed"] = -0.300000012, ["IntValue0"] = 7200, ["FloatValue2"] = -6 }));
    }

    [Fact]
    public void The_maximums_come_from_the_int_values()
    {
        Assert.Equal(new[] { ("Max Stamina", "-10"), ("Max Life", "-10") },
            Tree("Buff/Child_Subhealth", new Dictionary<string, double> { ["IntValue0"] = -10, ["IntValue1"] = -10 }));
        Assert.Equal(("Max Stamina", "-10"),
            Tree("Buff/Child_Dying", new Dictionary<string, double> { ["IntValue0"] = -10 })[0]);
    }

    [Fact]
    public void Critical_shows_its_gain_ratios_with_no_Fitness_line()
    {
        Assert.Equal(new[] { ("Satiety gain", "-10%"), ("Morale gain", "-10%"), ("Stamina gain", "-10%"), ("Max Stamina", "-10") },
            Tree("Buff/Child_Dying", new Dictionary<string, double>
            {
                ["IntValue0"] = -10, ["FloatValue0"] = -0.1, ["FloatValue1"] = -0.1, ["FloatValue2"] = -0.1, ["FloatValue3"] = -0.1,
            }));
    }

    [Fact]
    public void Fatigued_and_Depressed_show_their_gain_ratios()
    {
        Assert.Equal(new[] { ("Satiety gain", "-10%"), ("Morale gain", "-10%"), ("Life gain", "-10%") },
            Tree("Buff/Child_Fatigue", new Dictionary<string, double>
            {
                ["FloatValue0"] = -0.1, ["FloatValue1"] = -0.1, ["FloatValue2"] = -0.1, ["FloatValue3"] = -0.1,
            }));
        Assert.Equal(new[] { ("Satiety gain", "-10%"), ("Stamina gain", "-10%") },
            Tree("Buff/Child_Dismay", new Dictionary<string, double> { ["FloatValue0"] = -0.1, ["FloatValue1"] = -0.1 }));
    }

    [Fact]
    public void Energetic_shows_its_gains_while_resting()
    {
        Assert.Equal(new[] { ("Stamina gain while resting", "+100%"), ("Satiety natural loss", "+30%") },
            Tree("Buff/Buff_306510", new Dictionary<string, double> { ["FloatValue1"] = 1, ["FloatValue2"] = 0.300000012 }));
    }

    [Fact]
    public void Calm_in_Crisis_shows_the_lines_of_the_buff_that_it_adds_in_a_crisis()
    {
        var helper = new Dictionary<string, double> { ["BuffMoraleDel"] = -0.5 };

        Assert.Equal(new[] { ("Morale loss in a crisis", "-50%") },
            Tree("Buff/Buff_305310", new Dictionary<string, double> { ["IntValue0"] = 305311 }, id => id == 305311 ? helper : null));
    }

    [Fact]
    public void Overweight_shows_its_Satiety_loss_and_movement_speed_as_penalties_and_no_eat_line()
    {
        var fields = new Dictionary<string, double> { ["FloatValue0"] = -0.5, ["FloatValue1"] = 0.200000003, ["FloatValue2"] = 0.5 };

        Assert.Equal(new[] { ("Satiety loss", "+50%"), ("Movement Speed", "-50%") }, Tree("Buff/Child_Obesity", fields));
        Assert.All(EffectLines.FromTree("Buff/Child_Obesity", fields, 1, id => null), l => Assert.True(EffectLines.IsBad(l)));
        Assert.Equal(new[] { ("Satiety loss", "+100%"), ("Movement Speed", "-100%") },
            EffectLines.FromTree("Buff/Child_Obesity", fields, 2, id => null).Select(Text).ToArray());
    }

    private static (string, string)[] Overweight(double floatValue0, double talentRatio) =>
        EffectLines.FromTree("Buff/BagOverweight", new Dictionary<string, double> { ["FloatValue0"] = floatValue0 }, 1, id => null, talentRatio)
            .Select(Text).ToArray();

    [Theory]
    // Encumbered.
    [InlineData(-0.400000006, 0, "-40%")]
    [InlineData(-0.400000006, 0.5, "-20%")]
    // Limited Mobility: the game text says -30%.
    [InlineData(-0.5, 0, "-50%")]
    [InlineData(-0.5, 0.5, "-25%")]
    // A talent ratio below 0 counts as 0.
    [InlineData(-0.400000006, -0.5, "-40%")]
    public void The_overweight_tree_shows_the_movement_speed_that_the_game_applies_with_the_talent_factor(double floatValue0, double talentRatio, string value)
    {
        Assert.Equal(new[] { ("Movement Speed", value) }, Overweight(floatValue0, talentRatio));
    }

    [Fact]
    public void The_overweight_tree_gives_no_line_at_a_talent_ratio_of_1_or_more_and_its_line_is_a_penalty()
    {
        Assert.Empty(Overweight(-0.400000006, 1));
        Assert.Empty(Overweight(-0.400000006, 1.5));
        var line = Assert.Single(EffectLines.FromTree("Buff/BagOverweight", new Dictionary<string, double> { ["FloatValue0"] = -0.4 }, 1, id => null));
        Assert.True(EffectLines.IsBad(line));
    }

    // The trees of game 1.1.18317 below.
    [Fact]
    public void Disease_Proof_shows_no_natural_gain_of_Life_as_a_penalty_and_no_Fitness_line()
    {
        var fields = new Dictionary<string, double> { ["IntValue0"] = 1, ["FloatValue0"] = -1, ["FloatValue1"] = -1 };

        Assert.Equal(new[] { ("Life natural gain", "-100%") }, Tree("Buff/Buff_306410_DiseaseImmunity", fields));
        Assert.All(EffectLines.FromTree("Buff/Buff_306410_DiseaseImmunity", fields, 1, id => null), l => Assert.True(EffectLines.IsBad(l)));
    }

    [Fact]
    public void Athlete_shows_its_Stamina_gain_and_its_Max_Life_and_no_line_for_Fitness_or_its_target_value()
    {
        Assert.Equal(new[] { ("Stamina gain", "+50%"), ("Max Life", "+10") },
            Tree("Buff/Life_Movement", new Dictionary<string, double>
            {
                ["IntValue0"] = 10, ["IntValue1"] = 10, ["FloatValue0"] = 1, ["FloatValue1"] = 0.5,
            }));
    }

    [Fact]
    public void Sweet_Dreams_shows_its_Stamina_gain_while_resting()
    {
        Assert.Equal(new[] { ("Stamina gain while resting", "+50%") },
            Tree("Buff/QuickRest", new Dictionary<string, double> { ["FloatValue0"] = 0.5 }));
    }

    [Fact]
    public void A_tree_not_in_the_table_gives_no_line()
    {
        Assert.Empty(Tree("Buff/Unknown", new Dictionary<string, double> { ["FloatValue0"] = 0.5, ["BuffSpeed"] = -0.3 }));
    }

    private static EffectLines.Line L(EffectLines.Kind kind, int stat, double amount) =>
        new EffectLines.Line { Kind = kind, Stat = stat, Amount = amount };

    [Theory]
    [InlineData(EffectLines.Kind.Interval, -3, true)]
    [InlineData(EffectLines.Kind.Interval, 5, false)]
    [InlineData(EffectLines.Kind.Start, -10, true)]
    [InlineData(EffectLines.Kind.End, -100, true)]
    [InlineData(EffectLines.Kind.Gain, -0.2, true)]
    [InlineData(EffectLines.Kind.Gain, 0.2, false)]
    [InlineData(EffectLines.Kind.NaturalGain, -0.2, true)]
    [InlineData(EffectLines.Kind.Loss, -0.5, false)]
    [InlineData(EffectLines.Kind.Loss, 0.3, true)]
    [InlineData(EffectLines.Kind.NaturalLoss, 0.3, true)]
    [InlineData(EffectLines.Kind.NaturalLoss, -0.2, false)]
    [InlineData(EffectLines.Kind.Max, -10, true)]
    [InlineData(EffectLines.Kind.Food, -0.25, true)]
    [InlineData(EffectLines.Kind.Speed, -0.2, true)]
    [InlineData(EffectLines.Kind.InteractSpeed, 0.1, false)]
    [InlineData(EffectLines.Kind.EndBuff, 0, false)]
    public void A_line_is_a_penalty_when_it_takes_a_stat_or_adds_to_a_loss(EffectLines.Kind kind, double amount, bool bad)
    {
        Assert.Equal(bad, EffectLines.IsBad(L(kind, EffectLines.Stamina, amount)));
    }

    [Fact]
    public void The_penalties_come_after_the_other_lines_and_each_group_keeps_its_order()
    {
        // Adrenaline.
        var lines = new List<EffectLines.Line>
        {
            L(EffectLines.Kind.End, EffectLines.Stamina, -100),
            L(EffectLines.Kind.End, EffectLines.Life, -20),
            L(EffectLines.Kind.Loss, EffectLines.Stamina, -0.5),
        };

        var ordered = EffectLines.Ordered(lines);

        Assert.Equal(new[] { (EffectLines.Kind.Loss, EffectLines.Stamina), (EffectLines.Kind.End, EffectLines.Stamina), (EffectLines.Kind.End, EffectLines.Life) },
            ordered.Select(l => (l.Kind, l.Stat)));
    }
}
