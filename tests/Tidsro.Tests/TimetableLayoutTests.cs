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
    public void Every_row_has_one_cell_per_column()
    {
        var week = TimetableLayout.Build(
            new[] { Recurring(7, 0, Weekdays.Mon), Recurring(15, 0, Weekdays.Mon) }, At(1, 9, 0));

        Assert.All(week.Rows, r => Assert.Equal(week.GridDays.Count, r.Cells.Count));
    }

    [Fact]
    public void A_row_carries_the_slot_it_was_built_from()
    {
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon) }, At(1, 9, 0));

        foreach (var row in week.Rows) Assert.Same(week.Slots[row.Slot.Index], row.Slot);
    }

    [Fact]
    public void Cells_are_Monday_first_like_the_columns()
    {
        // A Sunday alarm, so all seven columns are drawn and the full order is under test.
        var week = TimetableLayout.Build(
            new[] { Recurring(9, 0, Weekdays.Mon), Recurring(9, 0, Weekdays.Sun) }, At(1, 9, 0));

        Assert.Equal(
            new[] { Weekdays.Mon, Weekdays.Tue, Weekdays.Wed, Weekdays.Thu, Weekdays.Fri, Weekdays.Sat, Weekdays.Sun },
            week.Rows[0].Cells.Select(c => c.Day));
    }

    [Fact]
    public void A_row_has_one_cell_per_column_in_the_columns_own_order()
    {
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon) }, At(1, 9, 0));

        Assert.Equal(week.GridDays.Select(d => d.Day), week.Rows.Single().Cells.Select(c => c.Day));
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
        Assert.Equal("Gym", week.Rows.Single(r => r.Slot.Index == 2).Cells[0].Entries.Single().DisplayLabel);
        Assert.Equal("Code class", week.Rows.Single(r => r.Slot.Index == 18).Cells[0].Entries.Single().DisplayLabel);
        Assert.Equal(2, week.Rows.Count);
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

        var nine = week.Rows.Single(r => r.Slot.Index == 6);
        var seven = week.Rows.Single(r => r.Slot.Index == 2);
        Assert.Equal("Standup", nine.Cells[1].Entries.Single().DisplayLabel);   // Tue 09:00
        Assert.Empty(nine.Cells[0].Entries);                                    // Mon is free at 09:00
        Assert.Empty(seven.Cells[1].Entries);                                   // Tue is free at 07:00
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

    // ── An entry that does not sit on a slot boundary ──────────────────────
    // The gutter names the SLOT, so it can only speak for an entry that starts when the slot does.
    // A 12:15 alarm lives in the 12:00 band and would otherwise be read off the gutter as 12:00.

    [Fact]
    public void An_entry_starting_with_its_slot_is_on_the_boundary()
    {
        var week = TimetableLayout.Build(
            new[] { Recurring(9, 0, Weekdays.Mon), Recurring(9, 30, Weekdays.Mon) }, At(1, 9, 0));

        var monday = week.Days.Single(d => d.Day == Weekdays.Mon);
        Assert.All(monday.Entries, e => Assert.True(e.IsOnSlotBoundary));
    }

    [Fact]
    public void An_entry_inside_a_slot_is_not_on_the_boundary()
    {
        var week = TimetableLayout.Build(new[] { Recurring(12, 15, Weekdays.Mon) }, At(1, 9, 0));

        var entry = week.Days.Single(d => d.Day == Weekdays.Mon).Entries.Single();
        Assert.False(entry.IsOnSlotBoundary);
        Assert.Equal("12:15", entry.TimeText);
    }

    // ── Rows are the occupied slots, and nothing else ──────────────────────
    // An empty half hour gets no row: the week lists the times it has something on, evenly spaced,
    // the way a printed timetable does. The vertical scale is deliberately not proportional — 07:00
    // and 15:00 sit next to each other when nothing falls between them. The earlier shape collapsed
    // a run of empties into one thin "7h 30m" band; four of those broke the row rhythm and the grid
    // stopped reading as a timetable.

    [Fact]
    public void An_empty_half_hour_between_entries_gets_no_row()
    {
        var week = TimetableLayout.Build(
            new[] { Recurring(7, 0, Weekdays.Mon, "Gym"), Recurring(15, 0, Weekdays.Mon, "Code class") },
            At(1, 9, 0));

        Assert.Equal(new[] { "07:00", "15:00" }, week.Rows.Select(r => r.Slot.Label));
    }

    [Fact]
    public void Empty_slots_before_the_first_entry_and_after_the_last_are_dropped()
    {
        // The span pads an hour each side and grows to a six-hour minimum, so a lone alarm sits in
        // a mostly empty span — none of which earns a row.
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon) }, At(1, 9, 0));

        Assert.Single(week.Rows);
        Assert.Equal("09:00", week.Rows[0].Slot.Label);
    }

    [Fact]
    public void Entries_in_adjacent_slots_each_keep_their_own_row()
    {
        var week = TimetableLayout.Build(
            new[] { Recurring(9, 0, Weekdays.Mon), Recurring(9, 30, Weekdays.Mon) }, At(1, 9, 0));

        Assert.Equal(new[] { "09:00", "09:30" }, week.Rows.Select(r => r.Slot.Label));
    }

    [Fact]
    public void An_empty_week_has_no_rows()
    {
        var week = TimetableLayout.Build(Array.Empty<TimerItem>(), At(1, 9, 0));

        Assert.Empty(week.Rows);
    }

    // ── The weekend only gets columns when it has something on it ──────────
    // Two empty columns spend two sevenths of the grid saying nothing. When neither weekend day has
    // an alarm the grid drops both and says so in one muted line instead — but never when the
    // weekend is where you are standing, which is the same exception the agenda already makes.

    [Fact]
    public void A_weekday_only_week_gets_no_weekend_columns()
    {
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon | Weekdays.Fri) }, At(1, 9, 0));

        Assert.Equal(
            new[] { Weekdays.Mon, Weekdays.Tue, Weekdays.Wed, Weekdays.Thu, Weekdays.Fri },
            week.GridDays.Select(d => d.Day));
        Assert.True(week.HidesWeekend);
    }

    [Fact]
    public void Dropping_the_weekend_columns_leaves_the_agenda_all_seven_days()
    {
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon) }, At(1, 9, 0));

        Assert.Equal(5, week.GridDays.Count);
        Assert.Equal(7, week.Days.Count);
    }

    [Fact]
    public void One_alarm_on_a_weekend_day_brings_both_columns_back()
    {
        // Saturday alone brings Sunday with it: the weekend is drawn as a pair, so a lone Sunday
        // column never appears at the end of a row on its own.
        var week = TimetableLayout.Build(
            new[] { Recurring(9, 0, Weekdays.Mon), Recurring(11, 0, Weekdays.Sat) }, At(1, 9, 0));

        Assert.Equal(7, week.GridDays.Count);
        Assert.False(week.HidesWeekend);
    }

    [Fact]
    public void The_weekend_keeps_its_columns_when_today_is_a_weekend_day()
    {
        // 2026-01-03 is a Saturday. A grid that silently omits the day you are standing in is the
        // defect TimetableDay.ShowInAgenda already avoids for the narrow rendering.
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon) }, At(3, 9, 0));

        Assert.Equal(7, week.GridDays.Count);
        Assert.False(week.HidesWeekend);
    }

    [Fact]
    public void An_empty_week_hides_nothing_because_it_draws_no_grid()
    {
        var week = TimetableLayout.Build(Array.Empty<TimerItem>(), At(1, 9, 0));

        Assert.False(week.HidesWeekend);
    }

    // ── The gutter states the row's own time ───────────────────────────────
    // The gutter names the SLOT, a half-hour band, so a row of 12:15 alarms would be read off it as
    // 12:00. When every entry in the row starts at the same time the gutter states that time
    // instead — the common case for a week of recurring alarms, and it keeps the exact time out of
    // the cells, where a time plus a label does not fit a hundred-pixel column.

    [Fact]
    public void A_row_is_named_by_its_slot_when_the_entries_start_with_it()
    {
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon) }, At(1, 9, 0));

        Assert.Equal("09:00", week.Rows.Single().GutterLabel);
        Assert.False(week.Rows.Single().ShowsCellTimes);
    }

    [Fact]
    public void A_row_whose_entries_share_an_off_slot_time_is_named_by_that_time()
    {
        var week = TimetableLayout.Build(
            new[]
            {
                Recurring(12, 15, Weekdays.Mon, "Deep work"),
                Recurring(12, 15, Weekdays.Tue, "Deep work"),
            },
            At(1, 9, 0));

        var row = week.Rows.Single();
        Assert.Equal("12:15", row.GutterLabel);
        Assert.False(row.ShowsCellTimes);
    }

    [Fact]
    public void A_row_of_mixed_times_keeps_its_slot_label_and_lets_the_cells_speak()
    {
        var week = TimetableLayout.Build(
            new[] { Recurring(12, 0, Weekdays.Mon), Recurring(12, 15, Weekdays.Tue) }, At(1, 9, 0));

        var row = week.Rows.Single();
        Assert.Equal("12:00", row.GutterLabel);
        Assert.True(row.ShowsCellTimes);
    }

    [Fact]
    public void A_row_is_named_by_the_time_it_states_if_WPF_ever_asks()
    {
        var week = TimetableLayout.Build(new[] { Recurring(12, 15, Weekdays.Mon) }, At(1, 9, 0));

        Assert.Equal("12:15", week.Rows.Single().ToString());
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
        var week = TimetableLayout.Build(
            new[] { Recurring(7, 0, Weekdays.Mon), Recurring(15, 0, Weekdays.Mon) }, At(1, 9, 0));

        Assert.Equal("07:00", week.Rows[0].ToString());
        Assert.Equal("15:00", week.Rows[^1].ToString());
    }

    // ---- Blocks (phase 2) ----------------------------------------------------------------

    private static TimerItem Block(int hour, int minute, int endMinute, Weekdays days,
        string? label = "Lecture", bool enabled = true) => new()
    {
        Label = label,
        TriggerType = TriggerType.Recurring,
        RecurringDays = days,
        EndsAt = At(1, hour, minute),
        IsEnabled = enabled,
        EndMinute = endMinute,
    };

    [Fact]
    public void A_block_gives_a_row_to_every_slot_it_covers()
    {
        // 09:00-10:30 covers the 09:00, 09:30 and 10:00 bands.
        var week = TimetableLayout.Build(new[] { Block(9, 0, 630, Weekdays.Mon) }, At(5, 8, 0));

        Assert.Equal(3, week.Rows.Count);
        Assert.Equal("09:00", week.Rows[0].GutterLabel);
        Assert.Equal("09:30", week.Rows[1].GutterLabel);   // a continuation names the band, not a start
        Assert.Equal("10:00", week.Rows[2].GutterLabel);
    }

    [Fact]
    public void Empty_time_between_two_blocks_stays_collapsed()
    {
        var week = TimetableLayout.Build(
            new[] { Block(9, 0, 570, Weekdays.Mon), Block(15, 0, 930, Weekdays.Mon) }, At(5, 8, 0));

        Assert.Equal(2, week.Rows.Count);   // 09:00 and 15:00, nothing between them
    }

    [Fact]
    public void A_block_over_three_rows_is_start_middle_end()
    {
        var week = TimetableLayout.Build(new[] { Block(9, 0, 630, Weekdays.Mon) }, At(5, 8, 0));
        var roles = week.Rows.Select(r => r.Cells[0].Entries.Single().Role).ToArray();

        Assert.Equal(new[] { SegmentRole.Start, SegmentRole.Middle, SegmentRole.End }, roles);
    }

    [Fact]
    public void A_block_inside_one_band_is_whole()
    {
        var week = TimetableLayout.Build(new[] { Block(9, 0, 550, Weekdays.Mon) }, At(5, 8, 0));

        Assert.Equal(SegmentRole.Whole, week.Rows.Single().Cells[0].Entries.Single().Role);
    }

    [Fact]
    public void An_alarm_with_no_end_is_an_instant()
    {
        var week = TimetableLayout.Build(new[] { Recurring(9, 0, Weekdays.Mon) }, At(5, 8, 0));
        var entry = week.Rows.Single().Cells[0].Entries.Single();

        Assert.Equal(SegmentRole.Instant, entry.Role);
        Assert.False(entry.IsBlock);
        Assert.Equal("09:00", entry.RangeText);
    }

    [Fact]
    public void A_block_states_its_range()
    {
        var week = TimetableLayout.Build(new[] { Block(9, 0, 630, Weekdays.Mon) }, At(5, 8, 0));
        var entry = week.Rows[0].Cells[0].Entries.Single();

        Assert.Equal("09:00–10:30", entry.RangeText);
        Assert.Equal("Lecture, Monday, 09:00 to 10:30", entry.AccessibleName);
    }

    [Fact]
    public void An_end_at_or_before_the_start_is_drawn_as_an_instant()
    {
        // Build is total. EndsAt gives the start and EndMinute the end -- two different sources, and
        // Sanitized compares EndMinute against Hour/Minute, not against EndsAt. A negative span must
        // never reach the covered-slot walk.
        var week = TimetableLayout.Build(new[] { Block(9, 0, 540, Weekdays.Mon) }, At(5, 8, 0));

        Assert.Equal(SegmentRole.Instant, week.Rows.Single().Cells[0].Entries.Single().Role);
    }

    [Fact]
    public void The_span_pads_from_the_latest_end_not_the_latest_start()
    {
        var week = TimetableLayout.Build(new[] { Block(9, 0, 1080, Weekdays.Mon) }, At(5, 8, 0));

        // 18:00 end + an hour of padding = 19:00, so the last half-hour band starts at 18:30.
        Assert.Equal("18:30", week.Slots[^1].Label);
    }

    // ---- Lanes ---------------------------------------------------------------------------

    [Fact]
    public void A_day_with_no_overlap_has_one_lane()
    {
        var week = TimetableLayout.Build(
            new[] { Block(9, 0, 570, Weekdays.Mon), Block(11, 0, 690, Weekdays.Mon) }, At(5, 8, 0));

        Assert.Equal(1, week.Days[0].LaneCount);
    }

    [Fact]
    public void Two_overlapping_blocks_take_a_lane_each()
    {
        var week = TimetableLayout.Build(
            new[] { Block(9, 0, 630, Weekdays.Mon), Block(9, 30, 660, Weekdays.Mon, "Lab") }, At(5, 8, 0));
        var cell = week.Rows.First(r => r.Slot.Label == "09:30").Cells[0];

        Assert.Equal(2, week.Days[0].LaneCount);
        Assert.Equal(2, cell.Lanes.Count);
        Assert.Equal("Lecture", Assert.Single(cell.Lanes[0].Entries).Label);
        Assert.Equal("Lab", Assert.Single(cell.Lanes[1].Entries).Label);
    }

    [Fact]
    public void A_lane_a_block_does_not_reach_is_left_empty_so_the_bar_keeps_its_width()
    {
        var week = TimetableLayout.Build(
            new[] { Block(9, 0, 630, Weekdays.Mon), Block(9, 30, 660, Weekdays.Mon, "Lab") }, At(5, 8, 0));
        var first = week.Rows.First(r => r.Slot.Label == "09:00").Cells[0];

        Assert.Equal(2, first.Lanes.Count);       // the day's lane count, not this band's occupancy
        Assert.Single(first.Lanes[0].Entries);
        Assert.Empty(first.Lanes[1].Entries);     // the Lab has not started yet
    }

    [Fact]
    public void An_instant_inside_a_block_takes_its_own_lane()
    {
        var week = TimetableLayout.Build(
            new[] { Block(9, 0, 660, Weekdays.Mon), Recurring(10, 0, Weekdays.Mon, "Stretch") }, At(5, 8, 0));
        var cell = week.Rows.First(r => r.Slot.Label == "10:00").Cells[0];

        Assert.Equal(2, cell.Entries.Count);
        Assert.Equal("Lecture", Assert.Single(cell.Lanes[0].Entries).Label);
        Assert.Equal("Stretch", Assert.Single(cell.Lanes[1].Entries).Label);
    }

    [Fact]
    public void Lane_order_is_start_order()
    {
        var week = TimetableLayout.Build(
            new[] { Block(9, 30, 660, Weekdays.Mon, "Later"), Block(9, 0, 630, Weekdays.Mon, "Earlier") },
            At(5, 8, 0));
        var cell = week.Rows.First(r => r.Slot.Label == "09:30").Cells[0];

        Assert.Equal("Earlier", Assert.Single(cell.Lanes[0].Entries).Label);
        Assert.Equal(0, Assert.Single(cell.Lanes[0].Entries).LaneIndex);
    }

    [Fact]
    public void A_fifty_way_overlap_caps_the_grid_at_three_lanes_and_counts_the_rest()
    {
        var many = Enumerable.Range(0, 50)
            .Select(i => Block(9, 0, 630, Weekdays.Mon, $"Class {i:D2}")).ToArray();

        var week = TimetableLayout.Build(many, At(5, 8, 0));
        var cell = week.Rows.First().Cells[0];

        Assert.Equal(TimetableLayout.MaxLanes, cell.Lanes.Count);
        Assert.Equal(3, cell.Entries.Count);
        Assert.True(cell.HasOverflow);
        Assert.Equal(47, cell.OverflowCount);
        Assert.Equal("+47 more", cell.OverflowText);
    }

    // ---- The current block ---------------------------------------------------------------

    private static TimetableEntry OnlyEntry(TimerItem item) =>
        TimetableLayout.Build(new[] { item }, At(5, 8, 0)).Rows[0].Cells[0].Entries[0];

    [Theory]
    [InlineData(540, true)]    // 09:00, the start minute, counts as current
    [InlineData(600, true)]    // 10:00, inside
    [InlineData(630, false)]   // 10:30, the end minute, does not
    [InlineData(539, false)]   // a minute before
    public void IsCurrent_is_start_inclusive_and_end_exclusive(int now, bool expected)
    {
        var entry = OnlyEntry(Block(9, 0, 630, Weekdays.Mon));
        Assert.Equal(expected, TimetableLayout.IsCurrent(entry, isToday: true, now));
    }

    [Fact]
    public void Nothing_is_current_on_another_day()
    {
        var entry = OnlyEntry(Block(9, 0, 630, Weekdays.Mon));
        Assert.False(TimetableLayout.IsCurrent(entry, isToday: false, 600));
    }

    [Fact]
    public void An_instant_is_never_current()
    {
        var entry = OnlyEntry(Recurring(9, 0, Weekdays.Mon));
        Assert.False(TimetableLayout.IsCurrent(entry, isToday: true, 540));
    }

    [Fact]
    public void A_disabled_block_is_never_lit()
    {
        var entry = OnlyEntry(Block(9, 0, 630, Weekdays.Mon, enabled: false));
        Assert.False(TimetableLayout.IsCurrent(entry, isToday: true, 600));
    }

    [Fact]
    public void A_continuation_segment_is_current_too_so_the_whole_bar_lights()
    {
        var week = TimetableLayout.Build(new[] { Block(9, 0, 630, Weekdays.Mon) }, At(5, 8, 0));
        var middle = week.Rows[1].Cells[0].Entries.Single();

        Assert.Equal(SegmentRole.Middle, middle.Role);
        Assert.True(TimetableLayout.IsCurrent(middle, isToday: true, 600));
    }

    [Fact]
    public void An_overlap_narrows_only_the_bands_it_reaches()
    {
        // One overlap at 11:00 must not halve an unrelated 07:30 alarm's width in the same column.
        var week = TimetableLayout.Build(new[]
        {
            Recurring(7, 30, Weekdays.Tue, "Morning walk"),
            Block(11, 0, 720, Weekdays.Tue, "Focus block"),
            Block(11, 30, 750, Weekdays.Tue, "Lab"),
        }, At(6, 7, 0));

        var early = week.Rows.First(r => r.Slot.Label == "07:30").Cells[1];
        var overlapped = week.Rows.First(r => r.Slot.Label == "11:30").Cells[1];

        Assert.Single(early.Lanes);            // full width, untouched by the later clash
        Assert.Equal(2, overlapped.Lanes.Count);
    }

    [Fact]
    public void A_block_keeps_one_width_for_its_whole_run()
    {
        var week = TimetableLayout.Build(new[]
        {
            Block(11, 0, 720, Weekdays.Tue, "Focus block"),
            Block(11, 30, 750, Weekdays.Tue, "Lab"),
        }, At(6, 7, 0));

        // Every band the cluster reaches is two lanes wide, including the one before the Lab starts.
        var widths = week.Rows.Select(r => r.Cells[1].Lanes.Count).Where(n => n > 0).Distinct().ToArray();
        Assert.Equal(new[] { 2 }, widths);
    }

    [Fact]
    public void An_empty_cell_has_no_lanes_at_all()
    {
        // Not a row of empty lanes: the view collapses an items control with nothing in it, and that
        // is what keeps most cells out of the automation tree.
        var week = TimetableLayout.Build(
            new[] { Block(9, 0, 630, Weekdays.Mon), Block(9, 30, 660, Weekdays.Mon, "Lab") }, At(5, 8, 0));
        var tuesday = week.Rows[0].Cells[1];

        Assert.Empty(tuesday.Lanes);
        Assert.Empty(tuesday.Entries);
    }

    [Fact]
    public void A_lane_announces_nothing_of_its_own()
    {
        var week = TimetableLayout.Build(new[] { Block(9, 0, 630, Weekdays.Mon) }, At(5, 8, 0));

        // If a container peer ever falls back to ToString(), it must not read out a collection type.
        Assert.Equal("", week.Rows[0].Cells[0].Lanes[0].ToString());
    }

    [Fact]
    public void The_agenda_still_lists_every_entry_the_grid_summarised()
    {
        var many = Enumerable.Range(0, 50)
            .Select(i => Block(9, 0, 570, Weekdays.Mon, $"Class {i:D2}")).ToArray();

        var week = TimetableLayout.Build(many, At(5, 8, 0));

        // The grid caps; the agenda is a list and hides nothing.
        Assert.Equal(50, week.Days[0].Entries.Count);
    }
}
