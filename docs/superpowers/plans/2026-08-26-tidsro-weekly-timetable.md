# Weekly Timetable View Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a read-only **Week** tab to Tidsro showing the recurring alarms that already exist, laid out Monday to Sunday.

**Architecture:** A pure static `TimetableLayout.Build` turns runtime alarms plus a clock reading into an immutable `TimetableWeek` (slots, days, entries) with no pixels and no I/O, so it is fully unit-testable. A thin `TimetableViewModel` holds the current week and re-projects on `MainViewModel.AlarmsChanged` and on day rollover. Two XAML renderings bind to that one structure — an agenda below 760px, a seven-column grid above — swapped by a new width converter.

**Tech Stack:** C# / .NET 10 WPF, CommunityToolkit.Mvvm, xUnit. No new dependencies.

**Spec:** `docs/superpowers/specs/2026-08-26-tidsro-weekly-timetable-design.md`

## Global Constraints

- **No schema change.** `TidsroData.CurrentSchema` stays `4`. Do not add `EndMinute` or any phase-2 field.
- **No scheduler change.** `SchedulerService` is not modified by this plan.
- **Read-only.** No commands, no edit affordances, no double-click-to-open on the Week tab.
- **`TimetableLayout.Build` is total** — it never throws, for any input, including `null`.
- **No `DateTime.Now` / `DateTimeOffset.Now`** anywhere in `Models/` or `ViewModels/`. Time arrives as a parameter or via `SchedulerService.Now`.
- Slot size is **30 minutes**; minimum span **6 hours**; padding **1 hour** each side; day clamp **00:00–24:00**.
- Responsive threshold is **760px**.
- Entry sort inside a slot: **minute → label (ordinal) → id**.
- Disabled entries use `TextMuted` (never `TextFaint`) and must clear **4.5:1**.
- Commit after every task. Never use `--no-verify`. **No `Co-Authored-By` and no Claude attribution in any commit message.**
- Full suite must be green before each commit: `dotnet test` from the repo root.
- **Tidsro must not be running** during build or test — it locks `bin/.../Tidsro.exe` (MSB3027). If a build fails that way: `Get-Process Tidsro | Stop-Process -Force`.

---

### Task 1: `TimetableLayout` and its result types

The pure core. Everything else binds to what this produces.

**Files:**
- Create: `src/Tidsro/Models/TimetableLayout.cs`
- Test: `tests/Tidsro.Tests/TimetableLayoutTests.cs`

**Interfaces:**
- Consumes: `TimerItem` (`RecurringDays`, `EndsAt`, `Label`, `Sound`, `IsEnabled`, `Id`), `Weekdays`, `RecurrenceRules.AllDays`, `SoundChoice`.
- Produces:
  - `TimetableLayout.Build(IEnumerable<TimerItem>? alarms, DateTimeOffset now) -> TimetableWeek`
  - `TimetableLayout.SlotMinutes = 30`, `MinimumSpanMinutes = 360`
  - `record TimetableEntry(Guid Id, string? Label, string DayName, int Hour, int Minute, SoundChoice Sound, bool IsEnabled, int SlotIndex)` with `string TimeText`
  - `record TimetableSlot(int Index, int Hour, int Minute)` with `bool IsWholeHour`, `string Label`
  - `record TimetableDay(Weekdays Day, string Name, bool IsToday, IReadOnlyList<TimetableEntry> Entries)`
  - `record TimetableWeek(bool IsEmpty, IReadOnlyList<TimetableSlot> Slots, IReadOnlyList<TimetableDay> Days)` with `bool LabelWholeHoursOnly`

- [ ] **Step 1: Write the failing tests**

Create `tests/Tidsro.Tests/TimetableLayoutTests.cs`:

```csharp
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
        TriggerType = TriggerType.ClockTime,
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
        Assert.Equal(8, week.Slots[0].Hour);                    // 09:00 floored, minus 1h
        Assert.Equal(16, week.Slots[^1].Hour);                  // 15:00 ceiled, plus 1h, last slot starts 15:30
        Assert.Equal(15, week.Slots[^1].Minute);                // sanity: see next assertion
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~TimetableLayoutTests`
Expected: FAIL — the build breaks with "The name 'TimetableLayout' does not exist".

- [ ] **Step 3: Write the implementation**

Create `src/Tidsro/Models/TimetableLayout.cs`:

