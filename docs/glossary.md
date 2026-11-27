# Glossary

The words Tidsro uses for its own parts. Use the term in the first column in UI text, docs, issues and new code. The "Also called" column lists older names that still exist in the code or the UI. They mean the same thing, and I rename them only when that code is touched anyway.

Decisions behind the design live in [`adr/`](adr/).

## Timers

| Term | Meaning | Also called | In code |
|------|---------|-------------|---------|
| **Timer** | Anything Tidsro counts toward: a countdown or an alarm. | In the README and on buttons ("Start timer"), often a countdown only. | `TimerItem` (all three trigger types) |
| **Countdown** | A timer that runs a duration. It can be paused, resumed and reset. | Quick timer (the tab name) | `TriggerType.Countdown` |
| **Alarm** | A timer that fires once at a clock time (HH:MM). | Clock-time alarm | `TriggerType.ClockTime`, `AlarmRecord` |
| **Recurring alarm** | An alarm whose Repeat is Daily, Weekdays, Weekends or custom days. It moves forward to its next occurrence each time it fires. | | `TriggerType.Recurring`, `RecurringAlarmRecord`, `RepeatOption` |
| **Repeat** | The choice that turns an alarm into a recurring one: Once, Daily, Weekdays, Weekends or Custom. | | `RepeatOption`, `Weekdays` |
| **Fire** | The moment a timer reaches its time. An alarm missed by sleep or a relaunch still fires within a 5-minute grace, and expires quietly after it. | | `TimerState.Fired`, `SchedulerService.Fired` |
| **Off** | An alarm that is kept but switched off. It does not fire and sits muted at the bottom of the Schedule. Switching it back on moves a recurring alarm to its next occurrence, so it never fires the instant it returns. | Disabled | `TimerItem.IsEnabled`, persisted as `Enabled` |
| **Skip** | Passes over the next occurrence of a recurring alarm that is on. Each press skips one more, up to 100. The row says "Skipped, next Mon 30.11" until the skipped time passes, and Undo skip takes back the latest. An edit keeps the skipped days, and the Edit dialog warns before a change brings one back. The Week tab still draws a block skipped today but does not light it as the current one. | Skip next | `SchedulerService.SkipNext`, `UndoSkip`, `SkippedCount` |
| **End time** | An optional end for an alarm. Tidsro chimes at the start only (see the Roadmap in the README). | | `TimerItem.EndMinute` |

## Cards

| Term | Meaning | Also called | In code |
|------|---------|-------------|---------|
| **Card** | The quiet box in the bottom-right corner when a timer fires. It never steals focus, and several cards stack upward. | Alert (tray menu: "Focus latest alert"), popup | `CompletionPopup`, `PopupViewModel` |
| **Heads-up** | A close-only card 5 minutes before an alarm that has "Warn me 5 minutes before" on. It closes itself when the alarm fires, or when that occurrence will no longer ring (skipped, switched off, deleted or moved by an edit). | Pre-alarm warning (README), warning | `WarnBefore`, `SchedulerService.Warning`, `WarningSent` |
| **+5** | The card button that arms a new 5-minute countdown. | Snooze (code only; the UI never says it) | `ShowSnooze` |
| **Restart** | The card button that runs a finished countdown again at its original duration. | | `OriginalDuration` |
| **Dismiss** | Closes a card. | | |

## Tabs

| Term | Meaning | Also called | In code |
|------|---------|-------------|---------|
| **Quick timers** | The tab for countdowns: presets and custom durations. | | `TimerItemViewModel` |
| **Schedule** | The tab that lists every alarm, sorted by next occurrence. | | `AlarmItemViewModel` |
| **Week** | The tab that draws alarms as a timetable for the week. | Timetable (code) | `TimetableViewModel`, `TimetableLayout` |
| **Running strip** | The compact strip below the tab content that shows whatever is counting down, whichever tab is open. | | |

## Week tab layout

| Term | Meaning | In code |
|------|---------|---------|
| **Row** | One occupied start time. Empty hours get no row (see [ADR 0006](adr/0006-week-rows-are-occupied-times.md)). | `TimetableRow` |
| **Gutter** | The time printed at the start of a row, when every entry in it starts at the same time. | `TimetableRow.GutterLabel` |
| **Instant** | An alarm without an end time, drawn as a point in its row. | `SegmentRole.Instant` |
| **Block** | An alarm with an end time, drawn at its real length, one segment per row it covers. | `SegmentRole` |
| **Lane** | A side-by-side column inside a day when blocks overlap. | `TimetableLayout.MinimumLaneWidth` |

## Data

| Term | Meaning | In code |
|------|---------|---------|
| **Data file** | `data.json` in `%AppData%\Tidsro`: settings and alarms, written whenever the alarms change and on exit (see [ADR 0001](adr/0001-one-json-data-file.md)). | `PersistenceService`, `TidsroData` |
| **Schema version** | The number inside the data file that tells Tidsro which format it is reading. | `TidsroData.CurrentSchema` |
| **Export / import** | A file with alarms and settings that can be moved to another machine. Import asks "Restore everything" or "Restore alarms only". | `DataTransferService`, `ImportChoice` |
| **Clear data** | The Settings action that removes all alarms ("Clear all alarms"). | |
| **My sound** | The one custom sound slot (a .wav, or an .mp3 decoded to .wav when it is chosen), next to the six built-in chimes (see [ADR 0003](adr/0003-my-sound-is-a-copy.md) and [ADR 0007](adr/0007-mp3-is-decoded-when-chosen.md)). | `CustomSoundStore`, `SoundChoice` |
