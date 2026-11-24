# 0006. Week rows are occupied times only

- Status: Accepted
- Date: 03.09.2026 (v2.3.0)

## Context

The Week tab shows recurring alarms as a timetable. The options were a full 24-hour grid that scrolls, a fixed 06:00–22:00 grid, one day at a time, and a grid that keeps empty time visible as thin gap rows. The gap-row version was built and rejected twice in review: it looked cluttered, and the empty bands carried no information.

## Decision

The grid has one row per start time that has something on it, and nothing else. All rows have the same height, so the vertical scale is deliberately not proportional to time. The gutter prints the row's time when every entry in it starts then; cells print their own time only in a mixed row. Saturday and Sunday get columns only when something falls on them or today is one of them.

The cells line up through a `UniformGrid` with one row over the visible day cells, each wrapped in a `Border`. Fixed `Grid.Column` indices are not used.

## Consequences

- The tab opens on the week as it is, with no scrolling past empty hours.
- The length of a gap between alarms is not visible. Blocks show their own length instead.
- Do not reintroduce fixed column indices or thin gap rows.
- The test suite checks the layout model, not the drawing. Three Week defects shipped with a green test run, so any change here must also be checked in the running app (see `tools/rigs/`).
