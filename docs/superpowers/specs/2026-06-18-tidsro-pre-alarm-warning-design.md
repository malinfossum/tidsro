# Tidsro v1.3 — Five-minute pre-alarm warning — Design Specification

- **Status:** Approved (brainstormed) — ready for planning
- **Date:** 2026-06-18
- **Author:** Malin Fossum
- **Type:** Desktop application feature design spec (v1.3, pre-release addition)
- **Builds on:** the [v1.3 recurring-alarms slice](2026-06-18-tidsro-v1.3-recurring-alarms-design.md), v1.1 (clock-time alarms), v1.2 (design polish), and the original [Tidsro design spec](2026-06-03-tidsro-design.md)

---

## 1. Context & scope

The v1.3 recurring-alarms slice turned Tidsro into a set-once daily focus tool: an alarm fires at its time, silent-by-default, via a calm bottom-right card. This adds an **optional 5-minute heads-up before an alarm** — a per-alarm toggle that, when on, gives a gentle warning five minutes before the alarm itself. It exists for the same reason the app does: an ADHD brain rarely stops *on* a hard cutoff. A short "wrap up, the next thing is coming" nudge buys the transition time that a single hard alarm doesn't.

It ships **inside v1.3.0**, before the release is tagged — the screenshots and clip Malin is about to record need to show the finished feature set.

The road is already paved: the scheduler raises `Fired` and `Expired` events on its 250 ms tick; recurring alarms already roll their `EndsAt` forward once per occurrence with durable dedup; alarms already carry a per-alarm sound. The warning is **another signal of the same shape**, not a new subsystem.

**In scope**
- A per-alarm **"Warn me 5 minutes before"** toggle in both the add-editor and the modal Edit dialog, off by default.
- A `Warning` signal raised once, five minutes before each occurrence of a warning-enabled Schedule alarm (one-shot or recurring).
- A **heads-up variant of the existing corner card** ("*<label> · in 5 minutes*"), close-only, no Snooze/Restart.
- Sound that **mirrors the per-alarm choice**: a soft chime when the alarm has any sound, silent when the alarm is silent — reusing the existing `SoftChime`, no new asset or setting.
- Persistence of the toggle, with automatic back-compat for alarms saved before this feature.

**Out of scope (parked or later)**
- A **configurable** lead time (10 / 15 min, multiple warnings). Fixed at 5 minutes; revisit only if asked.
- Warnings on **Quick timers** (short countdowns) — a 5-minute heads-up on a short timer is meaningless; warnings are Schedule-only.
- A **distinct warning sound asset** or a per-alarm warning-sound picker — the shared soft chime is deliberate.
- Snooze / actions on the warning card — it is informational only.

---

## 2. Decisions locked during brainstorming

1. **Per-alarm opt-in.** One new `bool WarnBefore` on the alarm, toggled in the editor beside Sound/Repeat. Off by default — the warning is a deliberate choice per alarm, not a global mode.
2. **Schedule alarms only.** One-shot and recurring clock-time alarms. Quick timers are excluded by design.
3. **Fixed 5-minute lead.** A single `WarningLead = 5 min` constant, mirroring the existing `Grace = 5 min`. No per-alarm or global lead setting.
4. **Sound mirrors the alarm's sound choice.** When the alarm is non-silent the warning plays the existing `SoftChime`; when the alarm is silent the warning is silent (card only). A soft chime rather than the alarm's exact sound, so the heads-up reads as *distinct* from the alarm going off early. No new sound asset, no new setting.
5. **A scheduler-level `Warning` event**, parallel to `Fired` / `Expired`, raised from the same tick. Rejected alternatives: arming a hidden second alarm five minutes early (doubles items, fouls the agenda + persistence) and computing warnings in the UI layer (duplicates the per-occurrence dedup the scheduler already owns).
6. **Dedup via a transient `WarningSent` flag, reset on roll-forward** — mirroring how recurring fires dedup by advancing `EndsAt`. Not persisted: a heads-up is ephemeral.
7. **Two calm guards.** An alarm armed *less than* five minutes before its time does **not** insta-warn; an app asleep through the warning window surfaces the existing quiet "missed" note (if the alarm itself was missed), never a stale heads-up.
8. **Reuse the corner card as a heads-up variant.** Same window, same calm placement/stacking; Snooze/Restart hidden; Dismiss is **close-only** (the alarm stays armed and still fires). Because Dismiss never mutates the alarm, the warning can carry the *live* alarm — no transient-copy guard is needed (unlike the recurring completion card).
9. **The heads-up auto-closes at fire.** If not dismissed first, the warning card closes the moment its alarm fires — replaced by the completion card. The app tracks each open warning card with the occurrence's fire time (captured when the warning is raised) and closes it on the tick that reaches that time. This is decoupled from the `Fired` event, so it behaves identically for one-shot and recurring alarms, and a recurring roll-forward never strands a card.

