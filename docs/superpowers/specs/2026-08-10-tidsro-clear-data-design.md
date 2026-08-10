# Tidsro — clear data from Settings (design)

**Date:** 2026-08-10
**Status:** Approved — ready for plan
**Origin:** A download should arrive in a clean state and stay under the user's control — whether
data is kept is the user's call, not the app's. The uninstaller now offers to delete
`%AppData%\Tidsro`, but there is no way to start over without uninstalling.

## Goal

Two explicit, separate actions in Settings: wipe the alarms, or reset the preferences. Each is
confirmed, each takes effect immediately, and neither touches the other's data.

## Scope

**In scope**
- **Clear all alarms** — every recurring alarm, one-shot alarm, running countdown, and the missed
  note. Preferences survive.
- **Reset all settings** — launch-at-startup, default sound, and remembered window placement return
  to `AppSettings.Defaults()`. Alarms survive.
- A themed confirmation dialog, reusable for any later destructive action.

**Out of scope** (not now)
- A single "reset everything" button. Two deliberate choices beat one ambiguous one.
- Export or backup before clearing.
- Deleting `tidsro.log`. It is diagnostics, not user data, and the tray already exposes
  "Open log folder".
- Undo. The confirmation is the safety net; see *Rejected alternatives*.

## Design

### 1. Clearing the alarms — `MainViewModel.ClearAllAlarms()`

`MainViewModel` holds two collections and one string that together are "the alarms":
`Running` (quick timers), `Alarms` (the Schedule), and `MissedNote`.

```
CommitPendingDelete();                       // settle any outstanding undo first
closeOpenPopups();                           // an open card's Snooze would re-arm what we just wiped
foreach item in _scheduler.Alarms + _scheduler.Running:   // over snapshots, not the live lists
    _scheduler.Cancel(item);                 // removes from both _running and _alarms
Running.Clear(); Alarms.Clear();
MissedNote = null;
AlarmsChanged?.Invoke(...);                  // App's existing SaveData path persists it
```

Three things here are load-bearing.

**Iterate the scheduler, not the view.** `Running` and `Alarms` are derived collections, and
`App.SaveData` builds `data.json` from `_scheduler.Alarms`. Clearing by walking the view would let
any scheduler entry the agenda doesn't currently reflect survive the wipe, stay armed, and be
written straight back by the save the wipe itself triggers. The scheduler is the source of truth on
both sides, so it drives the wipe — and the count in the confirmation message comes from it too,
or "nothing to clear" could be reported while alarms are still armed.

**Disarm before emptying.** `Cancel` runs before the collections are cleared, so nothing can fire
from the 250 ms tick in between. One call covers countdowns and alarms alike.

**Close open completion popups.** Their buttons are not inert: `PopupViewModel.Plus5` calls
`SchedulerService.Snooze` and then saves. Without this, clicking **+5** on a card still on screen
after a wipe re-arms an alarm into the emptied schedule and persists it, with no way for the user to
tell where it came from.

No new persistence code — raising `AlarmsChanged` reuses the save path every other mutation uses.
That path currently swallows `IOException` and `UnauthorizedAccessException`, which is a reasonable
non-critical choice for an ordinary edit but inverts the user's intent here: they asked for the data
to be gone, the write fails silently, and everything returns at the next launch. `App.SaveData`
therefore reports the failure through the existing `LogService.Log(ex, source)`, which already logs
and raises a tray balloon.

### 2. Resetting the preferences — `SettingsViewModel`

`AppSettings.Defaults()` is already exactly the target state: startup off, `SoundChoice.None`,
all four window coordinates null (so the next open uses default placement).

Reset must also call `StartupService.Disable()`. Leaving the Run key behind while the checkbox
reads off would make the UI lie about the user's machine.

Two implementation consequences, found while planning:

- `StartupService` moves behind an `IStartupService` interface, mirroring the existing
  `ISoundService` / `FakeSoundService` pattern. Otherwise a unit test covering the reset deletes the
  developer's own `HKCU\...\Run\Tidsro` value on every test run.
- `MainWindow.OnClosing` writes the live window's placement back into settings, so clearing the
  stored coordinates alone would be undone the next time the window closed. The reset therefore also
  returns the live window to 440x600 centred. The user-visible result is the spec's, but the stored
  values end up as those defaults rather than null.

Because Settings is a draft dialog, reset must then refresh the view-model's own
`LaunchAtStartup` and `DefaultSound` to the new defaults. Without that, clicking Save afterwards
writes the pre-reset preferences straight back over the reset — the one real trap in this feature.

