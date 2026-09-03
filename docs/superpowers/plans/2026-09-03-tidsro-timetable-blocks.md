# Timetable Blocks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A recurring alarm can carry an optional end time, and the Week tab draws it as a block at its real length with the current one highlighted.

**Architecture:** One nullable field, `int? EndMinute`, on `RecurringAlarmRecord` and `TimerItem` — the whole of schema 5, with no migration step because null already means "an instant". `TimetableLayout` grows three responsibilities: rows for slots a block *covers*, a segment role per row so a block draws as one bar without needing a `Grid.RowSpan` the layout cannot provide, and lane assignment for overlaps. The scheduler is not touched at all.

**Tech Stack:** C# / .NET 10 (`net10.0-windows`), WPF, CommunityToolkit.Mvvm, System.Text.Json, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-03-tidsro-timetable-blocks-design.md`

## Global Constraints

- **No second fire point.** `SchedulerService` must not read `EndMinute`. If it compiles without the field it must behave identically.
- **Schema 5.** `TidsroData.CurrentSchema` becomes 5. No migration step; a v4 file has no key and loads as null.
- **A bad end nulls the end, never drops the alarm.** In `Sanitized`, and independently in `Build`.
- **Same day only.** An end must be strictly after the start, 0–1439.
- **Lanes cap at 3**, and a lane needs 90px of readable width.
- **Commits carry no AI attribution.** No `Co-Authored-By: Claude`, no "Generated with Claude Code". Malin is the sole author.
- **Do not force-kill Tidsro to clear a build lock.** If `bin/.../Tidsro.exe` is locked, stop and say so — a force-kill skips `OnExit`/`SaveData` and destroys unsaved schedule edits.
- **Run tests with an out-dir** to sidestep the same lock: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj -o <scratch>`.

---

### Task 1: Schema 5 — the field, the round trip, the sanitiser

**Files:**
- Modify: `src/Tidsro/Models/RecurringAlarmRecord.cs`
- Modify: `src/Tidsro/Models/TimerItem.cs`
- Modify: `src/Tidsro/Models/TidsroData.cs` (`CurrentSchema`, the recurring loop in `Sanitized`)
- Modify: `src/Tidsro/Services/SchedulerService.cs:69-70` (`ArmRecurringAlarm` signature only)
- Modify: `src/Tidsro/App.xaml.cs:399` (`ToRecurringRecord`), `:424` (`ArmLoadedRecurring`)
- Test: `tests/Tidsro.Tests/PersistenceServiceTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `RecurringAlarmRecord.EndMinute` (`int?`), `TimerItem.EndMinute` (`int?`), `TidsroData.CurrentSchema == 5`, and `SchedulerService.ArmRecurringAlarm(..., bool enabled = true, int? endMinute = null)`.

- [ ] **Step 1: Write the failing tests**

In `PersistenceServiceTests.cs`:

```csharp
[Fact]
public void A_v4_file_loads_with_no_ends_and_loses_no_alarm()
{
    var json = """
    {"SchemaVersion":4,"Settings":null,"Alarms":[],
     "RecurringAlarms":[{"Id":"11111111-1111-1111-1111-111111111111","Hour":9,"Minute":0,
       "Days":1,"Label":"Class","Sound":0,"NextFireAt":"2026-01-05T09:00:00","WarnBefore":false,
       "Enabled":true}]}
    """;
    var data = JsonSerializer.Deserialize<TidsroData>(json)!.Sanitized();

    Assert.Single(data.RecurringAlarms);
    Assert.Null(data.RecurringAlarms[0].EndMinute);
    Assert.Equal(5, data.SchemaVersion);
}

[Theory]
[InlineData(-1)]      // below range
[InlineData(1440)]    // above range
[InlineData(540)]     // equal to a 09:00 start
[InlineData(480)]     // before a 09:00 start
public void A_bad_end_is_nulled_and_the_alarm_survives(int end)
{
    var data = new TidsroData
    {
        RecurringAlarms = { Recurring(hour: 9, minute: 0, endMinute: end) },
    }.Sanitized();

    Assert.Single(data.RecurringAlarms);
    Assert.Null(data.RecurringAlarms[0].EndMinute);
}

[Fact]
public void A_good_end_survives_a_round_trip()
{
    var data = new TidsroData
    {
        RecurringAlarms = { Recurring(hour: 9, minute: 0, endMinute: 630) },
    }.Sanitized();

    var json = JsonSerializer.Serialize(data);
    var back = JsonSerializer.Deserialize<TidsroData>(json)!.Sanitized();

    Assert.Equal(630, back.RecurringAlarms[0].EndMinute);
}

private static RecurringAlarmRecord Recurring(int hour, int minute, int? endMinute) => new()
{
    Hour = hour,
    Minute = minute,
    Days = Weekdays.Mon,
    Label = "Class",
    Sound = SoundChoice.None,
    NextFireAt = new DateTime(2026, 1, 5, hour, minute, 0),
    EndMinute = endMinute,
};
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj -o "$env:TEMP/tidsro-t1" --filter "FullyQualifiedName~PersistenceServiceTests"`
Expected: FAIL — `RecurringAlarmRecord` does not contain a definition for `EndMinute`.

