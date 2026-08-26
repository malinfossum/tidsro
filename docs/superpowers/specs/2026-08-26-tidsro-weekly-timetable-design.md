# Tidsro — weekly timetable view (design)

**Date:** 2026-08-26
**Status:** Approved — stress-tested 2026-08-26 (nine findings folded in), ready for plan
**Origin:** Tidsro holds a term's worth of recurring alarms, but the only way to see them is a flat
list on the Schedule tab. A student reading "Mon Wed Fri 09:00" three rows apart cannot see her week.
This slice adds the third tab the shell was built for (`AppSettings.cs:13` carries the note) and
renders the week she already has.

Committed to as a two-phase feature (2026-08-26). **This spec is phase 1 only.**

## Goal

A third tab, **Week**, showing the recurring alarms already in Tidsro laid out Monday to Sunday, so
the shape of the week is visible at a glance. Read-only. Nothing about firing, sound, or persistence
changes.

## Scope

**In scope**
- A `Week` tab beside Quick timers and Schedule; `AppSettings.TabCount` becomes 3.
- `TimetableLayout` — a pure function turning recurring alarms into a week structure.
- Two renderings of that structure: an agenda list (narrow) and a seven-column grid (wide), flipping
  at 760px.
- The current weekday marked as today.
- An empty state when no recurring alarms exist.

**Out of scope** (not now)
- **Timetable blocks with an end time** — the phase-2 feature, and the whole of schema 5. See
  *Phase 2, and what phase 1 owes it*.
- **Editing from this tab.** Adding, changing, and deleting alarms stays on the Schedule tab. The
  Week tab does not open the edit window, not even on double-click.
- One-shot alarms and countdowns. This is a repeating-pattern view; a dated alarm has no place in
  a week with no dates.
- Week navigation, dates, printing, export of the timetable.

## Design

### 1. No schema change, no scheduler change

Phase 1 reads what already exists. A recurring alarm is `RecurringAlarmRecord` — `Hour`, `Minute`,
`Days`, `Label`, `Sound`, `Enabled` — and a weekday pattern is exactly what a timetable draws.
`TidsroData.CurrentSchema` stays 4. `SchedulerService` is not touched. If this tab were deleted
tomorrow, nothing else in the app would notice.

This is the reason for building the view before the blocks: it is additive on every axis.

### 2. `Models/TimetableLayout.cs` — the pure function

A static class beside `RecurrenceRules`, `ClockTimeRules`, and `CountdownRules`, following the same
shape: no state, no I/O, no `DateTime.Now` — the current time arrives as a parameter.

```
public static TimetableWeek Build(IEnumerable<TimerItem> alarms, DateTimeOffset now)
```

Returns:

- `Slots` — the vertical axis, a list of 30-minute slots from `SpanStart` to `SpanEnd`.
- `Days` — seven `TimetableDay` values (Mon…Sun), each with `IsToday` and its entries.
- Each `TimetableEntry` — the alarm's id, label, time text, sound, `IsEnabled`, and `SlotIndex`.
- `IsEmpty` — true when no recurring alarm survived the filter.

**Which alarms.** Only those with `RecurringDays` set. A disabled alarm is *included but flagged*
`IsEnabled = false`, matching the existing treatment where off alarms park muted rather than vanish —
a class you have switched off is still a class you want to see in your week.

**`Build` is total — it never throws.** `data.json` is user-writable and, since v2.1.0, importable
from any file the user picks. `Sanitized()` guards the load path, but this function must not assume
it ran: an entry with no day bits left after masking against `RecurrenceRules.AllDays`, or an hour or
minute out of range, is **skipped silently** and the rest of the week still renders. This is a
deliberate departure from `RecurrenceRules.NextOccurrence`, which throws on an empty day set — that
behaviour is right for arming an alarm and wrong for drawing one, where a single bad row would
otherwise reach the global exception handler and read as "Tidsro is broken".

**Resolving the span.** Floor the earliest alarm to its half-hour and subtract one hour; ceil the
latest and add one. If the result is under six hours, grow it symmetrically to six so a single 09:00
alarm renders as a calm band and not a sliver. *Then* clamp to 00:00–24:00, and if clamping one end
shortened the span, give the length back at the other — an 06:00 alarm still gets its six hours, they
just all fall after midnight rather than straddling it. With no alarms, `IsEmpty` is true and the span
is not meaningful.

**Slots, not pixels.** The span is divided into fixed 30-minute slots and each entry is assigned a
`SlotIndex`. Nothing in the layout speaks in pixels, so the whole thing is testable without a window,
and the View becomes a uniform-row grid rather than a canvas with arithmetic in converters.

Two alarms inside the same half hour on the same day share a slot and stack in that cell. This is
accepted, not worked around: an 09:00 and an 09:15 alarm are genuinely the same moment at the scale
this view draws.

