# Per-Alarm On/Off Toggle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a toggle to each scheduled alarm so it can be switched off (kept, but never fires or warns) and back on, instead of deleting and re-creating it — e.g. silencing recurring alarms over the summer.

**Architecture:** A persisted `IsEnabled` flag on the alarm runs through every layer: the `SchedulerService.Tick()` skip-guard suppresses fire + warning for a disabled alarm; a new `SchedulerService.SetEnabled` rolls a re-enabled recurring alarm forward to its next future occurrence (never an instant fire); `MainViewModel.ToggleAlarmCommand` flips it, persists, announces, and rebuilds the agenda with disabled alarms parked in a muted group at the bottom; a reused gold `ToggleSwitch` on each row drives it.

**Tech Stack:** C# / .NET 10 / WPF, CommunityToolkit.Mvvm, xUnit. Spec: `docs/superpowers/specs/2026-06-23-tidsro-alarm-toggle-design.md`.

**Back-compat:** `Enabled` defaults to `true`, so every alarm in an existing `data.json` (no `Enabled` key) loads as on. The persisted-document schema bumps 3 → 4 to mark the shape change — no migration code is needed (additive field, default-on).

**Build gotcha:** A running `Tidsro.exe` locks the build output. Stop any running instance before `dotnet build` / `dotnet test`.

---

## File Structure

| File | Responsibility | Change |
|---|---|---|
| `src/Tidsro/Models/TimerItem.cs` | Runtime alarm/timer model | Add `IsEnabled` (default true) |
| `src/Tidsro/Models/AlarmRecord.cs` | Persisted one-shot alarm | Add `Enabled` (default true) |
| `src/Tidsro/Models/RecurringAlarmRecord.cs` | Persisted recurring alarm | Add `Enabled` (default true) |
| `src/Tidsro/Models/TidsroData.cs` | Load-time sanitisation | Copy `Enabled` through both loops |
| `src/Tidsro/Services/SchedulerService.cs` | Arming, ticking, fire/warn | `enabled` arg on arm methods; skip-guard; `SetEnabled` + roll-forward |
| `src/Tidsro/ViewModels/AlarmItemViewModel.cs` | One agenda row (presentation) | `IsEnabled`, `ToggleLabel`, `, off` cue |
| `src/Tidsro/ViewModels/MainViewModel.cs` | Agenda commands + ordering | `ToggleAlarmCommand`; undo preserves enabled; `RebuildAgenda` parks disabled |
| `src/Tidsro/App.xaml.cs` | Record ↔ runtime mapping | Thread `Enabled` through save + load |
| `src/Tidsro/Views/MainWindow.xaml` | Agenda row UI | Per-row switch + dimmed disabled content |
| `CHANGELOG.md` | Release notes | Unreleased entry |

Test files (all under `tests/Tidsro.Tests/`): `SchedulerServiceTests.cs`, `TidsroDataTests.cs`, `PersistenceServiceTests.cs`, `AlarmItemViewModelTests.cs`, `MainViewModelTests.cs`.

---

### Task 1: Scheduler — `IsEnabled` on the model + `enabled` arg on arm methods

**Files:**
- Modify: `src/Tidsro/Models/TimerItem.cs`
- Modify: `src/Tidsro/Services/SchedulerService.cs:30-45` (`ArmClockAlarm`), `:50-68` (`ArmRecurringAlarm`)
- Test: `tests/Tidsro.Tests/SchedulerServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Append inside the `SchedulerServiceTests` class:

```csharp
[Fact]
public void Armed_alarms_are_enabled_by_default()
{
    var (s, c) = New();
    var clock = s.ArmClockAlarm(c.Now + TimeSpan.FromHours(1), null, SoundChoice.None);
    var rec = s.ArmRecurringAlarm(7, 0, RecurrenceRules.AllDays, null, SoundChoice.None);
    Assert.True(clock.IsEnabled);
    Assert.True(rec.IsEnabled);
}

[Fact]
public void ArmClockAlarm_can_create_a_disabled_alarm()
{
    var (s, c) = New();
    var alarm = s.ArmClockAlarm(c.Now + TimeSpan.FromHours(1), null, SoundChoice.None, enabled: false);
    Assert.False(alarm.IsEnabled);
}