- [ ] **Step 3: Add the field to both models**

In `RecurringAlarmRecord.cs`, after `Enabled`:

```csharp
    /// <summary>Minutes from midnight when this block ends, or null for an instant. Schema 5.
    /// Null is the legacy meaning, which is why a v4 file needs no migration step.</summary>
    public int? EndMinute { get; set; }
```

In `TimerItem.cs`, beside `WarnBefore`:

```csharp
    /// <summary>Minutes from midnight when a recurring block ends, or null for an instant.
    /// Display only: the scheduler never reads this.</summary>
    public int? EndMinute { get; set; }
```

- [ ] **Step 4: Raise the schema and add the sanitiser rule**

In `TidsroData.cs`, change `CurrentSchema` to `5`. Inside the recurring loop, after the existing `Enabled` guard chain and before `recurring.Add(...)`:

```csharp
            // A bad end nulls the end; it never drops the alarm. Nothing about an unusable end
            // justifies losing an alarm out of the user's schedule — the same posture as
            // TimetableLayout.Build, which skips what it cannot place and renders the rest.
            var end = r.EndMinute;
            if (end is { } e && (e < 0 || e > 1439 || e <= r.Hour * 60 + r.Minute)) end = null;
```

and add `EndMinute = end,` to the `new RecurringAlarmRecord { ... }` initialiser.

- [ ] **Step 5: Carry it through the round trip**

In `SchedulerService.cs:69-70`, add a final optional parameter and assign it on the created item — and nothing else:

```csharp
    public TimerItem ArmRecurringAlarm(int hour, int minute, Weekdays days, string? label, SoundChoice sound,
        Guid? id = null, DateTimeOffset? nextFireAt = null, bool warnBefore = false, bool enabled = true,
        int? endMinute = null)
```

Set `EndMinute = endMinute,` in the `new TimerItem { ... }` initialiser. Do not reference it in `Tick`, in the warning path, or anywhere else.

In `App.xaml.cs`, add `EndMinute = a.EndMinute,` to `ToRecurringRecord`, and pass `r.EndMinute` as the last argument in `ArmLoadedRecurring`'s `ArmRecurringAlarm` call.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj -o "$env:TEMP/tidsro-t1"`
Expected: PASS, and the whole suite still green (480 tests before this task).

- [ ] **Step 7: Commit**

```bash
git add src/Tidsro/Models src/Tidsro/Services/SchedulerService.cs src/Tidsro/App.xaml.cs tests/Tidsro.Tests/PersistenceServiceTests.cs
git commit -m "feat: add EndMinute to recurring alarms (schema 5)"
```

---

### Task 2: Rows for covered slots, and segment roles

**Files:**
- Modify: `src/Tidsro/Models/TimetableLayout.cs`
- Test: `tests/Tidsro.Tests/TimetableLayoutTests.cs`

**Interfaces:**
- Consumes: `TimerItem.EndMinute` from Task 1.
- Produces: `SegmentRole` enum (`Instant`, `Start`, `Middle`, `End`, `Whole`); `TimetableEntry.EndMinute` (`int?`), `.IsBlock` (`bool`), `.Role` (`SegmentRole`), `.RangeText` (`string`); `Placed.EndMinute` (`int?`).

- [ ] **Step 1: Write the failing tests**

In `TimetableLayoutTests.cs`, extend the `Recurring` helper with an end and add:

