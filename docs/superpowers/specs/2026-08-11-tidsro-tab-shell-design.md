# Tidsro — tab shell and running-timer strip (design)

**Date:** 2026-08-11
**Status:** Approved — ready for plan (stress-tested 2026-08-11; findings folded in)
**Origin:** The main window shows Quick timers and the Schedule as one long scrolling page, with a
responsive flip to side-by-side above 760px. As the Schedule grows toward a weekly timetable, one
page holding everything stops working. Tabs are the smallest slice of that redesign, and the shell
the timetable will later slot into as a third tab.

## Goal

Two named tabs — **Quick timers** and **Schedule** — with real tab semantics for keyboard and
screen-reader users, and a compact strip that keeps whatever is counting down visible from either
tab.

## Scope

**In scope**
- A `TabControl`-based shell replacing the stacked/side-by-side page.
- A read-only running-timer strip pinned below the tab content.
- Remembering the selected tab across restarts.
- Removing the responsive layout code the tabs make redundant.

**Out of scope** (each its own later slice)
- The weekly timetable view, the read-only week grid, and the optional end time on recurring alarms
  (schema 5). This spec only leaves room for them.
- Any change to how timers or alarms behave, are added, edited, or fire.
- Redesigning the internals of either panel. Their contents move unchanged.
- Changing `MinWidth`/`MinHeight` (380x480). Lowering them would also mean moving the floor in
  `AppSettings.Sanitized`, which is not what this change is for.

## Design

### 1. Window structure — `MainWindow.xaml`

The root grid goes from three rows to five:

```
Row 0  Auto   TabControl — headers only
Row 1  *      content grid — both panels, one visible
Row 2  Auto   running-timer strip
Row 3  Auto   undo bar          (unchanged)
Row 4  Auto   Settings button   (unchanged)
```

The strip sits between the content and the undo bar so that nothing above it ever moves when it
appears or disappears. That placement is the whole point: the strip comes and goes several times a
day, and the tab headers are the one piece of furniture the user aims a mouse at. A strip above the
tabs would shift them down on every timer start.

### 2. The tabs — a header-only `TabControl`

The `TabControl` carries two `TabItem`s that hold only their headers. Its `ControlTemplate` renders
just the header panel, so the control has no content area of its own, and the two content panels
live beside it in the row below — a single `Grid` holding both, each wrapped in its own
`ScrollViewer`, with `Visibility` bound to the selected index through a new
`IndexToVisibleConverter`.

**Both panels stay loaded.** This is the reason for the unusual shape. A stock `TabControl` has one
content host and tears down the unselected tab's visual tree, so every return to the Schedule tab
would rebuild the alarm rows — re-running the `Loaded` fade-in storyboard on each row and resetting
the scroll position. Keeping two live panels avoids both, and makes the later Week tab one more
`TabItem` plus one more panel rather than a template rewrite.

The trade: the tab-to-panel relationship is by convention rather than containment. WPF does not
express that relationship to UIA in either arrangement, so nothing is lost that we had — but it is
the first thing to confirm in the manual pass.

Selection lives in the view model. `TabControl.SelectedIndex` two-way binds to
`MainViewModel.SelectedTabIndex`, and both panels bind their visibility to the same property, so
there is one source of truth and it is unit-testable without a window.

`TabControl`'s stock chrome is light-themed and will look wrong against `PageBg`. It needs a full
template in `tokens.xaml`, in the same spirit as the existing `ComboBox` restyle: an active tab in
`Text` with a gold underline, inactive in `TextFaint`, `ActionFocusVisual` for the keyboard focus
ring. Selected state must read from more than colour alone — the underline carries it. Headers get a
minimum height of 34px, matching `TextBox`, `ComboBox` and `DayChip`, so the app's only navigation
control is not also its smallest mouse target.

### 3. The running-timer strip

No new selection logic. `SortRunning` already orders active countdowns soonest-first and parks
paused ones below, so `Running.FirstOrDefault()` *is* the timer to show — and it degrades correctly
when every timer is paused, which an `IsNext`-based strip would have shown as empty.

Three derived members on `MainViewModel`:

- `StripTimer` — `Running.FirstOrDefault()`.
- `ShowStrip` — whether that is non-null.
- `StripExtraText` — `"+N more"` where N is `Running.Count - 1`, the timers the strip is *not*
  showing; null when only one timer is live.

Their change notifications are raised from a `Running.CollectionChanged` subscription taken in the
`MainViewModel` constructor — **not** from `RefreshAll`. `Add`, `CancelTimer`, `UndoDelete` and
`ClearAllAlarms` all mutate `Running` directly without going through `RefreshAll`, so driving the
strip from the tick would leave it showing a wiped timer for up to 250 ms after "Clear all alarms"
— precisely when the user is looking for confirmation that an irreversible action happened. One
subscription covers every existing path, every future one, and the `Move` calls in `SortRunning`
that change which timer is `Running[0]` without changing the count.