---

## 3. Feature spec

### 3.1 The toggle

The add-editor and the modal Edit dialog each gain one control after Repeat: a **labelled checkbox "Warn me 5 minutes before"**, default unchecked. It maps to a single `bool WarnBefore` on the alarm. Everything else in the editor (Time / Label / Sound / Repeat / Add) is unchanged; **Enter** still adds. On Add the flag is armed, persisted, and the editor resets the checkbox to off alongside the rest.

The agenda row gains a small **text cue** ("5-min warning") — never colour alone — shown only when the toggle is on, and folded into the row's screen-reader name.

### 3.2 The warning signal

On each 250 ms tick, for every armed alarm with `WarnBefore` on:

- when the clock first crosses into the alarm's **last five minutes** (`EndsAt - WarningLead <= now < EndsAt`) and the warning has not yet been sent for this occurrence, the scheduler raises **`Warning(item)`** once and marks `WarningSent = true`;
- the alarm's normal fire/expire logic is untouched and runs as today once `now >= EndsAt`.

`WarningSent` is **transient** (never persisted). It is initialised at arm time and reset whenever the alarm rolls forward:

- **At arm/restore:** `WarningSent = WarnBefore && now >= EndsAt - WarningLead`. So an alarm set (or relaunched) already inside its last five minutes does **not** fire a heads-up — only a clean crossing while the app is running does.
- **On recurring roll-forward:** when `Tick` advances `EndsAt` to the next occurrence, it resets `WarningSent = false`, so the next occurrence warns on its own crossing.

### 3.3 The heads-up card

A `Warning` raises the **same bottom-right card** as a completion, in a heads-up variant:

- **Title:** "*<label> · in 5 minutes*" (label, or "Alarm" when unlabelled).
- **Actions:** Dismiss only — **close-only**, the alarm stays armed and still fires at its time. No Snooze, no Restart.
- **Sound:** the App handler plays `SoftChime` **iff** the alarm's own sound is not `None`; a silent alarm gets a silent card.
- **Placement / motion / focus:** identical to the completion card — bottom-right, stacked, no focus steal, fade/slide honouring reduced-motion, the keyboard route + UIA announcement.

The warning card **auto-closes the moment its alarm fires** — the heads-up gives way to the completion card — unless dismissed first. The app captures the occurrence's fire time when the warning is raised and closes the card on the tick that reaches it, so a recurring alarm advancing its `EndsAt` never strands a stale card (§6).

### 3.4 Missed-while-away & edges

- **Late arm (< 5 min out):** suppressed by the `WarningSent` initial value — no insta-warn.
- **Asleep through the window:** on relaunch the alarm is restored with `now >= EndsAt - WarningLead` (or already past `EndsAt`), so `WarningSent` initialises true — no stale heads-up. A genuinely *missed* alarm still surfaces the existing quiet note via `Expired`; the warning never produces a note of its own.
- **Recurring:** each occurrence's next-fire is a future weekday/time, so a normally-armed recurring alarm warns on every occurrence; only a last-minute first occurrence is suppressed, after which roll-forward re-enables it.

---

## 4. Data model

**Runtime — `TimerItem` gains two fields:**
- **`bool WarnBefore`** (default `false`) — the persisted per-alarm choice; meaningful for `ClockTime` and `Recurring`, ignored for `Countdown`.
- **`bool WarningSent`** (default `false`, **not persisted**) — the per-occurrence dedup marker, managed entirely inside `SchedulerService`.