[Fact]
public void ArmRecurringAlarm_can_create_a_disabled_alarm()
{
    var (s, _) = New();
    var alarm = s.ArmRecurringAlarm(7, 0, RecurrenceRules.AllDays, null, SoundChoice.None, enabled: false);
    Assert.False(alarm.IsEnabled);
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~SchedulerServiceTests"`
Expected: FAIL — build error, `TimerItem` has no `IsEnabled`, arm methods have no `enabled` parameter.

- [ ] **Step 3: Add `IsEnabled` to `TimerItem`**

In `src/Tidsro/Models/TimerItem.cs`, after the `WarningSent` property (the last property), add:

```csharp
    // Per-alarm on/off. A disabled alarm stays armed and persisted but never fires or warns.
    public bool IsEnabled { get; set; } = true;
```

- [ ] **Step 4: Add the `enabled` parameter to both arm methods**

In `src/Tidsro/Services/SchedulerService.cs`, change the `ArmClockAlarm` signature and initializer:

```csharp
    public TimerItem ArmClockAlarm(DateTimeOffset fireAt, string? label, SoundChoice sound, Guid? id = null, bool warnBefore = false, bool enabled = true)
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
            WarningSent = warnBefore && _clock.Now >= fireAt - WarningLead,
            IsEnabled = enabled,
        };
        _alarms.Add(item);
        return item;
    }
```

And `ArmRecurringAlarm`:

```csharp
    public TimerItem ArmRecurringAlarm(int hour, int minute, Weekdays days, string? label, SoundChoice sound,
        Guid? id = null, DateTimeOffset? nextFireAt = null, bool warnBefore = false, bool enabled = true)
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
            IsEnabled = enabled,
        };
        _alarms.Add(item);
        return item;
    }
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~SchedulerServiceTests"`
Expected: PASS (all existing scheduler tests stay green — alarms default to enabled).

- [ ] **Step 6: Commit**

```bash
git add src/Tidsro/Models/TimerItem.cs src/Tidsro/Services/SchedulerService.cs tests/Tidsro.Tests/SchedulerServiceTests.cs
git commit -m "feat: add IsEnabled to alarms; arm methods take an enabled flag"
```

---

### Task 2: Scheduler — `Tick()` skips disabled alarms

**Files:**
- Modify: `src/Tidsro/Services/SchedulerService.cs:119-121` (the alarm loop guard)
- Test: `tests/Tidsro.Tests/SchedulerServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Append to `SchedulerServiceTests`:

```csharp
[Fact]
public void Tick_does_not_fire_a_disabled_clock_alarm_past_its_time()
{
    var (s, c) = New();
    s.ArmClockAlarm(c.Now + TimeSpan.FromMinutes(1), null, SoundChoice.None, enabled: false);
    var fired = 0; s.Fired += (_, _) => fired++;

    c.Advance(TimeSpan.FromMinutes(2));   // well past its time
    s.Tick();

    Assert.Equal(0, fired);
    Assert.Single(s.Alarms);              // stays armed-but-off, not removed
}

[Fact]
public void Tick_does_not_warn_for_a_disabled_alarm()
{
    var (s, c) = New();
    s.ArmClockAlarm(c.Now + TimeSpan.FromMinutes(10), null, SoundChoice.Bell, warnBefore: true, enabled: false);
    var warned = 0; s.Warning += (_, _) => warned++;

    c.Advance(TimeSpan.FromMinutes(6));   // inside [+5, +10)
    s.Tick();

    Assert.Equal(0, warned);
}

[Fact]
public void Tick_does_not_fire_or_advance_a_disabled_recurring_alarm()
{
    var (s, c) = New();
    var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None, enabled: false);
    var frozen = alarm.EndsAt;
    var fired = 0; s.Fired += (_, _) => fired++;

    c.Advance(TimeSpan.FromMinutes(61));   // past 10:00
    s.Tick();

    Assert.Equal(0, fired);
    Assert.Equal(frozen, alarm.EndsAt);    // not advanced while off
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~SchedulerServiceTests"`
Expected: FAIL — the disabled alarm fires/warns/advances (the guard doesn't check `IsEnabled` yet).

- [ ] **Step 3: Add `!alarm.IsEnabled` to the skip guard**

In `src/Tidsro/Services/SchedulerService.cs`, in the `foreach (var alarm in _alarms.ToList())` loop, change the guard line:

```csharp
            if (alarm.State != TimerState.Running || !alarm.IsEnabled || alarm.EndsAt is not { } end) continue;
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~SchedulerServiceTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/Services/SchedulerService.cs tests/Tidsro.Tests/SchedulerServiceTests.cs
git commit -m "feat: scheduler skips disabled alarms (no fire or warning)"
```

---

### Task 3: Scheduler — `SetEnabled` with recurring roll-forward

**Files:**
- Modify: `src/Tidsro/Services/SchedulerService.cs` (add a method near `RemoveAlarm`)
- Test: `tests/Tidsro.Tests/SchedulerServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Append to `SchedulerServiceTests`:

```csharp
[Fact]
public void SetEnabled_false_turns_an_alarm_off()
{
    var (s, c) = New();
    var alarm = s.ArmClockAlarm(c.Now + TimeSpan.FromHours(1), null, SoundChoice.None);
    s.SetEnabled(alarm, false);
    Assert.False(alarm.IsEnabled);
}

[Fact]
public void SetEnabled_true_rolls_a_past_recurring_alarm_forward_without_firing()
{
    var (s, c) = New();   // Thu 2026-01-01 09:00
    var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None, enabled: false);
    var fired = 0; s.Fired += (_, _) => fired++;

    c.Advance(TimeSpan.FromDays(1));   // Fri 09:00 — its frozen Thu-10:00 occurrence is now in the past
    s.SetEnabled(alarm, true);

    Assert.True(alarm.IsEnabled);
    Assert.Equal(0, fired);            // re-enabling never fires
    Assert.Equal(new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero), alarm.EndsAt);   // rolled to Fri 10:00

    s.Tick();
    Assert.Equal(0, fired);            // and the next tick doesn't retro-fire
}

