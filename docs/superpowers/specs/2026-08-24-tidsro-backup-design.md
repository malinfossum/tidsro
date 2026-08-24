# Tidsro — export and import data (design)

**Date:** 2026-08-24
**Status:** Approved — stress-tested 2026-08-24 (nine findings folded in), ready for plan
**Origin:** A schedule built up over a term is the most valuable thing Tidsro holds, and there is
no way to get it off the machine or back onto a new one. Cloud sync was dropped from the roadmap by
decision (2026-08-24) — Tidsro stays local-first, no accounts, no network — so the answer is a file
the user owns.

## Goal

Two buttons in the existing Settings "Data" section: write everything to a JSON file the user
chooses, and read one back. Import asks what to restore — the alarms alone, or the whole file.

## Scope

**In scope**
- **Export data…** — a Save dialog writing the complete `TidsroData` (settings and alarms) to a
  file of the user's choosing.
- **Import data…** — an Open dialog, validation, a three-way choice (alarms only / everything /
  cancel), and one safety copy of the pre-import state.
- `IFileDialogService`, so view-model tests never open a real dialog.

**Out of scope** (not now)
- **Rolling automatic backups.** Considered and rejected — see *Rejected alternatives*.
- A restore-from-backup browser. An export *is* the backup file; Import points at it.
- Cloud sync, encryption, scheduled or timed exports.
- Merging an imported file into the existing schedule. Import replaces; it does not merge.

## Design

### 1. The file — no new format

Export writes exactly what `PersistenceService.Save` writes: a `TidsroData` document at
`SchemaVersion = 4`, serialized with the same options (`WriteIndented`, case-insensitive). This
matters more than it looks:

- `%AppData%\Tidsro\data.json` can be copied out by hand and imported, and an exported file can be
  dropped in as `data.json`. The two are the same artifact.
- The v1.0/v2/v3 back-compat already in `Load()` applies to imports for free, so a file exported by
  an older Tidsro still imports.
- `TidsroData.Sanitized()` already exists to harden a document loaded from disk — unknown enum
  values, duplicate ids, out-of-range hours, `DateTime` values that would throw when armed. Import
  reuses it rather than growing a second validator.

`App.SaveData()` currently builds the `TidsroData` inline. Extract that into `BuildData()` so
export and save cannot drift apart.

`PersistenceService.Save` also needs splitting. Its atomic write moves into a private
`WriteTo(path, data)` — create the directory, write `path + ".tmp"`, `File.Replace` or `File.Move`,
and **delete the temp file in a `catch` before rethrowing**. `Save` becomes `WriteTo` followed by
`ClearQuarantine()`; export calls `WriteTo` alone. Export must not inherit quarantine semantics:
`ClearQuarantine` deletes `<path>.corrupt`, which is correct for the app's own data file and quietly
destructive against a user-chosen destination — exporting to `Documents\notes.json` would delete a
`notes.json.corrupt` sitting beside it.

### 2. Export — `SettingsViewModel.ExportDataCommand`

```
var path = _fileDialogs.AskSavePath($"tidsro-backup-{today:yyyy-MM-dd}.json");
if (path is null) return;                    // user cancelled — nothing happens
var count = _exportTo(path);                 // App: WriteTo(path, BuildData()); returns alarm count
_showMessage("Exported", $"Exported {count} alarms to {Path.GetFileName(path)}.");
Announce(...);                               // same text, through the UIA notifier
```

Export writes the **live** state via `BuildData()`, not a copy of `data.json`. If saves have been
failing — disk full, file locked — the export still captures good data, which is precisely when it
matters most.

**Success is reported.** The Save dialog closing is not feedback: a sighted user is left guessing
and a screen-reader user gets silence, which is what makes people click Export three times. Success
shows the single-OK dialog from §4 naming the file, and announces the same text.

A failed export (unwritable location, removable drive pulled) surfaces through that same in-app
dialog — **not** a tray balloon. Balloons never appear on this machine (`ToastEnabled = 0`), and a
silent failure here means the user believes they have a backup they do not have. This is the one
error path in the app where being invisible is actively dangerous.

### 3. Import — `SettingsViewModel.ImportDataCommand`

Order matters; each step can abort without touching the live schedule.