```csharp
namespace Tidsro.Models;

/// <summary>One alarm placed in the week. Layout data only — no pixels, no view concerns.</summary>
public sealed record TimetableEntry(
    Guid Id, string? Label, string DayName, int Hour, int Minute, SoundChoice Sound, bool IsEnabled, int SlotIndex)
{
    public string TimeText => $"{Hour:D2}:{Minute:D2}";

    /// <summary>What a screen reader reads for this row. Carries the weekday, because the grid
    /// rendering is reached by widening the window and its column headers are easy to navigate past;
    /// and carries the off state, which is otherwise encoded only by dimming.</summary>
    public string AccessibleName => IsEnabled
        ? $"{Label}, {DayName}, {TimeText}"
        : $"{Label}, {DayName}, {TimeText}, off";
}

/// <summary>One row of the vertical axis: a 30-minute band starting at Hour:Minute.</summary>
public sealed record TimetableSlot(int Index, int Hour, int Minute)
{
    public bool IsWholeHour => Minute == 0;
    public string Label => $"{Hour:D2}:{Minute:D2}";
}

/// <summary>One weekday column, Monday first.</summary>
public sealed record TimetableDay(Weekdays Day, string Name, bool IsToday, IReadOnlyList<TimetableEntry> Entries);

/// <summary>The whole projected week. Immutable; rebuilt rather than mutated.</summary>
public sealed record TimetableWeek(bool IsEmpty, IReadOnlyList<TimetableSlot> Slots, IReadOnlyList<TimetableDay> Days)
{
    /// <summary>Past a twelve-hour span the gutter labels only whole hours, so the text thins while the rows stay.</summary>
    public bool LabelWholeHoursOnly => Slots.Count > 24;
}

/// <summary>
/// Projects recurring alarms into a Monday-first week grid. Pure: no state, no I/O, and the current
/// time arrives as a parameter — the same shape as <see cref="RecurrenceRules"/>.
///
/// <para><b>Build is total.</b> data.json is user-writable and importable, so this never throws for any
/// input. An entry that cannot be placed is skipped and the rest of the week still renders. That is a
/// deliberate departure from RecurrenceRules.NextOccurrence, which throws on an empty day set: right
/// for arming an alarm, wrong for drawing one, where one bad row would otherwise reach the global
/// exception handler and read as "Tidsro is broken".</para>
/// </summary>
public static class TimetableLayout
{
    public const int SlotMinutes = 30;
    public const int MinimumSpanMinutes = 6 * 60;
    private const int PadMinutes = 60;
    private const int DayMinutes = 24 * 60;

    private static readonly (Weekdays Flag, string Name)[] Week =
    {
        (Weekdays.Mon, "Monday"), (Weekdays.Tue, "Tuesday"), (Weekdays.Wed, "Wednesday"),
        (Weekdays.Thu, "Thursday"), (Weekdays.Fri, "Friday"), (Weekdays.Sat, "Saturday"),
        (Weekdays.Sun, "Sunday"),
    };

    public static TimetableWeek Build(IEnumerable<TimerItem>? alarms, DateTimeOffset now)
    {
        var usable = Collect(alarms);
        if (usable.Count == 0) return Empty(now);

        var (startMinutes, slotCount) = ResolveSpan(usable);
        var slots = BuildSlots(startMinutes, slotCount);
        var today = DayFlag(now.DayOfWeek);

        var days = new List<TimetableDay>(Week.Length);
        foreach (var (flag, name) in Week)
        {
            var entries = usable
                .Where(u => (u.Days & flag) != 0)
                .OrderBy(u => u.Minutes)
                .ThenBy(u => u.Label, StringComparer.Ordinal)
                .ThenBy(u => u.Id)
                .Select(u => new TimetableEntry(
                    u.Id, u.Label, name, u.Minutes / 60, u.Minutes % 60, u.Sound, u.IsEnabled,
                    (FloorToSlot(u.Minutes) - startMinutes) / SlotMinutes))
                .ToList();
            days.Add(new TimetableDay(flag, name, flag == today, entries));
        }

        return new TimetableWeek(IsEmpty: false, slots, days);
    }

    private readonly record struct Placed(
        Guid Id, string? Label, int Minutes, SoundChoice Sound, bool IsEnabled, Weekdays Days);

    private static List<Placed> Collect(IEnumerable<TimerItem>? alarms)
    {
        var usable = new List<Placed>();
        if (alarms is null) return usable;

        foreach (var a in alarms)
        {
            if (a is null) continue;                              // a null element must not take the tab down
            if (a.RecurringDays is not { } raw) continue;         // one-shots and countdowns are not timetable rows
            var days = raw & RecurrenceRules.AllDays;             // strip unknown bits, as Sanitized does
            if (days == Weekdays.None) continue;
            if (a.EndsAt is not { } next) continue;               // no occurrence -> nothing to place

            usable.Add(new Placed(a.Id, a.Label, next.Hour * 60 + next.Minute, a.Sound, a.IsEnabled, days));
        }

        return usable;
    }

    // Pad an hour each side, grow to the six-hour minimum in whole slots, then clamp to the day —
    // giving the clamped length back at the far end so a 00:30 alarm still gets a full six hours.
    private static (int StartMinutes, int SlotCount) ResolveSpan(List<Placed> usable)
    {
        var start = FloorToSlot(usable.Min(u => u.Minutes)) - PadMinutes;
        var end = CeilToSlot(usable.Max(u => u.Minutes)) + PadMinutes;

        var deficitSlots = (MinimumSpanMinutes - (end - start) + SlotMinutes - 1) / SlotMinutes;
        if (deficitSlots > 0)
        {
            var before = deficitSlots / 2;
            start -= before * SlotMinutes;
            end += (deficitSlots - before) * SlotMinutes;
        }

        if (start < 0) { end = Math.Min(DayMinutes, end - start); start = 0; }
        if (end > DayMinutes) { start = Math.Max(0, start - (end - DayMinutes)); end = DayMinutes; }

        return (start, (end - start) / SlotMinutes);
    }

    private static List<TimetableSlot> BuildSlots(int startMinutes, int slotCount)
    {
        var slots = new List<TimetableSlot>(slotCount);
        for (var i = 0; i < slotCount; i++)
        {
            var minutes = startMinutes + i * SlotMinutes;
            slots.Add(new TimetableSlot(i, minutes / 60, minutes % 60));
        }
        return slots;
    }

    private static TimetableWeek Empty(DateTimeOffset now)
    {
        var today = DayFlag(now.DayOfWeek);
        var days = Week
            .Select(d => new TimetableDay(d.Flag, d.Name, d.Flag == today, Array.Empty<TimetableEntry>()))
            .ToList();
        return new TimetableWeek(IsEmpty: true, Array.Empty<TimetableSlot>(), days);
    }

    private static int FloorToSlot(int minutes) => minutes / SlotMinutes * SlotMinutes;

    private static int CeilToSlot(int minutes) => (minutes + SlotMinutes - 1) / SlotMinutes * SlotMinutes;

    private static Weekdays DayFlag(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => Weekdays.Mon,
        DayOfWeek.Tuesday => Weekdays.Tue,
        DayOfWeek.Wednesday => Weekdays.Wed,
        DayOfWeek.Thursday => Weekdays.Thu,
        DayOfWeek.Friday => Weekdays.Fri,
        DayOfWeek.Saturday => Weekdays.Sat,
        _ => Weekdays.Sun,
    };
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~TimetableLayoutTests`
Expected: PASS — every test in the new file.

If `Span_covers_the_earliest_and_latest_alarm_with_padding` fails on the last-slot assertion, read the actual value before changing the implementation — the last *slot start* is one slot below the span end, and the test asserts that deliberately.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test`
Expected: PASS. Note the total; each later task should raise it, never lower it.

- [ ] **Step 6: Commit**

```bash
git add src/Tidsro/Models/TimetableLayout.cs tests/Tidsro.Tests/TimetableLayoutTests.cs
git commit -m "feat(models): project recurring alarms into a week layout

