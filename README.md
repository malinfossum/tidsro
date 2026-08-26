<p align="center">
  <img src="docs/brand/tidsro-lockup.png" alt="Tidsro" width="480">
</p>

# Tidsro

A calm, dark-mode-first desktop timer for Windows — countdown timers and clock-time alarms that nudge you with a quiet corner card instead of a flashy notification.

> **Tidsro** is Norwegian: *tid* (time) + *ro* (calm / peace) — roughly *"calm time."* The name is the whole idea: a timer that's visible when you need it and invisible when you don't.

<p align="center">
  <img src="docs/screenshots/main-window.png" alt="Tidsro on the Quick timers tab: the running countdown shown large in its own card (29:42, done 19:52), preset buttons for 5, 30, and 60 minutes, fields for a custom duration and label with a sound picker, and two more timers stacked below — near-black surfaces with a single gold accent." width="560">
</p>

## Who it's for

Anyone who works or studies at a computer and wants to hold their focus through the day without reaching for a phone or juggling several apps. Set your day — or your whole week — once, then forget it: Tidsro runs quietly in the background and keeps you on track, so your phone can stay on Do Not Disturb or in another room. It's built to *hold* your attention, not grab it — no flashy notifications, nothing loud unless you ask for it. Every alarm is yours to shape: silent or with a chime, one-off or repeating.

**Why I built it.** I went looking for a focus tool while studying and couldn't find one that did both timers *and* recurring alarms while staying clean and minimal — so I built the one I wish I'd had before I started school. It turns out to be just as useful in a workday as a study day.

## Status

