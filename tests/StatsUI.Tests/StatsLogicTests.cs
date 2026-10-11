using StatsUI;
using Xunit;

// The gain mark of a stat bar from the gain ratio of the stat, and the ratio ids of each stat row.
public class StatsLogicTests
{
    private const double Cap = 2.0;

    [Theory]
    [InlineData(0.65, "x1.65", StatsLogic.Tone.Good)]
    [InlineData(0.2, "x1.2", StatsLogic.Tone.Good)]
    [InlineData(-0.1, "x0.9", StatsLogic.Tone.Bad)]
    [InlineData(0.5, "x1.5", StatsLogic.Tone.Good)]
    [InlineData(1.333, "x2.33", StatsLogic.Tone.Good)]
    public void The_mark_shows_the_multiplier_with_at_most_two_decimals_and_its_tone(double ratio, string text, StatsLogic.Tone tone)
    {
        var mark = StatsLogic.GainMark(ratio, Cap);

        Assert.NotNull(mark);
        Assert.Equal(text, mark.Value.Text);
        Assert.Equal(tone, mark.Value.Tone);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.004)]
    [InlineData(-0.004)]
    public void A_multiplier_within_half_a_hundredth_of_x1_shows_no_mark(double ratio)
    {
        Assert.Null(StatsLogic.GainMark(ratio, Cap));
    }

    [Theory]
    [InlineData(2.0)]
    [InlineData(2.5)]
    public void A_gain_ratio_at_its_attribute_maximum_shows_x3_with_the_cap_tone(double ratio)
    {
        var mark = StatsLogic.GainMark(ratio, Cap);

        Assert.Equal("x3", mark.Value.Text);
        Assert.Equal(StatsLogic.Tone.Cap, mark.Value.Tone);
    }

    [Fact]
    public void The_multiplier_text_of_a_loss_ratio_has_the_same_format()
    {
        Assert.Equal("x0.5", StatsLogic.MultiplierText(-0.5));
        Assert.Equal("x1.3", StatsLogic.MultiplierText(0.3));
    }

    // The ids of game/api/HotUpdate/GameCore.HotUpdate/AttrName.cs.
    [Fact]
    public void Each_stat_row_has_the_ratio_ids_of_its_stat()
    {
        var rows = StatsLogic.Rows;
        Assert.Equal(4, rows.Count);

        Assert.Equal((1, 20001, 30001, 40001, 50001), (rows[0].Stat, rows[0].Gain, rows[0].Loss, rows[0].NaturalGain, rows[0].NaturalLoss));
        Assert.Equal((2, 20002, 30002, 40002, 50002), (rows[1].Stat, rows[1].Gain, rows[1].Loss, rows[1].NaturalGain, rows[1].NaturalLoss));
        Assert.Equal((3, 20003, 30003, 40003, 50003), (rows[2].Stat, rows[2].Gain, rows[2].Loss, rows[2].NaturalGain, rows[2].NaturalLoss));
        Assert.Equal((5, 20005, 30005, 40005, 50005), (rows[3].Stat, rows[3].Gain, rows[3].Loss, rows[3].NaturalGain, rows[3].NaturalLoss));
    }

    [Fact]
    public void Satiety_and_Morale_have_a_gain_ratio_for_each_food_type_and_the_others_none()
    {
        var rows = StatsLogic.Rows;

        Assert.Equal(Enumerable.Range(60001, 11), rows[0].Food);
        Assert.Equal(Enumerable.Range(70001, 11), rows[1].Food);
        Assert.Empty(rows[2].Food);
        Assert.Empty(rows[3].Food);
    }

    [Fact]
    public void The_threshold_of_Well_Fed_is_IntValue1_to_start_and_IntValue3_to_end_of_its_effect_row()
    {
        // Effect 200010009 of passive 10009 in game 1.1.18293: Well-Fed 1300 at 80, removed below 60.
        var threshold = StatsLogic.WellFedThreshold(new[] { 1300, 80, 2, 60, 4 });

        Assert.NotNull(threshold);
        Assert.Equal(80, threshold.Value.Start);
        Assert.Equal(60, threshold.Value.End);
    }

    [Fact]
    public void The_raw_loss_ratio_is_the_sum_of_its_parts_past_the_limits_of_the_attribute()
    {
        // Fixture p: StaminaDel_Ratio 0/-1200/0 within -500 and 2000 per mille; the game uses -0.5.
        var ratio = StatsLogic.LossRatio(0, -1200, 0, -500, 2000);

        Assert.Equal(-1.2, ratio.Raw, 6);
        Assert.Equal(-0.5, ratio.Min, 6);
        Assert.Equal(2, ratio.Max, 6);
    }

    [Fact]
    public void A_stat_with_no_loss_ratio_entry_has_a_raw_ratio_of_0_and_the_limits_of_the_game()
    {
        Assert.Equal((0.0, -0.5, 2.0), StatsLogic.NoLossRatio);
    }

    [Fact]
    public void There_is_no_threshold_when_the_effect_row_is_missing()
    {
        Assert.Null(StatsLogic.WellFedThreshold(null));
    }

    [Fact]
    public void There_is_no_threshold_when_the_effect_row_has_no_start_value()
    {
        Assert.Null(StatsLogic.WellFedThreshold(new[] { 1300, 0, 2, 60, 4 }));
    }
}
