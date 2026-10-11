using System.Text.Json.Nodes;
using StatsUI;
using Xunit;

public class PageJsonTests
{
    private static JsonNode Fixture(string name) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", name)));

    private static readonly PageJson.Switches AllOn = new PageJson.Switches { WeightReadout = true, TimeStripes = true, ExtendedTooltips = true, StatTooltipGameText = true };

    [Fact]
    public void The_data_with_no_part_read_yet_matches_the_fixture()
    {
        string json = PageJson.DataJson(AllOn, PageJson.TextsJson(new Dictionary<string, string>()),
            PageJson.EmptyStats, PageJson.EmptyBuffs, PageJson.EmptyWeight);

        Assert.True(JsonNode.DeepEquals(Fixture("data.json"), JsonNode.Parse(json)), json);
    }

    [Fact]
    public void The_switches_go_as_they_are_set()
    {
        var switches = new PageJson.Switches { WeightReadout = false, TimeStripes = true, ExtendedTooltips = false, StatTooltipGameText = false };

        var data = JsonNode.Parse(PageJson.DataJson(switches, "{}", "[]", "[]", "null"));

        Assert.False((bool)data["switches"]["weightReadout"]);
        Assert.True((bool)data["switches"]["timeStripes"]);
        Assert.False((bool)data["switches"]["extendedTooltips"]);
        Assert.False((bool)data["switches"]["statTooltipGameText"]);
    }

    [Fact]
    public void A_text_with_a_quote_stays_one_JSON_string()
    {
        string json = PageJson.TextsJson(new Dictionary<string, string> { ["timeLeft"] = "Time \"left\"" });

        Assert.Equal("Time \"left\"", (string)JsonNode.Parse(json)["timeLeft"]);
    }

    [Fact]
    public void The_weight_part_holds_the_number_the_fill_the_line_and_the_tone()
    {
        var weight = JsonNode.Parse(PageJson.WeightJson(8600, 12000, 9600));

        Assert.Equal("8.6", (string)weight["text"]);
        Assert.Equal(0.72, (double)weight["fill"], 2);
        Assert.Equal(0.8, (double)weight["line"], 2);
        Assert.Equal("near", (string)weight["tone"]);
    }

    [Fact]
    public void Above_the_Encumbered_line_the_tone_is_over()
    {
        Assert.Equal("over", (string)JsonNode.Parse(PageJson.WeightJson(10000, 12000, 9600))["tone"]);
        Assert.Equal("good", (string)JsonNode.Parse(PageJson.WeightJson(1000, 12000, 9600))["tone"]);
    }

    [Fact]
    public void The_stats_part_holds_the_gain_mark_and_the_ratios_of_each_row()
    {
        var rows = new[]
        {
            new PageJson.StatRead { Gain = 0, GainCap = 2 },
            new PageJson.StatRead { Gain = 0.65, Loss = -0.5, NaturalGain = 0.2, NaturalLoss = 0.3, GainCap = 2 },
            new PageJson.StatRead { Gain = 2, GainCap = 2 },
            new PageJson.StatRead { Gain = -0.1, GainCap = 2 },
        };

        var stats = JsonNode.Parse(PageJson.StatsJson(rows)).AsArray();

        Assert.Equal(4, stats.Count);
        Assert.Null(stats[0]["mark"]);
        Assert.Equal("x1.65", (string)stats[1]["mark"]["text"]);
        Assert.Equal("good", (string)stats[1]["mark"]["tone"]);
        Assert.Equal(0.65, (double)stats[1]["gain"], 4);
        Assert.Equal(-0.5, (double)stats[1]["loss"], 4);
        Assert.Equal(0.2, (double)stats[1]["naturalGain"], 4);
        Assert.Equal(0.3, (double)stats[1]["naturalLoss"], 4);
        Assert.Equal("x3", (string)stats[2]["mark"]["text"]);
        Assert.Equal("cap", (string)stats[2]["mark"]["tone"]);
        Assert.Equal("bad", (string)stats[3]["mark"]["tone"]);
    }

    [Fact]
    public void A_stat_row_holds_its_threshold_or_null()
    {
        var rows = new[]
        {
            new PageJson.StatRead { GainCap = 2, Threshold = new StatsLogic.Threshold(80, 60) },
            new PageJson.StatRead { GainCap = 2 },
        };

        var stats = JsonNode.Parse(PageJson.StatsJson(rows)).AsArray();

        Assert.Equal(80, (int)stats[0]["threshold"]["start"]);
        Assert.Equal(60, (int)stats[0]["threshold"]["end"]);
        Assert.Null(stats[1]["threshold"]);
        Assert.True(stats[1].AsObject().ContainsKey("threshold"));
    }

