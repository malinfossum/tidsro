# Clear data from Settings — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the user two separate, confirmed actions in Settings — wipe every alarm, or reset every
preference — each taking effect immediately and leaving the other's data alone.

**Architecture:** `MainViewModel` gains one method that clears its three alarm-holding members and
disarms the scheduler. `SettingsViewModel` gains two `[RelayCommand]`s that reach the rest through
injected delegates, so both view-models stay free of UI and every path is unit-testable. A small
themed `ConfirmDialog` supplies the real confirmation; `App` wires the delegates together.

**Tech Stack:** C# / .NET 10 WPF, CommunityToolkit.Mvvm (`[RelayCommand]`, `[ObservableProperty]`),
xUnit. Spec: `docs/superpowers/specs/2026-08-10-tidsro-clear-data-design.md`.

## Global Constraints

- Branch: `feat/clear-data` (already created off `main`, spec committed).
- TDD throughout: write the failing test, watch it fail, then implement. Never the reverse.
- Baseline before starting: `dotnet test` is green at 270 tests on this branch.
- Stop any running Tidsro before building — a running instance locks the exe (MSB3027):
  `Get-Process Tidsro -ErrorAction SilentlyContinue | Stop-Process -Force`
- No `Co-Authored-By` and no Claude attribution in any commit message.
- View-models must not reference `System.Windows` — UI reaches them through injected delegates only.
- New XAML must use existing tokens (`PageBg`, `Text`, `TextMuted`, `FontSans`) and existing button
  styles (`GoldAction`, `QuietAction`), and carry `AutomationProperties.Name` on every control.

---

### Task 1: Clearing the alarms

**Files:**
- Modify: `src/Tidsro/ViewModels/MainViewModel.cs`
- Test: `tests/Tidsro.Tests/MainViewModelTests.cs`

**Interfaces:**
- Consumes: `SchedulerService.Cancel(TimerItem)` (removes from both the running and the alarm list),
  `MainViewModel.CommitPendingDelete()`, the `AlarmsChanged` event.
- Produces: `public void ClearAllAlarms()` on `MainViewModel` — Task 2 calls it through a delegate.

- [ ] **Step 1: Write the failing test**

Add to `tests/Tidsro.Tests/MainViewModelTests.cs`:

```csharp
[Fact]
public void ClearAllAlarms_empties_every_list_and_disarms_the_scheduler()
{
    var vm = New(out _, out var sched);
    vm.StartPresetCommand.Execute(30);                  // a running countdown
    vm.AlarmTimeInput = "14:30";
    vm.AddAlarmCommand.Execute(null);                   // a clock-time alarm
    vm.MissedNote = "Missed while away: Lunch · 11:30";

    vm.ClearAllAlarms();

    Assert.Empty(vm.Running);
    Assert.Empty(vm.Alarms);
    Assert.Null(vm.MissedNote);
    Assert.Empty(sched.Running);
    Assert.Empty(sched.Alarms);
}

[Fact]
public void ClearAllAlarms_persists_once()
{
    var vm = New(out _, out _);
    vm.AlarmTimeInput = "14:30";
    vm.AddAlarmCommand.Execute(null);
    var saves = 0;
    vm.AlarmsChanged += (_, _) => saves++;

    vm.ClearAllAlarms();

    Assert.Equal(1, saves);
}

[Fact]
public void ClearAllAlarms_settles_an_outstanding_undo_first()
{
    var vm = New(out _, out _);
    vm.AlarmTimeInput = "14:30";
    vm.AddAlarmCommand.Execute(null);
    vm.DeleteAlarmCommand.Execute(vm.Alarms.Count > 0 ? vm.Alarms[0] : null);

    vm.ClearAllAlarms();

    Assert.False(vm.HasPendingDelete);
    Assert.Null(vm.PendingDeleteLabel);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~MainViewModelTests.ClearAllAlarms`
Expected: FAIL — `error CS1061: 'MainViewModel' does not contain a definition for 'ClearAllAlarms'`.

- [ ] **Step 3: Write the implementation**

Add to `src/Tidsro/ViewModels/MainViewModel.cs`, next to `DeleteAlarm`:

