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
foreach item in Running + Alarms:            // over a snapshot, not the live collections
    _scheduler.Cancel(item);                 // removes from both _running and _alarms
Running.Clear(); Alarms.Clear();
MissedNote = null;
AlarmsChanged?.Invoke(...);                  // App's existing SaveData path persists it
```

Disarming through `SchedulerService.Cancel` **before** emptying the collections is the load-bearing
order: it guarantees nothing can fire from the 250 ms tick between the two steps. `Cancel` already
removes from both scheduler lists, so one call covers countdowns and alarms alike.

No new persistence code — raising `AlarmsChanged` reuses the save path every other mutation uses.

### 2. Resetting the preferences — `SettingsViewModel`

`AppSettings.Defaults()` is already exactly the target state: startup off, `SoundChoice.None`,
all four window coordinates null (so the next open uses default placement).

Reset must also call `StartupService.Disable()`. Leaving the Run key behind while the checkbox
reads off would make the UI lie about the user's machine.

Because Settings is a draft dialog, reset must then refresh the view-model's own
`LaunchAtStartup` and `DefaultSound` to the new defaults. Without that, clicking Save afterwards
writes the pre-reset preferences straight back over the reset — the one real trap in this feature.

### 3. Confirmation — `ConfirmDialog`

A small themed window built like `EditAlarmWindow`: `PageBg`/`Text` from `tokens.xaml`,
`GoldAction` for the confirming button, `QuietAction` for Cancel, `IsCancel="True"` so Esc backs
out, and `AutomationProperties.Name` on both buttons. Owner-centred and modal.

Messages name the count, so the user sees the size of what they are about to lose:

- *Delete all 6 alarms? This cannot be undone.*
- *Reset all settings? Launch at startup will be turned off.*

`SettingsViewModel` takes an injected `Func<string, bool> confirm` rather than calling the dialog
itself. The view-model stays free of UI, and both the accepted and declined paths become ordinary
unit tests. `SettingsWindow` supplies the real implementation.

### 4. Placement — `SettingsWindow.xaml`

Below the existing controls: a divider, a "Data" heading, then the two buttons in `QuietAction`
style, stacked with the alarms one first.

Both act the instant they are confirmed and persist at once. **Cancel does not undo them** — they
are outside the draft. This is deliberate and is why they sit in their own visually separated
section rather than among the preference controls.

## Testing

Headless view-model tests, following the existing suite:

- Clearing empties `Running`, `Alarms`, and `MissedNote`, and the scheduler has nothing armed
  afterwards.
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

## Rejected alternatives

- **Undo bar instead of a confirm.** Consistent with single deletes, but the bar holds one pending
  item, so a bulk wipe would need it to hold a set — machinery a confirmed, deliberate action does
  not need.
- **Staging the wipe in the draft.** Confirming a deletion and seeing nothing change until Save
  reads as a bug.
- **Stock `MessageBox`.** One line of code, but a light system dialog in the middle of a deliberately
  dark, calm UI is a visible seam.
