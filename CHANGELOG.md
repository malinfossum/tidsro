# Changelog

All notable changes to Tidsro are documented here. Dates are ISO 8601.

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

[1.6.0]: https://github.com/malinfossum/tidsro/releases/tag/v1.6.0
[1.5.0]: https://github.com/malinfossum/tidsro/releases/tag/v1.5.0
[1.4.0]: https://github.com/malinfossum/tidsro/releases/tag/v1.4.0
[1.3.2]: https://github.com/malinfossum/tidsro/releases/tag/v1.3.2
[1.3.1]: https://github.com/malinfossum/tidsro/releases/tag/v1.3.1
[1.3.0]: https://github.com/malinfossum/tidsro/releases/tag/v1.3.0
[1.2.0]: https://github.com/malinfossum/tidsro/releases/tag/v1.2.0
[1.1.0]: https://github.com/malinfossum/tidsro/releases/tag/v1.1.0
[1.0.0]: https://github.com/malinfossum/tidsro/releases/tag/v1.0.0