TimetableLayout.Build turns runtime alarms plus a clock reading into an
immutable week of 30-minute slots and Monday-first day columns. Pure and
total: it never throws, and an alarm that cannot be placed is skipped so a
single malformed row cannot take the tab down."
```

---

### Task 2: `TimetableViewModel`

**Files:**
- Create: `src/Tidsro/ViewModels/TimetableViewModel.cs`
- Modify: `src/Tidsro/ViewModels/MainViewModel.cs` (construct and own it; refresh it from the tick)
- Test: `tests/Tidsro.Tests/TimetableViewModelTests.cs`

**Interfaces:**
- Consumes: `TimetableLayout.Build`, `TimetableWeek` (Task 1); `SchedulerService.Now`, `SchedulerService.Alarms`; `MainViewModel.AlarmsChanged`.
- Produces:
  - `TimetableViewModel(SchedulerService scheduler)` with `TimetableWeek Week { get; }`
  - `void Rebuild()` — unconditional re-projection
  - `void RefreshForTick()` — re-projects **only** when the calendar date has changed
  - `MainViewModel.Timetable` — the single instance, app-lifetime

- [ ] **Step 1: Write the failing tests**

Create `tests/Tidsro.Tests/TimetableViewModelTests.cs`:

```csharp
using Tidsro.Models;
using Tidsro.Services;
using Tidsro.ViewModels;
using Xunit;

namespace Tidsro.Tests;

public class TimetableViewModelTests
{
    private static (TimetableViewModel vm, SchedulerService scheduler, FakeClock clock) Build()
    {
        var clock = new FakeClock { Now = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) };
        var scheduler = new SchedulerService(clock);
        return (new TimetableViewModel(scheduler), scheduler, clock);
    }

    [Fact]
    public void Starts_empty_when_there_are_no_recurring_alarms()
    {
        var (vm, _, _) = Build();
        Assert.True(vm.Week.IsEmpty);
    }

    [Fact]
    public void Rebuild_picks_up_a_newly_armed_recurring_alarm()
    {
        var (vm, scheduler, _) = Build();
        scheduler.ArmRecurringAlarm(9, 0, Weekdays.Mon, "Class", SoundChoice.None);

        vm.Rebuild();

        Assert.False(vm.Week.IsEmpty);
        Assert.Single(vm.Week.Days.Single(d => d.Day == Weekdays.Mon).Entries);
    }

    [Fact]
    public void RefreshForTick_does_not_reproject_within_the_same_day()
    {
        var (vm, scheduler, clock) = Build();
        scheduler.ArmRecurringAlarm(9, 0, Weekdays.Mon, "Class", SoundChoice.None);
        vm.Rebuild();
        var first = vm.Week;

        for (var i = 0; i < 1000; i++)
        {
            clock.Now = clock.Now.AddSeconds(1);
            vm.RefreshForTick();
        }

        Assert.Same(first, vm.Week);   // same instance -> no work was done
    }

    [Fact]
    public void RefreshForTick_reprojects_once_when_the_date_changes()
    {
        var (vm, scheduler, clock) = Build();
        scheduler.ArmRecurringAlarm(9, 0, Weekdays.Mon, "Class", SoundChoice.None);
        vm.Rebuild();
        var first = vm.Week;

        clock.Now = clock.Now.AddDays(1);   // Thursday -> Friday
        vm.RefreshForTick();
        var second = vm.Week;
        vm.RefreshForTick();

        Assert.NotSame(first, second);
        Assert.Same(second, vm.Week);       // the second call is a no-op
    }

    [Fact]
    public void Today_moves_when_the_date_changes()
    {
        var (vm, scheduler, clock) = Build();
        scheduler.ArmRecurringAlarm(9, 0, Weekdays.Mon, "Class", SoundChoice.None);
        vm.Rebuild();
        Assert.True(vm.Week.Days.Single(d => d.Day == Weekdays.Thu).IsToday);

        clock.Now = clock.Now.AddDays(1);
        vm.RefreshForTick();

        Assert.True(vm.Week.Days.Single(d => d.Day == Weekdays.Fri).IsToday);
    }

    [Fact]
    public void Raises_property_changed_so_the_view_redraws()
    {
        var (vm, scheduler, _) = Build();
        scheduler.ArmRecurringAlarm(9, 0, Weekdays.Mon, "Class", SoundChoice.None);
        var raised = 0;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.Week)) raised++; };

        vm.Rebuild();

        Assert.Equal(1, raised);
    }
}
```

Add to `tests/Tidsro.Tests/MainViewModelTests.cs` (append inside the existing class):

```csharp
    [Fact]
    public void Timetable_redraws_when_an_alarm_is_added()
    {
        var clock = new FakeClock { Now = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) };
        var scheduler = new SchedulerService(clock);
        var vm = new MainViewModel(scheduler, new FakeSoundService(), SoundChoice.None);

        Assert.True(vm.Timetable.Week.IsEmpty);

        scheduler.ArmRecurringAlarm(9, 0, Weekdays.Mon, "Class", SoundChoice.None);
        vm.RefreshAll();

        Assert.False(vm.Timetable.Week.IsEmpty);
    }

    [Fact]
    public void Timetable_empties_when_all_alarms_are_cleared()
    {
        var clock = new FakeClock { Now = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) };
        var scheduler = new SchedulerService(clock);
        var vm = new MainViewModel(scheduler, new FakeSoundService(), SoundChoice.None);
        scheduler.ArmRecurringAlarm(9, 0, Weekdays.Mon, "Class", SoundChoice.None);
        vm.RefreshAll();
        Assert.False(vm.Timetable.Week.IsEmpty);

        vm.ClearAllAlarms();

        Assert.True(vm.Timetable.Week.IsEmpty);
    }

    [Fact]
    public void Timetable_redraws_when_an_import_replaces_the_alarms()
    {
        var clock = new FakeClock { Now = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) };
        var scheduler = new SchedulerService(clock);
        var vm = new MainViewModel(scheduler, new FakeSoundService(), SoundChoice.None);

        vm.ReplaceAllAlarms(
            Array.Empty<AlarmRecord>(),
            new[]
            {
                new RecurringAlarmRecord
                {
                    Hour = 9, Minute = 0, Days = Weekdays.Mon, Label = "Imported",
                    NextFireAt = new DateTime(2026, 1, 5, 9, 0, 0),
                },
            });

        Assert.False(vm.Timetable.Week.IsEmpty);
        Assert.Equal("Imported", vm.Timetable.Week.Days.Single(d => d.Day == Weekdays.Mon).Entries.Single().Label);
    }