```csharp
/// <summary>Wipe every countdown, alarm and missed note. Disarms before emptying, so the 250 ms
/// tick can't fire something mid-wipe. Called from Settings; the confirmation happens there.</summary>
public void ClearAllAlarms()
{
    CommitPendingDelete();                 // settle any outstanding undo first

    foreach (var row in Running.ToList()) _scheduler.Cancel(row.Item);
    foreach (var row in Alarms.ToList()) _scheduler.Cancel(row.Item);

    Running.Clear();
    Alarms.Clear();
    MissedNote = null;

    OnPropertyChanged(nameof(IsDayEmpty));
    AlarmsChanged?.Invoke(this, EventArgs.Empty);
    Announce("All alarms cleared");
}
```

Iterating over `.ToList()` copies is required: `Cancel` mutates the scheduler's lists while
`RebuildAgenda` may read them, and enumerating a collection you are clearing throws.

If `System.Linq` is not already imported in this file, add `using System.Linq;` at the top.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~MainViewModelTests.ClearAllAlarms`
Expected: PASS, 3 tests.

Then run the whole suite: `dotnet test`
Expected: PASS, 273 tests.

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/ViewModels/MainViewModel.cs tests/Tidsro.Tests/MainViewModelTests.cs
git commit -m "feat(alarms): add ClearAllAlarms to MainViewModel"
```

---

### Task 2: Make the startup toggle fakeable

**Why this task exists:** the reset calls `StartupService.Disable()`, which deletes the real
`HKCU\...\Run\Tidsro` value. A unit test that exercised it would wipe the developer's own autostart
entry every time the suite ran. The codebase already solves exactly this for audio with
`ISoundService` / `FakeSoundService`; do the same here. No behaviour changes in this task.

**Files:**
- Create: `src/Tidsro/Services/IStartupService.cs`
- Modify: `src/Tidsro/Services/StartupService.cs`
- Modify: `src/Tidsro/ViewModels/SettingsViewModel.cs`
- Create: `tests/Tidsro.Tests/FakeStartupService.cs`

**Interfaces:**
- Produces: `IStartupService` with `bool IsEnabled()`, `void Enable()`, `void Disable()`;
  `FakeStartupService` recording `EnableCalls` and `DisableCalls`. Task 3 depends on both.

- [ ] **Step 1: Create the interface**

`src/Tidsro/Services/IStartupService.cs`:

```csharp
namespace Tidsro.Services;

/// <summary>The launch-at-startup toggle, behind an interface so view-model tests never touch the
/// real HKCU Run key. The path-repair logic stays on the concrete StartupService.</summary>
public interface IStartupService
{
    bool IsEnabled();
    void Enable();
    void Disable();
}
```

- [ ] **Step 2: Implement it**

In `src/Tidsro/Services/StartupService.cs`, change the class declaration only:

```csharp
public sealed class StartupService : IStartupService
```

`IsEnabled`, `Enable` and `Disable` already match the interface — no other change.

- [ ] **Step 3: Depend on the interface in SettingsViewModel**

In `src/Tidsro/ViewModels/SettingsViewModel.cs`, change the field and the constructor parameter type
from `StartupService` to `IStartupService`. Nothing else changes yet.

- [ ] **Step 4: Create the test double**

`tests/Tidsro.Tests/FakeStartupService.cs`:

```csharp
using Tidsro.Services;

namespace Tidsro.Tests;

// Records the calls instead of touching the registry.
public sealed class FakeStartupService : IStartupService
{
    public int EnableCalls { get; private set; }
    public int DisableCalls { get; private set; }
    public bool Enabled { get; set; }

    public bool IsEnabled() => Enabled;
    public void Enable() { EnableCalls++; Enabled = true; }
    public void Disable() { DisableCalls++; Enabled = false; }
}
```

- [ ] **Step 5: Point the existing tests at the fake**

In `tests/Tidsro.Tests/SettingsViewModelTests.cs`, replace both occurrences of
`new StartupService("Tidsro.exe")` with `new FakeStartupService()` and drop the now-inaccurate
"not exercised here" comments.

- [ ] **Step 6: Verify nothing broke**

Run: `dotnet test`
Expected: PASS, 273 tests — same count as after Task 1, since this task is a refactor.

- [ ] **Step 7: Commit**

```bash
git add src/Tidsro/Services/IStartupService.cs src/Tidsro/Services/StartupService.cs \
        src/Tidsro/ViewModels/SettingsViewModel.cs tests/Tidsro.Tests/FakeStartupService.cs \
        tests/Tidsro.Tests/SettingsViewModelTests.cs
git commit -m "refactor(startup): put the startup toggle behind IStartupService"
```