**`SchedulerService`:**
- new constant **`WarningLead = TimeSpan.FromMinutes(5)`** beside `Grace`;
- new event **`event EventHandler<TimerItem>? Warning;`**;
- `ArmClockAlarm` / `ArmRecurringAlarm` gain a **`bool warnBefore = false`** parameter; they set `WarnBefore` and initialise `WarningSent` per §3.2.

**Persisted DTOs — one new field each, defaulting false:**

```
AlarmRecord          { … existing …  bool WarnBefore; }
RecurringAlarmRecord { … existing …  bool WarnBefore; }
```

- **Back-compat is automatic:** a record saved before this feature has no `WarnBefore` key → it deserialises to `false` (alarm with no warning), exactly the pre-feature behaviour. The next save writes the field forward. No schema-version bump is required — this is an additive, default-safe field within the existing schema-3 document.
- **Sanitisation:** `WarnBefore` is a plain boolean with no invalid value, so `Sanitized()` needs no new rule; it carries through untouched.

---

## 5. How it maps onto the recurring-slice code

- `Models/TimerItem.cs` — add `bool WarnBefore` and `bool WarningSent`.
- `Models/AlarmRecord.cs` / `Models/RecurringAlarmRecord.cs` — add `bool WarnBefore`.
- `Services/SchedulerService.cs` — add `WarningLead`, the `Warning` event, and the `warnBefore` arm parameters; in `Tick`, before the fire guard, raise `Warning` once per occurrence within the lead window; reset `WarningSent` where a recurring alarm advances `EndsAt`.
- `ViewModels/MainViewModel.cs` — add `AlarmWarnBefore` editor state; pass it through `AddAlarm` → `ArmClockAlarm` / `ArmRecurringAlarm`; reset it in `ClearEditor`; thread `warnBefore` through `ApplyAlarmEdit`.
- `ViewModels/AlarmItemViewModel.cs` — expose `WarnBefore` + a `WarnText` cue; fold it into `AccessibleName`.
- `ViewModels/EditAlarmViewModel.cs` — carry `WarnBefore`; widen the apply callback to include it.
- `ViewModels/PopupViewModel.cs` — a warning mode: `IsWarning`, `ShowSnooze` / `ShowRestart` false in that mode, a caller-supplied title, close-only Dismiss (no-op `onDismiss`), and an `AnnouncementText` the card announces (completion → "…complete", warning → the title).
- `Views/CompletionPopup.xaml(.cs)` — bind Snooze/Restart visibility to `ShowSnooze` / `ShowRestart`; announce `AnnouncementText` instead of the hard-coded "complete".
- `Views/MainWindow.xaml` — the "Warn me 5 minutes before" checkbox in the add-editor; the agenda row's warning cue.
- `Views/EditAlarmWindow.xaml` — the same checkbox in the modal dialog.
- `App.xaml.cs` — subscribe `Warning`; the handler plays `SoftChime` when `item.Sound != None` and shows a heads-up card in the existing corner stack, **tracked with the occurrence's fire time (`item.EndsAt`, captured now)**; the existing 250 ms tick handler **closes any tracked warning card whose captured fire time has arrived**, so the heads-up gives way to the completion card; `ToRecord` / `ToRecurringRecord` write `WarnBefore`; `ArmLoadedAlarms` / `ArmLoadedRecurring` and the Edit-dialog factory pass it through.

---

## 6. Key behaviours & edge cases

- **Warn once per occurrence (transient dedup):** `WarningSent` blocks a second heads-up across consecutive ticks; advancing `EndsAt` on a recurring fire resets it for the next occurrence.
- **No warning after fire:** the window guard is `now < EndsAt`; at/after fire only the normal `Fired` path runs.
- **Off by default & for silent alarms:** `WarnBefore` off → no signal at all; on but the alarm is silent → card only, no chime.
- **Close-only Dismiss:** dismissing the heads-up never disarms the alarm; the alarm still fires at its time (this is why the warning can safely carry the live recurring alarm with no transient copy).
- **Late arm / relaunch inside the window:** suppressed via the `WarningSent` initial value — a calm "no surprise card on launch / on last-minute set" rule.
- **DST / time zones:** the warning is derived from the same local-wall-clock `EndsAt` as the alarm, so it inherits the recurring slice's DST behaviour with nothing new.
- **Heads-up lifecycle:** the warning card closes the moment its occurrence's fire time arrives (replaced by the completion card) or on earlier dismiss; the fire time is captured when the warning is raised, so a recurring roll-forward never strands it.