```csharp
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
    // 09:00-10:30 covers the 09:00, 09:30 and 10:00 slots.
    var week = TimetableLayout.Build(new[] { Block(9, 0, 630, Weekdays.Mon) }, At(5, 8, 0));

    Assert.Equal(3, week.Rows.Count);
    Assert.Equal("09:00", week.Rows[0].GutterLabel);
    Assert.Equal("09:30", week.Rows[1].GutterLabel);
    Assert.Equal("10:00", week.Rows[2].GutterLabel);
}

[Fact]
public void Empty_time_between_two_blocks_stays_collapsed()
{
    var week = TimetableLayout.Build(
        new[] { Block(9, 0, 570, Weekdays.Mon), Block(15, 0, 930, Weekdays.Mon) }, At(5, 8, 0));

    Assert.Equal(2, week.Rows.Count);   // 09:00 and 15:00, nothing between
}

[Fact]
public void A_block_over_three_rows_is_start_middle_end()
{
    var week = TimetableLayout.Build(new[] { Block(9, 0, 630, Weekdays.Mon) }, At(5, 8, 0));
    var roles = week.Rows.Select(r => r.Cells[0].Entries.Single().Role).ToArray();

    Assert.Equal(new[] { SegmentRole.Start, SegmentRole.Middle, SegmentRole.End }, roles);
}

[Fact]
public void A_block_inside_one_slot_is_whole()
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
}

[Fact]
public void An_end_at_or_before_the_start_is_drawn_as_an_instant()
{
    // Build is total: EndsAt and EndMinute are different sources and Sanitized compares
    // EndMinute against Hour/Minute, not against EndsAt. A negative span must not reach the walk.
    var week = TimetableLayout.Build(new[] { Block(9, 0, 540, Weekdays.Mon) }, At(5, 8, 0));

    Assert.Equal(SegmentRole.Instant, week.Rows.Single().Cells[0].Entries.Single().Role);
}

[Fact]
public void The_span_pads_from_the_latest_end_not_the_latest_start()
{
    var week = TimetableLayout.Build(new[] { Block(9, 0, 1080, Weekdays.Mon) }, At(5, 8, 0));
    Assert.Equal("19:00", week.Slots[^1].Label);   // 18:00 end + an hour of padding
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj -o "$env:TEMP/tidsro-t2" --filter "FullyQualifiedName~TimetableLayoutTests"`
Expected: FAIL — `SegmentRole` not found, `TimerItem.EndMinute` unknown to the helper.

- [ ] **Step 3: Add the role and the entry fields**

In `TimetableLayout.cs`, above `TimetableEntry`:

```csharp
/// <summary>Which piece of a block a row is drawing. The wide grid draws one independent element
/// per row, so there is no shared vertical grid for a Grid.RowSpan; a block is one segment per row
/// it covers, and these say which. Instant is an alarm with no end at all.</summary>
public enum SegmentRole { Instant, Start, Middle, End, Whole }
```

Extend the record and add the derived members:

```csharp
public sealed record TimetableEntry(
    Guid Id, string? Label, string DayName, int Hour, int Minute, SoundChoice Sound, bool IsEnabled,
    int SlotIndex, int? EndMinute, SegmentRole Role)
{
    public bool IsBlock => EndMinute is not null;

    /// <summary>"09:00" for an instant, "09:00–10:30" for a block. En dash, as the app's copy uses.</summary>
    public string RangeText => EndMinute is { } e
        ? $"{TimeText}–{e / 60:D2}:{e % 60:D2}"
        : TimeText;
```

and change `AccessibleName` to speak the range in words:

```csharp
    public string AccessibleName
    {
        get
        {
            var time = EndMinute is { } e ? $"{TimeText} to {e / 60:D2}:{e % 60:D2}" : TimeText;
            return IsEnabled ? $"{DisplayLabel}, {DayName}, {time}" : $"{DisplayLabel}, {DayName}, {time}, off";
        }
    }
```

- [ ] **Step 4: Place a block in every slot it covers**

In `Build`, extend `Placed` with `int? EndMinute` and populate it in `Collect` with the totality guard:

```csharp
            // Build is total. EndsAt gives the start, EndMinute gives the end, and the two are
            // different sources; an end that does not sit after the start is drawn as an instant
            // rather than reaching the covered-slot walk as a negative span.
            var start = next.Hour * 60 + next.Minute;
            var end = a.EndMinute is { } e && e > start && e <= DayMinutes ? e : (int?)null;
            usable.Add(new Placed(a.Id, a.Label, start, a.Sound, a.IsEnabled, days, end));
```

Use the end in `ResolveSpan` (`u.EndMinute ?? u.Minutes` for the max). In the day loop, replace the LINQ projection with a local loop — a block emits one entry per covered slot, and the loop is what makes the role assignment readable:

```csharp
            var entries = new List<TimetableEntry>();
            foreach (var u in usable.Where(u => (u.Days & flag) != 0)
                                    .OrderBy(u => u.Minutes)
                                    .ThenBy(u => u.Label, StringComparer.Ordinal)
                                    .ThenBy(u => u.Id))
            {
                var first = (FloorToSlot(u.Minutes) - startMinutes) / SlotMinutes;
                var last = u.EndMinute is { } e
                    ? (CeilToSlot(e) - SlotMinutes - startMinutes) / SlotMinutes
                    : first;
                if (last < first) last = first;

                for (var s = first; s <= last; s++)
                {
                    var role = u.EndMinute is null ? SegmentRole.Instant
                        : first == last ? SegmentRole.Whole
                        : s == first ? SegmentRole.Start
                        : s == last ? SegmentRole.End
                        : SegmentRole.Middle;
                    entries.Add(new TimetableEntry(
                        u.Id, u.Label, name, u.Minutes / 60, u.Minutes % 60, u.Sound, u.IsEnabled,
                        s, u.EndMinute, role));
                }
            }
```

