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

    [Fact]
    public void A_day_column_announces_its_name_and_load()
    {
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon) }, At(1, 9, 0));
        Assert.Equal("Monday, 1 alarm", week.Days.Single(d => d.Day == Weekdays.Mon).AccessibleName);
    }

    [Fact]
    public void Today_is_named_as_today()
    {
        // Jan 1 2026 is a Thursday, and nothing repeats on it here.
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon) }, At(1, 9, 0));
        Assert.Equal("Thursday, today, no alarms", week.Days.Single(d => d.Day == Weekdays.Thu).AccessibleName);
    }

    [Fact]
    public void Two_alarms_pluralise()
    {
        var week = TimetableLayout.Build(
            new[] { Recurring(9, 0, Weekdays.Mon, "A"), Recurring(11, 0, Weekdays.Mon, "B") }, At(1, 9, 0));
        Assert.Equal("Monday, 2 alarms", week.Days.Single(d => d.Day == Weekdays.Mon).AccessibleName);
    }

    // ── Rows: the row-major shape the wide grid draws ──────────────────────
    // Days is column-major and drives the agenda; Rows is the same week turned inside out, one row
    // per slot, and it is what makes the grid's gutter and its seven columns share a row structure
    // instead of two collections hoping to agree on a pixel height.

    [Fact]
    public void There_is_one_row_per_slot_and_seven_cells_per_row()
    {
        var week = TimetableLayout.Build(
            new[] { Recurring(7, 0, Weekdays.Mon), Recurring(15, 0, Weekdays.Mon) }, At(1, 9, 0));

        Assert.Equal(week.Slots.Count, week.Rows.Count);
        Assert.All(week.Rows, r => Assert.Equal(7, r.Cells.Count));
    }

    [Fact]
    public void A_row_carries_the_slot_it_was_built_from()
    {
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon) }, At(1, 9, 0));

        for (var i = 0; i < week.Rows.Count; i++) Assert.Same(week.Slots[i], week.Rows[i].Slot);
    }

    [Fact]
    public void Cells_are_Monday_first_like_the_columns()
    {
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon) }, At(1, 9, 0));

        Assert.Equal(
            new[] { Weekdays.Mon, Weekdays.Tue, Weekdays.Wed, Weekdays.Thu, Weekdays.Fri, Weekdays.Sat, Weekdays.Sun },
            week.Rows[0].Cells.Select(c => c.Day));
    }

    [Fact]
    public void An_entry_lands_in_the_row_its_slot_index_names()
    {
        // 07:00 and 15:00 give a 06:00-16:00 span, so the two alarms belong eight hours apart:
        // row 2 and row 18. Drawn in list order they would have stacked as rows 0 and 1.
        var week = TimetableLayout.Build(
            new[] { Recurring(7, 0, Weekdays.Mon, "Gym"), Recurring(15, 0, Weekdays.Mon, "Code class") },
            At(1, 9, 0));

        var monday = week.Days.Single(d => d.Day == Weekdays.Mon);
        Assert.Equal(new[] { 2, 18 }, monday.Entries.Select(e => e.SlotIndex));
        Assert.Equal("Gym", week.Rows[2].Cells[0].Entries.Single().DisplayLabel);
        Assert.Equal("Code class", week.Rows[18].Cells[0].Entries.Single().DisplayLabel);
        Assert.All(
            week.Rows.Where(r => r.Slot.Index is not 2 and not 18),
            r => Assert.Empty(r.Cells[0].Entries));
    }

    [Fact]
    public void Each_weekday_keeps_its_own_column_and_its_own_row()
    {
        // Gym 07:00 Mon, Code class 15:00 Mon, Standup 09:00 Tue — the case where a column-stacked
        // grid drew Tuesday's 09:00 level with Monday's 07:00 and called them the same moment.
        var week = TimetableLayout.Build(new[]
        {
            Recurring(7, 0, Weekdays.Mon, "Gym"),
            Recurring(15, 0, Weekdays.Mon, "Code class"),
            Recurring(9, 0, Weekdays.Tue, "Standup"),
        }, At(1, 9, 0));

        Assert.Equal("Standup", week.Rows[6].Cells[1].Entries.Single().DisplayLabel);   // Tue 09:00
        Assert.Empty(week.Rows[6].Cells[0].Entries);                                    // Mon is free at 09:00
        Assert.Empty(week.Rows[2].Cells[1].Entries);                                    // Tue is free at 07:00
    }

    [Fact]
    public void Two_alarms_in_one_half_hour_share_a_row()
    {
        var week = TimetableLayout.Build(
            new[] { Recurring(9, 0, Weekdays.Mon, "A"), Recurring(9, 29, Weekdays.Mon, "B") }, At(1, 9, 0));

        var occupied = week.Rows.Where(r => r.Cells[0].Entries.Count > 0).ToList();
        Assert.Single(occupied);
        Assert.Equal(new[] { "A", "B" }, occupied[0].Cells[0].Entries.Select(e => e.DisplayLabel));
    }

    [Fact]
    public void An_empty_week_has_no_rows()
    {
        var week = TimetableLayout.Build(Array.Empty<TimerItem>(), At(1, 9, 0));

        Assert.Empty(week.Rows);
    }

    // ── An alarm with no label ─────────────────────────────────────────────

    [Fact]
    public void An_unlabelled_alarm_announces_with_a_stand_in_not_a_leading_comma()
    {
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon, label: null) }, At(1, 9, 0));

        var entry = week.Days.Single(d => d.Day == Weekdays.Mon).Entries.Single();
        Assert.Equal("No label", entry.DisplayLabel);
        Assert.Equal("No label, Monday, 09:00", entry.AccessibleName);
    }

    [Fact]
    public void A_whitespace_label_gets_the_same_stand_in_including_when_off()
    {
        var week = TimetableLayout.Build(
            new[] { Recurring(9, 0, Weekdays.Mon, label: "   ", enabled: false) }, At(1, 9, 0));

        var entry = week.Days.Single(d => d.Day == Weekdays.Mon).Entries.Single();
        Assert.Equal("No label", entry.DisplayLabel);
        Assert.Equal("No label, Monday, 09:00, off", entry.AccessibleName);
    }

    // ── Which days the agenda draws ────────────────────────────────────────

    [Fact]
    public void A_day_with_nothing_on_it_is_left_out_of_the_agenda()
    {
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon) }, At(1, 9, 0));

        Assert.True(week.Days.Single(d => d.Day == Weekdays.Mon).ShowInAgenda);
        Assert.False(week.Days.Single(d => d.Day == Weekdays.Tue).ShowInAgenda);
    }

    [Fact]
    public void A_free_today_still_shows_in_the_agenda_and_says_so()
    {
        // Jan 1 2026 is a Thursday and nothing repeats on it here.
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon) }, At(1, 9, 0));

        var today = week.Days.Single(d => d.Day == Weekdays.Thu);
        Assert.True(today.IsToday);
        Assert.True(today.ShowInAgenda);
        Assert.True(today.IsFreeToday);
    }

    [Fact]
    public void A_today_with_alarms_on_it_needs_no_nothing_today_line()
    {
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Thu) }, At(1, 9, 0));

        var today = week.Days.Single(d => d.Day == Weekdays.Thu);
        Assert.True(today.ShowInAgenda);
        Assert.False(today.IsFreeToday);
    }

    [Fact]
    public void A_row_names_itself_by_its_time_if_anything_ever_asks()
    {
        // Not a display value: the grid names nothing per row. But a WPF item container that does
        // acquire an automation peer falls back to ToString(), and a record would read out the
        // whole row.
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon) }, At(1, 9, 0));

        Assert.Equal("06:00", week.Rows[0].ToString());
        Assert.Equal("06:30", week.Rows[1].ToString());
    }
}
