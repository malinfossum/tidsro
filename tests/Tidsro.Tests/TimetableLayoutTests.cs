using Tidsro.Models;
using Xunit;

namespace Tidsro.Tests;

public class TimetableLayoutTests
{
    // 2026-01-01 is a Thursday. Jan 5 is a Monday.
    private static DateTimeOffset At(int day, int hour, int minute) =>
        new(2026, 1, day, hour, minute, 0, TimeSpan.Zero);

    private static TimerItem Recurring(int hour, int minute, Weekdays days,
        string? label = "Class", bool enabled = true) => new()
    {
        Label = label,
        TriggerType = TriggerType.Recurring,
        RecurringDays = days,
        EndsAt = At(1, hour, minute),
        IsEnabled = enabled,
    };

    [Fact]
    public void Empty_input_is_flagged_empty()
    {
        var week = TimetableLayout.Build(Array.Empty<TimerItem>(), At(1, 9, 0));
        Assert.True(week.IsEmpty);
        Assert.Empty(week.Slots);
    }

    [Fact]
    public void Null_input_is_flagged_empty_and_does_not_throw()
    {
        var week = TimetableLayout.Build(null, At(1, 9, 0));
        Assert.True(week.IsEmpty);
    }

    [Fact]
    public void Span_pads_an_hour_each_side_of_the_only_alarm()
    {
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon) }, At(1, 9, 0));
        // 09:00 -> 08:00..10:00 is only 2h, so the 6h minimum grows it symmetrically to 06:00..12:00
        Assert.Equal(6, week.Slots[0].Hour);
        Assert.Equal(0, week.Slots[0].Minute);
        Assert.Equal(12, week.Slots.Count);   // 6 hours / 30 min
    }

    [Fact]
    public void Span_covers_the_earliest_and_latest_alarm_with_padding()
    {
        var week = TimetableLayout.Build(
            new[] { Recurring(9, 0, Weekdays.Mon), Recurring(15, 0, Weekdays.Tue) }, At(1, 9, 0));
        // 08:00 through 16:00 is 8 hours = 16 slots; the LAST SLOT STARTS at 15:30, not at the span end.
        Assert.Equal(8, week.Slots[0].Hour);
        Assert.Equal(0, week.Slots[0].Minute);
        Assert.Equal(16, week.Slots.Count);
        Assert.Equal(15, week.Slots[^1].Hour);
        Assert.Equal(30, week.Slots[^1].Minute);
    }

    [Fact]
    public void Span_clamps_at_midnight_and_gives_the_length_back_at_the_other_end()
    {
        var week = TimetableLayout.Build(new[] { Recurring(0, 30, Weekdays.Mon) }, At(1, 9, 0));
        Assert.Equal(0, week.Slots[0].Hour);
        Assert.Equal(0, week.Slots[0].Minute);
        Assert.Equal(12, week.Slots.Count);   // still a full 6 hours, all of it after midnight
    }

    [Fact]
    public void Span_clamps_at_the_end_of_the_day()
    {
        var week = TimetableLayout.Build(new[] { Recurring(23, 30, Weekdays.Mon) }, At(1, 9, 0));
        Assert.Equal(12, week.Slots.Count);
        Assert.Equal(18, week.Slots[0].Hour);   // 24:00 minus 6 hours
    }

    [Fact]
    public void A_full_day_span_is_forty_eight_slots()
    {
        var week = TimetableLayout.Build(
            new[] { Recurring(0, 30, Weekdays.Mon), Recurring(23, 30, Weekdays.Mon) }, At(1, 9, 0));
        Assert.Equal(48, week.Slots.Count);
        Assert.True(week.LabelWholeHoursOnly);
    }

    [Fact]
    public void An_alarm_appears_in_every_day_it_repeats_on()
    {
        var days = Weekdays.Mon | Weekdays.Wed | Weekdays.Fri;
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, days) }, At(1, 9, 0));
        Assert.Equal(3, week.Days.Count(d => d.Entries.Count == 1));
        Assert.Single(week.Days.Single(d => d.Day == Weekdays.Wed).Entries);
        Assert.Empty(week.Days.Single(d => d.Day == Weekdays.Tue).Entries);
    }

    [Fact]
    public void Days_run_monday_first()
    {
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon) }, At(1, 9, 0));
        Assert.Equal(7, week.Days.Count);
        Assert.Equal(Weekdays.Mon, week.Days[0].Day);
        Assert.Equal("Monday", week.Days[0].Name);
        Assert.Equal(Weekdays.Sun, week.Days[6].Day);
    }

    [Fact]
    public void Today_follows_the_injected_clock()
    {
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon) }, At(1, 9, 0));
        Assert.True(week.Days.Single(d => d.Day == Weekdays.Thu).IsToday);   // Jan 1 2026 is a Thursday
        Assert.Single(week.Days, d => d.IsToday);
    }

    [Fact]
    public void Alarms_in_the_same_half_hour_share_a_slot()
    {
        var week = TimetableLayout.Build(
            new[] { Recurring(9, 0, Weekdays.Mon, "A"), Recurring(9, 29, Weekdays.Mon, "B") }, At(1, 9, 0));
        var entries = week.Days.Single(d => d.Day == Weekdays.Mon).Entries;
        Assert.Equal(2, entries.Count);
        Assert.Equal(entries[0].SlotIndex, entries[1].SlotIndex);
    }

    [Fact]
    public void A_half_hour_boundary_starts_a_new_slot()
    {
        var week = TimetableLayout.Build(
            new[] { Recurring(9, 29, Weekdays.Mon, "A"), Recurring(9, 30, Weekdays.Mon, "B") }, At(1, 9, 0));
        var entries = week.Days.Single(d => d.Day == Weekdays.Mon).Entries;
        Assert.NotEqual(entries[0].SlotIndex, entries[1].SlotIndex);
    }

    [Fact]
    public void Entries_sort_by_minute_then_label()
    {
        var week = TimetableLayout.Build(new[]
        {
            Recurring(9, 15, Weekdays.Mon, "Zebra"),
            Recurring(9, 0, Weekdays.Mon, "Banana"),
            Recurring(9, 15, Weekdays.Mon, "Apple"),
        }, At(1, 9, 0));
        var entries = week.Days.Single(d => d.Day == Weekdays.Mon).Entries;
        Assert.Equal(new[] { "Banana", "Apple", "Zebra" }, entries.Select(e => e.Label));
    }

    [Fact]
    public void Disabled_alarms_are_present_and_flagged()
    {
        var week = TimetableLayout.Build(
            new[] { Recurring(9, 0, Weekdays.Mon, "Off one", enabled: false) }, At(1, 9, 0));
        var entry = week.Days.Single(d => d.Day == Weekdays.Mon).Entries.Single();
        Assert.False(entry.IsEnabled);
    }

    [Fact]
    public void One_shots_and_countdowns_are_excluded()
    {
        var oneShot = new TimerItem { TriggerType = TriggerType.ClockTime, EndsAt = At(1, 9, 0) };
        var countdown = new TimerItem { TriggerType = TriggerType.Countdown, EndsAt = At(1, 9, 0) };
        var week = TimetableLayout.Build(new[] { oneShot, countdown }, At(1, 9, 0));
        Assert.True(week.IsEmpty);
    }

    [Fact]
    public void Malformed_alarms_are_skipped_and_the_rest_still_render()
    {
        var unknownBitsOnly = Recurring(9, 0, (Weekdays)128);
        var noEndsAt = new TimerItem { RecurringDays = Weekdays.Mon, EndsAt = null };
        var good = Recurring(10, 0, Weekdays.Tue, "Survivor");

        var week = TimetableLayout.Build(new[] { unknownBitsOnly, noEndsAt, good }, At(1, 9, 0));

        Assert.False(week.IsEmpty);
        Assert.Equal("Survivor", week.Days.Single(d => d.Day == Weekdays.Tue).Entries.Single().Label);
        Assert.Empty(week.Days.Single(d => d.Day == Weekdays.Mon).Entries);
    }

    [Fact]
    public void A_null_item_in_the_sequence_does_not_throw()
    {
        var week = TimetableLayout.Build(new TimerItem?[] { null, Recurring(9, 0, Weekdays.Mon) }!, At(1, 9, 0));
        Assert.False(week.IsEmpty);
    }

    [Fact]
    public void Slot_labels_read_as_wall_clock_times()
    {
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon) }, At(1, 9, 0));
        Assert.Equal("06:00", week.Slots[0].Label);
        Assert.True(week.Slots[0].IsWholeHour);
        Assert.False(week.Slots[1].IsWholeHour);
    }

    [Fact]
    public void Entry_time_text_is_zero_padded()
    {
        var week = TimetableLayout.Build(new[] { Recurring(9, 5, Weekdays.Mon) }, At(1, 9, 0));
        Assert.Equal("09:05", week.Days.Single(d => d.Day == Weekdays.Mon).Entries.Single().TimeText);
    }

    [Fact]
    public void An_enabled_entry_announces_label_weekday_and_time()
    {
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon, "Code class") }, At(1, 9, 0));
        var entry = week.Days.Single(d => d.Day == Weekdays.Mon).Entries.Single();
        Assert.Equal("Code class, Monday, 09:00", entry.AccessibleName);
    }

    [Fact]
    public void A_disabled_entry_announces_that_it_is_off()
    {
        var week = TimetableLayout.Build(
            new[] { Recurring(9, 0, Weekdays.Mon, "Code class", enabled: false) }, At(1, 9, 0));
        var entry = week.Days.Single(d => d.Day == Weekdays.Mon).Entries.Single();
        Assert.Equal("Code class, Monday, 09:00, off", entry.AccessibleName);
    }

    [Fact]
    public void The_same_alarm_carries_its_own_weekday_in_each_column()
    {
        var week = TimetableLayout.Build(
            new[] { Recurring(9, 0, Weekdays.Mon | Weekdays.Wed, "Code class") }, At(1, 9, 0));
        Assert.Equal("Code class, Monday, 09:00",
            week.Days.Single(d => d.Day == Weekdays.Mon).Entries.Single().AccessibleName);
        Assert.Equal("Code class, Wednesday, 09:00",
            week.Days.Single(d => d.Day == Weekdays.Wed).Entries.Single().AccessibleName);
    }
}
