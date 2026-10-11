using System;
using StatsUI;
using Xunit;

public class PushScheduleTests
{
    // A build function that counts its calls and gives the JSON that the test sets.
    private sealed class Data
    {
        public string Json = "{\"a\":1}";
        public int Calls;
        public string Build() { Calls++; return Json; }
    }

    private static PushSchedule.Step Push(string json) => new PushSchedule.Step(PushSchedule.Kind.Push, json);
    private static readonly PushSchedule.Step None = new PushSchedule.Step(PushSchedule.Kind.None, null);
    private static readonly PushSchedule.Step Check = new PushSchedule.Step(PushSchedule.Kind.Check, null);

    [Fact]
    public void A_dirty_tick_pushes_the_new_json()
    {
        var s = new PushSchedule();
        var d = new Data();

        Assert.Equal(Push("{\"a\":1}"), s.Tick(10f, true, d.Build));
        Assert.Equal(1, d.Calls);
    }

    [Fact]
    public void A_dirty_tick_with_the_same_json_does_not_push()
    {
        var s = new PushSchedule();
        var d = new Data();
        s.Tick(10f, true, d.Build);

        Assert.Equal(None, s.Tick(10.2f, true, d.Build));
        Assert.Equal(2, d.Calls);
    }

    [Fact]
    public void A_dirty_tick_with_changed_json_pushes_at_once()
    {
        var s = new PushSchedule();
        var d = new Data();
        s.Tick(10f, true, d.Build);
        d.Json = "{\"a\":2}";

        Assert.Equal(Push("{\"a\":2}"), s.Tick(10.1f, true, d.Build));
    }

    [Fact]
    public void A_tick_with_nothing_dirty_does_not_build()
    {
        var s = new PushSchedule();
        var d = new Data();
        s.Tick(10f, true, d.Build);

        s.Tick(10.5f, false, d.Build);
        s.Tick(12f, false, d.Build);

        Assert.Equal(1, d.Calls);
    }

    [Fact]
    public void The_check_comes_one_real_second_after_the_push_and_then_each_second()
    {
        var s = new PushSchedule();
        var d = new Data();
        s.Tick(10f, true, d.Build);

        Assert.Equal(None, s.Tick(10.9f, false, d.Build));
        Assert.Equal(Check, s.Tick(11f, false, d.Build));
        Assert.Equal(None, s.Tick(11.5f, false, d.Build));
        Assert.Equal(Check, s.Tick(12.1f, false, d.Build));
    }

    [Fact]
    public void No_check_before_the_first_push()
    {
        var s = new PushSchedule();
        var d = new Data();

        Assert.Equal(None, s.Tick(10f, false, d.Build));
        Assert.Equal(None, s.Tick(20f, false, d.Build));
        Assert.Equal(0, d.Calls);
    }

    [Fact]
    public void After_ForgetLastPush_the_kept_json_goes_again_one_second_after_the_last_send()
    {
        var s = new PushSchedule();
        var d = new Data();
        s.Tick(10f, true, d.Build);
        s.ForgetLastPush();

        Assert.Equal(None, s.Tick(10.5f, false, d.Build));
        Assert.Equal(Push("{\"a\":1}"), s.Tick(11f, false, d.Build));
        Assert.Equal(1, d.Calls);
    }

    [Fact]
    public void After_a_failed_check_the_push_comes_one_second_after_the_check()
    {
        var s = new PushSchedule();
        var d = new Data();
        s.Tick(10f, true, d.Build);
        Assert.Equal(Check, s.Tick(11f, false, d.Build));
        s.ForgetLastPush();

        Assert.Equal(None, s.Tick(11.5f, false, d.Build));
        Assert.Equal(Push("{\"a\":1}"), s.Tick(12f, false, d.Build));
    }

    [Fact]
    public void While_the_retry_waits_a_dirty_tick_does_not_push_and_the_retry_sends_the_new_json()
    {
        var s = new PushSchedule();
        var d = new Data();
        s.Tick(10f, true, d.Build);
        s.ForgetLastPush();
        d.Json = "{\"a\":2}";

        Assert.Equal(None, s.Tick(10.1f, true, d.Build));
        Assert.Equal(None, s.Tick(10.2f, true, d.Build));
        Assert.Equal(Push("{\"a\":2}"), s.Tick(11f, false, d.Build));
    }