**Shipped, with versioned [releases](https://github.com/malinfossum/tidsro/releases/latest).** Tidsro does **countdown timers** (presets or custom, with pause/resume, reset, an optional label, and a per-timer sound) and a **Schedule** of **clock-time and recurring alarms** — fire once at an HH:MM time, or repeat on a weekday set (Daily, Weekdays, Weekends, or custom days). Each alarm takes an optional label, a per-alarm sound, and an optional **5-minute pre-alarm warning**; the Schedule is sorted by next occurrence, alarms can be **switched off without deleting** (kept and parked at the bottom until switched back on), edited in a dialog, and deleted with an undo window, and firing survives sleep and app-relaunch within a 5-minute grace. Settings (launch-at-startup, default sound) apply on **Save**, and your alarms and settings can be **exported to a file and imported back**.

See the [changelog](CHANGELOG.md) for what's new in each release.

## Install

**Most people — install it:**

1. Open the [Releases page](https://github.com/malinfossum/tidsro/releases) and download **`Tidsro-Setup.exe`** from the latest release.
2. Run it. Windows may warn *"Windows protected your PC"* because the app isn't code-signed yet — click **More info → Run anyway**.
3. Click through the short wizard. Tidsro installs just for you (no admin), adds a Start Menu shortcut, and starts in the system tray.

Uninstall any time from **Settings → Apps → Installed apps → Tidsro**.

**Prefer not to install?** Download **`Tidsro.exe`** (the portable build) from the same release and double-click it — it runs as-is, no installation. The same SmartScreen note applies.

Both builds are self-contained: they run on any 64-bit Windows PC with no .NET required. Your timers and settings stay on your machine in `%AppData%\Tidsro`.

## Using Tidsro

Launching Tidsro opens its window — it remembers where you last placed it and how big it was. Closing the window tucks Tidsro back into the system tray, where it keeps running until you choose **Quit** from the tray menu; left-click the tray icon any time to reopen it. When Tidsro is started automatically with Windows, it stays quietly in the tray.

- Pick a preset (5 / 30 / 60 min) or type a custom duration: `25` (minutes), `5:00` (mm:ss), or `1:30:00` (h:mm:ss) — with an optional **label** to tell timers apart. Invalid input shows a calm inline message.
- Choose a **sound** for the next timer from the dropdown — **▶** previews it. It starts from your default sound and applies to both presets and custom timers.
- Multiple countdowns can run at once, stacked soonest-first; each shows a live mm:ss (or h:mm:ss) countdown with **pause/resume, reset** (back to the full duration), and cancel — cancelling drops a brief **Undo** bar at the bottom. Paused timers dim and drop below the active ones; resetting while paused keeps the timer stopped at the start.
- When a timer finishes, a calm card appears in the bottom-right corner. It does not steal focus.
  - **+5** arms a new 5-minute countdown. **Restart** re-runs the original duration. **Dismiss** closes the card.
  - Press **Ctrl+Alt+T** to bring the latest card into keyboard focus; Tab reaches the buttons; Enter activates; focus returns to your previous app on dismiss.
  - Multiple finished cards stack upward and dismiss independently.

<p align="center">
  <img src="docs/screenshots/completion-card.png" alt="A finished timer shown as a small dark card reading complete, Laundry done, with +5 min, Restart, and Dismiss buttons." width="320"><br>
  <em>A finished timer surfaces as a calm corner card — it never steals focus.</em>
</p>

The **Schedule** is its own tab next to Quick timers, and a compact strip below the tab content shows whatever is counting down, whichever tab you're on. Type a time — `14:30`, or shorthand like `9`, `930`, or `1430` (24-hour) — an optional label, choose a sound, set **Repeat** (Once, or a weekday set), and click **Add** (or press **Enter**). The alarm is saved immediately. Turn on **Warn me 5 minutes before** for a quiet heads-up ahead of the alarm.

<p align="center">
  <img src="docs/screenshots/schedule.png" alt="The Schedule tab: a form for adding an alarm (time, label, sound, repeat, and a pre-alarm warning toggle), three weekday alarms below with gold on/off toggles, and a slim strip along the bottom showing a timer still counting down on the other tab (Running 00:10, Laundry done)." width="560"><br>
  <em>The Schedule — and the strip along the bottom keeping a running timer in view.</em>
</p>

- A one-shot alarm fires once; a recurring alarm repeats on its days, and the Schedule stays sorted by what's next.
- If Tidsro isn't running when an alarm time passes, it fires within a 5-minute grace window on next launch.
- Each alarm row shows its time, cadence, label, and sound. Click **Edit** (pencil) to change it in a dialog; **Save** commits, **Cancel** discards.
- **Delete** removes the alarm with a brief undo window — click **Undo** in the bar at the bottom to restore it.
- **Switch an alarm off** with the toggle on its row to keep it without it firing or warning — handy for pausing recurring alarms over a holiday — then switch it back on when you need it. Off alarms dim and drop to the bottom of the Schedule, and stay off across restarts.
- When an alarm fires, the same quiet bottom-right card appears, with **Snooze +5** (re-arms it 5 minutes later in the Schedule) and **Dismiss**.

<p align="center">
  <img src="docs/screenshots/alarm-dialog.png" alt="The Edit alarm dialog: an 08:00 alarm labelled School start, with a sound picker on Piano jingle, repeat set to Weekdays, the 5-minute pre-alarm warning switched on with a gold toggle, and Save and Cancel buttons." width="344"><br>
  <em>Editing an alarm — per-alarm sound, repeat, and the optional 5-minute warning.</em>
</p>

- Open **Settings** (bottom-left of the main window) to toggle launch-at-startup and choose a default sound. Changes apply when you click **Save**; **Cancel**, **Esc**, or closing the window discards them.

The **Week** tab shows your repeating alarms laid out Monday to Sunday — a day-by-day agenda on narrow screens and a seven-column grid when wide (760px+), with the time scale fitted to the hours you actually use.

### Backup and restore

**Settings → Data → Export data…** writes everything — your alarms *and* your settings — to a JSON file wherever you choose. **Import data…** reads one back, and asks first whether to restore only the alarms or everything, so a file from another machine can't move your window or change your launch-at-startup setting unless you say so.

Before an import replaces anything, Tidsro copies your current data to `%AppData%\Tidsro\data-before-import.json`. If an import turns out to be the wrong file, import *that* file to get back where you were.

There is only ever one such copy, and **every import replaces it** — so if an import wasn't what you wanted, restore it before importing anything else. Two imports in a row and the copy describes the first import, not your original data. If you want a backup that stays put, export one.

An export is an ordinary, unencrypted JSON file — your alarm labels are readable by anything on the machine. Windows also redirects **Documents** into OneDrive on many installs, so saving there uploads a copy; pick a local folder if you would rather it stayed on the machine.

## Roadmap

- Weekly timetable view

## Stack

C# · WPF (.NET) · MVVM. Local-first: no accounts, no network — your data stays on your machine.

## Building from source

Run it directly:

```
dotnet run --project src/Tidsro
```

Build the distributable downloads into `dist/` with `publish.ps1`:

```
./publish.ps1
```

It publishes a self-contained, single-file `Tidsro.exe` (portable) and wraps it in `Tidsro-Setup.exe` (a per-user installer) with [Inno Setup](https://jrsoftware.org/isinfo.php) — install that once via `winget install --id JRSoftware.InnoSetup -e`. Attach both `.exe` files to a [GitHub Release](https://github.com/malinfossum/tidsro/releases).

## License

Apache License 2.0 — see [LICENSE](LICENSE). © 2026 Malin Fossum.

Tidsro bundles IBM Plex Sans and IBM Plex Mono, licensed under the SIL Open Font License 1.1.
See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
