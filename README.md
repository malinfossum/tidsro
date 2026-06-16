# Tidsro

A calm, dark-mode-first desktop timer and alarm for Windows — countdown timers, clock-time alarms, and recurring alarms that nudge you with a quiet corner card instead of a flashy notification.

> **Tidsro** is Norwegian: *tid* (time) + *ro* (calm / peace) — roughly *"calm time."* The name is the whole idea: a timer that's visible when you need it and invisible when you don't.

## Status

In development, built in slices. **Slice 1 (countdowns) is implemented and merged**, with polish on top: Settings apply on **Save**, the running-timer row has clearer **pause/resume** plus a **Reset**, and each timer can use its own **sound**, chosen with a preview at setup. The full design lives in [`docs/superpowers/specs/2026-06-03-tidsro-design.md`](docs/superpowers/specs/2026-06-03-tidsro-design.md).

## Install

**Most people — install it:**

1. Open the [Releases page](https://github.com/malinfossum/tidsro/releases) and download **`Tidsro-Setup.exe`** from the latest release.
2. Run it. Windows may warn *"Windows protected your PC"* because the app isn't code-signed yet — click **More info → Run anyway**.
3. Click through the short wizard. Tidsro installs just for you (no admin), adds a Start Menu shortcut, and starts in the system tray.

Uninstall any time from **Settings → Apps → Installed apps → Tidsro**.

**Prefer not to install?** Download **`Tidsro.exe`** (the portable build) from the same release and double-click it — it runs as-is, no installation. The same SmartScreen note applies.

Both builds are self-contained: they run on any 64-bit Windows PC with no .NET required. Your timers and settings stay on your machine in `%AppData%\Tidsro`.

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

## Building a release (developers)

`publish.ps1` builds both downloads into `dist/`:

```
./publish.ps1
```

It publishes a self-contained, single-file `Tidsro.exe` (portable) and wraps it in `Tidsro-Setup.exe` (a per-user installer) with [Inno Setup](https://jrsoftware.org/isinfo.php) — install that once via `winget install --id JRSoftware.InnoSetup -e`. Attach both `.exe` files to a [GitHub Release](https://github.com/malinfossum/tidsro/releases).

## License

Apache License 2.0 — see [LICENSE](LICENSE). © 2026 Malin Fossum.
