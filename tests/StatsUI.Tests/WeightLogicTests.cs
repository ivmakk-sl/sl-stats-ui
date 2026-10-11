using StatsUI;
using Xunit;

// The weight readout of the bag button: the number, the fill of the weight bar, the place of the Encumbered line, and
// the tone.
public class WeightLogicTests
{
    [Theory]
    [InlineData(8600, "8.6")]
    [InlineData(8601, "8.7")]
    [InlineData(7210, "7.3")]
    [InlineData(0, "0.0")]
    [InlineData(12000, "12.0")]
    public void The_number_is_kilograms_with_one_decimal_rounded_up_as_the_storage_window_shows_it(long grams, string text)
    {
        Assert.Equal(text, WeightLogic.Text(grams));
    }

    [Theory]
    [InlineData(8600, 12000, 0.7167)]
    [InlineData(0, 12000, 0)]
    [InlineData(15000, 12000, 1)]
    [InlineData(500, 0, 0)]
    public void The_bar_fills_to_the_carried_weight_over_the_weight_limit_within_0_and_1(long carried, long limit, double fill)
    {
        Assert.Equal(fill, WeightLogic.Fill(carried, limit), 4);
    }

    [Fact]
    public void The_line_is_at_the_Encumbered_line_over_the_weight_limit()
    {
        Assert.Equal(0.8, WeightLogic.Fill(9600, 12000), 4);
    }

    [Theory]
    [InlineData(8000, 9600, WeightLogic.Tone.Good)]
    [InlineData(8159, 9600, WeightLogic.Tone.Good)]
    [InlineData(8160, 9600, WeightLogic.Tone.Near)]
    [InlineData(9600, 9600, WeightLogic.Tone.Near)]
    [InlineData(9601, 9600, WeightLogic.Tone.Over)]
    public void Green_below_85_percent_of_the_Encumbered_line_amber_up_to_it_red_above(long carried, long line, WeightLogic.Tone tone)
    {
        Assert.Equal(tone, WeightLogic.ToneOf(carried, line));
    }
}
