# Tidsro

A calm, dark-mode-first desktop timer and alarm for Windows — countdown timers, clock-time alarms, and recurring alarms that nudge you with a quiet corner card instead of a flashy notification.

> **Tidsro** is Norwegian: *tid* (time) + *ro* (calm / peace) — roughly *"calm time."* The name is the whole idea: a timer that's visible when you need it and invisible when you don't.

## Status

In development, built in slices. **Slice 1 (countdowns) is implemented and merged**, with polish on top: Settings apply on **Save**, the running-timer row has clearer **pause/resume** plus a **Reset**, and each timer can use its own **sound**, chosen with a preview at setup. The full design lives in [`docs/superpowers/specs/2026-06-03-tidsro-design.md`](docs/superpowers/specs/2026-06-03-tidsro-design.md).

## Stack

C# · WPF (.NET) · MVVM. Local-first: no accounts, no network — your data stays on your machine.

## Slice 1 — Countdowns

Countdown timers are implemented and working.

**Run:**

```
dotnet run --project src/Tidsro
```

Or build and launch the resulting `Tidsro.exe` directly. The app starts in the system tray — no window opens on launch.

**Basic use:**

- Left-click the tray icon to open the main window.
- Pick a preset (15 / 30 / 60 min) or type a custom duration: `25` (minutes), `5:00` (mm:ss), or `1:30:00` (h:mm:ss). Invalid input shows a calm inline message.
- Choose a **sound** for the next timer from the dropdown — **▶** previews it. It starts from your default sound and applies to both presets and custom timers.
- Multiple countdowns can run at once; each shows a live mm:ss (or h:mm:ss) countdown with **pause/resume, reset** (back to the full duration), and cancel. Paused timers dim; resetting while paused keeps the timer stopped at the start.
- When a timer finishes, a calm card appears in the bottom-right corner. It does not steal focus.
  - **+5** arms a new 5-minute countdown. **Restart** re-runs the original duration. **Dismiss** closes the card.
  - Press **Ctrl+Alt+T** to bring the latest card into keyboard focus; Tab reaches the buttons; Enter activates; focus returns to your previous app on dismiss.
  - Multiple finished cards stack upward and dismiss independently.
- Open **Settings** (bottom-left of the main window) to toggle launch-at-startup and choose a default sound. Changes apply when you click **Save**; **Cancel**, **Esc**, or closing the window discards them.

> Note: tray icon and sound files are placeholder assets in this slice.

## Planned (v1)

- Clock-time alarms and recurring (weekday) alarms
- An optional label per timer
- Cloud sync / backup (future slice)