`BuildRows` needs no change: a slot is occupied when any day's lookup holds it, and a covered slot now holds a segment.

- [ ] **Step 5: Keep the gutter honest**

`TimetableRow.SharedTime` must walk **starts only**, or a continuation row will claim a time nothing starts at:

```csharp
            foreach (var cell in Cells)
                foreach (var entry in cell.Entries)
                {
                    if (entry.Role is SegmentRole.Middle or SegmentRole.End) continue;
                    if (shared is null) shared = entry.TimeText;
                    else if (shared != entry.TimeText) return null;
                }
```

A row of nothing but continuations leaves `shared` null and falls back to `Slot.Label`, which is what the spec asks for.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj -o "$env:TEMP/tidsro-t2"`
Expected: PASS. Phase-1 tests that construct `TimetableEntry` positionally will need the two new arguments — update them, do not change their assertions.

- [ ] **Step 7: Commit**

```bash
git add src/Tidsro/Models/TimetableLayout.cs tests/Tidsro.Tests/TimetableLayoutTests.cs
git commit -m "feat: draw a block as one segment per row it covers"
```

---

### Task 3: Lanes

**Files:**
- Modify: `src/Tidsro/Models/TimetableLayout.cs`
- Test: `tests/Tidsro.Tests/TimetableLayoutTests.cs`

**Interfaces:**
- Consumes: `SegmentRole`, `TimetableEntry` from Task 2.
- Produces: `TimetableEntry.LaneIndex` (`int`), `.LaneCount` (`int`); `TimetableCell.OverflowCount` (`int`), `.HasOverflow` (`bool`); `TimetableLayout.MaxLanes` (`const int` = 3).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void A_day_with_no_overlap_has_one_lane()
{
    var week = TimetableLayout.Build(
        new[] { Block(9, 0, 570, Weekdays.Mon), Block(11, 0, 690, Weekdays.Mon) }, At(5, 8, 0));

    Assert.All(week.Rows.SelectMany(r => r.Cells[0].Entries), e => Assert.Equal(1, e.LaneCount));
}

[Fact]
public void Two_overlapping_blocks_take_two_lanes()
{
    var week = TimetableLayout.Build(
        new[] { Block(9, 0, 630, Weekdays.Mon), Block(9, 30, 660, Weekdays.Mon, "Lab") }, At(5, 8, 0));
    var row = week.Rows.First(r => r.GutterLabel == "09:30");

    Assert.Equal(new[] { 0, 1 }, row.Cells[0].Entries.Select(e => e.LaneIndex).ToArray());
    Assert.All(row.Cells[0].Entries, e => Assert.Equal(2, e.LaneCount));
}

[Fact]
public void An_instant_inside_a_block_takes_its_own_lane()
{
    var week = TimetableLayout.Build(
        new[] { Block(9, 0, 660, Weekdays.Mon), Recurring(10, 0, Weekdays.Mon, "Stretch") }, At(5, 8, 0));
    var row = week.Rows.First(r => r.GutterLabel == "10:00");

    Assert.Equal(2, row.Cells[0].Entries.Count);
    Assert.Equal(new[] { 0, 1 }, row.Cells[0].Entries.Select(e => e.LaneIndex).ToArray());
}

[Fact]
public void Lane_order_is_start_order()
{
    var week = TimetableLayout.Build(
        new[] { Block(9, 30, 660, Weekdays.Mon, "Later"), Block(9, 0, 630, Weekdays.Mon, "Earlier") },
        At(5, 8, 0));
    var row = week.Rows.First(r => r.GutterLabel == "09:30");

    Assert.Equal("Earlier", row.Cells[0].Entries[0].Label);
    Assert.Equal(0, row.Cells[0].Entries[0].LaneIndex);
}

[Fact]
public void A_fifty_way_overlap_caps_at_three_lanes_and_says_how_many_are_hidden()
{
    var many = Enumerable.Range(0, 50).Select(i => Block(9, 0, 630, Weekdays.Mon, $"Class {i}")).ToArray();
    var week = TimetableLayout.Build(many, At(5, 8, 0));
    var cell = week.Rows.First().Cells[0];

    Assert.Equal(TimetableLayout.MaxLanes, cell.Entries.Count);
    Assert.All(cell.Entries, e => Assert.True(e.LaneIndex < TimetableLayout.MaxLanes));
    Assert.True(cell.HasOverflow);
    Assert.Equal(47, cell.OverflowCount);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj -o "$env:TEMP/tidsro-t3" --filter "lane"`
