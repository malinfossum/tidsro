# Tidsro — timetable blocks (design)

**Date:** 2026-09-03
**Status:** Approved — stress-tested 2026-09-03 (ten findings folded in), ready for plan
**Origin:** Phase 2 of the weekly timetable, committed to on 2026-08-26 and left until phase 1
shipped as v2.3.0. Phase 1 draws every recurring alarm as an instant, so a three-hour lecture and a
one-minute reminder look identical. This slice gives an alarm an optional end, and draws it.

The phase-1 spec is `2026-08-26-tidsro-weekly-timetable-design.md`. **This spec supersedes its
*Phase 2, and what phase 1 owes it* section**, which assumed a block would span rows in a grid that,
as accepted, has no rows to span. See *4. Segments, not row spans*.

## Goal

An alarm can say when it ends. The Week tab draws it at its real length, the current block is lit
while it is happening, and nothing new makes a sound.

## Scope

**In scope**
- `int? EndMinute` on `RecurringAlarmRecord` — the whole of schema 5.
- Blocks drawn at their length in the wide grid, as segments; overlapping blocks split the day
  column into lanes.
- The range shown in the agenda rendering and on the Schedule tab.
- An optional end-time field on the Edit-alarm dialog, and on the add path beside it.
- A highlight on the block that is happening now.

**Out of scope** (not now)
- **An end chime, or any second fire point.** Ruled out on 2026-09-03, and the reasoning is worth
  keeping: the scheduler's dedup is deliberately ledger-free — a one-shot is removed before its
  event, a recurring alarm advances `NextFireAt` before its event. By the time an end arrives,
  `NextFireAt` already points at tomorrow's start, so "today's end is still pending" cannot be
  represented in schema 5. Making an end fire durably means adding exactly the per-occurrence
  ledger that design avoids. It is a separate feature wearing the same field; if I want it after
  living with blocks, it is strictly additive.
- **Blocks that cross midnight.** The grid is a day-column model whose span already clamps to 24
  hours; a wrapping block would live in two columns and break the shared-row alignment. A night
  shift is two blocks.
- **End times on one-shot clock alarms.** The Week tab shows recurring alarms only, and an end on
  an alarm no timetable draws would be a field with nowhere to appear.
- **Editing from the Week tab.** Still the Schedule tab's job, as in phase 1.

## Design

### 1. Schema 5

`RecurringAlarmRecord` gains `int? EndMinute` — minutes from midnight, `null` for an instant.
`TidsroData.CurrentSchema` becomes 5. There is no migration step: a v4 file has no `EndMinute` key,
System.Text.Json leaves it null, and null already means "an instant", which is what every v4 alarm
is. A v5 file read by an older Tidsro loses the ends and keeps the alarms, which is the right way
round for a downgrade.

`TimerItem` gains a matching `int? EndMinute`, carried through the same mapping as the other fields.
`SchedulerService` reads it nowhere. If the scheduler were compiled without this field it would
behave identically — that is the test of whether the "no second fire point" ruling actually holds.

One consequence worth naming rather than leaving to be rediscovered: **snooze cannot move a block.**
A fired recurring occurrence raises its card from a transient `ClockTime` snapshot precisely so
Snooze and Dismiss cannot mutate the live alarm, so no amount of snoozing drags a block around the
week. Nothing to build; it falls out of a decision already made.

### 2. Sanitized drops the end, never the alarm

`TidsroData.Sanitized()` gains one rule for `EndMinute`, and its shape is load-bearing: an end that
is out of range (not 0–1439), or not strictly after the start, is **set to null** — the alarm
survives as an instant. Nothing about a bad end justifies dropping a whole alarm out of my schedule.

That is the same posture as `TimetableLayout.Build`, which skips an entry it cannot place rather
than throwing: `data.json` is user-writable and importable, so both are hardening a hostile input,
and in both the failure mode has to be "shows less" and never "loses something".

The end is silently repaired rather than reported. A visible failure would need a card, and the
`FailureAlertPolicy` bar for that is a failure the user must act on; a corrupt end in a hand-edited
file is not.

### 3. Rows for covered slots

`BuildRows` currently gives a slot a row when something *starts* in it. It now gives a slot a row
when something starts in it **or a block covers it**. Empty time between blocks stays collapsed
exactly as it is today; time inside a block does not.

This is the reading of the accepted phase-1 layout that I want kept: the rows I rejected twice were
rows for nothing. A row inside a block is a row for something. The vertical scale becomes
proportional *within* a block and stays deliberately non-proportional across the week — 07:00 and
15:00 still sit next to each other when nothing falls between them.

`ResolveSpan` takes the end into account, so a block running to 18:00 pads from 18:00 rather than
from its start.