[Fact]
public void SetEnabled_true_leaves_a_still_future_recurring_alarm_unchanged()
{
    var (s, _) = New();   // Thu 09:00; EndsAt today 10:00 is still ahead
    var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None, enabled: false);
    s.SetEnabled(alarm, true);
    Assert.Equal(new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero), alarm.EndsAt);   // unchanged
}

[Fact]
public void SetEnabled_true_does_not_roll_a_one_shot_forward()
{
    var (s, c) = New();
    var fireAt = c.Now + TimeSpan.FromHours(2);
    var alarm = s.ArmClockAlarm(fireAt, null, SoundChoice.None, enabled: false);
    s.SetEnabled(alarm, true);
    Assert.Equal(fireAt, alarm.EndsAt);   // one-shots have no recurrence to roll
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~SchedulerServiceTests"`
Expected: FAIL — build error, `SetEnabled` does not exist.

- [ ] **Step 3: Add `SetEnabled`**

In `src/Tidsro/Services/SchedulerService.cs`, add after `RemoveAlarm`:

```csharp
    /// <summary>Turn an alarm on or off. Re-enabling a recurring alarm whose next occurrence has
    /// already passed (e.g. switched off over the summer) rolls it forward to the next future one,
    /// so it never fires the instant it comes back, and never emits a stale missed note.</summary>
    public void SetEnabled(TimerItem alarm, bool enabled)
    {
        alarm.IsEnabled = enabled;
        if (enabled
            && alarm.TriggerType == TriggerType.Recurring
            && alarm.RecurringDays is { } days
            && alarm.EndsAt is { } end
            && end <= _clock.Now)
        {
            var next = RecurrenceRules.NextOccurrence(_clock.Now, end.Hour, end.Minute, days);
            alarm.EndsAt = next;
            alarm.WarningSent = alarm.WarnBefore && _clock.Now >= next - WarningLead;
        }
    }
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~SchedulerServiceTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/Services/SchedulerService.cs tests/Tidsro.Tests/SchedulerServiceTests.cs
git commit -m "feat: SetEnabled rolls a re-enabled recurring alarm forward"
```

---

### Task 4: Persistence records — `Enabled` flag + `Sanitized` carries it

**Files:**
- Modify: `src/Tidsro/Models/AlarmRecord.cs`, `src/Tidsro/Models/RecurringAlarmRecord.cs`
- Modify: `src/Tidsro/Models/TidsroData.cs:7` (`CurrentSchema`), `:32-40` (clock loop), `:54-64` (recurring loop)
- Test: `tests/Tidsro.Tests/TidsroDataTests.cs`

- [ ] **Step 1: Write the failing tests**

Append to `TidsroDataTests`:

```csharp
[Fact]
public void A_record_is_enabled_by_default()
{
    Assert.True(Good().Enabled);
    Assert.True(GoodRec().Enabled);
}

[Fact]
public void Sanitized_preserves_the_enabled_flag_on_a_clock_alarm()
{
    var a = Good(); a.Enabled = false;
    var clean = new TidsroData { Settings = new(), Alarms = { a } }.Sanitized();
    Assert.False(Assert.Single(clean.Alarms).Enabled);
}

[Fact]
public void Sanitized_preserves_the_enabled_flag_on_a_recurring_alarm()
{
    var r = GoodRec(); r.Enabled = false;
    var clean = new TidsroData { Settings = new(), RecurringAlarms = { r } }.Sanitized();
    Assert.False(Assert.Single(clean.RecurringAlarms).Enabled);
}
```

Then update the existing schema assertion in `Sanitized_keeps_a_valid_alarm_and_defaults_null_settings` — change `Assert.Equal(3, clean.SchemaVersion)` to `Assert.Equal(4, clean.SchemaVersion)`.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~TidsroDataTests"`
Expected: FAIL — build error (records have no `Enabled`), and the schema assertion now expects 4.

- [ ] **Step 3: Add `Enabled` to both records**

In `src/Tidsro/Models/AlarmRecord.cs`, after `WarnBefore`:

```csharp
    public bool Enabled { get; set; } = true;
```

In `src/Tidsro/Models/RecurringAlarmRecord.cs`, after `WarnBefore`:

```csharp
    public bool Enabled { get; set; } = true;
```

- [ ] **Step 4: Bump the schema and carry `Enabled` through `Sanitized`**

In `src/Tidsro/Models/TidsroData.cs`, bump the schema constant (the persisted shape changed):

```csharp
    public const int CurrentSchema = 4;
```

Then, in the clock-alarm `alarms.Add(new AlarmRecord { ... })`, add a line after `WarnBefore = a.WarnBefore,`:

```csharp
                Enabled = a.Enabled,
```

In the recurring `recurring.Add(new RecurringAlarmRecord { ... })`, add after `WarnBefore = r.WarnBefore,`:

```csharp
                Enabled = r.Enabled,
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~TidsroDataTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Tidsro/Models/AlarmRecord.cs src/Tidsro/Models/RecurringAlarmRecord.cs src/Tidsro/Models/TidsroData.cs tests/Tidsro.Tests/TidsroDataTests.cs
git commit -m "feat: persist alarm Enabled flag (default on)"
```

---

### Task 5: Persistence — round-trip + missing-key back-compat

**Files:**
- Test only: `tests/Tidsro.Tests/PersistenceServiceTests.cs`

> These lock the file-level behaviour. Because Task 4 added `Enabled = true` to the records, the
> back-compat test passes on first run — it is a regression guard against a future default change.

- [ ] **Step 1: Write the tests**

Append to `PersistenceServiceTests`:

```csharp
[Fact]
public void Save_then_Load_round_trips_the_enabled_flag()
{
    var svc = new PersistenceService(_path);
    svc.Save(new TidsroData
    {
        Settings = new AppSettings(),
        Alarms = { new AlarmRecord { Id = Guid.NewGuid(), FireAt = new DateTime(2026, 6, 17, 14, 0, 0, DateTimeKind.Local), Label = "Lunch", Sound = SoundChoice.Bell, Enabled = false } },
        RecurringAlarms = { new RecurringAlarmRecord { Id = Guid.NewGuid(), Hour = 7, Minute = 0, Days = Weekdays.Mon, Label = "Stand-up", Sound = SoundChoice.Bell, NextFireAt = new DateTime(2026, 6, 19, 7, 0, 0, DateTimeKind.Local), Enabled = false } },
    });

    var data = svc.Load();
    Assert.False(Assert.Single(data.Alarms).Enabled);
    Assert.False(Assert.Single(data.RecurringAlarms).Enabled);
}

[Fact]
public void Load_an_alarm_saved_without_enabled_defaults_it_to_true()
{
    File.WriteAllText(_path,
        "{\"SchemaVersion\":3,\"Settings\":{\"DefaultSound\":0},\"Alarms\":[" +
        "{\"Id\":\"" + Guid.NewGuid() + "\",\"FireAt\":\"2026-06-17T14:00:00\",\"Label\":\"ok\",\"Sound\":3}]}");
    var data = new PersistenceService(_path).Load();
    Assert.True(Assert.Single(data.Alarms).Enabled);   // missing key -> on (back-compat)
}
```

- [ ] **Step 2: Run to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~PersistenceServiceTests"`
Expected: PASS. (To prove the back-compat guard bites, temporarily delete `= true` from `AlarmRecord.Enabled` — the second test fails — then restore it.)

- [ ] **Step 3: Commit**

```bash
git add tests/Tidsro.Tests/PersistenceServiceTests.cs
git commit -m "test: lock alarm enabled round-trip and back-compat default"
```

---

### Task 6: App — carry `Enabled` through save and load

**Files:**
- Modify: `src/Tidsro/App.xaml.cs:227-234` (`ToRecord`), `:236-246` (`ToRecurringRecord`), `:248-258` (`ArmLoadedAlarms`), `:260-273` (`ArmLoadedRecurring`)

> `App.xaml.cs` is the composition root — it has no unit-test coverage in this repo. This task is
> verified by a clean build and the manual acceptance in Task 11 (disabled state survives a restart).

- [ ] **Step 1: Add `Enabled` to both record mappers**

In `src/Tidsro/App.xaml.cs`, in `ToRecord`, add after `WarnBefore = a.WarnBefore,`:

```csharp
        Enabled = a.IsEnabled,
```

In `ToRecurringRecord`, add after `WarnBefore = a.WarnBefore,`:

```csharp
        Enabled = a.IsEnabled,
```

- [ ] **Step 2: Pass `Enabled` into the arm calls on load**

In `ArmLoadedAlarms`, change the arm call:

```csharp
                _scheduler.ArmClockAlarm(LocalToOffset(r.FireAt), r.Label, r.Sound, r.Id, r.WarnBefore, r.Enabled);
```

In `ArmLoadedRecurring`, change the arm call:

```csharp
                _scheduler.ArmRecurringAlarm(r.Hour, r.Minute, r.Days, r.Label, r.Sound, r.Id, next, r.WarnBefore, r.Enabled);
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add src/Tidsro/App.xaml.cs
git commit -m "feat: carry alarm enabled state through load and save"
```

---

### Task 7: AlarmItemViewModel — expose enabled state + off cue

**Files:**
- Modify: `src/Tidsro/ViewModels/AlarmItemViewModel.cs`
- Test: `tests/Tidsro.Tests/AlarmItemViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

Append to `AlarmItemViewModelTests`:

```csharp
[Fact]
public void Disabled_alarm_exposes_its_off_state_in_the_accessible_name()
{
    var item = Alarm("Lunch", SoundChoice.Bell, 14, 0);
    item.IsEnabled = false;
    var vm = new AlarmItemViewModel(item, isTomorrow: false, isNext: false);
    Assert.False(vm.IsEnabled);
    Assert.Contains("off", vm.AccessibleName);
}

[Fact]
public void Enabled_alarm_has_no_off_cue_and_exposes_a_toggle_label()
{
    var vm = new AlarmItemViewModel(Alarm("Lunch", SoundChoice.Bell, 14, 0), isTomorrow: false, isNext: false);
    Assert.True(vm.IsEnabled);
    Assert.DoesNotContain("off", vm.AccessibleName);
    Assert.Equal("Alarm at 14:00", vm.ToggleLabel);
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~AlarmItemViewModelTests"`
Expected: FAIL — build error, no `IsEnabled` / `ToggleLabel`; `AccessibleName` lacks the off cue.

- [ ] **Step 3: Add the members and the off cue**

In `src/Tidsro/ViewModels/AlarmItemViewModel.cs`, add two members (e.g. after `WarnText`):

```csharp
    public bool IsEnabled => Item.IsEnabled;
    public string ToggleLabel => $"Alarm at {TimeText}";
```

And change `AccessibleName` to append the off cue (state carried as text, never colour alone):

```csharp
    public string AccessibleName =>
        $"Alarm at {TimeText}{CadencePhrase}, {DisplayLabel}, {SoundText}{(WarnBefore ? ", warns 5 minutes before" : "")}{(IsNext ? ", next" : "")}{(IsEnabled ? "" : ", off")}";
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~AlarmItemViewModelTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/ViewModels/AlarmItemViewModel.cs tests/Tidsro.Tests/AlarmItemViewModelTests.cs
git commit -m "feat: expose alarm enabled state and off cue in the agenda row"
```

---

### Task 8: MainViewModel — `ToggleAlarmCommand` + undo preserves enabled

**Files:**
- Modify: `src/Tidsro/ViewModels/MainViewModel.cs` (add command; edit `UndoDelete`)
- Test: `tests/Tidsro.Tests/MainViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

Append to `MainViewModelTests`:

```csharp
[Fact]
public void ToggleAlarm_off_persists_and_announces()
{
    var vm = New(out _, out _);
    vm.AlarmTimeInput = "10:00"; vm.AddAlarmCommand.Execute(null);
    var changed = 0; vm.AlarmsChanged += (_, _) => changed++;
    string? announced = null; vm.Announcement += (_, m) => announced = m;

    vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);

    Assert.False(vm.Alarms[0].Item.IsEnabled);
    Assert.Equal(1, changed);                       // persisted via the event
    Assert.NotNull(announced);
    Assert.Contains("10:00", announced);
    Assert.Contains("off", announced);
}

[Fact]
public void ToggleAlarm_back_on_re_enables_and_announces_on()
{
    var vm = New(out _, out _);
    vm.AlarmTimeInput = "10:00"; vm.AddAlarmCommand.Execute(null);
    vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);    // off
    string? announced = null; vm.Announcement += (_, m) => announced = m;

    vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);    // on again

    Assert.True(vm.Alarms[0].Item.IsEnabled);
    Assert.NotNull(announced);
    Assert.Contains("on", announced);
}

