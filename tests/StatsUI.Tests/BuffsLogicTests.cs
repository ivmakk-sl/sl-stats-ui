using System.Collections.Generic;
using StatsUI;
using Xunit;

// The buff entries of the stat rows from the buff list of the leading role, as the game's HUD reducer puts the
// badges in the rows (Reducer_Web_CoreUI1, the build of buffsJson).
public class BuffsLogicTests
{
    private const int Rows = 4;
    private const string Default = "Buff/Buff_Default";

    private static BuffsLogic.BuffIn Buff(int id, int displayType, int end = 5000, double during = 10800, bool hidden = false, bool found = true) =>
        new BuffsLogic.BuffIn { ConfigId = id, DisplayType = displayType, EndTime = end, During = during, Hidden = hidden, Found = found, Layers = 1, IsGood = true };

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(5, 3)]
    [InlineData(0, -1)]
    [InlineData(6, -1)]
    [InlineData(-1, -1)]
    public void The_display_type_gives_the_stat_row(int displayType, int row)
    {
        Assert.Equal(row, BuffsLogic.RowOf(displayType, Rows));
    }

    [Fact]
    public void A_hidden_buff_a_buff_with_no_row_and_a_buff_with_no_config_are_left_out()
    {
        var entries = BuffsLogic.Entries(new[]
        {
            Buff(1, 2),
            Buff(2, 2, hidden: true),
            Buff(3, 0),
            Buff(4, 3, found: false),
        }, Rows);

        Assert.Equal(new[] { 1 }, entries.Select(e => e.ConfigId));
        Assert.Equal(1, entries[0].Row);
    }

    [Fact]
    public void The_entries_keep_the_order_of_the_buff_list_so_the_nth_entry_of_an_id_is_its_nth_badge()
    {
        var entries = BuffsLogic.Entries(new[]
        {
            Buff(7, 5, end: 100),
            Buff(8, 1),
            Buff(7, 4, end: 200),
        }, Rows);

        Assert.Equal(new[] { (7, 3, 100), (8, 0, 5000), (7, 3, 200) }, entries.Select(e => (e.ConfigId, e.Row, e.EndTime)));
    }

    [Fact]
    public void The_full_time_is_the_duration_of_the_config_with_the_duration_factors()
    {
        Assert.Equal(10800, BuffsLogic.FullTime(10800, 0, 0));
        Assert.Equal(16200 + 600, BuffsLogic.FullTime(10800, 0.5, 600));
    }

    [Fact]
    public void A_buff_with_no_end_keeps_the_end_time_minus_one_and_has_no_full_time()
    {
        var entries = BuffsLogic.Entries(new[] { Buff(9, 3, end: -1, during: -1) }, Rows);

        Assert.Equal(-1, entries[0].EndTime);
        Assert.Equal(-1, entries[0].FullTime);
        Assert.Equal(-1, BuffsLogic.FullTime(-1, 0.5, 600));
    }

    // Seen in game 1.1.18293: a buff with BuffDuring -1 that the game gives again each game hour (Comfortable,
    // Well-Fed) has an end time one game hour ahead, or one in the past.
    [Fact]
    public void A_buff_with_no_full_time_has_no_end_also_when_the_game_gives_it_an_end_time()
    {
        var entries = BuffsLogic.Entries(new[] { Buff(9, 3, end: 7909440, during: -1) }, Rows);

        Assert.Equal(-1, entries[0].EndTime);
        Assert.Equal(-1, entries[0].FullTime);
    }

    [Fact]
    public void An_entry_keeps_its_layers_and_whether_it_is_a_buff_or_a_debuff()
    {
        var input = Buff(10, 1);
        input.Layers = 3;
        input.IsGood = false;

        var entry = BuffsLogic.Entries(new[] { input }, Rows)[0];

        Assert.Equal(3, entry.Layers);
        Assert.False(entry.IsGood);
    }

    // The fields of the Buff/Buff_Default effect rows of game 1.1.18293 (game/config/tables/Effect.tsv).
    private static readonly Dictionary<string, double> Stimulants = new Dictionary<string, double>
    {
        ["BuffInterval"] = 3600, ["BuffStaminaInterval"] = 5, ["BuffVitalityInterval"] = -3,
    };

    private static readonly Dictionary<string, double> Adrenaline = new Dictionary<string, double>
    {
        ["BuffStaminaDel"] = -0.5, ["BuffStaminaEnd"] = -100, ["BuffVitalityEnd"] = -20,
    };

    private static readonly Dictionary<string, double> Freezing = new Dictionary<string, double>
    {
        ["BuffInterval"] = 3600, ["BuffSatietyInterval"] = -4, ["BuffMoraleInterval"] = -1, ["BuffVitalityInterval"] = -1,
    };

    private static readonly Dictionary<string, double> FirstAidKnowledge = new Dictionary<string, double>
    {
        ["BuffVitalityDel"] = -0.3,
    };

    private const int Satiety = 0, Morale = 1, Stamina = 2, Life = 3;

    [Fact]
    public void A_loss_each_interval_counts_and_a_gain_each_interval_does_not()
    {
        var losses = BuffsLogic.Losses(Stimulants, 1, 0);

        Assert.Equal(3, losses[Life].Each);
        Assert.Equal(3600, losses[Life].Every);
        Assert.Equal(0, losses[Stamina].Each);
        Assert.Equal(0, losses[Satiety].Each);
    }

    [Fact]
    public void A_loss_at_the_end_counts_with_Vitality_as_Life_and_the_loss_ratio_is_kept()
    {
        var losses = BuffsLogic.Losses(Adrenaline, 1, 0);

        Assert.Equal(100, losses[Stamina].AtEnd);
        Assert.Equal(20, losses[Life].AtEnd);
        Assert.Equal(-0.5, losses[Stamina].Ratio, 6);
        Assert.Equal(0, losses[Stamina].Each);
    }

    [Fact]
    public void The_layers_multiply_each_loss_and_the_loss_ratio()
    {
        var losses = BuffsLogic.Losses(Adrenaline, 2, 0);
        var each = BuffsLogic.Losses(Stimulants, 2, 0);

        Assert.Equal(200, losses[Stamina].AtEnd);
        Assert.Equal(-1.0, losses[Stamina].Ratio, 6);
        Assert.Equal(6, each[Life].Each);
    }

    [Fact]
    public void The_cold_reduction_lowers_the_interval_losses_only()
    {
        var losses = BuffsLogic.Losses(Freezing, 1, 0.25);

        Assert.Equal(3, losses[Satiety].Each, 6);
        Assert.Equal(0.75, losses[Morale].Each, 6);
        Assert.Equal(0.75, losses[Life].Each, 6);
    }

    [Fact]
    public void A_buff_with_no_interval_has_no_loss_each_interval()
    {
        var fields = new Dictionary<string, double> { ["BuffVitalityInterval"] = -3 };

        Assert.Equal(0, BuffsLogic.Losses(fields, 1, 0)[Life].Each);
    }

    [Fact]
    public void A_buff_with_no_effect_fields_has_no_losses()
    {
        Assert.Null(BuffsLogic.Losses(null, 1, 0));
    }

    [Fact]
    public void An_entry_carries_the_losses_of_its_effect_row_with_the_cold_reduction_of_a_cold_debuff()
    {
        var cold = Buff(1114, 5, end: -1, during: -1);
        cold.Tree = Default;
        cold.Fields = Freezing;
        cold.Cold = true;
        var warm = Buff(24040, 3);
        warm.Tree = Default;
        warm.Fields = Freezing;
        var tree = Buff(1300, 1);

        var entries = BuffsLogic.Entries(new[] { cold, warm, tree }, Rows, 0.5);

        Assert.Equal(2, entries[0].Losses[Satiety].Each, 6);
        Assert.Equal(4, entries[1].Losses[Satiety].Each, 6);
        Assert.Null(entries[2].Losses);
        Assert.Same(Freezing, entries[0].Effect);
        Assert.Null(entries[2].Effect);
        Assert.Null(entries[0].Tree);
    }

    [Fact]
    public void An_entry_of_a_code_tree_carries_the_tree_and_its_fields_and_no_losses()
    {
        var wellFed = Buff(1300, 1, end: -1, during: -1);
        var fields = new Dictionary<string, double> { ["FloatValue0"] = 0.3 };
        wellFed.Tree = "Buff/Buff_1300_Satiated";
        wellFed.Fields = fields;

        var entry = BuffsLogic.Entries(new[] { wellFed }, Rows)[0];

        Assert.Equal("Buff/Buff_1300_Satiated", entry.Tree);
        Assert.Same(fields, entry.TreeFields);
        Assert.Null(entry.Losses);
    }

    [Fact]
    public void A_debuff_of_a_symptom_tree_carries_the_losses_of_its_plain_fields()
    {
        // Diarrhea (game 1.1.18317): the plain fields and the fields of its forced action.
        var diarrhea = Buff(1101, 1, end: 50000, during: 86400);
        diarrhea.IsGood = false;
        diarrhea.Tree = "Buff/SymptomForcedToiletAction";
        diarrhea.Fields = new Dictionary<string, double>
        {
            ["BuffInterval"] = 10800, ["BuffSatietyInterval"] = -3, ["BuffAction"] = 134, ["IntValue0"] = 21600,
            ["IntValue1"] = 212, ["IntValue2"] = 9075, ["IntValue3"] = 248, ["FloatValue0"] = 1,
        };

        var entry = BuffsLogic.Entries(new[] { diarrhea }, Rows)[0];

        Assert.Same(diarrhea.Fields, entry.Effect);
        Assert.Null(entry.Tree);
        Assert.Equal((3.0, 10800.0, 0.0, 0.0), (entry.Losses[Satiety].Each, entry.Losses[Satiety].Every, entry.Losses[Satiety].AtEnd, entry.Losses[Satiety].Ratio));
        foreach (int row in new[] { Morale, Stamina, Life })
            Assert.Equal((0.0, 0.0, 0.0), (entry.Losses[row].Each, entry.Losses[row].AtEnd, entry.Losses[row].Ratio));
    }

    [Fact]
    public void The_rest_of_the_loss_ratio_leaves_out_the_part_of_each_buff_with_an_end()
    {
        var aid = Buff(3022, 5, end: 9000, during: 86400);
        aid.Tree = Default;
        aid.Fields = FirstAidKnowledge;
        var forever = Buff(77, 5, end: -1, during: -1);
        forever.Tree = Default;
        forever.Fields = new Dictionary<string, double> { ["BuffVitalityDel"] = -0.2 };
        var entries = BuffsLogic.Entries(new[] { aid, forever }, Rows);

        // The game total of 30005: -0.3 of First Aid Knowledge, -0.2 of the buff with no end.
        Assert.Equal(-0.2, BuffsLogic.LossRest(-0.5, entries, Life), 6);
        Assert.Equal(-0.1, BuffsLogic.LossRest(-0.1, entries, Stamina), 6);
    }

    [Fact]
    public void An_entry_carries_the_layer_cap_of_its_buff()
    {
        var buff = Buff(1122, 2);
        buff.Layers = 16;
        buff.MaxLayers = 100;

        var entry = BuffsLogic.Entries(new[] { buff }, 4)[0];

        Assert.Equal((16, 100), (entry.Layers, entry.MaxLayers));
    }
}