Expected: FAIL — `LaneIndex` not found.

- [ ] **Step 3: Assign lanes per day**

Add to `TimetableEntry`'s parameter list: `int LaneIndex, int LaneCount`. Add to `TimetableLayout`:

```csharp
    /// <summary>How many lanes a day column will draw side by side. Lanes are the one axis this
    /// view leaves unbounded — rows stop at 48 because the span clamps to a day, but a cluster is as
    /// wide as the number of alarms overlapping, and an import is capped at 8 MB rather than at a
    /// count. Three keeps a lane readable and keeps a hostile file from becoming a UniformGrid with
    /// thousands of columns in every cell.</summary>
    public const int MaxLanes = 3;

    /// <summary>Assign each entry the lowest lane free at its start, walking in start order so that
    /// lane order is time order — which is also announcement order. Entries past MaxLanes are
    /// dropped from the grid and counted; the agenda is a list and still shows every one.</summary>
    private static (List<Placed> Kept, int Overflow, int LaneCount) AssignLanes(List<Placed> day)
    {
        var laneEnds = new List<int>();     // laneEnds[i] = the minute lane i is free again
        var kept = new List<Placed>();
        var overflow = 0;

        foreach (var p in day.OrderBy(p => p.Minutes).ThenBy(p => p.Label, StringComparer.Ordinal)
                             .ThenBy(p => p.Id))
        {
            var end = p.EndMinute ?? p.Minutes + 1;      // an instant occupies a moment, not a span
            var lane = laneEnds.FindIndex(e => e <= p.Minutes);
            if (lane < 0) { lane = laneEnds.Count; laneEnds.Add(end); }
            else laneEnds[lane] = end;

            if (lane >= MaxLanes) { overflow++; continue; }
            kept.Add(p with { LaneIndex = lane });
        }

        return (kept, overflow, Math.Min(laneEnds.Count, MaxLanes));
    }
```

Add `int LaneIndex` to `Placed`, run `AssignLanes` per weekday before building entries, and give every entry of that day the returned `LaneCount`.

- [ ] **Step 4: Carry the overflow onto the cell**

```csharp
public sealed record TimetableCell(
    Weekdays Day, string DayName, IReadOnlyList<TimetableEntry> Entries, int OverflowCount)
{
    public bool HasOverflow => OverflowCount > 0;

    /// <summary>What the grid prints instead of the entries it had no lane for. The agenda shows
    /// them all, so this summarises rather than hides.</summary>
    public string OverflowText => $"+{OverflowCount} more";
}
```

The overflow count is per day, so `BuildRows` passes the day's count to every cell of that day.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj -o "$env:TEMP/tidsro-t3"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Tidsro/Models/TimetableLayout.cs tests/Tidsro.Tests/TimetableLayoutTests.cs
git commit -m "feat: lay overlapping blocks out in lanes, capped at three"
```

---

### Task 4: The current block

**Files:**
- Modify: `src/Tidsro/Models/TimetableLayout.cs` (`IsCurrent`)
- Modify: `src/Tidsro/ViewModels/TimetableViewModel.cs`
- Test: `tests/Tidsro.Tests/TimetableLayoutTests.cs`, `tests/Tidsro.Tests/TimetableViewModelTests.cs`

**Interfaces:**
- Consumes: `TimetableEntry` from Tasks 2–3.
- Produces: `TimetableLayout.IsCurrent(TimetableEntry entry, bool isToday, int nowMinuteOfDay)`; `TimetableViewModel.NowMinuteOfDay` (`int`, observable).

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory]
[InlineData(540, true)]    // 09:00, the start minute, is current
[InlineData(600, true)]    // 10:00, inside
[InlineData(630, false)]   // 10:30, the end minute, is not
[InlineData(539, false)]   // a minute before
public void IsCurrent_is_start_inclusive_and_end_exclusive(int now, bool expected)
{
    var entry = BlockEntry(hour: 9, minute: 0, endMinute: 630);
    Assert.Equal(expected, TimetableLayout.IsCurrent(entry, isToday: true, now));
}

[Fact]
public void Nothing_is_current_on_another_day()
{
    var entry = BlockEntry(9, 0, 630);
    Assert.False(TimetableLayout.IsCurrent(entry, isToday: false, 600));
}

[Fact]
public void An_instant_and_a_disabled_block_are_never_current()
{
    Assert.False(TimetableLayout.IsCurrent(BlockEntry(9, 0, null), true, 540));
    Assert.False(TimetableLayout.IsCurrent(BlockEntry(9, 0, 630, enabled: false), true, 600));
}
```