```

**Check `ReplaceAllAlarms`'s real signature at `MainViewModel.cs:382` before writing this one** — the argument order and types above are from a reading of that line, not from running it.

**Before writing these two, read `MainViewModelTests.cs` and match how it already builds a `MainViewModel`** — if a helper exists, use it instead of the inline construction above, and check `ArmRecurringAlarm`'s real signature in `SchedulerService.cs` rather than trusting the call above.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~TimetableViewModelTests`
Expected: FAIL — "The name 'TimetableViewModel' does not exist".

- [ ] **Step 3: Write the view model**

Create `src/Tidsro/ViewModels/TimetableViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Tidsro.Models;
using Tidsro.Services;

namespace Tidsro.ViewModels;

/// <summary>
/// Read-only projection of the scheduler's recurring alarms into a week. No commands: the Week tab
/// shows, it does not edit.
///
/// <para>Owned by <see cref="MainViewModel"/> for the application's lifetime — deliberately not created
/// per tab activation, which would leak a subscription per visit.</para>
/// </summary>
public sealed partial class TimetableViewModel : ObservableObject
{
    private readonly SchedulerService _scheduler;
    private DateOnly _builtFor;

    public TimetableViewModel(SchedulerService scheduler)
    {
        _scheduler = scheduler;
        Rebuild();
    }

    [ObservableProperty] private TimetableWeek _week = null!;

    /// <summary>Re-project unconditionally. Called when the alarm set changes.</summary>
    public void Rebuild()
    {
        var now = _scheduler.Now;
        _builtFor = DateOnly.FromDateTime(now.LocalDateTime);
        Week = TimetableLayout.Build(_scheduler.Alarms, now);
    }

    /// <summary>
    /// Re-project only if the calendar date moved, so "today" never sits on yesterday.
    /// The tick loop runs at 250 ms, so an unconditional rebuild here would be roughly 345,000 week
    /// projections a day. Comparing the date also survives the machine sleeping through midnight,
    /// because it measures a date rather than elapsed time.
    /// </summary>
    public void RefreshForTick()
    {
        if (DateOnly.FromDateTime(_scheduler.Now.LocalDateTime) != _builtFor) Rebuild();
    }
}
```

- [ ] **Step 4: Wire it into `MainViewModel`**

In `src/Tidsro/ViewModels/MainViewModel.cs`:

1. Add the property beside the other collections (near `public ObservableCollection<AlarmItemViewModel> Alarms`):

```csharp
    /// <summary>The Week tab's projection. Constructed once and held for the app's lifetime.</summary>
    public TimetableViewModel Timetable { get; }
```

2. In the constructor, after `_scheduler = scheduler;`:

```csharp
        Timetable = new TimetableViewModel(scheduler);
        AlarmsChanged += (_, _) => Timetable.Rebuild();
```

3. Find `RefreshAll()` and add as its last statement:

```csharp
        Timetable.RefreshForTick();
```

**Read `RefreshAll()` before editing it** — add the call, do not reorder or restructure what is already there.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~TimetableViewModelTests|FullyQualifiedName~MainViewModelTests"`
Expected: PASS.

- [ ] **Step 6: Run the full suite**

Run: `dotnet test`
Expected: PASS, with the new view-model tests included.

- [ ] **Step 7: Commit**

```bash
git add src/Tidsro/ViewModels/TimetableViewModel.cs src/Tidsro/ViewModels/MainViewModel.cs tests/Tidsro.Tests/TimetableViewModelTests.cs tests/Tidsro.Tests/MainViewModelTests.cs
git commit -m "feat(viewmodels): hold the week projection and refresh it

MainViewModel owns one TimetableViewModel for the app's lifetime, rebuilding
it whenever the alarm set changes. Day rollover is a cached date comparison
rather than a per-tick rebuild: the tick loop runs at 250 ms, so re-projecting
on every tick would be roughly 345,000 rebuilds a day."
```

---

### Task 3: `WidthToVisibleConverter`

**Files:**
- Modify: `src/Tidsro/Views/Converters.cs` (append)
- Test: `tests/Tidsro.Tests/WidthToVisibleConverterTests.cs`

**Interfaces:**
- Produces: `WidthToVisibleConverter` with `public const double Threshold = 760`. Parameter `"Wide"` shows at or above the threshold; `"Narrow"` shows below it. Anything else, or a non-double value, returns `Collapsed`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Tidsro.Tests/WidthToVisibleConverterTests.cs`:

```csharp
using System.Globalization;
using System.Windows;
using Tidsro.Views;
using Xunit;

namespace Tidsro.Tests;

public class WidthToVisibleConverterTests
{
    private static object Convert(object? value, string? parameter) =>
        new WidthToVisibleConverter().Convert(value, typeof(Visibility), parameter, CultureInfo.InvariantCulture);

    [Fact]
    public void Wide_shows_at_the_threshold()
        => Assert.Equal(Visibility.Visible, Convert(760d, "Wide"));

    [Fact]
    public void Wide_hides_below_the_threshold()
        => Assert.Equal(Visibility.Collapsed, Convert(759d, "Wide"));

    [Fact]
    public void Narrow_shows_below_the_threshold()
        => Assert.Equal(Visibility.Visible, Convert(759d, "Narrow"));

    [Fact]
    public void Narrow_hides_at_the_threshold()
        => Assert.Equal(Visibility.Collapsed, Convert(760d, "Narrow"));

    [Fact]
    public void Exactly_one_side_is_visible_at_any_width()
    {
        foreach (var width in new[] { 0d, 380d, 759d, 760d, 1200d })
        {
            var wide = Convert(width, "Wide");
            var narrow = Convert(width, "Narrow");
            Assert.NotEqual(wide, narrow);
        }
    }

    [Fact]
    public void NaN_width_falls_back_to_narrow()
    {
        Assert.Equal(Visibility.Visible, Convert(double.NaN, "Narrow"));
        Assert.Equal(Visibility.Collapsed, Convert(double.NaN, "Wide"));
    }

    [Fact]
    public void An_unknown_parameter_collapses()
        => Assert.Equal(Visibility.Collapsed, Convert(900d, "Sideways"));