**Order inside a slot is defined, not incidental:** minute, then label by ordinal comparison, then
id. Left to collection order the same timetable would redraw differently after an import or a
delete-and-re-add.

### 3. `ViewModels/TimetableViewModel.cs` — thin

Holds the current `TimetableWeek` and rebuilds it. It subscribes to `MainViewModel.AlarmsChanged`
(the event already raised whenever the alarm set changes) and re-projects.

**Day rollover is a cached comparison, never a per-tick rebuild.** The view model keeps the
`DateOnly` it last built for. On each scheduler tick it compares that to today's date and re-projects
**only when the two differ**. Rebuilding on every tick would be 86,400 full week projections a day,
each allocating, in an app whose whole point is to sit quietly in the tray. The comparison also
survives the machine sleeping through midnight for free, because it measures a date rather than
elapsed time.

**Ownership:** `MainViewModel` constructs it once and holds it for the application's lifetime. There
is no unsubscribe path because there is no teardown before exit — and specifically, it is *not*
created per tab activation, which would leak a subscription per visit.

No commands. Read-only means read-only.

### 4. Two renderings, one structure

Both panels live in the existing `Panels` grid beside the Quick timers and Schedule panels, and both
bind to the same `TimetableWeek`. Only one is visible at a time.

- **Agenda (narrow, <760px)** — day headings with their entries beneath, in time order. This is the
  rendering that works at the 380px minimum, and it is also the better one for a screen reader.
- **Grid (wide, ≥760px)** — a time gutter and seven day columns, uniform rows from `Slots`. The
  gutter labels every slot (09:00, 09:30, 10:00…); past a twelve-hour span it labels only the whole
  hours, so the rows stay while the text thins.

**The flip** is a new `WidthToVisibleConverter` in `Views/Converters.cs`, taking the threshold and
the wanted side as its parameter, bound to `Panels.ActualWidth`. This is deliberately the same
mechanism as the `IndexToVisibleConverter` that already swaps tab panels and the
`WidthToMeasureConverter` that already reads that same `ActualWidth` — no new responsive machinery,
no code-behind rebuild. The hidden rendering is `Collapsed`, so it leaves the automation tree too.

**Today** is marked with both a brass accent and a dot glyph, never colour alone (WCAG 1.4.1), and
the word "today" is in the day's accessible name.

**Entry rendering.** Labels are normalised to 200 characters on load and a wide-grid column is
roughly 90px, so the grid trims to a single line with `TextTrimming="CharacterEllipsis"` and the
agenda wraps to at most two lines. The full label survives in the accessible name and in a tooltip;
it is never only visible in a place a screen reader cannot reach.

A disabled entry uses `TextMuted` and **must still clear 4.5:1** against its cell background — the
palette's `TextFaint` is deliberately below body contrast and is not permitted here. "Off" is carried
by the accessible name and a glyph as well; dimming alone never encodes state.

**Scrolling is keyboard-reachable.** The tab is read-only and therefore contains nothing focusable by
default, which would leave a keyboard-only user able to open a 48-slot grid and see only its top.
Both panels' `ScrollViewer`s take `Focusable="True"` and an `AutomationProperties.Name` of "Week
timetable", giving the tab exactly one tab stop, which scrolls with the arrow keys and Page Up/Down.

### 5. Tab wiring

- `AppSettings.TabCount` 2 → 3. `Sanitized()` already validates `SelectedTab` against `TabCount`, so
  persistence and the Ctrl+Tab cycle both follow with no further change.
- A third `<TabItem Header="Week"/>` in `MainWindow.xaml`.
- A third panel with `IndexToVisible` parameter 2.

**`RescueFocusFromHiddenPanel` must be re-verified.** In the tab-shell slice this method read a stale
`Tabs.SelectedIndex` and parked focus on a header, which made tab headers unclickable — the
merge-blocking defect of that branch. A third tab is exactly the change that would wake it up. This
is a required item in the manual pass, not an afterthought.

### 6. Accessibility

- Entry names go on `ItemContainerStyle` with `TargetType="ContentPresenter"`, **not** on the
  `Border` at the root of the `DataTemplate`. A Border gets no automation peer, so a name set there is
  dead and every row announces as its class name. This cost real time in a previous slice; it is
  written down here so it costs none in this one.
- An entry announces label, weekday, time, and whether it is off — "Code class, Wednesday, 09:00" or
  "Code class, Wednesday, 09:00, off".
- **Empty cells generate no automation peers.** Only entries do. The spec calls the agenda the better
  screen-reader rendering, but the flip is driven by window width — which is a proxy for eyesight, and
  the wrong one. A screen-reader user on a wide monitor gets the grid, so the grid's automation tree
  must be the same handful of items the agenda's is, not a 48×7 lattice of blanks.