[Fact]
public void ToggleAlarm_with_null_row_does_nothing()
{
    var vm = New(out _, out _);
    var changed = 0; vm.AlarmsChanged += (_, _) => changed++;
    vm.ToggleAlarmCommand.Execute(null);
    Assert.Equal(0, changed);
}

[Fact]
public void ToggleAlarm_commits_an_outstanding_pending_delete_first()
{
    var vm = New(out _, out _);
    vm.AlarmTimeInput = "10:00"; vm.AddAlarmCommand.Execute(null);
    vm.AlarmTimeInput = "11:00"; vm.AddAlarmCommand.Execute(null);
    vm.DeleteAlarmCommand.Execute(vm.Alarms[0]);    // 10:00 now pending-delete

    vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);    // toggling 11:00 settles the pending delete

    Assert.False(vm.HasPendingDelete);
    Assert.False(vm.Alarms.Single().Item.IsEnabled);   // only 11:00 remains, now off
}

[Fact]
public void Undo_restores_a_disabled_alarm_still_disabled()
{
    var vm = New(out _, out _);
    vm.AlarmTimeInput = "10:00"; vm.AddAlarmCommand.Execute(null);
    vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);    // off
    vm.DeleteAlarmCommand.Execute(vm.Alarms[0]);    // delete the disabled alarm
    vm.UndoDeleteCommand.Execute(null);             // undo

    Assert.False(Assert.Single(vm.Alarms).Item.IsEnabled);   // comes back still off
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~MainViewModelTests"`
Expected: FAIL — build error, no `ToggleAlarmCommand`; undo re-arms enabled.