    [Fact]
    public void ConvertBack_is_not_supported()
        => Assert.Throws<NotSupportedException>(() =>
            new WidthToVisibleConverter().ConvertBack(Visibility.Visible, typeof(double), "Wide", CultureInfo.InvariantCulture));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~WidthToVisibleConverterTests`
Expected: FAIL — "The name 'WidthToVisibleConverter' does not exist".

- [ ] **Step 3: Write the converter**

Append to `src/Tidsro/Views/Converters.cs`:

```csharp
/// <summary>Picks one of two renderings of the same content by the width available to them.
/// The Week tab draws an agenda when narrow and a seven-column grid when wide; both panels exist,
/// and this collapses the one that does not fit. Same mechanism as IndexToVisibleConverter, which
/// already swaps the tab panels, and it reads the same ActualWidth WidthToMeasureConverter does.
/// A width WPF has not measured yet arrives as NaN — that falls back to Narrow, which is the
/// rendering that works at any size.</summary>
public sealed class WidthToVisibleConverter : IValueConverter
{
    public const double Threshold = 760;

    public object Convert(object? v, Type t, object? p, CultureInfo c)
    {
        var wide = v is double available && !double.IsNaN(available) && available >= Threshold;
        return p switch
        {
            "Wide" => wide ? Visibility.Visible : Visibility.Collapsed,
            "Narrow" => wide ? Visibility.Collapsed : Visibility.Visible,
            _ => Visibility.Collapsed,
        };
    }

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~WidthToVisibleConverterTests`
Expected: PASS.

- [ ] **Step 5: Run the full suite and commit**

Run: `dotnet test` — expected PASS.

```bash
git add src/Tidsro/Views/Converters.cs tests/Tidsro.Tests/WidthToVisibleConverterTests.cs
git commit -m "feat(views): add the width converter that picks a week rendering

Collapses whichever of the two Week-tab renderings does not fit, at a 760px
threshold. An unmeasured NaN width falls back to the narrow agenda, which is
the rendering that works at any size."
```

---

### Task 4: The Week tab shell and its empty state

Adds the tab and a panel that renders the empty state only. The two real renderings arrive in Tasks 5 and 6, so this task ends with a tab you can click that says "no repeating alarms yet".

**Files:**
- Modify: `src/Tidsro/Models/AppSettings.cs:13`
- Modify: `src/Tidsro/Views/MainWindow.xaml`
- Test: `tests/Tidsro.Tests/MainViewModelTests.cs` (append)

**Interfaces:**
- Consumes: `MainViewModel.Timetable` (Task 2), `WidthToVisibleConverter` (Task 3).
- Produces: a `Grid` named `WeekPanel` in the `Panels` grid, visible at `SelectedTabIndex == 2`.

- [ ] **Step 1: Write the failing test**

Append to `tests/Tidsro.Tests/MainViewModelTests.cs`:

```csharp
    [Fact]
    public void Ctrl_tab_cycles_through_three_tabs()
    {
        var clock = new FakeClock { Now = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) };
        var vm = new MainViewModel(new SchedulerService(clock), new FakeSoundService(), SoundChoice.None);

        Assert.Equal(0, vm.SelectedTabIndex);
        vm.AdvanceTabCommand.Execute(null);
        Assert.Equal(1, vm.SelectedTabIndex);
        vm.AdvanceTabCommand.Execute(null);
        Assert.Equal(2, vm.SelectedTabIndex);
        vm.AdvanceTabCommand.Execute(null);
        Assert.Equal(0, vm.SelectedTabIndex);
    }
```

Append to `tests/Tidsro.Tests/TidsroDataTests.cs` (or `AppSettings`'s existing test file — check which one covers `Sanitized`):

```csharp
    [Fact]
    public void The_week_tab_index_survives_sanitising()
    {
        var settings = new AppSettings { SelectedTab = 2 }.Sanitized();
        Assert.Equal(2, settings.SelectedTab);
    }