---

### Task 3: The two Settings commands

**Files:**
- Modify: `src/Tidsro/ViewModels/SettingsViewModel.cs`
- Test: `tests/Tidsro.Tests/SettingsViewModelTests.cs`

**Interfaces:**
- Consumes: `MainViewModel.ClearAllAlarms()` from Task 1 (as an injected `Action`),
  `IStartupService` and `FakeStartupService` from Task 2, `AppSettings.Defaults()`.
- Produces: `SettingsViewModel.ClearAlarmsCommand` and `SettingsViewModel.ResetSettingsCommand`,
  plus this constructor signature, which Task 4 must call:

```csharp
public SettingsViewModel(AppSettings settings, IStartupService startup,
    Action save, Action<SoundChoice> onDefaultSoundChanged,
    Action clearAllAlarms, Func<int> alarmCount,
    Action resetWindowPlacement, Func<string, bool> confirm)
```

- [ ] **Step 1: Write the failing tests**

Add to `tests/Tidsro.Tests/SettingsViewModelTests.cs`. The two tests already in this file used the
4-argument constructor and must gain the four new arguments too — that edit is in Step 3 below.

```csharp
[Fact]
public void Clearing_alarms_asks_first_and_names_the_count()
{
    var shared = new AppSettings();
    string? message = null;
    var cleared = 0;
    var vm = new SettingsViewModel(shared, new FakeStartupService(),
        () => { }, _ => { }, () => cleared++, () => 6, () => { },
        m => { message = m; return true; });

    vm.ClearAlarmsCommand.Execute(null);

    Assert.Equal("Delete all 6 alarms? This cannot be undone.", message);
    Assert.Equal(1, cleared);
}

[Fact]
public void Declining_the_confirm_clears_nothing()
{
    var shared = new AppSettings();
    var cleared = 0;
    var vm = new SettingsViewModel(shared, new FakeStartupService(),
        () => { }, _ => { }, () => cleared++, () => 6, () => { }, _ => false);

    vm.ClearAlarmsCommand.Execute(null);

    Assert.Equal(0, cleared);
}

[Fact]
public void Clearing_with_nothing_to_clear_does_not_even_ask()
{
    var shared = new AppSettings();
    var asked = false;
    var cleared = 0;
    var vm = new SettingsViewModel(shared, new FakeStartupService(),
        () => { }, _ => { }, () => cleared++, () => 0, () => { },
        _ => { asked = true; return true; });

    vm.ClearAlarmsCommand.Execute(null);

    Assert.False(asked);
    Assert.Equal(0, cleared);
}

[Fact]
public void Resetting_restores_every_default_and_refreshes_the_draft()
{
    var shared = new AppSettings
    {
        LaunchAtStartup = true, DefaultSound = SoundChoice.Bell,
        WindowLeft = 100, WindowTop = 200, WindowWidth = 900, WindowHeight = 900,
    };
    var startup = new FakeStartupService { Enabled = true };
    var placementResets = 0; var saves = 0;
    var vm = new SettingsViewModel(shared, startup,
        () => saves++, _ => { }, () => { }, () => 6, () => placementResets++, _ => true);

    vm.ResetSettingsCommand.Execute(null);

    Assert.Equal(1, startup.DisableCalls);   // the Run key must go with the checkbox
    Assert.False(shared.LaunchAtStartup);
    Assert.Equal(SoundChoice.None, shared.DefaultSound);
    Assert.Null(shared.WindowLeft);
    Assert.Null(shared.WindowTop);
    Assert.Null(shared.WindowWidth);
    Assert.Null(shared.WindowHeight);
    Assert.False(vm.LaunchAtStartup);                 // draft refreshed...
    Assert.Equal(SoundChoice.None, vm.DefaultSound);  // ...so a later Save can't rewrite the old values
    Assert.Equal(1, placementResets);
    Assert.Equal(1, saves);
}

[Fact]
public void Saving_after_a_reset_keeps_the_defaults()
{
    var shared = new AppSettings { LaunchAtStartup = true, DefaultSound = SoundChoice.Bell };
    var vm = new SettingsViewModel(shared, new FakeStartupService(),
        () => { }, _ => { }, () => { }, () => 6, () => { }, _ => true);

    vm.ResetSettingsCommand.Execute(null);
    vm.Save();

    Assert.Equal(SoundChoice.None, shared.DefaultSound);
    Assert.False(shared.LaunchAtStartup);
}

[Fact]
public void Declining_the_reset_changes_nothing()
{
    var shared = new AppSettings { LaunchAtStartup = true, DefaultSound = SoundChoice.Bell };
    var vm = new SettingsViewModel(shared, new FakeStartupService(),
        () => { }, _ => { }, () => { }, () => 6, () => { }, _ => false);

    vm.ResetSettingsCommand.Execute(null);

    Assert.True(shared.LaunchAtStartup);
    Assert.Equal(SoundChoice.Bell, shared.DefaultSound);
}
```