- [ ] **Step 3: Add `ToggleAlarm`**

In `src/Tidsro/ViewModels/MainViewModel.cs`, add after `DeleteAlarm` (before `UndoDelete`):

```csharp
    [RelayCommand]
    private void ToggleAlarm(AlarmItemViewModel? row)
    {
        if (row is null) return;
        CommitPendingDelete();                          // settle any outstanding undo first
        var item = row.Item;
        _scheduler.SetEnabled(item, !item.IsEnabled);   // re-enable rolls a stale recurring alarm forward
        RebuildAgenda();
        AlarmsChanged?.Invoke(this, EventArgs.Empty);   // the on/off change is persisted
        Announce($"Alarm at {row.TimeText} turned {(item.IsEnabled ? "on" : "off")}");
    }
```

- [ ] **Step 4: Make `UndoDelete` preserve the enabled flag**

In `UndoDelete`, pass `item.IsEnabled` into both re-arm calls. The recurring branch:

```csharp
            _scheduler.ArmRecurringAlarm(next.Hour, next.Minute, days, item.Label, item.Sound, item.Id, next, item.WarnBefore, item.IsEnabled);
```

The clock branch:

```csharp
            _scheduler.ArmClockAlarm(fireAt, item.Label, item.Sound, item.Id, item.WarnBefore, item.IsEnabled);   // re-arm; next tick re-checks grace if past
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~MainViewModelTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Tidsro/ViewModels/MainViewModel.cs tests/Tidsro.Tests/MainViewModelTests.cs
git commit -m "feat: add per-alarm toggle command; undo preserves enabled state"
```

