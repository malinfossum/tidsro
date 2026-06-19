# Five-minute pre-alarm warning — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an optional per-alarm "Warn me 5 minutes before" heads-up to Schedule alarms — a gentle nudge that buys transition time before the alarm itself.

**Architecture:** A new `Warning` event on `SchedulerService`, raised once per occurrence the instant the clock crosses into the last five minutes before an alarm (dedup via a transient `WarningSent` flag, reset when a recurring alarm rolls its `EndsAt` forward). A persisted per-alarm `WarnBefore` bool drives it. The heads-up reuses the existing corner card in a close-only "warning" variant; `App` plays the soft chime only when the alarm itself is sounded, and closes the card once the alarm fires.

**Tech Stack:** C# / .NET 10, WPF, CommunityToolkit.Mvvm (source-generated `[ObservableProperty]` / `[RelayCommand]`), xUnit. Spec: `docs/superpowers/specs/2026-06-18-tidsro-pre-alarm-warning-design.md`.

**Branch:** `feature/pre-alarm-warning` (already checked out).

**Conventions:**
- Tests run with: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~<name>"` (run from the repo root). Full suite: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj`.
- Stop any running `Tidsro.exe` before building (a running exe locks the build output).
- Commit messages: conventional, **no `Co-Authored-By` / attribution trailer** (per the repo owner's rule).

---

## File Structure

**Modified — production:**
- `src/Tidsro/Models/TimerItem.cs` — add `WarnBefore` (persisted opt-in) + `WarningSent` (transient per-occurrence guard).
- `src/Tidsro/Models/AlarmRecord.cs` / `RecurringAlarmRecord.cs` — add persisted `WarnBefore`.
- `src/Tidsro/Models/TidsroData.cs` — carry `WarnBefore` through `Sanitized()`.
- `src/Tidsro/Services/SchedulerService.cs` — `WarningLead` constant, `Warning` event, `warnBefore` arm params, the tick warning-check + recurring reset.
- `src/Tidsro/ViewModels/PopupViewModel.cs` — a close-only "warning" variant (header/announcement/visible-actions differ).
- `src/Tidsro/ViewModels/AlarmItemViewModel.cs` — agenda warning cue + accessible-name fold.
- `src/Tidsro/ViewModels/MainViewModel.cs` — `AlarmWarnBefore` editor state; thread it through add / edit / undo.
- `src/Tidsro/ViewModels/EditAlarmViewModel.cs` — carry `WarnBefore`; widen the apply callback.
- `src/Tidsro/Views/MainWindow.xaml` / `EditAlarmWindow.xaml` — the editor checkbox.
- `src/Tidsro/Views/CompletionPopup.xaml(.cs)` — bind header/visible-actions/announcement so the card serves both variants.
- `src/Tidsro/App.xaml.cs` — subscribe `Warning` (soft chime + heads-up card), close warnings at fire, persist `WarnBefore`, pass it to the edit dialog.

**Modified — tests:**
- `SchedulerServiceTests.cs`, `TidsroDataTests.cs`, `PersistenceServiceTests.cs`, `PopupViewModelTests.cs`, `AlarmItemViewModelTests.cs`, `EditAlarmViewModelTests.cs`, `MainViewModelTests.cs`.

**No unit tests** for `App.xaml.cs` and the `*.xaml` views — they are the composition root / pure view, manually accepted as in every prior slice (Task 8).

---

## Task 1: Scheduler — the `Warning` signal

**Files:**
- Modify: `src/Tidsro/Models/TimerItem.cs`
- Modify: `src/Tidsro/Services/SchedulerService.cs`
- Test: `tests/Tidsro.Tests/SchedulerServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Append inside `SchedulerServiceTests` (before the closing brace):

```csharp
    // ── Pre-alarm warning (heads-up 5 minutes before) ──

    [Fact]
    public void Tick_raises_Warning_once_when_crossing_into_the_last_five_minutes()
    {
        var (s, c) = New();
        s.ArmClockAlarm(c.Now + TimeSpan.FromMinutes(10), "lunch", SoundChoice.Bell, warnBefore: true); // fires +10, warns +5
        var warned = 0; s.Warning += (_, _) => warned++;

        c.Advance(TimeSpan.FromMinutes(6));   // 6 min in -> inside [+5, +10)
        s.Tick(); s.Tick();                    // two ticks in the window

        Assert.Equal(1, warned);               // once per occurrence
    }

    [Fact]
    public void Tick_does_not_raise_Warning_when_warn_before_is_off()
    {
        var (s, c) = New();
        s.ArmClockAlarm(c.Now + TimeSpan.FromMinutes(10), null, SoundChoice.Bell); // warnBefore defaults false
        var warned = 0; s.Warning += (_, _) => warned++;

        c.Advance(TimeSpan.FromMinutes(6));
        s.Tick();

        Assert.Equal(0, warned);
    }

    [Fact]
    public void Tick_does_not_raise_Warning_at_or_after_the_fire_time()
    {
        var (s, c) = New();
        s.ArmClockAlarm(c.Now + TimeSpan.FromMinutes(10), null, SoundChoice.None, warnBefore: true);
        var warned = 0; s.Warning += (_, _) => warned++;

        c.Advance(TimeSpan.FromMinutes(10));   // exactly the fire time
        s.Tick();                               // fires; not a warning

        Assert.Equal(0, warned);
    }

    [Fact]
    public void An_alarm_armed_inside_the_last_five_minutes_does_not_warn()
    {
        var (s, c) = New();
        s.ArmClockAlarm(c.Now + TimeSpan.FromMinutes(3), null, SoundChoice.Bell, warnBefore: true); // already inside the window
        var warned = 0; s.Warning += (_, _) => warned++;

        c.Advance(TimeSpan.FromMinutes(1));    // 1 min in, still before fire
        s.Tick();

        Assert.Equal(0, warned);               // suppressed: WarningSent initialised true at arm
    }

    [Fact]
    public void Warning_carries_the_live_alarm_so_the_app_can_mirror_its_sound()
    {
        var (s, c) = New();
        var alarm = s.ArmClockAlarm(c.Now + TimeSpan.FromMinutes(10), "lunch", SoundChoice.Bell, warnBefore: true);
        TimerItem? warned = null; s.Warning += (_, i) => warned = i;

        c.Advance(TimeSpan.FromMinutes(6));
        s.Tick();

        Assert.Same(alarm, warned);
        Assert.Equal(SoundChoice.Bell, warned!.Sound);
    }

    [Fact]
    public void A_recurring_alarm_warns_before_each_occurrence()
    {
        var (s, c) = New();   // Thu 2026-01-01 09:00
        s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.Bell, warnBefore: true); // today 10:00
        var warned = 0; s.Warning += (_, _) => warned++;

        c.Advance(TimeSpan.FromMinutes(56));   // 09:56 -> inside [09:55, 10:00)
        s.Tick();
        Assert.Equal(1, warned);

        c.Advance(TimeSpan.FromMinutes(5));    // 10:01 -> fires, advances to Fri 10:00, resets the heads-up
        s.Tick();

        c.Advance(TimeSpan.FromHours(23) + TimeSpan.FromMinutes(55));   // Fri 09:56
        s.Tick();
        Assert.Equal(2, warned);               // warned again for the next occurrence
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~Warning"`
Expected: FAIL — compile errors, `'SchedulerService' does not contain a definition for 'Warning'` and no `warnBefore` parameter on the arm methods.

- [ ] **Step 3: Add the two model fields**

In `src/Tidsro/Models/TimerItem.cs`, add after the `State` property (after line 18):

```csharp
    // Pre-alarm warning (heads-up): the persisted per-alarm opt-in, plus a transient per-occurrence guard
    // (managed inside SchedulerService; never persisted).
    public bool WarnBefore { get; set; }
    public bool WarningSent { get; set; }
```

- [ ] **Step 4: Add the constant, event, arm parameters, and tick logic**

In `src/Tidsro/Services/SchedulerService.cs`:

(a) Add the event after `Fired` (after line 14):

```csharp
    /// <summary>Raised once, WarningLead before an alarm with WarnBefore on, so the App can show a heads-up.</summary>
    public event EventHandler<TimerItem>? Warning;
```

(b) Add the constant after `Grace` (after line 17):

```csharp
    /// <summary>How long before an alarm a WarnBefore heads-up fires.</summary>
    public static readonly TimeSpan WarningLead = TimeSpan.FromMinutes(5);
```

(c) Replace `ArmClockAlarm` (lines 24-37) with:

```csharp
    /// <summary>Arm a one-shot clock-time alarm. Pass <paramref name="id"/> to restore a persisted alarm's identity.</summary>
    public TimerItem ArmClockAlarm(DateTimeOffset fireAt, string? label, SoundChoice sound, Guid? id = null, bool warnBefore = false)
    {
        var item = new TimerItem
        {
            Id = id ?? Guid.NewGuid(),
            TriggerType = TriggerType.ClockTime,
            Label = label,
            Sound = sound,
            EndsAt = fireAt,
            State = TimerState.Running,
            WarnBefore = warnBefore,
            WarningSent = warnBefore && _clock.Now >= fireAt - WarningLead,   // armed inside the window -> no insta-warn
        };
        _alarms.Add(item);
        return item;
    }
```

(d) Replace `ArmRecurringAlarm` (lines 42-57) with:

```csharp
    /// <summary>Arm a recurring alarm. Pass <paramref name="nextFireAt"/> to restore a persisted alarm's next occurrence.</summary>
    public TimerItem ArmRecurringAlarm(int hour, int minute, Weekdays days, string? label, SoundChoice sound,
        Guid? id = null, DateTimeOffset? nextFireAt = null, bool warnBefore = false)
    {
        var ends = nextFireAt ?? RecurrenceRules.NextOccurrence(_clock.Now, hour, minute, days);
        var item = new TimerItem
        {
            Id = id ?? Guid.NewGuid(),
            TriggerType = TriggerType.Recurring,
            Label = label,
            Sound = sound,
            RecurringDays = days,
            EndsAt = ends,
            State = TimerState.Running,
            WarnBefore = warnBefore,
            WarningSent = warnBefore && _clock.Now >= ends - WarningLead,
        };
        _alarms.Add(item);
        return item;
    }
```

(e) Replace the `_alarms` loop in `Tick` (lines 108-133) with — note the new warning check and the `WarningSent = false` reset on roll-forward:

```csharp
        foreach (var alarm in _alarms.ToList())
        {
            if (alarm.State != TimerState.Running || alarm.EndsAt is not { } end) continue;

            // Heads-up: raise Warning once when we cross into the last WarningLead before the alarm.
            if (alarm.WarnBefore && !alarm.WarningSent && now >= end - WarningLead && now < end)
            {
                alarm.WarningSent = true;
                Warning?.Invoke(this, alarm);      // App: soft chime (if sounded) + a heads-up card
            }

            if (now < end) continue;               // not yet fire time

            if (alarm.TriggerType == TriggerType.Recurring && alarm.RecurringDays is { } days)
            {
                var prev = RecurrenceRules.MostRecentOccurrence(now, end.Hour, end.Minute, days);
                alarm.EndsAt = RecurrenceRules.NextOccurrence(now, end.Hour, end.Minute, days);  // advance first: in-session dedup
                alarm.WarningSent = false;         // re-arm the heads-up for the next occurrence
                if (now - prev <= Grace)
                    Fired?.Invoke(this, OccurrenceSnapshot(alarm, prev));    // transient copy -> card can't mutate the live alarm
                else
                    Expired?.Invoke(this, OccurrenceSnapshot(alarm, prev));  // quiet missed-while-away note
                continue;
            }

            _alarms.Remove(alarm);                 // one-shot leaves the armed set whether it fires or expires
            if (now - end <= Grace)
            {
                alarm.State = TimerState.Fired;    // removal + Fired-state == durable single-fire across a tick gap
                Fired?.Invoke(this, alarm);        // sound + corner card (App handler)
            }
            else
            {
                Expired?.Invoke(this, alarm);      // quiet missed-while-away note, no sound/card
            }
        }
```

- [ ] **Step 5: Run the new tests to verify they pass**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~Warning"`
Expected: PASS (6 passed).

- [ ] **Step 6: Run the full suite (no regressions)**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj`
Expected: PASS, 0 warnings.

- [ ] **Step 7: Commit**

```bash
git add src/Tidsro/Models/TimerItem.cs src/Tidsro/Services/SchedulerService.cs tests/Tidsro.Tests/SchedulerServiceTests.cs
git commit -m "feat: raise a Warning signal five minutes before an alarm"
```

---

## Task 2: Persistence — carry `WarnBefore` to disk

**Files:**
- Modify: `src/Tidsro/Models/AlarmRecord.cs`
- Modify: `src/Tidsro/Models/RecurringAlarmRecord.cs`
- Modify: `src/Tidsro/Models/TidsroData.cs`
- Test: `tests/Tidsro.Tests/TidsroDataTests.cs`, `tests/Tidsro.Tests/PersistenceServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

In `TidsroDataTests.cs`, append inside the class:

```csharp
    [Fact]
    public void Sanitized_preserves_the_warn_before_flag_on_a_clock_alarm()
    {
        var a = Good(); a.WarnBefore = true;
        var clean = new TidsroData { Settings = new(), Alarms = { a } }.Sanitized();
        Assert.True(Assert.Single(clean.Alarms).WarnBefore);
    }

    [Fact]
    public void Sanitized_preserves_the_warn_before_flag_on_a_recurring_alarm()
    {
        var r = GoodRec(); r.WarnBefore = true;
        var clean = new TidsroData { Settings = new(), RecurringAlarms = { r } }.Sanitized();
        Assert.True(Assert.Single(clean.RecurringAlarms).WarnBefore);
    }
```

In `PersistenceServiceTests.cs`, append inside the class:

```csharp
    [Fact]
    public void Save_then_Load_round_trips_the_warn_before_flag()
    {
        var svc = new PersistenceService(_path);
        svc.Save(new TidsroData
        {
            Settings = new AppSettings(),
            Alarms = { new AlarmRecord { Id = Guid.NewGuid(), FireAt = new DateTime(2026, 6, 17, 14, 0, 0, DateTimeKind.Local), Label = "Lunch", Sound = SoundChoice.Bell, WarnBefore = true } },
            RecurringAlarms = { new RecurringAlarmRecord { Id = Guid.NewGuid(), Hour = 7, Minute = 0, Days = Weekdays.Mon, Label = "Stand-up", Sound = SoundChoice.Bell, NextFireAt = new DateTime(2026, 6, 19, 7, 0, 0, DateTimeKind.Local), WarnBefore = true } },
        });

        var data = svc.Load();
        Assert.True(Assert.Single(data.Alarms).WarnBefore);
        Assert.True(Assert.Single(data.RecurringAlarms).WarnBefore);
    }

    [Fact]
    public void Load_an_alarm_saved_without_warn_before_defaults_it_to_false()
    {
        File.WriteAllText(_path,
            "{\"SchemaVersion\":3,\"Settings\":{\"DefaultSound\":0},\"Alarms\":[" +
            "{\"Id\":\"" + Guid.NewGuid() + "\",\"FireAt\":\"2026-06-17T14:00:00\",\"Label\":\"ok\",\"Sound\":3}]}");
        var data = new PersistenceService(_path).Load();
        Assert.False(Assert.Single(data.Alarms).WarnBefore);   // missing key -> false (back-compat)
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~warn_before"`
Expected: FAIL — compile error, `AlarmRecord` / `RecurringAlarmRecord` have no `WarnBefore`.

- [ ] **Step 3: Add `WarnBefore` to both records**

In `src/Tidsro/Models/AlarmRecord.cs`, add after the `Sound` property:

```csharp
    public bool WarnBefore { get; set; }
```

In `src/Tidsro/Models/RecurringAlarmRecord.cs`, add after the `Sound` property (before `NextFireAt`, or after — order is irrelevant):

```csharp
    public bool WarnBefore { get; set; }
```

- [ ] **Step 4: Carry it through `Sanitized()`**

In `src/Tidsro/Models/TidsroData.cs`, in the `AlarmRecord` rebuild block (lines 32-38), add the field:

```csharp
            alarms.Add(new AlarmRecord
            {
                Id = a.Id,
                FireAt = a.FireAt,
                Label = NormaliseLabel(a.Label),
                Sound = a.Sound,
                WarnBefore = a.WarnBefore,
            });
```

And in the `RecurringAlarmRecord` rebuild block (lines 53-62), add the field:

```csharp
            recurring.Add(new RecurringAlarmRecord
            {
                Id = r.Id,
                Hour = r.Hour,
                Minute = r.Minute,
                Days = days,
                Label = NormaliseLabel(r.Label),
                Sound = r.Sound,
                NextFireAt = r.NextFireAt,
                WarnBefore = r.WarnBefore,
            });
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~warn_before"`
Expected: PASS (4 passed).

- [ ] **Step 6: Run the full suite**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Tidsro/Models/AlarmRecord.cs src/Tidsro/Models/RecurringAlarmRecord.cs src/Tidsro/Models/TidsroData.cs tests/Tidsro.Tests/TidsroDataTests.cs tests/Tidsro.Tests/PersistenceServiceTests.cs
git commit -m "feat: persist the per-alarm WarnBefore flag"
```

---

## Task 3: PopupViewModel — the heads-up (warning) variant

**Files:**
- Modify: `src/Tidsro/ViewModels/PopupViewModel.cs`
- Test: `tests/Tidsro.Tests/PopupViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

Append inside `PopupViewModelTests`:

```csharp
    [Fact]
    public void Warning_variant_uses_the_given_title_hides_completion_actions_and_speaks_the_lead()
    {
        var item = new TimerItem { TriggerType = TriggerType.ClockTime, Label = "Stand-up" };
        var vm = new PopupViewModel(item, "Stand-up");
        Assert.True(vm.IsWarning);
        Assert.Equal("Stand-up", vm.Title);
        Assert.False(vm.ShowSnooze);
        Assert.False(vm.ShowRestart);
        Assert.Equal(" in 5 minutes", vm.HeaderText);
        Assert.Equal("Stand-up in 5 minutes", vm.AnnouncementText);
    }

    [Fact]
    public void Warning_dismiss_closes_the_card_without_a_scheduler_callback()
    {
        var item = new TimerItem { TriggerType = TriggerType.ClockTime, Label = "Stand-up" };
        var vm = new PopupViewModel(item, "Stand-up");
        var closed = 0; vm.CloseRequested += (_, _) => closed++;
        vm.DismissCommand.Execute(null);
        Assert.Equal(1, closed);   // close-only: nothing to cancel, the alarm stays armed
    }

    [Fact]
    public void Completion_variant_keeps_its_header_actions_and_announcement()
    {
        var vm = new PopupViewModel(Item("focus"), _ => Item(), _ => Item(), _ => { });
        Assert.False(vm.IsWarning);
        Assert.True(vm.ShowSnooze);
        Assert.Equal(" complete", vm.HeaderText);
        Assert.Equal("focus complete", vm.AnnouncementText);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~PopupViewModelTests"`
Expected: FAIL — compile error, no `(TimerItem, string)` constructor and no `IsWarning` / `ShowSnooze` / `HeaderText` / `AnnouncementText`.

- [ ] **Step 3: Add the warning variant**

Replace `src/Tidsro/ViewModels/PopupViewModel.cs` with:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidsro.Models;

namespace Tidsro.ViewModels;

public partial class PopupViewModel : ObservableObject
{
    private readonly TimerItem _item;
    private readonly Func<TimerItem, TimerItem> _onSnooze;
    private readonly Func<TimerItem, TimerItem> _onRestart;
    private readonly Action<TimerItem> _onDismiss;
    private readonly bool _isWarning;
    private bool _handled;   // debounce: one action per card

    [ObservableProperty] private string _title;

    public PopupViewModel(TimerItem item,
        Func<TimerItem, TimerItem> onSnooze,
        Func<TimerItem, TimerItem> onRestart,
        Action<TimerItem> onDismiss)
    {
        _item = item; _onSnooze = onSnooze; _onRestart = onRestart; _onDismiss = onDismiss;
        _title = string.IsNullOrWhiteSpace(item.Label) ? "Timer complete" : item.Label!;
    }

    // Heads-up (5-minute warning) variant: informational and close-only — no Snooze/Restart, and Dismiss
    // never disarms the alarm (it stays armed and still fires). App supplies the title (label, or "Alarm").
    public PopupViewModel(TimerItem item, string title)
    {
        _item = item;
        _title = title;
        _isWarning = true;
        _onSnooze = i => i;
        _onRestart = i => i;
        _onDismiss = _ => { };   // close-only
    }

    public TimerItem Item => _item;
    public bool IsWarning => _isWarning;
    /// <summary>Snooze (+5) is a completion action; the heads-up hides it.</summary>
    public bool ShowSnooze => !_isWarning;
    /// <summary>Restart re-runs a duration; meaningless for an alarm or a heads-up, so the card hides it.</summary>
    public bool ShowRestart => !_isWarning && _item.TriggerType == TriggerType.Countdown;
    /// <summary>The faint status line after the glyph (leading space matches the layout): " complete" / " in 5 minutes".</summary>
    public string HeaderText => _isWarning ? " in 5 minutes" : " complete";
    /// <summary>What the card announces to a screen reader on appear.</summary>
    public string AnnouncementText => _isWarning ? $"{Title} in 5 minutes" : $"{Title} complete";
    public event EventHandler? CloseRequested;

    [RelayCommand] private void Plus5()   { if (Begin()) { _onSnooze(_item);  Close(); } }
    [RelayCommand] private void Restart() { if (Begin()) { _onRestart(_item); Close(); } }
    [RelayCommand] private void Dismiss() { if (Begin()) { _onDismiss(_item); Close(); } }

    private bool Begin() { if (_handled) return false; _handled = true; return true; }
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~PopupViewModelTests"`
Expected: PASS (the three new tests plus the existing five).

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/ViewModels/PopupViewModel.cs tests/Tidsro.Tests/PopupViewModelTests.cs
git commit -m "feat: add a close-only heads-up variant to PopupViewModel"
```

---

## Task 4: AlarmItemViewModel — the agenda warning cue

**Files:**
- Modify: `src/Tidsro/ViewModels/AlarmItemViewModel.cs`
- Test: `tests/Tidsro.Tests/AlarmItemViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

Append inside `AlarmItemViewModelTests`:

```csharp
    [Fact]
    public void Warning_enabled_alarm_exposes_a_text_cue_and_speaks_it()
    {
        var item = Alarm("Stand-up", SoundChoice.Bell, 7, 0);
        item.WarnBefore = true;
        var vm = new AlarmItemViewModel(item, isTomorrow: false, isNext: false);
        Assert.True(vm.WarnBefore);
        Assert.Equal("5-min warning", vm.WarnText);
        Assert.Contains("warns 5 minutes before", vm.AccessibleName);
    }

    [Fact]
    public void Alarm_without_warning_has_an_empty_cue_and_no_warning_phrase()
    {
        var vm = new AlarmItemViewModel(Alarm("Lunch", SoundChoice.Bell, 14, 0), isTomorrow: false, isNext: false);
        Assert.False(vm.WarnBefore);
        Assert.Equal("", vm.WarnText);
        Assert.DoesNotContain("warns", vm.AccessibleName);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~AlarmItemViewModelTests"`
Expected: FAIL — `AlarmItemViewModel` has no `WarnBefore` / `WarnText`, and `AccessibleName` lacks the phrase.

- [ ] **Step 3: Add the cue and fold it into the accessible name**

In `src/Tidsro/ViewModels/AlarmItemViewModel.cs`, add after the `TomorrowText` property (after line 23):

```csharp
    public bool WarnBefore => Item.WarnBefore;
    public string WarnText => WarnBefore ? "5-min warning" : "";
```

Replace the `AccessibleName` property (lines 32-33) with — the warn phrase sits before the "next" cue:

```csharp
    public string AccessibleName =>
        $"Alarm at {TimeText}{CadencePhrase}, {DisplayLabel}, {SoundText}{(WarnBefore ? ", warns 5 minutes before" : "")}{(IsNext ? ", next" : "")}";
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~AlarmItemViewModelTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/ViewModels/AlarmItemViewModel.cs tests/Tidsro.Tests/AlarmItemViewModelTests.cs
git commit -m "feat: show a 5-min warning cue on the agenda row"
```

---

## Task 5: Editor wiring — toggle through add, edit, and undo

This task widens the edit-apply callback (a signature shared by `EditAlarmViewModel`, `MainViewModel.ApplyAlarmEdit`, and `App`'s edit factory). Those three change together in Step A so the project keeps compiling; Steps B and C are additive.

**Files:**
- Modify: `src/Tidsro/ViewModels/EditAlarmViewModel.cs`
- Modify: `src/Tidsro/ViewModels/MainViewModel.cs`
- Modify: `src/Tidsro/App.xaml.cs` (the edit-factory call only)
- Test: `tests/Tidsro.Tests/EditAlarmViewModelTests.cs`, `tests/Tidsro.Tests/MainViewModelTests.cs`

### Step A — thread `WarnBefore` through the edit path

- [ ] **A1: Update the `EditAlarmViewModelTests` helper and add a test**

In `EditAlarmViewModelTests.cs`, replace the `New` helper (lines 12-20) with the widened tuple + constructor arg:

```csharp
    private static EditAlarmViewModel New(string timeInput, out List<(Guid id, int h, int m, Weekdays days, string? label, SoundChoice sound, bool warnBefore)> applied,
        out FakeSoundService sound, Guid? id = null, Weekdays days = Weekdays.None, bool warnBefore = false)
    {
        var captured = new List<(Guid, int, int, Weekdays, string?, SoundChoice, bool)>();
        applied = captured;
        sound = new FakeSoundService();
        return new EditAlarmViewModel(id ?? Guid.NewGuid(), timeInput, "Tea", SoundChoice.Bell, days, warnBefore,
            Options, (i, h, m, d, l, s, w) => captured.Add((i, h, m, d, l, s, w)), sound);
    }
```

Then append two tests:

```csharp
    [Fact]
    public void Save_passes_the_warn_before_flag_through()
    {
        var vm = New("11:15", out var applied, out _, warnBefore: true);
        vm.SaveCommand.Execute(null);
        Assert.True(Assert.Single(applied).warnBefore);
    }

    [Fact]
    public void Save_passes_false_when_the_warning_is_off()
    {
        var vm = New("11:15", out var applied, out _);   // warnBefore defaults false
        vm.SaveCommand.Execute(null);
        Assert.False(Assert.Single(applied).warnBefore);
    }
```

- [ ] **A2: Add the `MainViewModel.ApplyAlarmEdit` test**

In `MainViewModelTests.cs`, append:

```csharp
    [Fact]
    public void ApplyAlarmEdit_carries_the_warn_before_flag()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AddAlarmCommand.Execute(null);
        var id = vm.Alarms[0].Item.Id;

        vm.ApplyAlarmEdit(id, 11, 0, Weekdays.None, "Tea", SoundChoice.Bell, warnBefore: true);

        Assert.True(vm.Alarms[0].Item.WarnBefore);
    }
```

- [ ] **A3: Run to verify failure**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~warn_before|FullyQualifiedName~ApplyAlarmEdit_carries"`
Expected: FAIL — compile errors (the `EditAlarmViewModel` constructor and `ApplyAlarmEdit` don't take a `bool` yet).

- [ ] **A4: Widen `EditAlarmViewModel`**

In `src/Tidsro/ViewModels/EditAlarmViewModel.cs`:

Change the apply-callback field type (line 16):

```csharp
    private readonly Action<Guid, int, int, Weekdays, string?, SoundChoice, bool> _apply;
```

Add a `WarnBefore` observable property next to the others (after line 29's `_error`):

```csharp
    [ObservableProperty] private bool _warnBefore;
```

Replace the constructor signature + body (lines 37-50) with — note the new `bool warnBefore` parameter after `days` and the widened `apply` delegate:

```csharp
    public EditAlarmViewModel(Guid id, string timeInput, string label, SoundChoice sound, Weekdays days, bool warnBefore,
        SoundChoice[] soundOptions, Action<Guid, int, int, Weekdays, string?, SoundChoice, bool> apply, ISoundService soundSvc)
    {
        _id = id;
        _timeInput = timeInput;
        _label = label;
        _selectedSound = sound;
        _repeat = RecurrenceRules.OptionFor(days);
        _warnBefore = warnBefore;
        DayToggles = DayToggleViewModel.Week();
        foreach (var t in DayToggles) t.IsSelected = (days & t.Flag) != 0;
        SoundOptions = soundOptions;
        _apply = apply;
        _sound = soundSvc;
    }
```

Replace the `_apply(...)` call in `Save` (line 63) with:

```csharp
        _apply(_id, h, m, ResolveDays(), Label, SelectedSound, WarnBefore);
```

- [ ] **A5: Widen `MainViewModel.ApplyAlarmEdit`**

In `src/Tidsro/ViewModels/MainViewModel.cs`, replace the `ApplyAlarmEdit` signature (line 191) with:

```csharp
    public void ApplyAlarmEdit(Guid id, int hour, int minute, Weekdays days, string? label, SoundChoice sound, bool warnBefore)
```

Inside it, replace the two arm calls (the `ArmClockAlarm` on line 199 and `ArmRecurringAlarm` on line 204) with:

```csharp
            _scheduler.ArmClockAlarm(fireAt, clean, sound, id, warnBefore);
```

```csharp
            _scheduler.ArmRecurringAlarm(hour, minute, days, clean, sound, id, warnBefore: warnBefore);
```

- [ ] **A6: Update `App`'s edit factory to the new constructor**

In `src/Tidsro/App.xaml.cs`, replace the `editFactory` in `ShowMainWindow` (lines 126-129) with — passing `row.Item.WarnBefore` after the days argument:

```csharp
        Func<AlarmItemViewModel, EditAlarmWindow> editFactory = row => new EditAlarmWindow(
            new EditAlarmViewModel(row.Item.Id, row.Item.EndsAt?.ToString("HH\\:mm") ?? "",
                row.Item.Label ?? "", row.Item.Sound, row.Item.RecurringDays ?? Weekdays.None, row.Item.WarnBefore,
                _mainVm.SoundOptions, _mainVm.ApplyAlarmEdit, _sound));
```

- [ ] **A7: Run to verify pass**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~warn_before|FullyQualifiedName~ApplyAlarmEdit_carries"`
Expected: PASS.

- [ ] **A8: Run the full suite, then commit**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj` → PASS.

```bash
git add src/Tidsro/ViewModels/EditAlarmViewModel.cs src/Tidsro/ViewModels/MainViewModel.cs src/Tidsro/App.xaml.cs tests/Tidsro.Tests/EditAlarmViewModelTests.cs tests/Tidsro.Tests/MainViewModelTests.cs
git commit -m "feat: carry WarnBefore through the alarm edit dialog"
```

### Step B — `AlarmWarnBefore` editor state on add

- [ ] **B1: Write the failing tests**

In `MainViewModelTests.cs`, append:

```csharp
    [Fact]
    public void AddAlarm_with_warning_on_arms_an_alarm_that_warns_before()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00";
        vm.AlarmWarnBefore = true;
        vm.AddAlarmCommand.Execute(null);
        Assert.True(vm.Alarms[0].Item.WarnBefore);
    }

    [Fact]
    public void AddAlarm_resets_the_warning_toggle_after_adding()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00";
        vm.AlarmWarnBefore = true;
        vm.AddAlarmCommand.Execute(null);
        Assert.False(vm.AlarmWarnBefore);   // editor cleared, like the rest
    }
```

- [ ] **B2: Run to verify failure**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~AddAlarm_with_warning|FullyQualifiedName~resets_the_warning_toggle"`
Expected: FAIL — `MainViewModel` has no `AlarmWarnBefore`.

- [ ] **B3: Add the editor state and thread it through add + clear**

In `src/Tidsro/ViewModels/MainViewModel.cs`:

Add the observable property next to the other alarm-editor fields (after line 35's `_alarmRepeat`):

```csharp
    [ObservableProperty] private bool _alarmWarnBefore;
```

In `AddAlarm`, replace the two arm calls (lines 162 and 167) with:

```csharp
            _scheduler.ArmClockAlarm(fireAt, label, AlarmSound, warnBefore: AlarmWarnBefore);
```

```csharp
            _scheduler.ArmRecurringAlarm(hour, minute, days, label, AlarmSound, warnBefore: AlarmWarnBefore);
```

In `ClearEditor` (lines 271-278), add the reset:

```csharp
        AlarmWarnBefore = false;
```

- [ ] **B4: Run to verify pass**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~AddAlarm_with_warning|FullyQualifiedName~resets_the_warning_toggle"`
Expected: PASS.

- [ ] **B5: Commit**

```bash
git add src/Tidsro/ViewModels/MainViewModel.cs tests/Tidsro.Tests/MainViewModelTests.cs
git commit -m "feat: add the warn-before toggle to the alarm add-editor"
```

### Step C — preserve the flag through delete + undo

- [ ] **C1: Write the failing test**

In `MainViewModelTests.cs`, append:

```csharp
    [Fact]
    public void UndoDelete_restores_an_alarm_with_its_warning_setting_intact()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AlarmWarnBefore = true;
        vm.AddAlarmCommand.Execute(null);

        vm.DeleteAlarmCommand.Execute(vm.Alarms[0]);
        vm.UndoDeleteCommand.Execute(null);

        Assert.True(Assert.Single(vm.Alarms).Item.WarnBefore);
    }
```

- [ ] **C2: Run to verify failure**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~UndoDelete_restores_an_alarm_with_its_warning"`
Expected: FAIL — the re-armed alarm comes back with `WarnBefore` false.

- [ ] **C3: Preserve `WarnBefore` in the `UndoDelete` re-arm**

In `src/Tidsro/ViewModels/MainViewModel.cs`, in `UndoDelete`, replace the recurring re-arm (line 241) with:

```csharp
            _scheduler.ArmRecurringAlarm(next.Hour, next.Minute, days, item.Label, item.Sound, item.Id, next, item.WarnBefore);
```

and the clock-time re-arm (line 248) with:

```csharp
            _scheduler.ArmClockAlarm(fireAt, item.Label, item.Sound, item.Id, item.WarnBefore);   // re-arm; next tick re-checks grace if past
```

- [ ] **C4: Run to verify pass, then the full suite**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~UndoDelete_restores_an_alarm_with_its_warning"` → PASS.
Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj` → PASS.

- [ ] **C5: Commit**

```bash
git add src/Tidsro/ViewModels/MainViewModel.cs tests/Tidsro.Tests/MainViewModelTests.cs
git commit -m "feat: keep WarnBefore through alarm delete-undo"
```

---

## Task 6: Views — the editor checkbox and the dual-purpose card

No unit tests (pure view). Build to confirm the XAML compiles; behaviour is verified in Task 8.

**Files:**
- Modify: `src/Tidsro/Views/MainWindow.xaml`
- Modify: `src/Tidsro/Views/EditAlarmWindow.xaml`
- Modify: `src/Tidsro/Views/CompletionPopup.xaml`
- Modify: `src/Tidsro/Views/CompletionPopup.xaml.cs`

- [ ] **Step 1: Add the checkbox to the add-editor**

In `src/Tidsro/Views/MainWindow.xaml`, insert between the day-toggle `</ItemsControl>` (line 175) and the `<Button Content="Add" ...>` (line 176). It matches the `SettingsWindow` checkbox style (plain checkbox, themed foreground):

```xml
            <CheckBox Content="Warn me 5 minutes before" IsChecked="{Binding AlarmWarnBefore}"
                      Foreground="{StaticResource Text}" Margin="0,12,0,0"
                      AutomationProperties.Name="Warn me 5 minutes before"/>
```

- [ ] **Step 2: Add the checkbox to the Edit dialog**

In `src/Tidsro/Views/EditAlarmWindow.xaml`, insert between the day-toggle `</ItemsControl>` (line 47) and the error `<TextBlock ...>` (line 48):

```xml
    <CheckBox Content="Warn me 5 minutes before" IsChecked="{Binding WarnBefore}"
              Foreground="{StaticResource Text}" Margin="0,12,0,0"
              AutomationProperties.Name="Warn me 5 minutes before"/>
```

- [ ] **Step 3: Make the card serve both variants**

In `src/Tidsro/Views/CompletionPopup.xaml`:

Bind the window's accessible name (line 7) — replace `AutomationProperties.Name="Timer complete"` with:

```xml
        AutomationProperties.Name="{Binding AnnouncementText}"
```

Bind the faint header text — replace the second `<Run>` (line 19, currently `<Run Text=" complete"/>`) so the whole header line reads:

```xml
        <TextBlock Foreground="{StaticResource TextFaint}" FontSize="{StaticResource TextXs}" VerticalAlignment="Center">
          <Run FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" Text="&#xE73E;"/><Run Text="{Binding HeaderText}"/>
        </TextBlock>
```

Hide the `+5 min` button in the warning variant — add a `Visibility` to the Plus5 button (line 33):

```xml
        <Button Content="+5 min" Command="{Binding Plus5Command}"
                AutomationProperties.Name="Add 5 minutes" Style="{StaticResource QuietAction}"
                Visibility="{Binding ShowSnooze, Converter={StaticResource BoolToVisible}}"/>
```

(The Restart button already binds `Visibility` to `ShowRestart`, which is false for the warning variant; the Dismiss button stays visible for both.)

- [ ] **Step 4: Announce the right text on appear**

In `src/Tidsro/Views/CompletionPopup.xaml.cs`, replace the announce call in `Loaded` (line 51):

```csharp
            UiaNotifier.Announce(this, _vm.AnnouncementText);
```

- [ ] **Step 5: Build to confirm the XAML compiles**

Run: `dotnet build src/Tidsro/Tidsro.csproj -c Debug`
Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/Tidsro/Views/MainWindow.xaml src/Tidsro/Views/EditAlarmWindow.xaml src/Tidsro/Views/CompletionPopup.xaml src/Tidsro/Views/CompletionPopup.xaml.cs
git commit -m "feat: add the warn-before checkbox and a heads-up card variant"
```

---

## Task 7: App wiring — raise the heads-up, mirror the sound, close at fire, persist

No unit tests (composition root). Build + a real-time smoke check; full acceptance is Task 8.

**Files:**
- Modify: `src/Tidsro/App.xaml.cs`

- [ ] **Step 1: Track open warning cards and subscribe to `Warning`**

In `src/Tidsro/App.xaml.cs`, add a field next to `_openPopups` (after line 30):

```csharp
    private readonly Dictionary<CompletionPopup, DateTimeOffset> _warningFireTimes = new();
```

In `OnStartup`, after `_scheduler.Fired += OnTimerFired;` (line 64), subscribe:

```csharp
        _scheduler.Warning += OnAlarmWarning;
```

- [ ] **Step 2: Close fired heads-ups on the existing tick**

Replace the tick handler (line 68) with:

```csharp
        _timer.Tick += (_, _) => { _scheduler.Tick(); _mainVm.RefreshAll(); CloseFiredWarnings(); };
```

- [ ] **Step 3: Add the warning handler and the close sweep**

Add these methods after `OnTimerFired` (after line 106):

```csharp
    private void OnAlarmWarning(object? sender, TimerItem item)
    {
        // Mirror the alarm's sound choice: a soft chime only when the alarm itself is sounded; silent otherwise.
        if (item.Sound != SoundChoice.None) _sound.Play(SoundChoice.SoftChime);

        var head = string.IsNullOrWhiteSpace(item.Label) ? "Alarm" : item.Label!.Trim();
        var popup = new CompletionPopup(new PopupViewModel(item, head));   // heads-up (close-only) variant
        popup.Closed += (_, _) => { _openPopups.Remove(popup); _warningFireTimes.Remove(popup); RestackPopups(); };
        popup.ContentRendered += (_, _) => RestackPopups();
        _openPopups.Add(popup);
        _warningFireTimes[popup] = item.EndsAt ?? _scheduler.Now;   // capture this occurrence's fire time
        PositionPopup(popup, _openPopups.Count - 1);
        popup.Show();   // ShowActivated=false -> appears without stealing focus
    }

    // The heads-up gives way to the completion card: close any warning whose alarm has reached its fire time.
    // Decoupled from Fired, so it works for one-shots and recurring alike (the captured fire time is the occurrence's,
    // not the live alarm's already-advanced EndsAt).
    private void CloseFiredWarnings()
    {
        var now = _scheduler.Now;
        foreach (var (popup, fireAt) in _warningFireTimes.ToList())
            if (now >= fireAt) popup.Close();   // Closed handler removes it from both collections and restacks
    }
```

- [ ] **Step 4: Persist and restore `WarnBefore`**

Replace `ToRecord` (lines 163-169) with:

```csharp
    private static AlarmRecord ToRecord(TimerItem a) => new()
    {
        Id = a.Id,
        FireAt = a.EndsAt?.LocalDateTime ?? default,
        Label = a.Label,
        Sound = a.Sound,
        WarnBefore = a.WarnBefore,
    };
```

In `ToRecurringRecord` (lines 171-180), add the field:

```csharp
        WarnBefore = a.WarnBefore,
```

In `ArmLoadedAlarms`, replace the arm call (line 189) with:

```csharp
                _scheduler.ArmClockAlarm(fireAt, r.Label, r.Sound, r.Id, r.WarnBefore);
```

In `ArmLoadedRecurring`, replace the arm call (line 204) with:

```csharp
                _scheduler.ArmRecurringAlarm(r.Hour, r.Minute, r.Days, r.Label, r.Sound, r.Id, next, r.WarnBefore);
```

- [ ] **Step 5: Build**

Run: `dotnet build src/Tidsro/Tidsro.csproj -c Debug`
Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 6: Run the full suite (nothing regressed)**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Tidsro/App.xaml.cs
git commit -m "feat: wire the pre-alarm heads-up — soft chime, card, close-at-fire, persistence"
```

---

## Task 8: Manual acceptance

No code. Build, launch (stop any running `Tidsro.exe` first; launch the built exe rather than `dotnet run`), and walk the checklist. A real-time check uses a short lead: set an alarm ~6 minutes ahead with the toggle on, then watch.

- [ ] Build + launch: `dotnet build src/Tidsro/Tidsro.csproj -c Debug` then start `src/Tidsro/bin/Debug/net10.0-windows/Tidsro.exe`.
- [ ] **Add-editor:** the "Warn me 5 minutes before" checkbox appears below the day toggles, is keyboard-reachable, and reads its label to a screen reader.
- [ ] **Agenda cue:** an alarm added with the toggle on shows the "5-min warning" text cue; a screen reader hears "…warns 5 minutes before…" in the row name.
- [ ] **Edit dialog:** the checkbox is pre-filled from the row and round-trips on Save.
- [ ] **Sounded alarm:** ~5 min before, a heads-up card slides in bottom-right with a soft chime and reads "*<label> · in 5 minutes*"; UIA announces it.
- [ ] **Silent alarm:** the heads-up card appears with **no** sound.
- [ ] **Dismiss:** dismissing the heads-up closes only the card; the alarm still fires at its time.
- [ ] **Auto-close at fire:** a heads-up left undismissed closes the instant the alarm fires (the completion card takes over).
- [ ] **Recurring:** a recurring alarm warns before each occurrence (verify a second occurrence after one fires, or by reasoning from the unit test).
- [ ] **Calm guards:** an alarm set ~3 min out with the toggle on gives **no** insta-warn; quitting and relaunching within the window gives **no** stale card.
- [ ] **Reduced motion + keyboard:** with reduced motion on, the card appears without animation; the global hotkey / tray "Focus latest alert" focuses it and Dismiss works by keyboard.

When the checklist passes, the feature is ready to finish (see below).

---

## Self-Review (completed during planning)

- **Spec coverage:** §3.1 toggle → Tasks 5–6; §3.2 warning signal → Task 1; §3.3 card + sound → Tasks 3, 6, 7; §3.4 edges → Task 1 (`WarningSent` init + reset); §4 data model → Tasks 1–2; §6 lifecycle/auto-close → Task 7 (`CloseFiredWarnings`); §7 a11y → Tasks 4, 6; §9 testing → each task's tests + Task 8. All covered.
- **Type consistency:** `ArmClockAlarm(..., Guid? id, bool warnBefore)` and `ArmRecurringAlarm(..., Guid? id, DateTimeOffset? nextFireAt, bool warnBefore)` are called with the same argument order in Tasks 5 and 7. `ApplyAlarmEdit(..., bool warnBefore)` and the 7-arg `EditAlarmViewModel` apply delegate match across `EditAlarmViewModel`, `MainViewModel`, `App`, and the test helper. `PopupViewModel(item, title)`, `IsWarning`, `ShowSnooze`, `HeaderText`, `AnnouncementText`, `WarnBefore`, `WarnText`, `AlarmWarnBefore` are defined once and used consistently in the views/App.
- **No placeholders:** every step has concrete code, an exact command, and an expected result.

---

## Release follow-ups (owner-driven, after acceptance — not plan tasks)

These complete the v1.3.0 release that this feature lands in; the repo owner drives them:
- Add one line to the README feature summary, e.g. *"Optional 5-minute heads-up before any alarm — a gentle nudge to wrap up the current thing."*
- Open the PR for `feature/pre-alarm-warning`; the polish PR (`fix/schedule-editor-polish`) merges first, then this.
- Screenshots + short clip, README Status/Roadmap flip, `publish.ps1`, tag `v1.3.0`, `gh release`.