    [Fact]
    public void A_tab_index_past_the_last_tab_falls_back_to_the_first()
    {
        var settings = new AppSettings { SelectedTab = 3 }.Sanitized();
        Assert.Equal(0, settings.SelectedTab);
    }
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~Ctrl_tab_cycles|FullyQualifiedName~week_tab_index"`
Expected: FAIL — the cycle wraps at 2, and `SelectedTab = 2` sanitises to 0.

- [ ] **Step 3: Raise the tab count**

In `src/Tidsro/Models/AppSettings.cs`, change line 13 and its comment:

```csharp
    /// <summary>Tabs the shell has: 0 Quick timers, 1 Schedule, 2 Week.</summary>
    public const int TabCount = 3;
```

Also update the `SelectedTab` doc comment above it to name the third tab.

- [ ] **Step 4: Run to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~Ctrl_tab_cycles|FullyQualifiedName~week_tab_index"`
Expected: PASS.

- [ ] **Step 5: Add the tab header and the panel**

In `src/Tidsro/Views/MainWindow.xaml`, add a third header after `<TabItem Header="Schedule"/>`:

```xml
      <TabItem Header="Week"/>
```

Then, inside the `Panels` grid and **after** the existing Schedule `ScrollViewer`, add:

```xml
      <!-- Week: a read-only projection of the recurring alarms. Two renderings of one structure —
           the agenda works at the 380px minimum, the grid needs room. Both are built from
           Timetable.Week; WidthToVisible collapses whichever does not fit.
           Focusable so a keyboard user can scroll it: the tab is read-only, so without this it
           contains no tab stop at all and the grid could only ever be read from the top. -->
      <ScrollViewer VerticalScrollBarVisibility="Auto" Focusable="True"
                    AutomationProperties.Name="Week timetable"
                    Visibility="{Binding SelectedTabIndex, Converter={StaticResource IndexToVisible}, ConverterParameter=2}">
        <Grid x:Name="WeekPanel" Margin="0,8,0,0">
          <TextBlock Text="No repeating alarms yet — add one on the Schedule tab."
                     Foreground="{StaticResource TextMuted}" TextWrapping="Wrap"
                     HorizontalAlignment="Center" Margin="16,32,16,32"
                     Visibility="{Binding Timetable.Week.IsEmpty, Converter={StaticResource BoolToVisible}}"/>
        </Grid>
      </ScrollViewer>
```

**Check the resource keys against the top of `MainWindow.xaml` before pasting** — use whatever key names the file already uses for `IndexToVisible`, `BoolToVisible`, and `TextMuted`.

- [ ] **Step 6: Build and look at it**

Run: `dotnet build`
Expected: succeeds with no XAML errors.

Then run the app and confirm by eye:
- A third tab reading "Week" appears beside Quick timers and Schedule.
- Clicking it shows the empty-state line (with no recurring alarms present).
- Ctrl+Tab cycles through all three headers and back.
- **Tab headers still respond to a single click.** This is the defect that blocked the tab-shell branch: `RescueFocusFromHiddenPanel` read a stale `Tabs.SelectedIndex` and parked focus on a header. A third tab is exactly the change that would wake it. If clicking a header does nothing, stop and report it — do not work around it in XAML.

```bash
dotnet build && Start-Process src/Tidsro/bin/Debug/net10.0-windows/Tidsro.exe
```

- [ ] **Step 7: Run the full suite and commit**

Run: `dotnet test` — expected PASS.

```bash
git add src/Tidsro/Models/AppSettings.cs src/Tidsro/Views/MainWindow.xaml tests/Tidsro.Tests/MainViewModelTests.cs tests/Tidsro.Tests/TidsroDataTests.cs
git commit -m "feat(app): add the Week tab shell and its empty state

Third tab, TabCount 3, and a panel that so far renders only the empty state.
Sanitized() already bounds SelectedTab by TabCount and AdvanceTab already wraps
on it, so persistence and Ctrl+Tab follow with no further change.

The panel's ScrollViewer is focusable and named: the tab is read-only, so
without a tab stop a keyboard user could never scroll it."
```

---

### Task 5: The agenda rendering (narrow)

The rendering most people will see — the window opens at 440px. It is not a fallback.

**Files:**
- Modify: `src/Tidsro/Views/MainWindow.xaml` (inside `WeekPanel`)

**Interfaces:**
- Consumes: `Timetable.Week.Days` → `TimetableDay(Name, IsToday, Entries)` → `TimetableEntry(Label, TimeText, IsEnabled)` (Task 1); `WidthToVisibleConverter` (Task 3).

- [ ] **Step 1: Add the agenda**

Inside `WeekPanel`, after the empty-state `TextBlock`, add:

```xml
          <!-- Agenda: the narrow rendering, and the one that works at the 380px minimum.
               Days with no entries are collapsed — an empty Tuesday heading is noise, not
               information. -->
          <ItemsControl ItemsSource="{Binding Timetable.Week.Days}"
                        Visibility="{Binding ActualWidth, ElementName=Panels, Converter={StaticResource WidthToVisible}, ConverterParameter=Narrow}">
            <ItemsControl.ItemTemplate>
              <DataTemplate>
                <StackPanel Margin="0,0,0,16"
                            Visibility="{Binding Entries.Count, Converter={StaticResource CountToVisible}}">
                  <TextBlock FontSize="{StaticResource TextXs}" Foreground="{StaticResource TextFaint}">
                    <Run Text="{Binding Name, Mode=OneWay}"/>
                  </TextBlock>
                  <ItemsControl ItemsSource="{Binding Entries}" Margin="0,4,0,0">
                    <ItemsControl.ItemContainerStyle>
                      <!-- The accessible name MUST live here, on the ContentPresenter. A Border at
                           the root of the DataTemplate gets no automation peer, so a name set there
                           is dead and every row announces as its class name. -->
                      <Style TargetType="ContentPresenter">
                        <Setter Property="AutomationProperties.Name" Value="{Binding AccessibleName}"/>
                      </Style>
                    </ItemsControl.ItemContainerStyle>
                    <ItemsControl.ItemTemplate>
                      <DataTemplate>
                        <!-- Border root, not Grid: the disabled-state trigger sets
                             TextElement.Foreground for the whole row, and a Border is the natural
                             carrier for that in both renderings. -->
                        <Border Margin="0,0,0,6" ToolTip="{Binding Label}" Background="Transparent">
                          <Border.Style>
                            <Style TargetType="Border">
                              <Style.Triggers>
                                <DataTrigger Binding="{Binding IsEnabled}" Value="False">
                                  <!-- TextMuted, never TextFaint: a disabled row still has to clear
                                       4.5:1. "Off" is also in the accessible name — dimming alone
                                       never encodes state. -->
                                  <Setter Property="TextElement.Foreground" Value="{StaticResource TextMuted}"/>
                                </DataTrigger>
                              </Style.Triggers>
                            </Style>
                          </Border.Style>
                          <Grid>
                            <Grid.ColumnDefinitions>
                              <ColumnDefinition Width="Auto"/>
                              <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Text="{Binding TimeText}" FontFamily="{StaticResource FontMono}"
                                       Margin="0,0,10,0" VerticalAlignment="Top"/>
                            <TextBlock Grid.Column="1" Text="{Binding Label}" TextWrapping="Wrap"
                                       MaxHeight="40"/>
                          </Grid>
                        </Border>
                      </DataTemplate>
                    </ItemsControl.ItemTemplate>
                  </ItemsControl>
                </StackPanel>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>
```

- [ ] **Step 2: Add the converter the template needs**

`AccessibleName` already exists on `TimetableEntry` from Task 1. `CountToVisible` does not.

Append to `src/Tidsro/Views/Converters.cs`:

```csharp
/// <summary>Collapses a section whose collection is empty — an empty weekday heading in the agenda
/// is noise, not information.</summary>
public sealed class CountToVisibleConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
```

Register both new converters (`WidthToVisible`, `CountToVisible`) in the same resource dictionary where `IndexToVisible` and `WidthToMeasure` are declared — **find that block first and follow its exact style**.

- [ ] **Step 3: Add tests for the converter**

Create `tests/Tidsro.Tests/CountToVisibleConverterTests.cs`:

```csharp
using System.Globalization;
using System.Windows;
using Tidsro.Views;
using Xunit;

namespace Tidsro.Tests;

public class CountToVisibleConverterTests
{
    private static object Convert(object? value) =>
        new CountToVisibleConverter().Convert(value, typeof(Visibility), null, CultureInfo.InvariantCulture);

    [Fact]
    public void A_populated_collection_is_visible() => Assert.Equal(Visibility.Visible, Convert(3));

    [Fact]
    public void An_empty_collection_collapses() => Assert.Equal(Visibility.Collapsed, Convert(0));

    [Fact]
    public void A_non_integer_collapses() => Assert.Equal(Visibility.Collapsed, Convert("three"));
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Look at it**

```bash
dotnet build && Start-Process src/Tidsro/bin/Debug/net10.0-windows/Tidsro.exe
```

Add two or three recurring alarms on different days first, then check:
- The Week tab lists each day that has alarms, with its entries in time order.
- Days with nothing on them do not appear.
- The window at its 380px minimum still reads cleanly with no horizontal scrolling.
- Tab reaches the panel and the arrow keys scroll it.

Then read the UIA tree in this narrow rendering (Windows PowerShell, `UIAutomationClient`, walking
`ControlViewWalker`). Entries must announce as "Code class, Monday, 09:00"; if anything announces as
`Tidsro.Models.TimetableEntry`, an accessible name landed somewhere without an automation peer. The
wide rendering gets its own read in Task 6 — check both, not just the default one.

- [ ] **Step 6: Commit**

```bash
git add src/Tidsro/Views/MainWindow.xaml src/Tidsro/Views/Converters.cs src/Tidsro/Models/TimetableLayout.cs tests/Tidsro.Tests/
git commit -m "feat(app): render the week as an agenda when the window is narrow

Day headings with their entries beneath, empty days collapsed. This is the
rendering the default 440px window shows, so it is the primary one rather than
a fallback.

Entry accessible names sit on the ItemContainerStyle's ContentPresenter, not on
a Border — a Border gets no automation peer, so a name set there is dead."
```

---

### Task 6: The grid rendering (wide)

**Files:**
- Modify: `src/Tidsro/Views/MainWindow.xaml` (inside `WeekPanel`)
- Modify: `src/Tidsro/Models/TimetableLayout.cs` (day accessible name)
- Test: `tests/Tidsro.Tests/TimetableLayoutTests.cs` (append)

**Interfaces:**
- Consumes: `Timetable.Week.Slots`, `.Days`, `.LabelWholeHoursOnly` (Task 1); `WidthToVisibleConverter` (Task 3).
- Produces: `TimetableDay.AccessibleName`.

- [ ] **Step 1: Add the day accessible name and its tests**

In `src/Tidsro/Models/TimetableLayout.cs`, replace the `TimetableDay` record with:

```csharp
/// <summary>One weekday column, Monday first.</summary>
public sealed record TimetableDay(Weekdays Day, string Name, bool IsToday, IReadOnlyList<TimetableEntry> Entries)
{
    /// <summary>Names the column for a screen reader. Without it the wide grid is navigable but
    /// structureless — the rendering is chosen by window width, which is a poor proxy for eyesight,
    /// so the grid has to stand on its own.</summary>
    public string AccessibleName
    {
        get
        {
            var count = Entries.Count switch
            {
                0 => "no alarms",
                1 => "1 alarm",
                var n => $"{n} alarms",
            };
            return IsToday ? $"{Name}, today, {count}" : $"{Name}, {count}";
        }
    }
}
```

Append to `tests/Tidsro.Tests/TimetableLayoutTests.cs`:

```csharp
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
```

Run: `dotnet test --filter FullyQualifiedName~TimetableLayoutTests` — expected PASS.

- [ ] **Step 2: Add the grid**

Inside `WeekPanel`, after the agenda `ItemsControl`, add:

```xml
          <!-- Grid: the wide rendering. A time gutter plus seven day columns, uniform rows from
               Slots. Entries are positioned by SlotIndex, so nothing here does pixel arithmetic —
               the layout function already decided which row each alarm belongs to.
               Empty cells are not drawn at all, which is what keeps the automation tree the same
               handful of items the agenda has rather than a 48x7 lattice of blanks. -->
          <Grid Visibility="{Binding ActualWidth, ElementName=Panels, Converter={StaticResource WidthToVisible}, ConverterParameter=Wide}">
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="Auto"/>
              <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <Grid.RowDefinitions>
              <RowDefinition Height="Auto"/>
              <RowDefinition Height="*"/>
            </Grid.RowDefinitions>