**`Build` treats an end at or before the start as an instant, in its own right.** A block's start
comes from `EndsAt` — the next occurrence — while its end comes from `EndMinute`, minutes from
midnight. Those are two different sources, and `Sanitized` compares the end against `Hour`/`Minute`,
not against `EndsAt`. Any disagreement between them would otherwise reach the covered-slot walk as a
negative span, which has no defined behaviour. `Build` is total by contract, so it makes this check
itself rather than trusting an upstream one.

### 4. Segments, not row spans

The wide grid draws one independent element per row: an `ItemsControl` over `Week.Rows`, each row
its own `Grid` of gutter plus a `UniformGrid` of `Border`s (`MainWindow.xaml`). There is no shared
vertical grid, so there is nothing for a `Grid.RowSpan` to span. Rebuilding the grid as one
monolithic `Grid` with `RowDefinition`s would buy the span at the cost of the alignment invariant
established in v2.3.0 — the `SharedSizeGroup` gutter and the `UniformGrid` over visible `Border`s,
which is also how the weekend is dropped.

So a block is drawn as **one segment per row it covers**. `TimetableEntry` gains:

- `int? EndMinute`, and `IsBlock => EndMinute is not null`
- `SegmentRole` — `Instant`, `Start`, `Middle`, `End`, `Whole` (a block confined to one row)
- `LaneIndex` and `LaneCount`

`TimetableLayout` decides all of it. The XAML picks a template per role and draws no arithmetic,
which is the phase-1 principle intact: the maths that decides where things land stays where tests
can reach it.

A `Start` segment carries the label and the range; `Middle` and `End` segments draw the bar and
nothing else, so a block reads as one continuous bar rather than three stacked boxes.

### 5. Lanes

Within one day, blocks that overlap in time are clustered and each cluster's members are assigned a
lane, so they sit side by side and neither hides the other. A day with no overlap yields one lane
per entry and renders exactly as it does today.

An instant inside a block — "Focus block 09:00–11:00" with "Stretch" at 10:00 — is the common case,
not an overlap to resolve: an instant occupies its own lane in the rows it appears in, beside the
block's bar.

The clustering is the ordinary interval-graph pass: sort by start, open a cluster, extend it while
the next entry starts before the cluster's running maximum end, assign each member the lowest lane
free at its start. Pure, and small enough to test exhaustively. **Lanes are assigned in start
order**, which makes announcement order time order — a guarantee, not an accident of the algorithm.

**Lanes are capped at 3.** They are the one axis this slice leaves unbounded: rows stop at 48 because
the span clamps to a day, but a cluster is as wide as the number of alarms overlapping, and
`DataTransferService` caps an import at 8 MB — "thousands of alarms", as its own comment says —
without capping their count. Uncapped, an imported or hand-edited file becomes a `UniformGrid` with
thousands of columns inside every cell of every row. A cluster with more than three members spends
its third lane on a muted `+N more`; the agenda rendering is a list and still shows every one of
them, so nothing is hidden, only summarised.

### 6. Rendering

- **Wide grid.** Each cell `Border` holds a `UniformGrid Rows="1"` over its lanes. Continuation
  segments draw bare.
- **A lane has a minimum readable width of 90px.** Phase 1 already had to keep a time out of a
  hundred-pixel column to stop the label becoming an ellipsis; splitting that column three ways
  leaves 33px, which is bars with no labels — unreadable first for low vision, then for everyone.
  So the lane count a day can draw is bounded by its own width, not only by the cap: the grid takes
  the lanes that fit at 90px each and summarises the rest. A window too narrow for two lanes shows
  the agenda, which is already the rendering it flips to.
- **Agenda (narrow).** The rendering the window opens at, and the one most people see. A block
  prints its range: `09:00–10:30  Focus block`. An instant is unchanged.
- **Schedule tab.** A recurring row with an end reads `Mon Wed Fri 09:00–10:30` so the two tabs
  agree about what an alarm is.
- **Gutter labels.** `TimetableRow.GutterLabel` keeps its rule and computes it from *starts* only.
  A row that exists solely because a block covers it has no start to name, so it falls back to the
  slot's own label — which is honest: that row is the band, not an alarm's time.

### 7. The current block

Computed, never rebuilt. `TimetableViewModel` exposes an observable `NowMinuteOfDay`, updated from
the existing tick; a converter asks a pure `TimetableLayout.IsCurrent(entry, isToday, nowMinute)`
per entry. Start inclusive, end exclusive.

**`NowMinuteOfDay` changes only when the minute does** — 1,440 notifications a day, not the 345,000
that assigning it on every 250 ms tick would produce. Each notification re-runs the highlight
converter for every entry on screen, so raising it four times a second would reintroduce one layer
down exactly the cost `RefreshForTick` exists to avoid.

**The date check runs first.** At 00:00 the date and the minute change on the same tick; if the
minute were read before the week is re-projected, a block in yesterday's column would light for one
tick. `IsCurrent` takes `isToday` from the freshly built week, never from a cached day.