### 3. Confirmation — `ConfirmDialog`

A small themed window built like `EditAlarmWindow`: `PageBg`/`Text` from `tokens.xaml`,
`GoldAction` for the confirming button, `QuietAction` for Cancel, `IsCancel="True"` so Esc backs
out, and `AutomationProperties.Name` on both buttons. Owner-centred and modal.

**Cancel is the default button and takes initial focus.** Confirming an irreversible wipe must cost
a deliberate Tab or a click. The button that opened the dialog was itself activated by Enter or
Space, and a held or repeated key would otherwise carry straight through into the confirmation. The
uninstaller's prompt already defaults to No; this matches it.

`Ask(owner, title, message)` sets the window title as well as the body, because a screen reader
announces the title when a modal opens — "Tidsro" would say only that *something* opened.

- Title *Delete alarms?* — *Delete all 6 alarms? This cannot be undone.*
- Title *Reset settings?* — *Reset all settings? Launch at startup will be turned off. Your alarms
  and the diagnostic log are kept.*

Naming the log in the copy matters: someone clearing their data before handing the machine on would
reasonably assume nothing personal remains, and `LogService` records exception text that can carry
an alarm's label.

`SettingsViewModel` takes an injected `Func<string, bool> confirm` rather than calling the dialog
itself. The view-model stays free of UI, and both the accepted and declined paths become ordinary
unit tests. `SettingsWindow` supplies the real implementation.

### 4. Placement — `SettingsWindow.xaml`

Below the existing controls: a divider, a "Data" heading, then the two buttons in `QuietAction`
style, stacked with the alarms one first.

Both act the instant they are confirmed and persist at once. **Cancel does not undo them** — they
are outside the draft. This is deliberate and is why they sit in their own visually separated
section rather than among the preference controls.

The sentence saying so is rendered in `Text`, not `TextMuted`. It is the only warning shown before
an irreversible action, and the muted token is the lowest-contrast text in the dialog — the wrong
place for the highest-stakes line. `TextMuted` stays on the "Data" heading.

## Testing

Headless view-model tests, following the existing suite:

- Clearing empties `Running`, `Alarms`, and `MissedNote`, and the scheduler has nothing armed
  afterwards.
- Clearing disarms an alarm armed directly on the scheduler without an intervening agenda rebuild —
  proving the wipe follows the source of truth rather than the view.
- Clearing raises the close-popups request exactly once, and before anything is disarmed. That the
  popups actually close, and that a snooze afterwards re-arms nothing, is a manual check — the
  windows themselves are outside the view-model.
- Clearing raises `AlarmsChanged` exactly once.
- A **declined** confirm leaves every alarm in place — for both buttons.
- Reset restores each default field and calls `StartupService.Disable()`.
- Reset refreshes the draft, so a following `Save()` cannot rewrite the pre-reset values.
- Isolation both ways: resetting settings leaves alarms untouched; clearing alarms leaves
  preferences untouched.
- Clearing with an outstanding pending delete commits it first and does not throw.

Manual pass (not automatable here): both dialogs read correctly under Narrator, Esc cancels, and
the dark styling matches the app.

## Risks and edge cases

- **A countdown running while clearing.** Handled by disarming before emptying: it stops rather
  than fires.
- **A completion popup already on screen.** Popups carry a transient snapshot and are detached from
  the live alarm by design, so an open popup is left alone rather than force-closed.
- **Reset while the startup Run key points elsewhere.** `Disable()` deletes the value by name, so
  it clears regardless of the path recorded — consistent with the autostart work on
  `fix/autostart-survives-move`.
- **The success announcement lands on a window the screen reader isn't in.** `ClearAllAlarms` raises
  `Announce("All alarms cleared")` through the main window's UIA live region while the Settings
  dialog is modal and focused. The announcement is kept, since the method is callable from anywhere
  and it costs nothing, but a Narrator user's reliable confirmation is the dialog closing and the
  empty schedule on return. Giving Settings its own live region would duplicate the main window's
  UIA plumbing for one message; **accepted as a limitation rather than fixed.**

## Rejected alternatives

- **Undo bar instead of a confirm.** Consistent with single deletes, but the bar holds one pending
  item, so a bulk wipe would need it to hold a set — machinery a confirmed, deliberate action does
  not need.
- **Staging the wipe in the draft.** Confirming a deletion and seeing nothing change until Save
  reads as a bug.
- **Stock `MessageBox`.** One line of code, but a light system dialog in the middle of a deliberately
  dark, calm UI is a visible seam.
