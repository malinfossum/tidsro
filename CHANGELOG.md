# Changelog

All notable changes to Tidsro are documented here. Dates are ISO 8601.

## [2.5.0] — 2026-09-04

### Added
- **The Week tab offers to make room for the grid** — on a window too narrow for the timetable the
  tab shows the day-by-day agenda instead, and nothing said the grid was a window-width away. A
  quiet line under the agenda now offers to widen the window, and one click does it. Only on a
  click: a window that resized itself would override a width you chose on purpose. The line stays
  away when widening would not help — when the window is already wide enough, or the screen itself
  is too narrow to hold the grid.

### Fixed
- **The block marked as happening now is the one that is** — where a block ended exactly where the
  next began, the Week grid went on marking the one that had finished and left the one that had
  started unmarked, for up to half an hour. Each block is now judged on its own start and end.

## [2.4.2] — 2026-09-04

### Fixed
- **An alarm off the half hour says so again** — in the Week grid, a repeating alarm at 12:15 sitting
  in a row of 12:00s was drawn without its own time, so it read as though it started at 12:00. Only a
  week where one row holds two different start times was affected. Where a day is split between two
  overlapping blocks the column is too narrow for a time beside a label, so there the time now sits
  above it.

## [2.4.1] — 2026-09-03

### Fixed
- **Overlapping blocks keep their labels** — when two blocks share a day, the Week grid splits that
  column so neither hides the other, and in a window only just wide enough the two halves were left
  about seventy pixels each: *Focus block* came out as *Focus blo...*. The grid now asks for enough
  width to keep every label readable and shows the day-by-day agenda when the window cannot give it,
  which lists the same alarms in full at any size. A week with no overlapping blocks is unchanged.

## [2.4.0] — 2026-09-03

### Added
- **Alarms can say when they end** — an optional *Ends* time on any repeating alarm turns it into a
  block, and the Week tab draws it at its real length instead of as a single point. A lecture from
  10:00 to 11:30 now looks like a lecture. Two blocks that overlap sit side by side rather than
  hiding each other, and an alarm inside a block — a stretch break in the middle of a focus block —
  keeps its own place. The block happening right now is marked. Both tabs read the same:
  `09:00–10:30` on the Week tab and on the Schedule.
- Nothing new makes a sound. An end is a length, not a second alarm: Tidsro still chimes only at the
  start, exactly as before.

### Changed
- Saved data moves to schema 5. Older files load unchanged — an alarm with no end is simply an alarm
  with no end, which is what all of them were — and a v5 backup restored into an older Tidsro keeps
  every alarm and drops the ends.

## [2.3.0] — 2026-09-03

### Added
- **Week tab** — a read-only timetable of your repeating alarms. Narrow windows show a day-by-day
  agenda; widen the window and it becomes a grid. It lists only the times you have something on, so
  a free afternoon costs no space, and every row states its own time — an alarm at 12:15 says 12:15
  rather than the half hour it falls in. Saturday and Sunday get columns only when something falls
  on them, or when today is one of them; otherwise the week is five columns wide and says beneath
  that the weekend is free. Today is marked in both views.

## [2.2.0] — 2026-08-26

### Added
- **Tidsro tells you when a save fails** — if your alarms cannot be written to disk, or an error is caught before it can close the app, you now get a dialog instead of a tray balloon that Windows may never have been allowed to show. A repeating failure cannot stack dialogs on top of each other: a failed save is announced once per outage and only speaks up again after a save has succeeded, and a caught error once per run.
- **The warning at quit says what is actually at stake** — a save that fails as Tidsro closes loses the changes rather than merely risking them, so it gets its own wording.

### Fixed
- **"Focus latest alert" is no longer a dead menu item** — the tray menu entry stayed available with no alert on screen and then quietly did nothing. It is now greyed out when there is nothing to focus, which a screen reader announces as unavailable.

## [2.1.0] — 2026-08-24

### Added
- **Back up your alarms and restore them** — a new *Export data…* in **Settings ▸ Data** writes everything, alarms and settings alike, to a JSON file wherever you choose. *Import data…* reads one back and asks first whether to restore only the alarms or everything, so a file from another machine can't quietly move your window or flip launch-at-startup.
- **A way back from a mistaken import** — before an import replaces anything, your current data is copied to `%AppData%\Tidsro\data-before-import.json`. Import that file to undo. There is only one such copy and each import replaces it, so restore it before importing anything else.

## [2.0.0] — 2026-08-18

### Added
- **Tabs** — the window is split into **Quick timers** and **Schedule** instead of one long scrolling page, so the half you are not using stays out of the way. A slim strip along the bottom keeps any running timer visible from the Schedule tab, and Tidsro reopens on the tab you left it on.
- **A countdown worth looking at** — the running timer is lifted out of the list into a card of its own, with the finish time beside it. The next alarm gets the same treatment on the Schedule tab.
- **Its own typeface** — IBM Plex Sans and IBM Plex Mono ship inside the app, so Tidsro looks the same on every machine. Times are monospaced, which stops the digits shuffling sideways as a timer counts down.

### Changed
- **A new look** — near-black surfaces with a single brass accent, drawn from the app's own icon. Cards are separated by shadow and a lit top edge rather than by outlines, text fields read as recessed into the card they sit on, and the scrollbar is no longer the stock Windows one.
- **The window has a measure** — widen Tidsro and the content takes a steady share of the window, centred, instead of stretching a dropdown that says "Once" across the whole screen or freezing at one width while the page grows around it.
- **Deleting is easier to aim at** — the delete button turns red as you point at it, and only then.