```
1. path = _fileDialogs.AskOpenPath()          -> null: cancelled, stop
2. data = _readFile(path)                     -> null: unreadable/not a Tidsro file, show error, stop
3. choice = _askImportChoice(counts)          -> Cancel: stop
4. _snapshotBeforeImport()                    -> best effort, never blocks the import
5. apply(data, choice)
```

**Step 2 — validate before replacing.** Three gates, in order.

1. **Size.** Reject above 8 MB on `FileInfo.Length` before reading a byte. `File.ReadAllText` on a
   file the user picked by accident — a log, a video — allocates until it throws
   `OutOfMemoryException`, which is outside the caught set and would reach the global handler as a
   crash. 8 MB is thousands of alarms. `OutOfMemoryException` joins the caught set anyway.
2. **Shape.** Reject any JSON document carrying none of `SchemaVersion`, `Settings`, `Alarms` or
   `RecurringAlarms` at the top level. This gate is the important one: `JsonSerializer` succeeds on
   *any* JSON object, so `{"foo":1}` deserializes to an empty-but-valid `TidsroData`, sanitizes into
   `AppSettings.Defaults()`, and walks through every later check. Without a shape gate, picking
   `package.json` by mistake reads as a legitimate empty backup and — once confirmed — destroys the
   live schedule.
3. **Sanitize.** `TidsroData.Sanitized()` as normal.

A file failing any gate produces the error dialog and changes nothing. A document that passes the
shape gate but sanitizes down to zero alarms is *not* an error — an empty schedule is a legitimate
thing to restore — and the choice dialog states the counts, so it stays visible before it is
applied. "Empty is legitimate" applies only to files that are genuinely Tidsro documents.

**Step 3 — the choice.** A new `ChoiceDialog` built on `ConfirmDialog`'s shape: owner-centred,
themed, three buttons.

| Button | Style | Notes |
|---|---|---|
| Restore alarms only | `GoldAction` | The common case, so it carries the accent |
| Restore everything | `QuietAction` | Settings as well as alarms |
| Cancel | `QuietAction` | `IsCancel`, `IsDefault`, and focused |

Cancel keeps the same safety posture as `ConfirmDialog`: closing with the title-bar X leaves
`DialogResult` null, which reads as cancel, and so does Esc via `IsCancel`. Tab order is
**Restore alarms only → Restore everything → Cancel**, and focus returns to the Import button when
the dialog closes. `ConfirmDialog`'s `KeyboardNavigation.AcceptsReturn="True"` on the confirm button
is a deliberate oddity that stops Enter confirming; it is restated here rather than copied by
instinct — with Cancel as `IsDefault`, Enter cancels.

The message names what was found *and* what will be protected: "This file holds 7 alarms and 3
recurring alarms. Your current data is copied to `data-before-import.json` first." A recovery file
nobody knows about is not a recovery file, and the dialog is the only moment the user is thinking
about it.

**Step 4 — one safety copy.** Before applying, copy the current `data.json` to
`%AppData%\Tidsro\data-before-import.json`. Single file, same folder, overwritten on every import,
no rotation and no new directory. Import is the only action in the app that destroys the current
data as a side effect of wanting something else — "Clear all alarms" at least destroys the thing
the user was looking at and confirmed about. The copy is best effort and must never throw or block
the import, matching `PersistenceService.Quarantine()`.

**Step 5 — applying.** Restoring the alarms mirrors `MainViewModel.ClearAllAlarms()` exactly,
because it is the same operation with an arming pass on the end:

```
CommitPendingDelete();                        // settle any outstanding undo first
ClosePopupsRequested?.Invoke(...);             // an open card's Snooze would re-arm into the old set
foreach item in _scheduler.Alarms + _scheduler.Running:   // snapshots, not the live lists
    _scheduler.Cancel(item);
Running.Clear(); Alarms.Clear(); MissedNote = null;
arm every record from the imported document;
RebuildAgenda();
AlarmsChanged?.Invoke(...);                    // App's SaveData persists the new state at once
Announce("Imported N alarms");                 // screen-reader confirmation
```

The three load-bearing rules from the clear-data slice carry over unchanged: walk the **scheduler**
and not the derived view collections (`SaveData` persists from the scheduler, so anything the
agenda has not caught up with would survive and be written straight back); disarm before emptying,
so nothing fires from the 250 ms tick in between; close open popups first.

