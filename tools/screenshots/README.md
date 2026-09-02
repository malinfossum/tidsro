# Screenshot rig

Re-shoots the README screenshots against a fictional fixture schedule.

```powershell
./tools/screenshots/Shoot-Screenshots.ps1
```

The README screenshots must never be taken against my own data. A week grid publishes what I do, on
which days, at which hours, in a public repo, and the Schedule and Edit-alarm shots leak the same
thing more thinly. This is a release-checklist item, and it was breached once already: the v2.3.0
`week.png` was my real timetable.

## What it does

It builds a throwaway copy of the app under `%TEMP%\tidsro-shoot` and patches two strings **in the
copy only** — the single-instance mutex name, and the `%AppData%\Tidsro` path in `PersistenceService`
and `LogService`. That copy reads a fixture schedule from its own scratch folder, so my installed
Tidsro can stay running throughout and never has its data or its launch-at-startup registry value
touched. Both are hashed before and after, and the run fails loudly if either moved.

Shoots `week.png`, `schedule.png` and `alarm-dialog.png`. `main-window.png` and `completion-card.png`
are still hand-shot — they need a live countdown and a fired card, which this doesn't drive yet.

Needs Windows PowerShell for `UIAutomationClient`; started from pwsh 7 it re-launches itself there.

## Things that cost me time

- **Persisted enums are integers.** A string enum name in the fixture makes `Load` throw, quarantine
  the file and open an empty app — which reads as "the fixture was ignored", not as a parse error.
- **A WPF modal opened through a UIA `Invoke()` does not appear under `RootElement`'s children.** The
  invoke returns immediately and the dialog is genuinely there; enumerate top-level windows with
  `EnumWindows` instead. Looking in the wrong place stacked four invisible dialogs before I noticed.
- **Capture with `PrintWindow(h, hdc, 2)`, not `CopyFromScreen`.** `CopyFromScreen` photographs the
  desktop, so it captures whatever is on top; `SetForegroundWindow` from a background process loses
  to the foreground lock, so "just raise it first" doesn't hold. Synthetic clicks are unsafe for the
  same reason — one of mine landed in a different application.
- **`PrintWindow` renders from the window origin.** Crop to the client rect for the shots that carry
  no title bar, and to `DWMWA_EXTENDED_FRAME_BOUNDS` for the ones that do, or the invisible resize
  border ends up in the frame.
- **Matching a UIA element on name alone** also matches the `TextBlock` inside a button, which has no
  `InvokePattern`. Always AND in a control type.
