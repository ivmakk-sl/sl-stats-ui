using StatsUI;
using Xunit;

// The cures and the suppressors of a debuff, from the item effects and the buffs of the config (game 1.1.19611).
public class CuresLogicTests
{
    private static CuresLogic.ItemIn Item(int id, string types, string ids, string add, string del, int end1 = 0, int end2 = 0) =>
        new CuresLogic.ItemIn
        {
            ItemId = id,
            TargetTypes = Ints(types),
            TargetIds = Ints(ids),
            AddBuffs = Ints(add),
            DelBuffs = Ints(del),
            EndBuff1 = end1,
            EndBuff2 = end2,
        };

    private static CuresLogic.BuffIn Buff(int id, bool good, string tags = "", string tree = "Buff/Buff_Default", int intValue0 = 0) =>
        new CuresLogic.BuffIn
        {
            BuffId = id,
            IsGood = good,
            Tags = tags.Length == 0 ? new string[0] : tags.Split(','),
            Tree = tree,
            IntValue0 = intValue0,
        };

    private static int[] Ints(string list) =>
        list.Length == 0 ? new int[0] : list.Split(',').Select(int.Parse).ToArray();

    // The item rows of the config of today: each med with a buff list, Rubber Chicken, and two items with no buff
    // list of their effect.
    private static readonly CuresLogic.ItemIn[] TodayItems =
    {
        Item(2131, "", "", "", "", 20020),                           // Sports Drink
        Item(2400, "1", "0", "", "1111"),                            // Bandage
        Item(2401, "1,1", "0,0", "", "1111,1113"),                   // Military Medical Kit
        Item(2405, "1,1,1,1", "0,0,0,0", "1127,1128", "1103,1104,1112,1125"), // Antibiotics
        Item(2406, "1,1", "0,0", "1127", "1110,1104"),               // Fever Meds
        Item(2407, "1,1,1", "0,0,0", "", "1101,40030,1126"),         // Diarrhea Meds
        Item(2408, "1", "0", "", "1102"),                            // Painkillers
        Item(2410, "1", "0", "1123", ""),                            // Anxiety Meds
        Item(2412, "1", "0", "", "1105"),                            // Antidote
        Item(2413, "1,1", "0,0", "", "1102,1111"),                   // Kangfuxin Liquid
        Item(2916, "1", "0", "1129", ""),                            // Morphine Injection
        Item(11006, "1", "", "", "1010,803", 11006),                 // Rubber Chicken
    };

    private static readonly CuresLogic.BuffIn[] TodayBuffs =
    {
        Buff(4, false),                                              // Encumbered
        Buff(803, false),                                            // Disgusted
        Buff(1010, false),                                           // Wish Unfulfilled
        Buff(1101, false, "SuppressibleDebuff"),                     // Diarrhea
        Buff(1102, false, "SuppressibleDebuff"),                     // Stomachache
        Buff(1103, false, "SuppressibleDebuff"),                     // Vomiting
        Buff(1104, false, "SuppressibleDebuff"),                     // Fever
        Buff(1105, false, "SuppressibleDebuff"),                     // Poisoned
        Buff(1110, false, "SuppressibleDebuff"),                     // High Temperature
        Buff(1111, false, "SuppressibleDebuff"),                     // Bleeding
        Buff(1112, false, "SuppressibleDebuff"),                     // Severely Infected
        Buff(1113, false, "SuppressibleDebuff"),                     // Concussion with Bleeding
        Buff(1114, false, "ColdDebuff,SuppressibleDebuff"),          // Freezing
        Buff(1121, false),                                           // Disease Outbreak
        Buff(1123, true),                                            // the buff of Anxiety Meds
        Buff(1124, false),                                           // Outbreak Worsening
        Buff(1125, false, "SuppressibleDebuff"),                     // Outbreak: Nausea
        Buff(1126, false, "SuppressibleDebuff"),                     // Outbreak: Upset Stomach
        Buff(1127, true, "", "Buff/Buff_CondOnOtherBuff", 1121),     // Fever Reducing
        Buff(1128, true, "", "Buff/Buff_CondOnOtherBuff", 1124),     // Anti-Infection
        Buff(1129, true, "DebuffSuppressor"),                        // Analgesia
        Buff(1300, true),                                            // Well-Fed
        Buff(11006, true),                                           // the buff of Rubber Chicken
        Buff(20020, true),                                           // the buff of Sports Drink
        Buff(40030, false, "SuppressibleDebuff"),                    // Mild Diarrhea
    };