## [1.7.0] — 2026-08-11

### Added
- **Clear your data from Settings** — a new **Data** section with *Clear all alarms* and *Reset settings*. Each asks first, in a dialog that matches the rest of the app, and the two are independent: clearing alarms leaves your settings alone, and resetting settings leaves your alarms alone. Reset also turns off launch-at-startup and forgets the saved window position.
- **Uninstall asks about your data** — the uninstaller now offers to delete alarms and settings as well. *No* is the default, so a reinstall picks up where you left off unless you choose otherwise.

### Fixed
- **Autostart is no longer hijacked by a stray copy** — running Tidsro from a build folder or a portable copy used to silently repoint launch-at-startup at that copy, so deleting or moving it broke startup at the next boot. Only the installed copy may claim startup now, and a healthy registration is left alone; moving or reinstalling still repairs itself.

## [1.6.0] — 2026-07-13

### Changed
- **New brand identity** — a custom pine-in-hourglass mark, drawn from the name *tid* + *ro* (*calm time*), replaces the generic clock icon and now appears in the window title bar, taskbar, system tray, and the Windows installer.

## [1.5.0] — 2026-07-07

### Added
- **Crash logging** — unhandled errors are now caught and written to a rotating crash log, with a notification pointing to the log folder, instead of the app closing silently. Logging is hardened so it never brings the app down itself.
- **Timer finish time** — a running countdown now shows the wall-clock time it will finish (e.g. *done 21:20*) beside the remaining time; hidden while paused. Contributed by Henry.

## [1.4.0] — 2026-06-23

### Added
- Per-alarm on/off toggle in the Schedule. Switch an alarm off to keep it without it firing or
  warning — useful for silencing recurring alarms over a break — and back on when you need it.
  Disabled alarms are kept across restarts and parked, muted, at the bottom of the list.

## [1.3.2] — 2026-06-22

### Changed
- **Quick timers stack by time** — running Quick timers now sort soonest-first, matching the Schedule, instead of the order they were added. Paused timers move below the active ones.

## [1.3.1] — 2026-06-19

### Fixed
- **Piano jingle and Electric piano jingle played the same sound** — the audio lookup matched the wrong embedded file, so both used the electric piano clip. Each sound now plays its own clip.

## [1.3.0] — 2026-06-19

### Added
- **Recurring alarms** — repeat an alarm on a weekday set (Daily, Weekdays, Weekends, or custom days), shown in one **Schedule** sorted by next occurrence.
- **5-minute pre-alarm warning** — an optional per-alarm heads-up that appears five minutes before the alarm, using the alarm's sound.
- **More sounds** — Piano jingle, Electric piano jingle, and Bell jingle (contributed by Henry), alongside the original chimes.

### Changed
- **Snooze keeps alarms in the Schedule** — pressing **+5** on an alarm re-arms it five minutes later in the Schedule; countdown timers still snooze as Quick timers.
- **Refreshed Schedule editor** — toggle switches for the warn-before and launch-at-startup options, gold day-chips for custom days, aligned rows, and a gold highlight on the next Quick timer.

## [1.2.0] — 2026-06-18
Design and interaction polish: a responsive layout, the gold accent carried into the UI, a modal edit-alarm dialog, an undo bar for timers and alarms, and snappier timers.

## [1.1.0] — 2026-06-17
Clock-time alarms — a "Your day" agenda with one-shot fire-at-HH:MM alarms, optional labels and per-alarm sounds, inline editing, and a missed-while-away grace window.

## [1.0.0] — 2026-06-16
First release — countdown timers with presets or custom durations, pause/resume, reset, and per-timer sounds.

[2.5.0]: https://github.com/malinfossum/tidsro/releases/tag/v2.5.0
[2.4.2]: https://github.com/malinfossum/tidsro/releases/tag/v2.4.2
[2.4.1]: https://github.com/malinfossum/tidsro/releases/tag/v2.4.1
[2.4.0]: https://github.com/malinfossum/tidsro/releases/tag/v2.4.0
[2.3.0]: https://github.com/malinfossum/tidsro/releases/tag/v2.3.0
[2.2.0]: https://github.com/malinfossum/tidsro/releases/tag/v2.2.0
[2.1.0]: https://github.com/malinfossum/tidsro/releases/tag/v2.1.0
[2.0.0]: https://github.com/malinfossum/tidsro/releases/tag/v2.0.0
[1.7.0]: https://github.com/malinfossum/tidsro/releases/tag/v1.7.0
[1.6.0]: https://github.com/malinfossum/tidsro/releases/tag/v1.6.0
[1.5.0]: https://github.com/malinfossum/tidsro/releases/tag/v1.5.0
[1.4.0]: https://github.com/malinfossum/tidsro/releases/tag/v1.4.0
[1.3.2]: https://github.com/malinfossum/tidsro/releases/tag/v1.3.2
[1.3.1]: https://github.com/malinfossum/tidsro/releases/tag/v1.3.1
[1.3.0]: https://github.com/malinfossum/tidsro/releases/tag/v1.3.0
[1.2.0]: https://github.com/malinfossum/tidsro/releases/tag/v1.2.0
[1.1.0]: https://github.com/malinfossum/tidsro/releases/tag/v1.1.0
[1.0.0]: https://github.com/malinfossum/tidsro/releases/tag/v1.0.0