The per-second remaining-time updates need no new plumbing: the strip binds through the same
`TimerItemViewModel` the card does.

The strip shows the accent dot, a muted "Running" caption, the remaining time in `FontMono`, the
label, and the extra-count text. The caption exists for two reasons: without it a gold dot is the
only thing saying what the strip is, and it gives the accessible name somewhere to live that
actually reaches the accessibility tree (see §7). An unlabelled timer shows no label, the same way its card does — the time is the identity. It
is read-only — pause, reset and cancel stay on the Quick timers card, so there is exactly
one place to control a timer. When nothing is running it collapses entirely, so an idle Tidsro looks
as calm as it does today. It appears and disappears without animation: the app's reduced-motion
handling is a non-obvious code-behind `ClientAreaAnimation` gate rather than a resource lookup, and
a strip that comes and goes several times a day is the wrong place to spend that complexity.

### 4. Remembering the tab — `AppSettings.SelectedTab`

One new property, `int SelectedTab`, defaulting to 0.

**`Sanitized()` must carry it.** That method rebuilds `AppSettings` property by property, so a field
added to the class but not to `Sanitized` is silently dropped on every load — the setting would
appear to work all session and reset at every launch. It is clamped there too: anything outside
`0..TabCount-1`, including a negative, becomes 0. `TabCount` is a constant on `AppSettings`; the
class already holds UI-shaped knowledge in its 380/480 placement floors, and this keeps the Week
tab to a one-line change.

No schema bump. A v4 file written before this change has no `SelectedTab` key, which deserializes to
0 — exactly the wanted default.

`MainWindow` already receives `AppSettings` and already writes placement back on close, so it seeds
`vm.SelectedTabIndex` from the setting at construction and saves it on that same path. No App-level
wiring, and no disk write on every tab click.

**One deliberate extension beyond a like-for-like change:** `SavePlacement` splits in two. A new
public `CaptureWindowState()` mutates `_settings` only; `SavePlacement()` calls it and then
persists. `OnClosing` keeps calling `SavePlacement` as it does today, and `App.OnExit` calls
`CaptureWindowState` on `_main` before its existing `SaveData()` — capturing without a second disk
write. `_main` is legitimately null when the window was never opened (launched at startup, quit from
the tray), and the null-conditional call is the correct handling, not something to work around.

Today the placement save only happens in `OnClosing`, which the tray's Quit never triggers — so quitting from the tray loses the session's window position, and
would lose the selected tab the same way. Since remembering the tab is the entire point of this
part, it is fixed rather than inherited. Window placement gets the same fix as a consequence, which
is an improvement but is called out here so it is approved rather than discovered.

### 5. Settings reset — `SettingsViewModel.ResetSettings`

`ResetSettings` gains `_settings.SelectedTab = defaults.SelectedTab`, alongside the four placement
fields it already clears.

The live view model must be returned to tab 0 as well, or the reset is invisible until the next
launch. App's existing `resetWindowPlacement` lambda does both — resetting the window and the
selected tab — rather than adding a tenth constructor parameter to a view model that already takes
nine.

### 6. What gets removed

- `ApplyLayout`, `WideBreakpoint`, `_wideApplied`, and the `SizeChanged` / `Loaded` handlers that
  drive them.
- The `Sections` grid, the `Divider` border, and the `QuickPanel` / `DayPanel` names.
- The "Quick timers" and "Schedule" heading `TextBlock`s — the tab labels are now the headings.

Roughly forty lines, including the most intricate code in the window.

### 7. Accessibility

- The `TabControl` is what buys the semantics: Tab and TabItem control types, "Quick timers, tab, 1
  of 2" under Narrator, left/right arrows between headers, Ctrl+Tab to cycle. Hand-rolled toggle
  buttons would announce as buttons; see *Rejected alternatives*.
- The strip carries an `AutomationProperties.Name` identifying it as the running timer, and
  deliberately **no** `LiveSetting` — a live region there would announce a new time every second.
- **That name lives on the "Running" caption `TextBlock`, not on the strip's `Border`.** A `Border`
  creates no automation peer, so a name set on it never reaches the accessibility tree at all —
  the same defect `37c2f25` fixed for the alarm rows, where a name on a template-root `Border` was
  silently inert. A `TextBlock` has a peer, and its `Name` overrides its text, so the caption reads
  as "Running timer". Amended 2026-08-11 after the Task 5 implementer caught the original wording
  mandating the dead placement.
- That name is a **static string**, never a binding. A composed name carrying the remaining time
  would recompute on every 250 ms refresh and raise a UIA name-change up to four times a second;
  screen readers that re-announce a changed name on the focused element would defeat the live-region
  ban through the back door. The time and label are read from the child TextBlocks.