---

### Task 9: MainViewModel — park disabled alarms below, skip them for "next"

**Files:**
- Modify: `src/Tidsro/ViewModels/MainViewModel.cs` (`RebuildAgenda`, ~lines 339-357)
- Test: `tests/Tidsro.Tests/MainViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

Append to `MainViewModelTests`:

```csharp
[Fact]
public void A_disabled_alarm_parks_below_the_enabled_ones()
{
    var vm = New(out _, out _);                       // 09:00
    vm.AlarmTimeInput = "11:00"; vm.AddAlarmCommand.Execute(null);
    vm.AlarmTimeInput = "16:00"; vm.AddAlarmCommand.Execute(null);

    vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);      // turn off the 11:00 (currently on top)

    Assert.Equal("16:00", vm.Alarms[0].TimeText);     // enabled 16:00 rises to the top...
    Assert.True(vm.Alarms[0].IsNext);                 // ...and is next
    Assert.Equal("11:00", vm.Alarms[1].TimeText);     // disabled 11:00 parks below
    Assert.False(vm.Alarms[1].IsEnabled);
    Assert.False(vm.Alarms[1].IsNext);
}

[Fact]
public void With_every_alarm_off_none_is_marked_next()
{
    var vm = New(out _, out _);
    vm.AlarmTimeInput = "11:00"; vm.AddAlarmCommand.Execute(null);

    vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);      // the only alarm, now off

    var row = Assert.Single(vm.Alarms);
    Assert.False(row.IsEnabled);
    Assert.False(row.IsNext);                          // nothing is "next" when all are off
}