            <!-- Day headers -->
            <ItemsControl Grid.Row="0" Grid.Column="1" ItemsSource="{Binding Timetable.Week.Days}">
              <ItemsControl.ItemsPanel>
                <ItemsPanelTemplate><UniformGrid Rows="1"/></ItemsPanelTemplate>
              </ItemsControl.ItemsPanel>
              <ItemsControl.ItemContainerStyle>
                <Style TargetType="ContentPresenter">
                  <Setter Property="AutomationProperties.Name" Value="{Binding AccessibleName}"/>
                </Style>
              </ItemsControl.ItemContainerStyle>
              <ItemsControl.ItemTemplate>
                <DataTemplate>
                  <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,0,0,6">
                    <!-- Today is marked by a glyph as well as the accent: colour alone never
                         encodes state (WCAG 1.4.1). -->
                    <TextBlock Text="●" Margin="0,0,4,0" Foreground="{StaticResource Accent}"
                               Visibility="{Binding IsToday, Converter={StaticResource BoolToVisible}}"/>
                    <TextBlock Text="{Binding Name}" FontSize="{StaticResource TextXs}"/>
                  </StackPanel>
                </DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>

            <!-- Time gutter -->
            <ItemsControl Grid.Row="1" Grid.Column="0" ItemsSource="{Binding Timetable.Week.Slots}"
                          Margin="0,0,10,0">
              <ItemsControl.ItemTemplate>
                <DataTemplate>
                  <TextBlock Text="{Binding Label}" Height="24" FontFamily="{StaticResource FontMono}"
                             FontSize="{StaticResource TextXs}" Foreground="{StaticResource TextFaint}"/>
                </DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>