    [Fact]
    public void In_the_retry_case_nothing_goes_more_often_than_once_each_second()
    {
        var s = new PushSchedule();
        var d = new Data();
        int sends = 0;
        // One tick each 1/60 s for 5 s. Each send fails (no CoreUI1 frame), and each frame is dirty.
        for (int frame = 0; frame <= 300; frame++)
        {
            d.Json = "{\"frame\":" + frame + "}";
            var step = s.Tick(10f + frame / 60f, true, d.Build);
            if (step.Kind == PushSchedule.Kind.None) continue;
            sends++;
            s.ForgetLastPush();
        }

        Assert.InRange(sends, 5, 6);
    }

    [Fact]
    public void A_build_that_throws_keeps_the_change_and_builds_again_one_second_later()
    {
        var s = new PushSchedule();
        int calls = 0;
        string Failing() { calls++; throw new InvalidOperationException("no world"); }

        Assert.Throws<InvalidOperationException>(() => s.Tick(10f, true, Failing));
        // No new trigger: the change stays pending, and the build waits one real second.
        Assert.Equal(None, s.Tick(10.5f, false, Failing));
        Assert.Equal(1, calls);
        Assert.Throws<InvalidOperationException>(() => s.Tick(11f, false, Failing));
        Assert.Equal(2, calls);

        var d = new Data();
        Assert.Equal(Push("{\"a\":1}"), s.Tick(12f, false, d.Build));
    }

    [Fact]
    public void A_retry_whose_build_throws_is_kept_for_the_next_second()
    {
        var s = new PushSchedule();
        var d = new Data();
        s.Tick(10f, true, d.Build);
        s.ForgetLastPush();

        Assert.Throws<InvalidOperationException>(() => s.Tick(11f, true, () => throw new InvalidOperationException("no world")));
        Assert.Equal(None, s.Tick(11.5f, false, d.Build));
        d.Json = "{\"a\":2}";
        Assert.Equal(Push("{\"a\":2}"), s.Tick(12f, false, d.Build));
    }

    [Theory]
    [InlineData("ok 3 2 1500", true, 3, 2, 1500)]
    [InlineData("ok 3 1", true, 3, 1, 0)]
    [InlineData("ok", true, 0, 0, 0)]
    [InlineData("no script", false, 0, 0, 0)]
    [InlineData("no CoreUI1 frame", false, 0, 0, 0)]
    [InlineData("no observer", false, 0, 0, 0)]
    [InlineData(null, false, 0, 0, 0)]
    public void IsOk_reads_the_check_result(string result, bool ok, int runs, int passes, int passMicros)
    {
        Assert.Equal((ok, runs, passes, passMicros), PushSchedule.IsOk(result));
    }

    [Fact]
    public void ObserverSum_gives_the_sums_once_each_real_minute()
    {
        var s = new PushSchedule();

        Assert.Null(s.ObserverSum(10f, 3, 1, 400));
        Assert.Null(s.ObserverSum(40f, 4, 0, 0));
        Assert.Equal((7, 1, 400), s.ObserverSum(70f, 0, 0, 0));
        Assert.Null(s.ObserverSum(71f, 2, 2, 900));
        Assert.Equal((2, 2, 900), s.ObserverSum(130f, 0, 0, 0));
    }
}

// The answer of check() with a missing part of the page: the page has the script and the observer, so it is ok, and
// the missing part goes to the log.
public class CheckAnswerTests
{
    [Fact]
    public void An_ok_with_missing_parts_is_ok_and_names_the_parts()
    {
        var (ok, runs, passes, _) = PushSchedule.IsOk("ok 3 1; missing: bag button");

        Assert.True(ok);
        Assert.Equal(3, runs);
        Assert.Equal(1, passes);
        Assert.Equal(new[] { "bag button" }, PushSchedule.MissingParts("ok 3 1; missing: bag button"));
        Assert.Empty(PushSchedule.MissingParts("ok 3 1"));
    }