- **Day columns are named**: "Wednesday, today, 3 alarms". Without it the grid is navigable but
  structureless.
- The grid is not a live region. Nothing here announces on a tick.
- Verification is a UIA tree read (Windows PowerShell + `UIAutomationClient`, walking
  `ControlViewWalker`), performed in **both** renderings — the wide one specifically, since it is the
  one this section is hardening and the easy mistake is to check only the narrow default.
- Keyboard: Tab into the panel, then arrow and Page Down through the whole span. A required item in
  the manual pass.

## Testing

`TimetableLayout` carries the weight, and needs no window:

- Span: floor/ceil to the half hour, the one-hour padding, the six-hour minimum, clamping at
  midnight and at 23:59, and a single-alarm week.
- Days: an alarm repeating Mon/Wed/Fri appears in three day columns; day flags map to the right
  columns with Monday first; `IsToday` follows the injected `now`.
- Entries: slot assignment at boundaries (09:00 and 09:29 share a slot, 09:30 does not); two alarms
  in one slot both survive **and come back in minute → label → id order**; disabled alarms are present
  and flagged; one-shots and countdowns are excluded; empty input gives `IsEmpty`.
- Totality: a week containing one malformed alarm (no day bits, hour 24, minute −1) renders every
  other alarm and throws nothing.
- `TimetableViewModel`: re-projects on `AlarmsChanged`; a thousand ticks inside one day produce
  exactly **one** projection; crossing midnight produces a second and moves `IsToday`.
- Reacts to the existing bulk paths: `ClearAllAlarms` empties the week, and an import that replaces
  the alarm set redraws it.

No DST cases — a pattern week has no dates, so there is nothing for a transition to shift.

The renderings themselves are covered by the manual pass, not by unit tests.

## Risks and edge cases

- **The scale moves when you add an early alarm.** Chosen deliberately over a fixed 06:00–22:00 grid,
  but it means adding a 06:00 alarm rescales the week. Accepted; the alternative was permanent dead
  space at both ends.
- **Focus rescue with three tabs** — see §5. The known defect in this exact area.
- **A very wide span** (an alarm at 00:30 and another at 23:30) clamps to the full day: 48 slots and
  a tall grid. The panel scrolls, as the other tabs do, and the gutter thins to whole hours.
- **Alarms that are all disabled** still draw a full week. Correct — they are still your timetable.
- **The narrow rendering is the real one.** The window opens at 440px, so most people will see the
  agenda first and may never see the grid. It has to be good on its own, not a fallback.

## Phase 2, and what phase 1 owes it

Phase 2 makes a timetable entry a block: `int? EndMinute` on `RecurringAlarmRecord`, which is the
entirety of schema 5 (nullable, so every v4 file loads as instants with no migration step). The slot
grid then spans rows instead of occupying one, and editing arrives.

Phase 1 owes phase 2 exactly two things, both satisfied above: entries carry a `SlotIndex` rather than
a pixel offset, so a span is a row count; and `TimetableEntry` is a layout type of its own rather than
a naked `TimerItem`, so adding an end does not ripple into the View.

Nothing in phase 1 should be built *for* phase 2 beyond that. No unused `EndMinute`, no disabled edit
affordances.

## Documentation

- `CHANGELOG.md` — a `## [2.3.0]` section.
- `README.md` — a line in the feature list and a fresh screenshot of the tab, wide rendering.

**The screenshot must be shot against a fixture timetable, never the live schedule.** A week grid is
different in kind from the alarm-list screenshots that shipped before: it publishes what the user
does, on which days, at which hours, for a whole term — in a public repo, beside the maintainer's
name. Invent class names and hours, point Tidsro at a scratch `%AppData%` copy, shoot that, and
restore. This is a release-checklist item, not advice.

## Rejected alternatives

- **Blocks first (schema 5 up front).** Cleaner end-state, but it puts a schema migration and a
  scheduler change before the first look at a grid. View-first was Malin's call and it is the right
  one: the cheap half is also the half that proves the layout.
- **Layout maths in XAML converters.** Fastest to a picture, but it puts the arithmetic that decides
  where things land somewhere no test can reach.
- **A dated calendar week**, with one-shots overlaid and browsing arrows. That is a small calendar,
  not a timetable, and it drags in week boundaries and a rule for Sunday night.
- **Full 24 hours, scrollable.** Everything representable, nothing special-cased — and the tab opens
  on an empty grid you have to scroll. Against how Tidsro is meant to feel.
- **Fixed 06:00–22:00.** A stable scale, at the price of dead rows at both ends and a special case
  for anything outside them.
- **One day at a time with arrows.** Comfortable at any width, and it loses the only thing the tab
  is for.