**Restore everything** additionally applies the settings, in this order:

1. `DefaultSound` → `_settings` and `_onDefaultSoundChanged(...)`, so new timers pick it up.
2. `LaunchAtStartup` → through `IStartupService.Enable()`/`Disable()`, never by writing the field
   alone. A checkbox that disagrees with the HKCU Run key is exactly the class of bug PR #16 fixed.
3. `SelectedTab` → applied to the live view, or the change is invisible until the next launch.
   `AppSettings.Sanitized()` already clamps it to `0..TabCount`, so no extra bounds check is needed.
4. Window placement → applied to the **live window** through a `MainWindow` call, mirroring the
   `resetWindowPlacement` callback already wired into `SettingsViewModel`, and through the same
   off-screen guard used at launch so a file exported on a three-monitor setup cannot park the
   window where it cannot be reached. Writing `_settings` alone is not enough: `OnClosing` writes
   the *current* placement back on every close, so the restored coordinates would silently revert
   the next time the window shuts. This is the trap `ResetSettings` already had to solve.

Then **refresh the Settings draft** (`LaunchAtStartup`, `DefaultSound` on the view model). Without
this a later Save writes the pre-import values straight back — the clear-data lesson.

Both commands act immediately and sit outside the Save/Cancel draft, under the section's existing
"These take effect immediately. Cancel will not undo them." caption.

### 4. Dialog boundary — `IFileDialogService`

```csharp
public interface IFileDialogService
{
    string? AskSavePath(string suggestedFileName);
    string? AskOpenPath();
}
```

`FileDialogService` wraps `Microsoft.Win32.SaveFileDialog`/`OpenFileDialog` with filter
`Tidsro backup (*.json)|*.json|All files (*.*)|*.*`, `DefaultExt = ".json"`, and the Documents
folder as the initial directory. A fake in the test project returns canned paths, so the
view-model tests never open a real dialog — the same reason `IStartupService` exists.

The error surface is a themed message dialog (`ConfirmDialog` with a single OK, or `ChoiceDialog`
with one button) reached through the same `Func` indirection the existing `confirm` callback uses,
so the view model stays free of `System.Windows`.

### 5. Placement — `SettingsWindow.xaml`

Both buttons join the existing "Data" section, above the two destructive ones, in the established
`QuietAction` / `MinWidth="150"` / `HorizontalAlignment="Left"` shape:

```
Data
These take effect immediately. Cancel will not undo them.
  [ Export data…      ]
  [ Import data…      ]
  [ Clear all alarms  ]
  [ Reset all settings]
```

Reading order is deliberate: the two recoverable actions come before the two destructive ones.
Each button carries an `AutomationProperties.Name` matching its visible text.

## Testing

**`SettingsViewModel`** (fake dialog service, fake startup service, callbacks recorded):
- Export with a cancelled dialog writes nothing.
- Export passes the suggested `tidsro-backup-<date>.json` name and writes to the chosen path.
- Export reports success with the file name; a failed write reports the error instead.
- Import with a cancelled open dialog changes nothing.
- Import of an unreadable file shows the error and changes nothing.
- **Import of a valid JSON file that is not a Tidsro document** (`{"foo":1}`, a `package.json`)
  takes the error path and never reaches the choice dialog. This is the data-loss guard; it gets an
  explicit test.
- Import of a file above the size ceiling takes the error path without reading it.
- Import cancelled at the choice dialog changes nothing — and, critically, has **not** written the
  pre-import copy, because step 4 comes after step 3.
- Alarms-only leaves `LaunchAtStartup`, `DefaultSound` and placement untouched, and never calls
  `IStartupService`.
- Everything applies the settings and calls `Enable`/`Disable` to match, and refreshes the draft so
  a following `Save()` does not undo it.

**`MainViewModel`**: replacing the alarms walks the scheduler rather than the view collections,
commits a pending delete, raises `ClosePopupsRequested` before arming, and raises `AlarmsChanged`
once at the end.

**`PersistenceService`**: a round trip through export and import returns an equivalent document,
including a file at an older schema version; `WriteTo` does not delete a `<path>.corrupt` neighbour
and leaves no `.tmp` behind when the write fails.