    [Fact]
    public void The_buffs_part_holds_each_entry_with_its_row_its_times_and_the_text_of_the_game()
    {
        var entries = new List<BuffsLogic.Entry>
        {
            new BuffsLogic.Entry { ConfigId = 2, Row = 1, IsGood = true, Layers = 1, EndTime = 5000, FullTime = 10800 },
            new BuffsLogic.Entry { ConfigId = 1008, Row = 3, IsGood = false, Layers = 2, MaxLayers = 100, EndTime = -1, FullTime = -1 },
        };

        var buffs = JsonNode.Parse(PageJson.BuffsJson(entries, e => e.ConfigId == 2 ? "For 3 hours, \"Morale\" gain +50%." : "")).AsArray();

        Assert.Equal(2, buffs.Count);
        Assert.Equal(2, (int)buffs[0]["id"]);
        Assert.Equal(1, (int)buffs[0]["row"]);
        Assert.True((bool)buffs[0]["good"]);
        Assert.Equal(1, (int)buffs[0]["layers"]);
        Assert.Equal(5000, (int)buffs[0]["end"]);
        Assert.Equal(10800, (double)buffs[0]["full"]);
        Assert.Equal("For 3 hours, \"Morale\" gain +50%.", (string)buffs[0]["desc"]);
        Assert.False((bool)buffs[1]["good"]);
        Assert.Equal(100, (int)buffs[1]["maxLayers"]);
        Assert.Equal(-1, (int)buffs[1]["end"]);
        Assert.Equal(-1, (double)buffs[1]["full"]);
    }

    [Fact]
    public void A_buff_entry_holds_the_loss_of_each_stat_row_or_null()
    {
        var losses = new BuffsLogic.Loss[4];
        losses[2] = new BuffsLogic.Loss { AtEnd = 100, Ratio = -0.5 };
        losses[3] = new BuffsLogic.Loss { Each = 3, Every = 3600, AtEnd = 20 };
        var entries = new List<BuffsLogic.Entry>
        {
            new BuffsLogic.Entry { ConfigId = 24030, Row = 2, EndTime = 5000, FullTime = 86400, Losses = losses },
            new BuffsLogic.Entry { ConfigId = 1300, Row = 0, EndTime = -1, FullTime = -1 },
        };

        var buffs = JsonNode.Parse(PageJson.BuffsJson(entries, e => "")).AsArray();

        var json = buffs[0]["losses"].AsArray();
        Assert.Equal(4, json.Count);
        Assert.Equal(0, (double)json[0]["each"]);
        Assert.Equal(100, (double)json[2]["atEnd"]);
        Assert.Equal(-0.5, (double)json[2]["ratio"], 4);
        Assert.Equal(3, (double)json[3]["each"]);
        Assert.Equal(3600, (double)json[3]["every"]);
        Assert.Equal(20, (double)json[3]["atEnd"]);
        Assert.Null(buffs[1]["losses"]);
        Assert.True(buffs[1].AsObject().ContainsKey("losses"));
    }

    [Fact]
    public void A_buff_entry_holds_its_effect_lines_as_label_and_value()
    {
        var entries = new List<BuffsLogic.Entry>
        {
            new BuffsLogic.Entry { ConfigId = 24040, Row = 2, EndTime = 5000, FullTime = 86400 },
            new BuffsLogic.Entry { ConfigId = 1300, Row = 0, EndTime = -1, FullTime = -1 },
        };

        var buffs = JsonNode.Parse(PageJson.BuffsJson(entries, e => "",
            e => e.ConfigId == 24040 ? new[] { ("Stamina each hour", "+5", false), ("Life each hour", "-3", true) } : new (string, string, bool)[0])).AsArray();

        var lines = buffs[0]["lines"].AsArray();
        Assert.Equal(2, lines.Count);
        Assert.Equal("Stamina each hour", (string)lines[0]["label"]);
        Assert.Equal("+5", (string)lines[0]["value"]);
        Assert.False((bool)lines[0]["bad"]);
        Assert.Equal("-3", (string)lines[1]["value"]);
        Assert.True((bool)lines[1]["bad"]);
        Assert.Empty(buffs[1]["lines"].AsArray());
    }

    [Fact]
    public void A_stat_row_holds_its_food_type_gain_ratios_with_their_names()
    {
        var rows = new[]
        {
            new PageJson.StatRead { GainCap = 2, Food = new[] { ("Meat", 0.5), ("Fish", 0.25) } },
            new PageJson.StatRead { GainCap = 2 },
        };

        var stats = JsonNode.Parse(PageJson.StatsJson(rows)).AsArray();

        var food = stats[0]["food"].AsArray();
        Assert.Equal(2, food.Count);
        Assert.Equal("Meat", (string)food[0]["name"]);
        Assert.Equal(0.5, (double)food[0]["ratio"], 4);
        Assert.Equal("Fish", (string)food[1]["name"]);
        Assert.Empty(stats[1]["food"].AsArray());
    }

    [Fact]
    public void A_stat_row_holds_the_rest_of_its_loss_ratio()
    {
        var rows = new[] { new PageJson.StatRead { Loss = -0.5, LossRest = -0.2, LossMin = -0.5, LossMax = 2, GainCap = 2 } };

        var stats = JsonNode.Parse(PageJson.StatsJson(rows)).AsArray();

        Assert.Equal(-0.5, (double)stats[0]["loss"], 4);
        Assert.Equal(-0.2, (double)stats[0]["lossRest"], 4);
        Assert.Equal(-0.5, (double)stats[0]["lossMin"], 4);
        Assert.Equal(2, (double)stats[0]["lossMax"], 4);
    }

    [Fact]
    public void The_check_and_the_push_carry_the_game_time_outside_the_data()
    {
        Assert.Equal("window.__statsui?window.__statsui.check(4321):'no script'", PageJson.CheckCommand(4321));
        Assert.Equal("window.__statsui?window.__statsui.setData({\"a\":1},4321):'no script'", PageJson.SetDataCommand("{\"a\":1}", 4321));
        Assert.Equal("S;window.__statsui.setData({\"a\":1},4321);", PageJson.SetDataWithScriptCommand("S", "{\"a\":1}", 4321));
    }
}