    private static CuresLogic.Map Today() => CuresLogic.Build(TodayItems, TodayBuffs);

    [Fact]
    public void Painkillers_and_Kangfuxin_Liquid_cure_Stomachache()
    {
        Assert.Equal(new[] { 2408, 2413 }, Today().CuresOf(1102));
    }

    [Fact]
    public void Bandage_Military_Medical_Kit_and_Kangfuxin_Liquid_cure_Bleeding()
    {
        Assert.Equal(new[] { 2400, 2401, 2413 }, Today().CuresOf(1111));
    }

    [Fact]
    public void Rubber_Chicken_with_no_target_id_removes_nothing()
    {
        var map = Today();

        Assert.Empty(map.CuresOf(803));
        Assert.Empty(map.CuresOf(1010));
    }

    [Fact]
    public void An_index_past_the_shorter_target_list_adds_and_removes_nothing()
    {
        var map = CuresLogic.Build(
            new[]
            {
                Item(1, "1,1", "0", "", "1102,1111"),       // two types, one target id
                Item(2, "1", "0,0", "", "1102,1111"),       // one type, two target ids
                Item(3, "1,1", "0,0", "", "1102"),          // a shorter remove list
            },
            TodayBuffs);

        Assert.Equal(new[] { 1, 2, 3 }, map.CuresOf(1102));
        Assert.Empty(map.CuresOf(1111));
    }

    [Fact]
    public void A_target_other_than_the_player_is_no_cure_and_no_suppressor()
    {
        var map = CuresLogic.Build(new[] { Item(1, "2", "5", "1129", "1102") }, TodayBuffs);

        Assert.Empty(map.CuresOf(1102));
        Assert.Empty(map.SuppressorsOf(1102));
    }

    [Fact]
    public void Morphine_Injection_suppresses_each_suppressible_debuff_and_no_other()
    {
        var map = Today();

        Assert.Equal(new[] { 2916 }, map.SuppressorsOf(1102));
        Assert.Equal(new[] { 2916 }, map.SuppressorsOf(1111));
        Assert.Equal(new[] { 2916 }, map.SuppressorsOf(1114));
        Assert.DoesNotContain(2916, map.SuppressorsOf(1121));
        Assert.DoesNotContain(2916, map.SuppressorsOf(1124));
        Assert.Empty(map.SuppressorsOf(4));
        Assert.Empty(map.SuppressorsOf(803));
    }

    [Fact]
    public void Fever_Meds_and_Antibiotics_suppress_Disease_Outbreak_and_only_Antibiotics_suppress_Outbreak_Worsening()
    {
        var map = Today();

        Assert.Equal(new[] { 2405, 2406 }, map.SuppressorsOf(1121));
        Assert.Equal(new[] { 2405 }, map.SuppressorsOf(1124));
        Assert.Empty(map.CuresOf(1121));
        Assert.Empty(map.CuresOf(1124));
    }

    [Fact]
    public void A_buff_at_the_end_of_the_use_counts_as_an_added_buff()
    {
        var map = CuresLogic.Build(
            new[] { Item(1, "", "", "", "", 0, 1129), Item(2, "", "", "", "", 1128) },
            TodayBuffs);

        Assert.Equal(new[] { 1 }, map.SuppressorsOf(1102));
        Assert.Equal(new[] { 2 }, map.SuppressorsOf(1124));
    }

    [Fact]
    public void Encumbered_and_a_buff_have_no_list()
    {
        var map = CuresLogic.Build(TodayItems.Append(Item(1, "1", "0", "", "1300")), TodayBuffs);

        Assert.False(map.HasLinks(4));
        Assert.False(map.HasLinks(1300));
        Assert.Empty(map.CuresOf(1300));
        Assert.False(map.HasLinks(1129));
    }

    private static Func<int, int> Totals(params (int itemId, int total)[] totals)
    {
        var map = totals.ToDictionary(t => t.itemId, t => t.total);
        return id => map.TryGetValue(id, out var n) ? n : 0;
    }