[Fact]
public void Re_enabling_a_recurring_alarm_returns_it_to_the_active_group()
{
    var vm = New(out var clock, out _);               // Thu 2026-01-01 09:00
    vm.AlarmTimeInput = "10:00"; vm.AlarmRepeat = RepeatOption.Daily;
    vm.AddAlarmCommand.Execute(null);

    vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);      // off
    Assert.False(vm.Alarms[0].Item.IsEnabled);

    clock.Advance(TimeSpan.FromDays(1));               // Fri 09:00 — its frozen 10:00 occurrence has passed
    vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);      // back on

    var row = Assert.Single(vm.Alarms);
    Assert.True(row.Item.IsEnabled);
    Assert.True(row.IsNext);                           // active again
    Assert.Equal(new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero), row.Item.EndsAt);   // rolled to Fri 10:00
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~MainViewModelTests"`
Expected: FAIL — the disabled 11:00 still sorts by timestamp and/or is marked next.

- [ ] **Step 3: Rewrite `RebuildAgenda` to park disabled alarms**

In `src/Tidsro/ViewModels/MainViewModel.cs`, replace the body of `RebuildAgenda`:

```csharp
    private void RebuildAgenda()
    {
        var today = _scheduler.Now.Date;

        // Enabled alarms first, in fire-time order.
        var enabled = _scheduler.Alarms
            .Where(a => a.IsEnabled)
            .OrderBy(a => a.EndsAt)
            .ThenBy(a => a.Label)
            .ThenBy(a => a.Id);
        // Disabled alarms park below, ordered by time-of-day — a disabled recurring alarm's date can
        // be stale (frozen while off), so the full timestamp would mis-sort it.
        var disabled = _scheduler.Alarms
            .Where(a => !a.IsEnabled)
            .OrderBy(a => a.EndsAt?.TimeOfDay)
            .ThenBy(a => a.Label)
            .ThenBy(a => a.Id);
        var ordered = enabled.Concat(disabled).ToList();

        Alarms.Clear();
        for (var i = 0; i < ordered.Count; i++)
        {
            var a = ordered[i];
            var isTomorrow = a.EndsAt is { } e && e.Date != today;
            var isNext = i == 0 && a.IsEnabled;   // a disabled alarm is never "next"
            Alarms.Add(new AlarmItemViewModel(a, isTomorrow, isNext));
        }
        OnPropertyChanged(nameof(IsDayEmpty));
        _agendaSignature = AgendaSignature();
    }
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~MainViewModelTests"`
Expected: PASS (including the existing `Alarms_are_sorted_by_fire_time` — all-enabled order is unchanged).

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/ViewModels/MainViewModel.cs tests/Tidsro.Tests/MainViewModelTests.cs
git commit -m "feat: park disabled alarms below active ones, skip them for next"
```

---

### Task 10: View — on/off switch on each alarm row

**Files:**
- Modify: `src/Tidsro/Views/MainWindow.xaml` (the `Alarms` `ItemsControl` row template, ~lines 213-266)

> XAML — verified by build + the Task 11 manual acceptance, not unit tests.

- [ ] **Step 1: Add the toggle switch to the row's action cluster**

In `src/Tidsro/Views/MainWindow.xaml`, inside the alarm-row `<DockPanel>`, add the switch as the **last** right-docked child — after the Edit button (`Content="&#xE70F;"`) and before the `<StackPanel Orientation="Horizontal">` content block:

```xml
                  <CheckBox DockPanel.Dock="Right" Style="{StaticResource ToggleSwitch}"
                            IsChecked="{Binding IsEnabled, Mode=OneWay}"
                            Command="{Binding DataContext.ToggleAlarmCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                            CommandParameter="{Binding}"
                            VerticalAlignment="Center" Margin="0,0,10,0"
                            AutomationProperties.Name="{Binding ToggleLabel}"/>