---

## 7. Accessibility

- The **checkbox** carries `AutomationProperties.Name` ("Warn me 5 minutes before") and a clear checked/unchecked state; it is keyboard-reachable in the editor tab order.
- The agenda row's warning indicator is **text** ("5-min warning"), folded into the row's accessible name (e.g. *"Alarm at 07:00, Morning, weekdays, chime, warns 5 minutes before, next"*), never colour alone.
- The **heads-up card** inherits the completion card's keyboard route and announces "*<label> in 5 minutes*" via the UIA notifier on appear.
- Add / edit announcements may note the warning is on, consistent with the existing add/edit announcements.

---

## 8. Privacy & security

- Unchanged posture: fully local, no telemetry, no network. The new `WarnBefore` flag persists only to `%AppData%\Tidsro\data.json`.
- No new personal data; labels stay out of logs; loaded JSON stays untrusted, and the additive boolean needs no new validation. The launch arm pass stays wrapped so a residual bad record never crashes startup.

---

## 9. Testing

Backend is **TDD** (Model / Services / VM); the card, chime, and editor visuals are **manual acceptance** — as in every slice.

Unit tests (`dotnet test`):
- `SchedulerService`:
  - a `WarnBefore` alarm raises `Warning` **once** when a tick crosses into `[EndsAt - 5 min, EndsAt)`, and does **not** raise it again on the next tick;
  - `WarnBefore` off → `Warning` never raised;
  - no `Warning` at or after `EndsAt` (the fire path runs instead);
  - an alarm armed **inside** the last five minutes does not warn (suppressed `WarningSent`);
  - a **recurring** alarm warns before an occurrence, fires + advances, then warns again before the *next* occurrence (roll-forward reset);
  - the `Warning` event carries the live alarm with label + sound intact, and the subsequent fire/roll-forward is unaffected.
- `MainViewModel`: `AddAlarm` with the toggle on arms an alarm whose `WarnBefore` is true; `ClearEditor` resets `AlarmWarnBefore`; `ApplyAlarmEdit` carries `WarnBefore` through an edit; the agenda row exposes the warning cue and includes it in `AccessibleName`.
- `PersistenceService` / `TidsroData`: `WarnBefore` round-trips for both record types; a record saved **without** the field loads as `false` (back-compat); sanitisation leaves the boolean untouched.

Manual acceptance (with a screen reader for the a11y items): enable the toggle on a one-shot and on a recurring alarm; confirm the heads-up card appears five minutes before with a soft chime for a sounded alarm and silently for a silent one; Dismiss closes the card and the alarm **still fires** five minutes later; a heads-up left undismissed **closes itself when the alarm fires**, replaced by the completion card; a last-minute alarm gives no insta-warn; a relaunch inside the window gives no stale card; the editor checkbox + agenda cue legibility, keyboard path, and UIA announcements.

---

## 10. Release note (within v1.3.0)

- **README upkeep:** add the 5-minute warning to the feature summary (a line under the Schedule/alarms feature — "optional 5-minute heads-up before any alarm, so you can wrap up the current thing"). It reinforces the "built to hold focus, for an ADHD brain that needs transition time" framing already going into the README.
- This feature is part of the same v1.3.0 tag — no separate release; it lands before the screenshots/clip and the tag.

---

## 11. Open decisions

None blocking. The warning-card lifecycle is now **decided** — it auto-closes when its alarm fires, or on earlier dismiss (§2 #9, §3.3). Items deliberately left to implementation / manual acceptance, following the slice's established values:
- **Editor control treatment:** a labelled checkbox is specified for clarity and accessibility; its exact visual fit beside the Sound/Repeat rows (checkbox vs. a single toggle-chip) is a manual-acceptance polish call.
- **Agenda cue wording/placement:** "5-min warning" text vs. a small glyph-plus-text, tuned for legibility during acceptance.