    private static (int, int)[] Pairs(IEnumerable<CuresLogic.Line> lines) =>
        lines.Select(l => (l.ItemId, l.Total)).ToArray();

    [Fact]
    public void The_lines_of_Stomachache_with_2_Painkillers_at_home()
    {
        var lines = CuresLogic.LinesOf(Today(), 1102, Totals((2408, 2)));

        Assert.Equal(new[] { (2408, 2), (2413, 0) }, Pairs(lines.Cures));
        Assert.Equal(new[] { (2916, 0) }, Pairs(lines.Suppressors));
    }

    [Fact]
    public void The_items_with_a_total_above_0_come_first_then_each_group_by_item_id()
    {
        var lines = CuresLogic.LinesOf(Today(), 1111, Totals((2401, 1)));

        Assert.Equal(new[] { (2401, 1), (2400, 0), (2413, 0) }, Pairs(lines.Cures));
    }

    [Fact]
    public void The_suppressors_sort_the_same_way()
    {
        var lines = CuresLogic.LinesOf(Today(), 1121, Totals((2406, 3)));

        Assert.Empty(lines.Cures);
        Assert.Equal(new[] { (2406, 3), (2405, 0) }, Pairs(lines.Suppressors));
    }

    // A made-up map with cure items 1..cures and suppressor items 101..(100 + suppressors) of debuff 1102.
    private static CuresLogic.Map Many(int cures, int suppressors)
    {
        var items = new List<CuresLogic.ItemIn>();
        for (int i = 1; i <= cures; i++) items.Add(Item(i, "1", "0", "", "1102"));
        for (int i = 101; i <= 100 + suppressors; i++) items.Add(Item(i, "1", "0", "1129", ""));
        return CuresLogic.Build(items, TodayBuffs);
    }

    [Fact]
    public void At_most_10_lines_with_the_cures_first()
    {
        var lines = CuresLogic.LinesOf(Many(8, 5), 1102, Totals((103, 1)));

        Assert.Equal(8, lines.Cures.Count);
        Assert.Equal(new[] { (103, 1), (101, 0) }, Pairs(lines.Suppressors));
    }

    [Fact]
    public void More_than_10_cures_leave_no_line_for_the_suppressors_and_keep_the_items_at_home()
    {
        var lines = CuresLogic.LinesOf(Many(12, 2), 1102, Totals((12, 4), (11, 1)));

        Assert.Equal(new[] { 11, 12, 1, 2, 3, 4, 5, 6, 7, 8 }, lines.Cures.Select(l => l.ItemId).ToArray());
        Assert.Empty(lines.Suppressors);
    }

    [Fact]
    public void A_debuff_with_no_link_has_no_lines()
    {
        var lines = CuresLogic.LinesOf(Today(), 4, Totals((2408, 2)));

        Assert.Empty(lines.Cures);
        Assert.Empty(lines.Suppressors);
    }

    [Fact]
    public void The_item_pool_is_read_only_while_a_debuff_of_the_buff_list_has_a_link_and_both_switches_are_on()
    {
        var map = Today();

        Assert.True(CuresLogic.NeedsPool(map, new[] { 1300, 1102 }, cures: true, extended: true));
        Assert.False(CuresLogic.NeedsPool(map, new[] { 1300, 4 }, cures: true, extended: true));
        Assert.False(CuresLogic.NeedsPool(map, new int[0], cures: true, extended: true));
        Assert.False(CuresLogic.NeedsPool(map, new[] { 1102 }, cures: false, extended: true));
        Assert.False(CuresLogic.NeedsPool(map, new[] { 1102 }, cures: true, extended: false));
    }

    [Fact]
    public void The_counts_of_the_map_name_the_debuffs_with_a_link_and_their_items()
    {
        var map = Today();

        Assert.True(map.HasLinks(1102));
        Assert.True(map.HasLinks(1121));
        // Each suppressible debuff of the rows has Morphine Injection; Encumbered, Disgusted, and Wish Unfulfilled
        // have no link.
        Assert.Equal(15, map.DebuffCount);
        // Each med of the rows but Anxiety Meds, which adds a buff that suppresses nothing.
        Assert.Equal(9, map.ItemCount);
    }
}
