# Verification rigs

A rig is a throwaway copy of Tidsro with its own single-instance mutex and its own data path. It
can run beside an installed Tidsro and never reads or writes `%AppData%\Tidsro`, so a check can
drive the real UI without risking my schedule.

Use one when the suite cannot see the thing being checked. The Week tab has shipped three defects
green: a forward `StaticResource` drew the whole grid blank with no exception, a cell-time ancestor
walk resolved to nothing, and a lane bar lit the wrong block. The suite proves the projection, not
the rendering.

## Scripts

| Script | What it does |
|---|---|
| `Build-Rig.ps1` | Copies `src/Tidsro` to a scratch folder, patches the mutex and data path, builds Release. `-Ref` builds any tag, so an old version can be run against a new file. |
| `Seed-Fixture.ps1` | Writes a fictional week (two blocks, two instants, one one-shot), starts the rig, optionally screenshots it, and returns the process. |
| `Click-Button.ps1` | Clicks buttons by accessible name through UI Automation. |
| `Front-Dialog.ps1` | Brings the rig's Win32 file dialog to the front. |
| `Type-IntoDialog.ps1` | Types a path into that dialog and presses Enter, refusing if the foreground window is not the rig. |
| `Verify-Import.ps1` | End to end: seed, clear, import, and assert both blocks kept their end times. This is the check that found the v2.5.1 bug. |

```powershell
./tools/rigs/Verify-Import.ps1                 # the working tree
./tools/rigs/Verify-Import.ps1 -Ref v2.5.0     # fails: the release the bug shipped in
```

## Traps, each of which cost real time

- **UI Automation needs Windows PowerShell.** `UIAutomationClient` does not load cleanly in pwsh 7.
  Call the UIA scripts with `powershell.exe`.
- **Ask for the accessible name, not the label.** The Settings dialog's save button is
  `Save settings`. Guessing `Save` finds nothing.
- **`FindFirst` on a name alone matches the `TextBlock` inside the button**, which has no
  `InvokePattern`. AND a `ControlType` into the condition.
- **Search from the root element, not from the app's window.** A WPF modal sits beside the window,
  not under it, so a descendants search of the window misses every button in the dialog.
- **The file dialog is a separate `#32770` window.** Foregrounding the app's main window steals
  focus back from it and swallows the Enter. Front the dialog.
- **A Save dialog will not reliably take a typed path** - the text can land in the file list as
  type-ahead and the pre-filled name is saved instead. Read the result off disk. An Open dialog
  does take a path.
- **`ShowDialog()` makes `Invoke()` block** for the modal's lifetime, so open a dialog from a
  separate process and drive it from the caller.
- **Enums persist as integers.** A string enum name in a fixture makes `Load` throw, quarantines the
  file and opens an empty app - which reads as though the fixture had been ignored.
- **A fixture keeps `LaunchAtStartup` false.** A dev build writes its own path into
  `HKCU\...\Run\Tidsro` whenever startup is switched on, including through an import's *Restore
  everything*. `Verify-Import.ps1` asserts the value is unchanged.
- **A "nothing found" result means check the rendering first.** A rig that lands in the agenda
  instead of the grid has no lane bars to find, and looks exactly like a pass.