The week projection is **not** re-run every minute. Replacing `Week` rebinds the `ItemsControl`,
which would reset the grid's scroll position and any focus inside it once a minute — a defect that
would only show up in use, which is the kind this app keeps producing. `RefreshForTick` keeps its
date gate exactly as written, including the `DateTimeOffset.Date` reasoning in its comment.

An instant has no duration and is never "current". A disabled block is never lit.

### 8. Entering an end

The Edit-alarm dialog gains an optional end field beside the existing time input, parsed by the
same `ClockTimeRules.TryParse` the start uses, so the two inputs accept and reject identically.
Empty means no end, which is how every existing alarm stays valid.

`Save` validates in the same pass as the start and shows an error in the same place: an end that
does not parse, or that is not after the start, keeps the dialog open. This is the one place a bad
end is reported rather than repaired, because here there is a person to tell.

The add path gets the same field.

## Accessibility

- A block's `AccessibleName` reads `Focus block, Monday, 09:00 to 10:30`. Spoken words, not a dash.
- **Continuation segments must not reach the automation tree**, or a three-hour block is announced
  three times over. The mechanism already exists: a `Border` has no automation peer, so the
  container stays laid out while the collapsed content inside it stays unannounced — the same
  mechanism that drops the weekend.
- A lane is a layout device, not information: nothing announces "lane 2 of 2". Lanes are assigned in
  start order, so what is announced comes out in time order.
- The current-block highlight must not be colour alone. It reuses the accent already used for the
  next-alarm treatment, and the block's accessible name gains `, now`.
- **`, now` is announced to nobody, and that is accepted.** A screen reader reads a name when it
  reaches the element; changing the name of something already on screen raises nothing. The
  alternative is a live region firing every time a block starts, which is noise — the Week tab is a
  view, not a notifier. The range in the name is what carries the information.
- **The bar is a non-text element and needs 3:1.** An ordinary block's bar is drawn in
  `BorderControl`, which sits just above that gate by design; the current block's is the brass
  accent. The highlight never animates, consistent with the app's reduced-motion posture.
- Verify with a UIA tree read, per the usual method — walk `ControlViewWalker`, filter
  `ControlType.DataItem`. Not Narrator.

## Testing

Model-level xUnit, following the phase-1 tests:

- **Persistence** — schema-5 round trip; a v4 file loads with every end null and no alarm lost; a
  v5 file written and read back keeps its ends.
- **Sanitized** — end below 0, above 1439, equal to the start, before the start: each nulls the end
  and keeps the alarm.
- **Rows** — a slot with no start but covered by a block gets a row; empty time between two blocks
  does not; `ResolveSpan` pads from the latest end, not the latest start.
- **Segments** — a block confined to one row is `Whole`; a block over three rows is
  `Start`/`Middle`/`End` in order; an instant is `Instant`.
- **Lanes** — no overlap gives one lane; two overlapping blocks give two; an instant inside a block
  takes its own lane; three-way overlap assigns the lowest free lane; a fifty-way overlap caps at
  three with the rest summarised; lane order equals start order.
- **Totality** — an end at or before the start reaches `Build` as an instant, without `Sanitized`
  having run first.
- **Tick** — a tick inside the same minute raises no property change; the tick that crosses a minute
  raises exactly one.
- **IsCurrent** — the start minute is current, the end minute is not, a different weekday is not, a
  disabled block is not, an instant never is.
- **Dialog** — an end that does not parse and an end at or before the start both keep the dialog
  open with an error; an empty end saves as an instant.

Then a manual pass on the real machine, my hands: the grid at both renderings, an overlap, the
highlight crossing an end boundary, and a screen-reader read of a block.

## Documentation

- `CHANGELOG.md` — a `## [2.4.0]` section.
- `README.md` — a line in the feature list, and the Week screenshot re-shot.
- **Re-run `tools/screenshots/Shoot-Screenshots.ps1`**, do not hand-shoot. Its fixture week gains a
  block or two so the shot shows what the release is about. Every published screenshot is a fixture
  shot; this is a release-checklist item, not advice.

## Rejected alternatives

- **An end chime.** See *Out of scope* — a second fire point with nowhere durable to live.
- **One monolithic `Grid` with `RowDefinition`s and `Grid.RowSpan`.** The straightforward way to get
  a real span, at the price of the alignment invariant accepted in v2.3.0 and of fixed row heights.
  Segments keep the row-major structure that made the alignment structural.
- **Span only through rows that already exist.** A block would extend downward only where some
  other alarm happened to create a row, so the same block would be drawn at different heights
  depending on unrelated alarms. Unexplainable to a user.
- **Refusing overlapping blocks at save time.** Keeps the grid simple by constraining my own data
  for the layout's convenience. A real timetable does double-book.
- **Proportional rows across the whole week.** Rejected in phase 1 and still rejected: it brings
  back the dead space the accepted design exists to remove.