In `TimetableViewModelTests.cs` — **read that file's existing fixture first and use its own construction helper and fake clock**; the shape below is the assertion, not the setup:

```csharp
[Fact]
public void A_tick_inside_the_same_minute_raises_nothing()
{
    var (vm, clock) = MakeViewModel(at: new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero));
    var raised = 0;
    vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.NowMinuteOfDay)) raised++; };

    clock.Advance(TimeSpan.FromMilliseconds(250));
    vm.RefreshForTick();
    Assert.Equal(0, raised);

    clock.Advance(TimeSpan.FromMinutes(1));
    vm.RefreshForTick();
    Assert.Equal(1, raised);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj -o "$env:TEMP/tidsro-t4" --filter "current|Tick"`
Expected: FAIL — `IsCurrent` and `NowMinuteOfDay` not found.

- [ ] **Step 3: Add the pure rule**

```csharp
    /// <summary>Whether this entry is happening now. Start inclusive, end exclusive. An instant has
    /// no duration and is never current; a disabled block is never lit.</summary>
    public static bool IsCurrent(TimetableEntry entry, bool isToday, int nowMinuteOfDay)
    {
        if (!isToday || !entry.IsEnabled || entry.EndMinute is not { } end) return false;
        var start = entry.Hour * 60 + entry.Minute;
        return nowMinuteOfDay >= start && nowMinuteOfDay < end;
    }
```

- [ ] **Step 4: Gate the notification on the minute**

In `TimetableViewModel.RefreshForTick`, **date check first**, then the minute:

```csharp
    public void RefreshForTick()
    {
        var now = _scheduler.Now;

        // The date check runs first: at 00:00 both change on the same tick, and reading the minute
        // against a week built for yesterday would light a block in yesterday's column for one tick.
        if (DateOnly.FromDateTime(now.Date) != _builtFor) Rebuild();

        // Only when the minute actually changes. Assigning on every 250 ms tick would raise this
        // 345,000 times a day and re-run the highlight converter for every entry on screen — the
        // cost the date gate above exists to avoid, reintroduced one layer down.
        var minute = now.Hour * 60 + now.Minute;
        if (minute != NowMinuteOfDay) NowMinuteOfDay = minute;
    }
```

with `[ObservableProperty] private int _nowMinuteOfDay;` initialised in the constructor alongside `Rebuild()`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj -o "$env:TEMP/tidsro-t4"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Tidsro/Models/TimetableLayout.cs src/Tidsro/ViewModels/TimetableViewModel.cs tests/Tidsro.Tests
git commit -m "feat: mark the block that is happening now"
```

---

### Task 5: The wide grid

**Files:**
- Modify: `src/Tidsro/Views/MainWindow.xaml` (the `TimetableCell` style ~line 29; the row template ~lines 738-800)
- Modify: `src/Tidsro/Views/Converters.cs`
- Test: manual, plus a UIA read in Task 8

**Interfaces:**
- Consumes: `SegmentRole`, `LaneIndex`, `LaneCount`, `OverflowText`, `IsCurrent`, `NowMinuteOfDay`.
- Produces: no C# API; the rendering other tasks assume.

- [ ] **Step 1: Add the highlight converter**

In `Converters.cs`, a multi-value converter over `(entry, isToday, nowMinuteOfDay)` that calls the pure rule — no arithmetic of its own, per the spec's principle:

```csharp
/// <summary>Asks TimetableLayout whether an entry is happening now. The rule lives in the model
/// where tests reach it; this only plumbs three bindings into it.</summary>
public sealed class EntryIsCurrentConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values is [TimetableEntry entry, bool isToday, int now]
        && TimetableLayout.IsCurrent(entry, isToday, now);

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 2: Draw lanes inside each cell**

Replace the `TimetableCell` `ItemsControl`'s default panel with a `UniformGrid Rows="1"` bound to nothing — the entries already carry `LaneIndex` in order, so a plain horizontal `UniformGrid` over the cell's entries puts them in lane order. Under it, the overflow line:

```xml
<StackPanel>
  <ItemsControl Style="{StaticResource TimetableCell}" ItemsSource="{Binding Cells[0].Entries}">
    <ItemsControl.ItemsPanel>
      <ItemsPanelTemplate><UniformGrid Rows="1"/></ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
  </ItemsControl>
  <TextBlock Text="{Binding Cells[0].OverflowText}" Style="{StaticResource Muted}"
             Visibility="{Binding Cells[0].HasOverflow, Converter={StaticResource BoolToVisible}}"/>
</StackPanel>
```