```

This places it left of Edit/Delete (DockPanel docks right-to-left in child order). `IsChecked` is one-way — the command does the work and `RebuildAgenda` re-creates the row, so the switch reflects the model.

- [ ] **Step 2: Dim the row content when the alarm is off**

In the same row, give the inner text `<StackPanel>` (the one holding `TimeText` / `CadenceText` / `DisplayLabel` / sound — the sibling after the `<Ellipse>`) a style that dims it when disabled. Wrap its opening tag:

```xml
                    <StackPanel>
                      <StackPanel.Style>
                        <Style TargetType="StackPanel">
                          <Style.Triggers>
                            <DataTrigger Binding="{Binding IsEnabled}" Value="False">
                              <Setter Property="Opacity" Value="0.45"/>
                            </DataTrigger>
                          </Style.Triggers>
                        </Style>
                      </StackPanel.Style>
```

(Dimming only the text keeps the switch and Edit/Delete fully legible.) Leave the rest of the inner `StackPanel` children unchanged.

- [ ] **Step 3: Build and launch to verify**

Run (stop any running instance first):

```bash
dotnet build
```

Expected: Build succeeded, 0 warnings. Then launch the built exe and confirm a switch shows on each alarm row.

```bash
Start-Process "src/Tidsro/bin/Debug/net10.0-windows/Tidsro.exe"
```

- [ ] **Step 4: Commit**

```bash
git add src/Tidsro/Views/MainWindow.xaml
git commit -m "feat: add on/off switch to each alarm row"
```

---

### Task 11: Full suite + manual acceptance + changelog

**Files:**
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Run the full test suite**

Run (stop any running `Tidsro.exe` first):

```bash
dotnet test
```

Expected: PASS — the full suite (235 prior + the new tests), 0 failures, 0 warnings.

- [ ] **Step 2: Manual GUI acceptance**

Launch the app and confirm:
- Add a few alarms (mix one-shot + recurring); toggle some off → they dim and drop to the bottom; the gold "next" highlight is on the soonest enabled alarm.
- Toggle all off → none is highlighted as "next".
- Quit and relaunch → disabled alarms are still present and still off.
- Re-enable a recurring alarm → it returns to the active group at its next future time.
- Screen-reader pass (Narrator): the switch reads its alarm + checked/unchecked state, and toggling announces "turned off / on".

- [ ] **Step 3: Add a changelog entry**

In `CHANGELOG.md`, add a new `## [Unreleased]` section directly above `## [1.3.2] — 2026-06-22`:

```markdown
## [Unreleased]

### Added
- Per-alarm on/off toggle in the Schedule. Switch an alarm off to keep it without it firing or
  warning — useful for silencing recurring alarms over a break — and back on when you need it.
  Disabled alarms are kept across restarts and parked, muted, at the bottom of the list.
```

- [ ] **Step 4: Commit**

```bash
git add CHANGELOG.md
git commit -m "docs: changelog for per-alarm toggle"
```

---

## After the plan

All work is on `feat/alarm-toggle`. Once manual acceptance passes, this is ready for a PR to `main`
(and a version bump + `publish.ps1` + tag, if releasing — Malin's call on patch vs minor).