**Manual pass** (only Malin can run it): real dialogs, a real export to Documents, a re-import of
both kinds, an import of a deliberately corrupted file, an import of a valid non-Tidsro JSON file,
an import while an alarm fires, a keyboard-only pass over `ChoiceDialog` (Tab order, Esc, focus
return), and a UIA name check on the four Data buttons. Back up the live `%AppData%\Tidsro\data.json`
and the HKCU Run value first, and close Tidsro gracefully rather than force-killing it — a
force-kill discards unsaved in-memory edits.

## Risks and edge cases

- **A running countdown is not in the file.** Countdowns are ephemeral and `SaveData` never writes
  them. Import cancels them along with everything else; that is correct, and the announcement says
  how many alarms were restored, not how many were cancelled.
- **Importing while a completion popup is open.** Handled by the `ClosePopupsRequested` step; the
  same hazard the clear-data slice found.
- **An export is plaintext**, alarm labels included. Anything on the machine can read one, and the
  suggested destination deserves naming: on a default Windows 11 setup Documents is redirected into
  **OneDrive**, so exporting there quietly uploads the whole schedule to Microsoft. For an app whose
  README says "no accounts, no network — your data stays on your machine", that is worth stating
  outright. Documented rather than solved in code — the user picks the path, and encrypting a file
  they must be able to restore without a password would be theatre.
- **Two imports in a row destroy the original pre-import copy.** The snapshot is a single slot, so the
  second import overwrites the first import's copy and the "undo" then describes the first import
  rather than the user's own data. Found in the manual pass on 2026-08-24, where it was easy to hit on
  the first try. Accepted rather than fixed: the alternative is the rolling rotation rejected above.
  The choice dialog and the README now say the copy is replaced by each import, so the user is told at
  the moment they can still act on it. Revisit only if it bites in real use.
- **An alarm can fire while the import dialogs are open.** The 250 ms tick keeps running through the
  Open dialog and the choice dialog, so an alarm can raise a completion popup over the modal, add a
  missed note, and trigger a `SaveData` that the import then wipes. Accepted: nothing is lost that
  the user did not ask to replace, and suppressing the tick for a dialog's lifetime would delay real
  alarms — a worse trade for an alarm clock. The pre-import copy is taken after that save, so it
  captures post-fire state; that is correct, since it is the state being replaced.
- **A file exported on a future schema.** `SchemaVersion` above 4 still deserializes on a
  best-effort basis and sanitizes; unknown fields are ignored by `System.Text.Json`. Acceptable —
  the alternative is refusing to import a file a newer Tidsro wrote.
- **The destination is a removable drive that vanishes mid-write.** The temp-then-replace write
  fails cleanly and surfaces through the error dialog.

## Documentation

- README: remove "Cloud sync / backup" from the Roadmap, add backup and restore to the feature
  list, note that an export is an unencrypted file **and that Documents is often OneDrive-synced**,
  and document recovery from `%AppData%\Tidsro\data-before-import.json` next to the import
  instructions.
- CHANGELOG: a `## [Unreleased]` entry describing export and import.

## Rejected alternatives

**Rolling automatic backups** (`backups/data-<date>.json`, keep the last five). Rejected by Malin
on 2026-08-24: a directory quietly filling with files the user never asked for is clutter, and it
carries a clock dependency and a prune rule to maintain. Two facts made it easy to drop — corrupted
files are already quarantined to `data.json.corrupt` rather than lost, and the single
`data-before-import.json` copy covers the one destructive path that has no confirmation of its own.
Reconsider only if a real loss happens that an export would not have prevented.

**Choosing what to export rather than what to import.** Rejected: a file exported as alarms-only can
never serve a full restore, so the decision would be locked in at the wrong moment. Export always
writes everything; the choice lives where the consequences land.

**Merging an imported file into the existing schedule.** Rejected: merge needs a conflict rule for
two alarms at the same time with different labels, and no rule is obviously right. Replace is
predictable, and the pre-import copy makes it reversible.

**Reporting failures through the tray balloon.** Rejected: balloons are invisible on this machine,
and a backup that silently failed is worse than no backup.

**Our own "overwrite this file?" confirmation on export.** Rejected: the Windows Save dialog already
prompts, and a second one trains the user to click through both.

**Suppressing the scheduler tick while an import dialog is open.** Rejected: it would delay a real
alarm to tidy up a cosmetic overlap. See *Risks and edge cases*.