Repeat per column index, keeping the existing `Border` wrapper and the two weekend `Border`s with their `HidesWeekend` visibility — **do not reintroduce fixed `Grid.Column` indices**; the `UniformGrid Rows="1"` over visible `Border`s is the alignment invariant from v2.3.0.

- [ ] **Step 3: Template the segment roles**

A `DataTrigger` set on the entry template keyed off `Role`:

- `Instant` — unchanged from phase 1.
- `Start` / `Whole` — the bar, with the label and `RangeText`.
- `Middle` / `End` — the bar only, with `AutomationProperties.Name` unset and the text content collapsed, so a three-row block is announced once rather than three times. The `Border` stays visible (it holds the column open); its content collapses.

The bar's fill is `BorderControl`; when the highlight converter returns true it is the brass accent, and the entry's name gains `, now`. No animation on the change.

- [ ] **Step 4: Hold a lane to 90px**

The grid/agenda flip already runs through `WidthToVisibleConverter` at 760px reading `Panels.ActualWidth`. Extend it so the threshold accounts for the widest day: `760 + (maxLanes - 1) * 90 * 5`. Below that the agenda shows, which is the rendering that lists everything anyway.

- [ ] **Step 5: Run the app and look at it**

Build in the foreground, then `Start-Process` the built exe — background `dotnet run` does not surface the window on this machine.

```bash
dotnet build src/Tidsro/Tidsro.csproj
```

Check: a block reads as one bar; two overlapping blocks sit side by side; the gutter of a continuation row shows the band; the current block is lit.

- [ ] **Step 6: Commit**

```bash
git add src/Tidsro/Views
git commit -m "feat: draw blocks and lanes in the week grid"
```

---

### Task 6: The agenda and the Schedule tab

**Files:**
- Modify: `src/Tidsro/Views/MainWindow.xaml` (the agenda `ItemsControl` ~line 558)
- Modify: `src/Tidsro/ViewModels/AlarmItemViewModel.cs`
- Test: `tests/Tidsro.Tests/AlarmItemViewModelTests.cs`

**Interfaces:**
- Consumes: `TimetableEntry.RangeText`, `TimerItem.EndMinute`.
- Produces: `AlarmItemViewModel.TimeText` returning a range for a block.

- [ ] **Step 1: Write the failing test**

Find where the Schedule row's time string is built and which test file already covers it — `AlarmItemViewModelTests.cs` if it exists, otherwise the file that tests that view model — and follow its existing construction helper:

```csharp
[Fact]
public void A_recurring_row_with_an_end_reads_as_a_range()
{
    var vm = MakeRecurring(hour: 9, minute: 0, days: Weekdays.Mon | Weekdays.Wed, endMinute: 630);
    Assert.Equal("Mon Wed 09:00–10:30", vm.TimeText);
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj -o "$env:TEMP/tidsro-t6" --filter "range"`
Expected: FAIL — reads `Mon Wed 09:00`.

- [ ] **Step 3: Append the end where the row builds its time**

Follow the existing string construction; append `–HH:MM` when `EndMinute` is not null, using the same en dash as `RangeText`.

- [ ] **Step 4: Bind the agenda to `RangeText`**

In the agenda entry template, swap `TimeText` for `RangeText`. Nothing else changes: an instant's `RangeText` is its `TimeText`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj -o "$env:TEMP/tidsro-t6"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Tidsro/Views/MainWindow.xaml src/Tidsro/ViewModels/AlarmItemViewModel.cs tests/Tidsro.Tests
git commit -m "feat: show the range on the agenda and the schedule row"
```

---

### Task 7: Entering an end

**Files:**
- Modify: `src/Tidsro/ViewModels/EditAlarmViewModel.cs:16,39,50,61-65`
- Modify: `src/Tidsro/Views/EditAlarmWindow.xaml`
- Modify: `src/Tidsro/ViewModels/MainViewModel.cs` (the apply callback and the add path)
- Test: `tests/Tidsro.Tests/EditAlarmViewModelTests.cs`

**Interfaces:**
- Consumes: `ClockTimeRules.TryParse`, `TimerItem.EndMinute`.
- Produces: `EditAlarmViewModel.EndInput` (`string`); `_apply` becomes `Action<Guid, int, int, Weekdays, string?, SoundChoice, bool, int?>`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void An_empty_end_saves_as_an_instant()
{
    var vm = MakeEdit(time: "09:00", end: "");
    vm.SaveCommand.Execute(null);
    Assert.Null(CapturedEndMinute);
    Assert.Null(vm.Error);
}

[Fact]
public void An_unparseable_end_keeps_the_dialog_open()
{
    var vm = MakeEdit(time: "09:00", end: "half nine");
    vm.SaveCommand.Execute(null);
    Assert.NotNull(vm.Error);
    Assert.False(Saved);
}

[Fact]
public void An_end_at_or_before_the_start_keeps_the_dialog_open()
{
    var vm = MakeEdit(time: "09:00", end: "09:00");
    vm.SaveCommand.Execute(null);
    Assert.Equal("The end must be after the start.", vm.Error);
    Assert.False(Saved);
}

[Fact]
public void A_good_end_is_saved_as_minutes_from_midnight()
{
    var vm = MakeEdit(time: "09:00", end: "10:30");
    vm.SaveCommand.Execute(null);
    Assert.Equal(630, CapturedEndMinute);
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj -o "$env:TEMP/tidsro-t7" --filter "EditAlarm"`
Expected: FAIL — `EndInput` not found.