Update the two pre-existing tests (`Editing_a_setting_does_not_apply_until_Save`
and `Save_applies_changes_to_the_shared_AppSettings_and_persists`) to pass the four new arguments:

```csharp
var vm = new SettingsViewModel(shared, startup, save: () => saves++, _ => { },
    clearAllAlarms: () => { }, alarmCount: () => 0, resetWindowPlacement: () => { },
    confirm: _ => true);
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~SettingsViewModelTests`
Expected: FAIL — `error CS1729: 'SettingsViewModel' does not contain a constructor that takes 8 arguments`.

- [ ] **Step 3: Write the implementation**

Replace the field block and constructor in `src/Tidsro/ViewModels/SettingsViewModel.cs`, and add the
two commands. The file needs `using CommunityToolkit.Mvvm.Input;` for `[RelayCommand]`:

```csharp
private readonly IStartupService _startup;      // interface came from Task 2
private readonly Action _save;                              // bundles settings + alarms at the App level
private readonly Action<SoundChoice> _onDefaultSoundChanged;
private readonly AppSettings _settings;   // the in-memory snapshot App reuses to open this window; keep it current
private readonly Action _clearAllAlarms;
private readonly Func<int> _alarmCount;
private readonly Action _resetWindowPlacement;
private readonly Func<string, bool> _confirm;

public SettingsViewModel(AppSettings settings, IStartupService startup,
    Action save, Action<SoundChoice> onDefaultSoundChanged,
    Action clearAllAlarms, Func<int> alarmCount,
    Action resetWindowPlacement, Func<string, bool> confirm)
{
    _settings = settings;
    _startup = startup; _save = save; _onDefaultSoundChanged = onDefaultSoundChanged;
    _clearAllAlarms = clearAllAlarms; _alarmCount = alarmCount;
    _resetWindowPlacement = resetWindowPlacement; _confirm = confirm;
    _launchAtStartup = settings.LaunchAtStartup;
    _defaultSound = settings.DefaultSound;
}

// Both of these act at once and are outside the Save/Cancel draft — Cancel does not undo them,
// which is why the view keeps them in their own separated section.

[RelayCommand]
private void ClearAlarms()
{
    var count = _alarmCount();
    if (count == 0) return;                     // nothing to lose: don't ask a pointless question
    if (!_confirm($"Delete all {count} alarms? This cannot be undone.")) return;

    _clearAllAlarms();                          // raises AlarmsChanged, which persists via App
}

[RelayCommand]
private void ResetSettings()
{
    if (!_confirm("Reset all settings? Launch at startup will be turned off.")) return;

    var defaults = AppSettings.Defaults();
    _startup.Disable();                         // never leave the Run key behind a checkbox that reads off

    _settings.LaunchAtStartup = defaults.LaunchAtStartup;
    _settings.DefaultSound = defaults.DefaultSound;
    _settings.WindowLeft = null;
    _settings.WindowTop = null;
    _settings.WindowWidth = null;
    _settings.WindowHeight = null;

    // Refresh the draft, or a following Save writes the pre-reset values straight back.
    LaunchAtStartup = defaults.LaunchAtStartup;
    DefaultSound = defaults.DefaultSound;
    _onDefaultSoundChanged(defaults.DefaultSound);

    _resetWindowPlacement();                    // main window returns to 440x600 centred, so its
                                                // OnClosing can't re-save the old coordinates
    _save();
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~SettingsViewModelTests`
Expected: PASS, 8 tests.