- The strip is not focusable. It is information, not a control, and every control it describes is
  reachable on the Quick timers tab.
- **Focus must not be stranded by a tab switch.** A collapsed panel cannot hold focus, so a switch
  made while focus sits in the panel content — which Ctrl+Tab allows from anywhere in the window —
  drops focus to the window itself, restarting Tab order from the top and losing a screen reader's
  place. On a selection change, the view moves focus to the selected tab header when focus is inside
  the panel being collapsed **or has already fallen to the window itself**. Both conditions are
  needed: the handler is subscribed after the panels' visibility bindings and therefore runs second,
  so whether it still sees focus inside the panel depends on whether WPF has already reassigned it —
  and that ordering is not something the headless suite can pin down. Covering both states makes the
  rescue correct either way. Clicking a header leaves focus on the `TabItem`, which is neither state,
  so the normal path steals nothing. Amended 2026-08-11 after the Task 6 review raised the ordering
  risk.
- **That focus move is gated on the main window being active.** `ResetSettings` changes the selected
  tab while the Settings dialog is modal and focused; an ungated move would pull focus out of the
  modal to a header behind it — the same defect class as the popup-close path that stole foreground
  from a modal during the clear-data work.
- The alarm-row container naming from `37c2f25` must survive untouched. The `ItemContainerStyle`
  carrying `AutomationProperties.Name` moves with the panel and must not be disturbed by the
  restructure — a regression here would silently return every row to announcing its class name.

## Testing

Headless view-model and model tests, following the existing suite:

- `SelectedTabIndex` defaults to 0.
- `Sanitized` preserves a valid `SelectedTab`, and maps a negative and an out-of-range value to 0.
- A settings payload with no `SelectedTab` key loads as 0 — the back-compat case.
- `SelectedTab` survives a `PersistenceService` save/load round trip.
- `ResetSettings` returns `SelectedTab` to 0.
- The strip is null with no countdowns, is the soonest active countdown when several run, and is the
  first paused timer when every timer is paused.
- `StripExtraText` is null for one timer, `"+1 more"` for two, `"+2 more"` for three.
- Adding and cancelling a countdown raises `PropertyChanged` for the strip members, so the strip
  tracks the collection.
- `IndexToVisibleConverter` returns Visible for a matching index and Collapsed otherwise.
- `ClearAllAlarms` empties the strip immediately, without waiting for a tick — the regression the
  `CollectionChanged` subscription exists to prevent.

Manual pass (not automatable here): Narrator reads the headers as tabs with position, arrow keys and
Ctrl+Tab move between them, the strip is named but does not announce every second, starting a timer
moves nothing above the strip, and switching tabs preserves both scroll position and the absence of
a re-run fade-in. Two focus checks belong here too, since neither is reachable from a headless test:
Ctrl+Tab pressed while focus is inside the panel content lands focus on the selected header rather
than nowhere, and a settings reset performed with the modal open moves no focus at all. Reading the
UIA tree covers the announcement checks without launching Narrator.

## Risks and edge cases

- **Templating `TabControl` is the bulk of the work.** Its default chrome is light-themed and
  unusable here. Bounded, and the `ComboBox` restyle is the pattern to copy.
- **The header-only template is unusual.** If UIA turns out not to report the headers as tabs
  without a content host, the fallback is a stock content host plus two always-present presenters
  inside a custom template — more rigid, but it keeps the semantics.
- **Every timer paused.** Covered by construction: the strip follows `Running[0]` rather than the
  "next to fire" flag, so it shows the paused timer rather than vanishing.
- **A tab index from a hand-edited or corrupted file.** Clamped in `Sanitized`, the same treatment
  the window coordinates get.
- **Losing the side-by-side view.** Deliberate. It was the one place both sections were visible at
  once, and the strip replaces the part of that worth keeping — seeing what is counting down.

## Rejected alternatives

- **Two `ToggleButton`s styled like `DayChip`.** Visually the cheapest, since the chip style already
  exists, but it would announce as buttons rather than tabs and needs arrow-key navigation written
  by hand. The wrong trade in an app where the UIA tree has already had real effort spent on it.
- **Keeping the side-by-side layout above 760px.** A tab bar that appears and disappears as the
  window is resized is harder to learn than one that does not, and `ApplyLayout` would have grown
  rather than gone.
- **The strip above the tab headers.** More prominent and read first, but it shifts the headers and
  all content down every time a timer starts or ends.
- **The strip as the full running list, with pause and cancel inline.** Removes the duplication with
  the Quick timers cards, but at 440px a row with three buttons is cramped, and the strip would grow
  tall with several timers running.
- **Not remembering the tab.** One fewer setting, and within a session the tab already persists
  because closing the window only hides it. Rejected: Tidsro launches at startup and restarts
  rarely, so the times it does restart are exactly the times the wrong tab is most jarring.