- [ ] **Step 3: Validate in the same pass as the start**

```csharp
    [ObservableProperty] private string _endInput = string.Empty;

    private void Save()
    {
        if (!ClockTimeRules.TryParse(TimeInput, out var h, out var m, out var err)) { Error = err; return; }

        int? end = null;
        if (!string.IsNullOrWhiteSpace(EndInput))
        {
            // The same parser as the start, so the two inputs accept and reject identically.
            if (!ClockTimeRules.TryParse(EndInput, out var eh, out var em, out var endErr))
            { Error = endErr; return; }

            end = eh * 60 + em;
            // The one place a bad end is reported rather than repaired: here there is a person to tell.
            if (end <= h * 60 + m) { Error = "The end must be after the start."; return; }
        }

        Error = null;
        _apply(_id, h, m, ResolveDays(), Label, SelectedSound, WarnBefore, end);
    }
```

- [ ] **Step 4: Add the field to the dialog**

A `TextBox` beside the existing time input, labelled "Ends (optional)", bound `Text="{Binding EndInput, UpdateSourceTrigger=PropertyChanged}"`, with an `AutomationProperties.LabeledBy` pointing at its label — a `Run.Text` binding here would default to TwoWay and is the documented trap. Pre-fill it from the alarm's `EndMinute` when the dialog opens. The add path gets the same field.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj -o "$env:TEMP/tidsro-t7"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Tidsro/ViewModels src/Tidsro/Views/EditAlarmWindow.xaml tests/Tidsro.Tests
git commit -m "feat: let an alarm carry an end time"
```

---

### Task 8: Verify, document, release-prep

**Files:**
- Modify: `CHANGELOG.md`, `README.md`, `src/Tidsro/Tidsro.csproj` (`<Version>`)
- Modify: `tools/screenshots/Shoot-Screenshots.ps1` (fixture week gains blocks)

- [ ] **Step 1: Read the automation tree**

Windows PowerShell with `UIAutomationClient`; walk `ControlViewWalker`, filter `ControlType.DataItem`. Confirm a three-row block is announced **once**, that its name reads "Lecture, Monday, 09:00 to 10:30", that a lane is not announced, and that the current block's name ends `, now`. Not Narrator.

- [ ] **Step 2: Add blocks to the fixture week**

In `Shoot-Screenshots.ps1`, give the fictional week a block or two — a "Focus block" with a "Stretch" instant inside it shows lanes and segments in one shot. **Never point the rig at real data**; it hashes `data.json` and re-reads `HKCU\...\Run` afterwards and fails if either moved.

- [ ] **Step 3: Re-shoot**

```bash
pwsh ./tools/screenshots/Shoot-Screenshots.ps1
```

- [ ] **Step 4: Document**

`CHANGELOG.md` gets a `## [2.4.0]` section and a bottom link; `README.md` gets a feature line and the new Week shot. Bump `<Version>` in `src/Tidsro/Tidsro.csproj` — the single source of truth for the exe, the installer and `publish.ps1`.

- [ ] **Step 5: Full suite, then hand over**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj -o "$env:TEMP/tidsro-final"`
Expected: PASS, ~505 tests.

Then stop. The manual pass, the merge and the release are Malin's: close Tidsro from its tray first (`publish.ps1` force-kills it and any unsaved edits die), then `./publish.ps1`, tag, and `gh release create` with both binaries.

- [ ] **Step 6: Commit**

```bash
git add CHANGELOG.md README.md src/Tidsro/Tidsro.csproj tools/screenshots docs/screenshots
git commit -m "release: v2.4.0"
```