Then: `dotnet test`
Expected: PASS, 279 tests.

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/ViewModels/SettingsViewModel.cs tests/Tidsro.Tests/SettingsViewModelTests.cs
git commit -m "feat(settings): add clear-alarms and reset-settings commands"
```

---

### Task 4: The dialog, the buttons and the wiring

This task has no unit tests — it is XAML and composition, verified by running the app. Everything
decision-shaped was already tested in Tasks 1 and 3.

**Files:**
- Create: `src/Tidsro/Views/ConfirmDialog.xaml`
- Create: `src/Tidsro/Views/ConfirmDialog.xaml.cs`
- Modify: `src/Tidsro/Views/SettingsWindow.xaml`
- Modify: `src/Tidsro/Views/SettingsWindow.xaml.cs`
- Modify: `src/Tidsro/Views/MainWindow.xaml.cs`
- Modify: `src/Tidsro/App.xaml.cs:256-259`

**Interfaces:**
- Consumes: the Task 3 constructor signature, `MainViewModel.ClearAllAlarms()` from Task 1.
- Produces: `ConfirmDialog.Ask(Window owner, string message)` returning `bool`;
  `MainWindow.ResetPlacement()`.

- [ ] **Step 1: Create the themed confirm dialog**

`src/Tidsro/Views/ConfirmDialog.xaml`:

```xml
<Window x:Class="Tidsro.Views.ConfirmDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Tidsro" Width="340" SizeToContent="Height" WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize" ShowInTaskbar="False"
        Background="{StaticResource PageBg}" Foreground="{StaticResource Text}"
        FontFamily="{StaticResource FontSans}">
  <StackPanel Margin="24">
    <TextBlock x:Name="MessageText" TextWrapping="Wrap"
               AutomationProperties.Name="Confirmation message"/>
    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,24,0,0">
      <Button Content="Yes" Style="{StaticResource GoldAction}" IsDefault="True"
              MinWidth="84" Margin="0,0,8,0" Click="Yes_Click"
              AutomationProperties.Name="Confirm"/>
      <Button Content="Cancel" Style="{StaticResource QuietAction}" IsCancel="True"
              MinWidth="84"
              AutomationProperties.Name="Cancel"/>
    </StackPanel>
  </StackPanel>
</Window>
```

`src/Tidsro/Views/ConfirmDialog.xaml.cs`:

```csharp
using System.Windows;

namespace Tidsro.Views;

// A dark, owner-centred yes/no in the app's own styling. Esc cancels via IsCancel.
public partial class ConfirmDialog : Window
{
    private ConfirmDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    /// <summary>Show the question modally. True only when the user explicitly confirms.</summary>
    public static bool Ask(Window owner, string message) =>
        new ConfirmDialog(message) { Owner = owner }.ShowDialog() == true;