            <!-- Day columns -->
            <ItemsControl Grid.Row="1" Grid.Column="1" ItemsSource="{Binding Timetable.Week.Days}">
              <ItemsControl.ItemsPanel>
                <ItemsPanelTemplate><UniformGrid Rows="1"/></ItemsPanelTemplate>
              </ItemsControl.ItemsPanel>
              <ItemsControl.ItemTemplate>
                <DataTemplate>
                  <ItemsControl ItemsSource="{Binding Entries}">
                    <ItemsControl.ItemContainerStyle>
                      <Style TargetType="ContentPresenter">
                        <Setter Property="AutomationProperties.Name" Value="{Binding AccessibleName}"/>
                      </Style>
                    </ItemsControl.ItemContainerStyle>
                    <ItemsControl.ItemTemplate>
                      <DataTemplate>
                        <Border Background="{StaticResource ElevatedBg}" BorderBrush="{StaticResource Border}"
                                BorderThickness="1" CornerRadius="{StaticResource RadiusSm}"
                                Margin="2,0,2,2" Padding="4,2" ToolTip="{Binding Label}">
                          <StackPanel>
                            <TextBlock Text="{Binding TimeText}" FontFamily="{StaticResource FontMono}"
                                       FontSize="{StaticResource TextXs}"/>
                            <TextBlock Text="{Binding Label}" FontSize="{StaticResource TextXs}"
                                       TextTrimming="CharacterEllipsis"/>
                          </StackPanel>
                        </Border>
                      </DataTemplate>
                    </ItemsControl.ItemTemplate>
                  </ItemsControl>
                </DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>
          </Grid>
```

**Note on fidelity:** this places each day's entries stacked in time order beside a gutter of slot rows, rather than pinning each entry to its exact slot row. That is the honest, simple version and it is what this task ships. If the vertical alignment between gutter and entries reads as wrong when you look at it, **stop and report that** rather than inventing a Canvas or a converter — `SlotIndex` exists precisely so a later change can align them with row definitions, and that is a decision for Malin, not a silent redesign.

- [ ] **Step 3: Dim disabled entries in the grid**

The agenda already carries this trigger from Task 5. Add the same one to the grid's entry `Border`:

```xml
                          <Border.Style>
                            <Style TargetType="Border">
                              <Style.Triggers>
                                <DataTrigger Binding="{Binding IsEnabled}" Value="False">
                                  <!-- TextMuted, never TextFaint: a disabled row still has to clear
                                       4.5:1. The "off" state is also in the accessible name and is
                                       never carried by dimming alone. -->
                                  <Setter Property="TextElement.Foreground" Value="{StaticResource TextMuted}"/>
                                  <Setter Property="Opacity" Value="0.85"/>
                                </DataTrigger>
                              </Style.Triggers>
                            </Style>
                          </Border.Style>
```

**Check the exact token key names** (`Accent`, `ElevatedBg`, `Border`, `RadiusSm`, `TextMuted`, `TextFaint`, `TextXs`) against `src/Tidsro/Resources/tokens.xaml` before pasting, and use whatever that file actually defines.

- [ ] **Step 4: Build, test, and look at it wide**

Run: `dotnet test` — expected PASS.

```bash
dotnet build && Start-Process src/Tidsro/bin/Debug/net10.0-windows/Tidsro.exe
```

Widen the window past 760px and check:
- The agenda gives way to the grid, and narrowing brings it back.
- Seven day columns, today marked with both a dot and the brass accent.
- A disabled alarm reads as muted but is still comfortably legible.
- A very long label ellipsises rather than pushing the column wide.

- [ ] **Step 5: Read the UIA tree in the wide rendering**

With the window wider than 760px and at least two recurring alarms present, run the UIA read (Windows PowerShell, `UIAutomationClient`, walking `ControlViewWalker`). Confirm:
- Day columns announce as "Wednesday, today, 2 alarms" and the like.
- Entries announce as "Code class, Wednesday, 09:00" / "Code class, Wednesday, 09:00, off".
- Nothing announces as `Tidsro.Models.TimetableEntry` — that means an accessible name landed on a Border and is dead.
- The tree does not contain a cell per empty slot.

- [ ] **Step 6: Commit**

```bash
git add src/Tidsro/Views/MainWindow.xaml src/Tidsro/Models/TimetableLayout.cs tests/Tidsro.Tests/TimetableLayoutTests.cs
git commit -m "feat(app): render the week as a seven-column grid when there is room

A time gutter and seven day columns above 760px, with today marked by a glyph
as well as the accent so colour never carries state alone. Day columns are
named for a screen reader, since the rendering is chosen by window width and
width is a poor proxy for eyesight."
```

---

### Task 7: Documentation

**Files:**
- Modify: `CHANGELOG.md`
- Modify: `README.md`

- [ ] **Step 1: Add the changelog entry**

Add a `## [2.3.0]` section at the top of the entries in `CHANGELOG.md`, matching the format of the existing `## [2.2.0]` section exactly (including the bottom link line). Content:

```markdown
### Added
- **Week tab** — a read-only view of your repeating alarms laid out Monday to Sunday. Narrow windows
  show a day-by-day agenda; widen past 760px for a seven-column grid. The scale fits the hours you
  actually use rather than showing an empty 24 hours.
```

Do **not** bump `<Version>` in `src/Tidsro/Tidsro.csproj` — releases are a separate, hand-run process.

- [ ] **Step 2: Add the README line**

Add one line to the README's feature list, in the voice of the lines already there. No new section, no screenshot yet.

- [ ] **Step 3: Note the screenshot requirement**

The README screenshot is **not** part of this task, because it must not be shot against the real schedule. Leave the feature list line without an image and record in the PR description that the release pass owes:

> Week-tab screenshot, shot against a **fixture** timetable (invented class names and hours, a scratch `%AppData%\Tidsro\data.json`), never the live schedule — a week grid publishes what its owner does, on which days, at which hours.

- [ ] **Step 4: Commit**

```bash
git add CHANGELOG.md README.md
git commit -m "docs: describe the Week tab

Changelog entry and a README feature line. No screenshot: a week grid publishes
its owner's actual schedule, so the shot owes a fixture timetable and belongs to
the release pass."
```

---

## Done when

- `dotnet test` is green, with more tests than the branch started with and none removed.
- The Week tab exists, cycles with Ctrl+Tab, and renders both ways around 760px.
- Tab headers still respond to a single click (the tab-shell regression).
- The UIA read is clean in both renderings.
- `CHANGELOG.md` and `README.md` describe the feature; no version bump, no screenshot.