    [Fact]
    public void Each_missing_part_is_one_item_also_after_an_install()
    {
        Assert.Equal(new[] { ".status-row", ".progress-text", ".attr-name" },
            PushSchedule.MissingParts("installed; missing: .status-row, .progress-text, .attr-name"));
        Assert.Empty(PushSchedule.MissingParts(null));
    }
}

// The parts of the page data: a mark of one part reads only that part again, and the schedule pushes the joined
// data once.
public class DataPartsTests
{
    private sealed class Game
    {
        public readonly Dictionary<DataParts.Part, string> Json = new Dictionary<DataParts.Part, string>
        {
            [DataParts.Part.Stats] = "[1]",
            [DataParts.Part.Buffs] = "[2]",
            [DataParts.Part.Weight] = "3",
            [DataParts.Part.Texts] = "{}",
        };
        public readonly List<DataParts.Part> Reads = new List<DataParts.Part>();
        public string Read(DataParts.Part part) { Reads.Add(part); return Json[part]; }
    }

    private static string Join(IReadOnlyList<string> parts) => string.Join("|", parts);

    [Fact]
    public void The_first_build_reads_each_part()
    {
        var game = new Game();
        var parts = new DataParts();

        Assert.True(parts.AnyDirty);
        Assert.Equal("[1]|[2]|3|{}", Join(parts.Read(game.Read)));
        Assert.Equal(4, game.Reads.Count);
        Assert.False(parts.AnyDirty);
    }

    [Fact]
    public void A_mark_of_one_part_makes_the_next_push_carry_its_new_JSON_and_reads_only_that_part()
    {
        var game = new Game();
        var parts = new DataParts();
        var schedule = new PushSchedule();
        schedule.Tick(0f, parts.AnyDirty, () => Join(parts.Read(game.Read)));
        game.Reads.Clear();

        game.Json[DataParts.Part.Weight] = "4";
        parts.Mark(DataParts.Part.Weight);
        var step = schedule.Tick(0.1f, parts.AnyDirty, () => Join(parts.Read(game.Read)));

        Assert.Equal(new PushSchedule.Step(PushSchedule.Kind.Push, "[1]|[2]|4|{}"), step);
        Assert.Equal(new[] { DataParts.Part.Weight }, game.Reads);
    }

    [Fact]
    public void A_mark_with_the_same_JSON_as_the_confirmed_push_sends_nothing()
    {
        var game = new Game();
        var parts = new DataParts();
        var schedule = new PushSchedule();
        schedule.Tick(0f, parts.AnyDirty, () => Join(parts.Read(game.Read)));

        parts.Mark(DataParts.Part.Stats);
        var step = schedule.Tick(0.1f, parts.AnyDirty, () => Join(parts.Read(game.Read)));

        Assert.Equal(PushSchedule.Kind.None, step.Kind);
    }

    [Fact]
    public void Several_marks_in_one_frame_give_one_push()
    {
        var game = new Game();
        var parts = new DataParts();
        var schedule = new PushSchedule();
        schedule.Tick(0f, parts.AnyDirty, () => Join(parts.Read(game.Read)));

        game.Json[DataParts.Part.Stats] = "[5]";
        game.Json[DataParts.Part.Buffs] = "[6]";
        parts.Mark(DataParts.Part.Stats);
        parts.Mark(DataParts.Part.Buffs);
        parts.Mark(DataParts.Part.Stats);
        var first = schedule.Tick(0.1f, parts.AnyDirty, () => Join(parts.Read(game.Read)));
        var second = schedule.Tick(0.2f, parts.AnyDirty, () => Join(parts.Read(game.Read)));

        Assert.Equal(new PushSchedule.Step(PushSchedule.Kind.Push, "[5]|[6]|3|{}"), first);
        Assert.Equal(PushSchedule.Kind.None, second.Kind);
    }

    [Fact]
    public void A_read_that_throws_keeps_its_mark_for_the_next_build()
    {
        var parts = new DataParts();
        Assert.Throws<InvalidOperationException>(() => parts.Read(_ => throw new InvalidOperationException("no save")));

        Assert.True(parts.AnyDirty);
        Assert.Equal("[1]|[2]|3|{}", Join(parts.Read(new Game().Read)));
    }
}