    private void Yes_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
```

- [ ] **Step 2: Add the Data section to the Settings window**

In `src/Tidsro/Views/SettingsWindow.xaml`, insert between the sound `ComboBox` and the
Save/Cancel `StackPanel`:

```xml
    <Separator Margin="0,24,0,0" Background="{StaticResource TextMuted}" Opacity="0.3"/>
    <TextBlock Text="Data" Margin="0,16,0,4" Foreground="{StaticResource TextMuted}"/>
    <TextBlock Text="These take effect immediately. Cancel will not undo them."
               TextWrapping="Wrap" Margin="0,0,0,10" Foreground="{StaticResource TextMuted}"/>
    <Button Content="Clear all alarms" Style="{StaticResource QuietAction}"
            HorizontalAlignment="Left" MinWidth="150" Margin="0,0,0,8"
            Command="{Binding ClearAlarmsCommand}"
            AutomationProperties.Name="Clear all alarms"/>
    <Button Content="Reset all settings" Style="{StaticResource QuietAction}"
            HorizontalAlignment="Left" MinWidth="150"
            Command="{Binding ResetSettingsCommand}"
            AutomationProperties.Name="Reset all settings"/>
```

- [ ] **Step 3: Give MainWindow a placement reset**

Add to `src/Tidsro/Views/MainWindow.xaml.cs`, next to `ApplyPlacement`:

```csharp
/// <summary>Return to the XAML defaults after a settings reset, so OnClosing can't re-save the
/// coordinates the reset just cleared.</summary>
public void ResetPlacement()
{
    Width = 440;
    Height = 600;
    WindowStartupLocation = WindowStartupLocation.CenterScreen;
    Left = (SystemParameters.WorkArea.Width - Width) / 2 + SystemParameters.WorkArea.Left;
    Top = (SystemParameters.WorkArea.Height - Height) / 2 + SystemParameters.WorkArea.Top;
}
```

- [ ] **Step 4: Supply the real confirm from the Settings window**

In `src/Tidsro/Views/SettingsWindow.xaml.cs`, the view owns the dialog so the view-model does not
have to. Replace the constructor:

```csharp
public SettingsWindow(Func<Func<string, bool>, SettingsViewModel> vmFactory)
{
    InitializeComponent();
    DataContext = vmFactory(message => ConfirmDialog.Ask(this, message));
}
```

The factory shape exists because the confirm function needs `this` as the dialog owner, which does
not exist until the window is constructed.

- [ ] **Step 5: Wire it up in App**

In `src/Tidsro/App.xaml.cs`, replace the `SettingsWindow` construction at lines 256-259:

```csharp
        _main ??= new MainWindow(_mainVm, () => new SettingsWindow(confirm =>
                new SettingsViewModel(_settings, new StartupService(StartupService.CurrentExePath),
                    SaveData, _mainVm.SetDefaultSound,
                    clearAllAlarms: _mainVm.ClearAllAlarms,
                    alarmCount: () => _mainVm.Alarms.Count + _mainVm.Running.Count,
                    resetWindowPlacement: () => _main?.ResetPlacement(),
                    confirm: confirm)),
            editFactory, _settings, SaveData);
```

- [ ] **Step 6: Build and run the app**

```bash
dotnet test
```
Expected: PASS, 279 tests (this task adds no tests).

```bash
Get-Process Tidsro -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build src/Tidsro/Tidsro.csproj
Start-Process src\Tidsro\bin\Debug\net10.0-windows\Tidsro.exe
```

- [ ] **Step 7: Manual verification pass**

Work through each of these in the running app:

1. Open Settings with alarms present. The Data section appears below a divider, with both buttons.
2. Click **Clear all alarms** → the dialog is dark, centred on Settings, and names the real count.
3. Press Esc → nothing is deleted.
4. Click it again and confirm → every quick timer, alarm and the missed note disappear from the main
   window immediately.
5. Reopen the app. The alarms are still gone, confirming it persisted.
6. Recreate an alarm, set default sound to a chime and turn launch-at-startup on, then Save.
7. Click **Reset all settings** and confirm → the checkbox clears, the sound picker returns to None,
   and the main window returns to its default size, centred.
8. Click **Save** immediately afterwards, close and reopen the app: the defaults must have stuck.
   This is the reset-then-Save trap — if the old sound comes back, the draft refresh in Task 2 is wrong.
9. Verify the alarm from step 6 survived the settings reset.
10. Check the Run key is gone after the reset:
    `Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name Tidsro -ErrorAction SilentlyContinue`
11. Start Narrator and tab through the Data section: both buttons and the dialog announce their names.

- [ ] **Step 8: Commit**

```bash
git add src/Tidsro/Views/ConfirmDialog.xaml src/Tidsro/Views/ConfirmDialog.xaml.cs \
        src/Tidsro/Views/SettingsWindow.xaml src/Tidsro/Views/SettingsWindow.xaml.cs \
        src/Tidsro/Views/MainWindow.xaml.cs src/Tidsro/App.xaml.cs
git commit -m "feat(settings): add a Data section with a themed confirm dialog"
```

---

## Notes for the reviewer

- **Window placement deviates from the spec, deliberately.** The spec says a reset forgets the
  remembered window coordinates. `MainWindow.OnClosing` writes the live window's placement back into
  settings on every close, so clearing the stored values alone would be silently undone the next time
  the window closed. Task 4 Step 3 resets the live window too, so what gets re-saved is the default
  placement. The user-visible outcome matches the spec; the stored values end up as 440x600 centred
  rather than null.
- **`IStartupService` is not in the spec** and was added in Task 2 for a concrete reason: without it,
  the reset test deletes the developer's own `HKCU\...\Run\Tidsro` value every time `dotnet test`
  runs. It follows the existing `ISoundService` / `FakeSoundService` pattern and changes no behaviour.
- **Test counts** assume the branch starts at 270. Task 1 adds 3, Task 2 adds none (refactor),
  Task 3 adds 6 while updating the 2 existing Settings tests. Final: 279.
