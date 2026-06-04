# Tidsro — Design Specification

- **Status:** Draft for review
- **Date:** 2026-06-03 (revised 2026-06-04 after spec stress-test)
- **Author:** Malin Fossum
- **Type:** Desktop application design spec

> **Revision 2026-06-04:** Folded in stress-test findings — the completion popup gains a full keyboard path and a screen-reader announcement (§5.3, §9); persistence becomes crash-safe and treats loaded data as untrusted (§5.6); sleep/resume fire-dedup, an alarm-delete guard, and several edge-case semantics are pinned (§8, §5.1–5.2, §13); startup-entry hygiene and a no-labels-in-logs privacy rule are added (§5.7, §10–11).

---

## 1. Overview

Tidsro is a tiny, calm, dark-mode-first **Windows desktop timer and alarm app**, built in **C# / WPF**. It runs quietly in the system tray and helps structure a focused workday: ad-hoc countdowns (pomodoro, "code hour", breaks), clock-time alarms ("lunch at 12:00"), and recurring alarms (every weekday at 09:30). Each timer carries an optional short label and either stays silent (the default) or plays a single gentle sound. When a timer finishes, Tidsro shows a quiet card in the bottom-right corner that **never steals focus** and **stays until dismissed**.

The name is Norwegian: *tid* (time) + *ro* (calm / peace) — "calm time". It reflects the tool's whole intent: visible but never flashy, present but never intrusive.

> **Why it exists:** Most timers are either too bare to plan a day around or too noisy to keep open while you focus. Tidsro stays simple and local, but adds a cleaner, more accessible, modern design, multiple simultaneous timers, recurring alarms, and a calm completion experience — for people who work with notifications off and don't want to reach for their phone.

---

## 2. Goals & non-goals

**Goals**
- A daily go-to focus tool — polished enough to reach for every day, and genuinely useful to others too.
- Local-first, low-RAM, runs in the background in the tray.
- Calm, accessible, dark-mode-first UI.
- Countdowns + clock-time + recurring alarms, each labelled, silent-or-gentle-sound.
- A completion alert that is impossible to miss yet never annoying.

**Non-goals (for now)**
- Mobile (a someday hope, possibly via the separate **Ignite** task app — explicitly out of scope here).
- Cloud sync / backup (wanted, but a planned future slice — see §6).
- Custom user-supplied sound files (later slice).
- Light theme; themes/skins.
- An auto-cycling pomodoro engine (the popup's **Restart** action covers looping).

---

## 3. Users & key use cases

Primary user: a developer working heads-down with Windows notifications off.

- "Give me 25 focused minutes, then nudge me" — countdown, silent.
- "Set up my day" — 09:30 standup, 12:00 lunch, 14:00 meeting prep, 16:30 stretch; some recurring.
- "5 minutes before my meeting" — countdown or clock-time.
- "Pomodoro loop" — start 25 min; on finish hit **Restart**.
- "Tea's ready" — short countdown with a gentle chime.

---

## 4. Architecture

**Stack:** C# / WPF on .NET (version per the `csharp-wpf` scaffold). Pattern: **MVVM** — WPF's native expression of the MVC separation already used on the web.

| Layer | In Tidsro | Rule |
|---|---|---|
| **Model** | Domain entities + services: timer/alarm items, the scheduler (ticking brain), persistence, sound, startup, recurrence math. No UI. | State & logic only; unit-testable. |
| **View** | XAML: main window, completion popup, settings, tray icon. | No logic; binds to ViewModels. |
| **ViewModel** | Exposes state and commands (start, pause, dismiss, +5 min, restart, edit) to the View; subscribes to the scheduler. | Glue only; no XAML, no file/registry access (delegates to services). |

**Proposed project structure** (refine during planning):

```
Tidsro/
  Tidsro.sln
  src/Tidsro/
    App.xaml(.cs)              // startup -> tray; no window shown by default
    Models/
      TimerItem.cs             // one timer/alarm (see §7)
      TriggerType.cs           // Countdown | ClockTime | Recurring
      Recurrence.cs            // days-of-week + time-of-day
      SoundChoice.cs           // None (silent) | one of the built-ins
    Services/
      SchedulerService.cs      // single ~1s tick; raises Fired(item)
      PersistenceService.cs    // System.Text.Json <-> %AppData%\Tidsro\data.json
      SoundService.cs          // play a gentle built-in sound
      StartupService.cs        // launch-at-startup toggle (registry Run key)
      TrayService.cs           // tray icon + menu (Open, Quit)
    ViewModels/
      MainViewModel.cs, TimerItemViewModel.cs,
      AlarmEditViewModel.cs, SettingsViewModel.cs, PopupViewModel.cs
    Views/
      MainWindow.xaml, CompletionPopup.xaml, SettingsWindow.xaml
    Assets/   sounds/ (3-5 .wav), icons/
    Resources/ tokens.xaml     // mirrors design-system tokens (palette, spacing, type)
  tests/Tidsro.Tests/          // scheduler, recurrence, persistence
```

**Dependencies — keep minimal (confirm in planning):**
- **Tray icon:** WPF has no native tray. Lean **H.NotifyIcon.Wpf** (clean, popular) — or built-in `System.Windows.Forms.NotifyIcon` for zero extra packages.
- **Sound:** built-in `System.Media.SoundPlayer` (WAV, simplest) or WPF `MediaPlayer`. No NuGet.
- **JSON:** built-in `System.Text.Json`.
- No other dependencies planned.

---

## 5. Feature spec

### 5.1 Triggers
- **Countdown** — a duration (HH:MM:SS); fires at zero. Presets 15 / 30 / 60 min + custom. Custom duration must be **greater than zero** and is capped at a sane maximum (e.g. 24 h); the editor rejects `00:00:00` and out-of-range input.
- **Clock-time alarm** — fires at a specific time today (e.g., 14:00). One-shot. If the chosen time has **already passed today**, the editor rolls it to the same time **tomorrow** (rather than firing instantly or silently doing nothing) and shows which day it will fire.
- **Recurring alarm** — fires at a time on selected days (daily, or pick weekdays). Reschedules to the next occurrence after firing.

### 5.2 Main window (Layout B — two zones)
- **Quick timers** — presets `15 / 30 / 60` + "custom", an optional label field, and the list of running countdowns (label, remaining time, silent/sound tag, pause/cancel). Cancelling a countdown is one click with no confirm — it is transient, session-only, so there is nothing to lose.
- **Your day** — agenda of scheduled + recurring alarms sorted by next fire time, then by label, then `Id` (a stable order when two alarms share a time) — showing time, label, daily/once tag, edit/delete. Friendly empty-state until Slice 2 ("Nothing scheduled yet — add an alarm"). **Deleting a clock-time or recurring alarm is guarded by a brief, dismissable undo** (preferred over a confirm dialog, to stay calm) so a stray click can't silently erase a configured alarm.
- **Window chrome:** the close button (✕) **minimizes to tray** (keeps running). Real quit lives in the tray menu. The app starts to the tray; opening the tray icon shows this window.

### 5.3 Completion alert
- A small card in the **bottom-right** of the **current** working area (the screen the app is on; clamped so it can never land off-screen — see §13).
- **Topmost but non-activating** (`Topmost=true`, `ShowActivated=false`, `ShowInTaskbar=false`): it appears over other windows but **does not steal keyboard focus** — the user keeps typing.
- **Persists until dismissed.** No auto-fade, no flashing.
- **Actions** — **+5 min** (extend / snooze), **Restart** (re-run the original duration; pomodoro loop), **Dismiss**. All three are real focusable `Button`s. For a **mouse** user the card stays quiet: only ✕ shows at rest, the rest reveal **on hover**. For a **keyboard** user they reveal **on focus** too — hover is never the only way in.
- **Keyboard path (first-class, not an afterthought):** because the card never auto-activates, a **global hotkey** (registered via `RegisterHotKey`, configurable) moves focus to the **most-recent** card and cycles through stacked ones; once focused, Tab reaches +5 / Restart / Dismiss, and **focus returns to the previously-active app** on dismiss so the user drops straight back into what they were typing. Active cards are also listed in the tray menu as a fallback. (This makes good on the §9 "full keyboard operation" promise for this surface.)
- **Screen-reader announcement:** on fire, raise a UI Automation **Notification** event (`AutomationPeer.RaiseNotificationEvent`) — e.g. *"Pomodoro complete"* — so completion is announced **without moving focus**. The card carries an `AutomationProperties.Name`. This matters most for **silent** timers, which otherwise give a screen-reader user no perceivable signal at all.
- **Stacking:** multiple finished timers stack upward from the corner; each dismissed independently.
- **Silent timers:** visual only, zero sound (but still announced via the Notification event above). **Sound timers:** play the chosen gentle built-in once (or a couple of soft repeats) then stop; the card remains regardless.
- Styling from `tokens.xaml` (mirrors `design-system`); subtle fade-in, respecting reduced-motion (see §9).

### 5.4 Sounds
- 3–5 curated, gentle, non-jarring built-in sounds (e.g., soft chime, marimba, gentle bell), bundled as assets.
- Chosen per-timer; **silent is the default.** Played via a built-in API; plays once/short, never a nagging loop. No custom files yet (later slice).

### 5.5 Tray & background
- Single lightweight process living in the tray. Tray menu: **Open**, **Quit**. Left-click tray → open main window.
- Honest footprint: WPF idles at a few tens of MB — heavier than bare Win32, far lighter than Electron. Acceptable for "low RAM".

### 5.6 Persistence
- Alarms (clock-time + recurring) and settings saved as JSON to `%AppData%\Tidsro\data.json`, loaded on launch; scheduled/recurring alarms **re-arm to their next future occurrence**.
- Ad-hoc countdowns are **session-only** — a mid-run countdown does not resurrect after restart/reboot.
- **Crash-safe save (atomic):** write to `data.json.tmp`, flush, then `File.Replace`/atomic move over `data.json`, so a process kill or power loss mid-write can never leave a half-written file.
- **Tolerant load:** on a parse/validation failure, rename the bad file to `data.json.corrupt` (kept for inspection), start from safe defaults, and surface a quiet note — **the app must never fail to launch because of a bad data file.**
- **Loaded JSON is treated as untrusted input** (it becomes genuinely untrusted once cloud sync lands — §6): polymorphic / `$type` deserialization is never enabled, and values are range-validated on load (durations > 0, valid `DaysOfWeek`, parseable `TimeOnly`). `System.Text.Json` with explicit, non-polymorphic contracts.

### 5.7 Settings
- **Launch at startup** toggle — **off by default** (adds/removes a user-scope `HKCU` registry Run entry via `StartupService`). The entry stores the **fully-quoted** absolute executable path (so a space in the path can't mis-parse), `StartupService` **refreshes the path** when the app has moved/updated, and **removes the entry** on uninstall — so the toggle can't silently break or orphan a key.
- **Default sound** for new timers (default: silent).
- Minimal; grows only as needed.

---

## 6. Build slices

1. **Slice 1 — Countdowns (a usable tool).** Countdown engine + presets/custom + labels + main window (Quick timers live, Your day empty-state) + tray + completion popup (with +5 / Restart / Dismiss) + silent/sound + multiple simultaneous + stacking popups. Settings persistence only.
   - *Stress-test fixes in this slice:* completion-popup keyboard path + focus return (§5.3, §9); UIA Notification announcement (§5.3, §9); crash-safe + tolerant persistence with untrusted-JSON load (§5.6); custom-duration bounds (§5.1); +5/Restart debounce (§8); off-screen popup clamp (§5.3, §13). Startup-entry hygiene (§5.7) lands with the launch-at-startup toggle, in whichever slice ships it.
2. **Slice 2 — Clock-time alarms.** One-shot "at HH:MM". The Your day agenda becomes live. Alarm persistence.
   - *Stress-test fixes in this slice:* past-time clock-alarm semantics (§5.1); alarm-delete undo guard (§5.2); stable tie-break ordering in the agenda (§5.2).
3. **Slice 3 — Recurring + full persistence.** Daily / weekday-selectable recurrence, next-occurrence math, re-arm on launch, missed-while-asleep handling (§8).
   - *Stress-test fixes in this slice:* single fire-dedup path + 5-minute grace window for sleep/resume (§8); edit-during-fire suppression (§8).

**Cross-cutting (every slice):** no telemetry and no labels in any log (§10); loaded JSON always treated as untrusted (§5.6).

**Future (post-v1):** cloud sync / backup of alarms & settings (explicitly wanted, deferred); custom sound files; possibly mobile (separate effort).

---

## 7. Data model

`TimerItem` — one record for any trigger:

- `Id` (Guid)
- `Label` (string, optional)
- `TriggerType` (Countdown | ClockTime | Recurring)
- `Duration` (TimeSpan?, for Countdown)
- `TimeOfDay` (TimeOnly?, for ClockTime / Recurring)
- `Recurrence` (set of DaysOfWeek, for Recurring; empty = one-shot)
- `Sound` (SoundChoice; None = silent)
- Runtime-only: `State` (Idle / Running / Paused / Fired), `RemainingOrNextFire` (computed)

Persisted: alarms (ClockTime / Recurring) + settings. Not persisted: running countdown state.

---

## 8. Key behaviours & edge cases

- **Single scheduler tick:** one `DispatcherTimer` (~1s) updates all running countdowns and checks alarm fire times. O(n) over a tiny n; negligible CPU/RAM.
- **Fire at most once (dedup):** every alarm records a `LastFired` (its last fired occurrence). The tick, tick-gap detection, and the power-resume handler all fire through **one path** that checks `LastFired`, so an occurrence can never double-fire even when two of those paths coincide.
- **App closed / reboot:** countdowns lost; clock-time/recurring re-arm to next future occurrence. **Missed occurrences are skipped, not backlogged** (no flood of old alarms on launch).
- **Sleep / hibernate:** on resume, if a clock-time/recurring alarm's time passed during sleep within a **grace window of 5 minutes**, fire it once (late beats silent for a depended-on alarm); older than the window → skip to next. Both the power-resume signal and tick-gap detection route through the dedup guard above. (Listen for power-resume / detect tick gaps.)
- **Edit during fire:** if an alarm is being edited at the moment it would fire, the in-progress edit wins — firing is suppressed for that occurrence and re-evaluated against the saved (or cancelled) edit, so a save can't race the popup.
- **DST / time zones:** all times local; recurrence computed against local `DateTime`.
- **Multiple fire at once:** stack popups bottom-right.
- **+5 min on a finished countdown:** re-arms a 5-min countdown from the card. **Restart:** re-runs the original duration. Both are **debounced** — a fast double-click performs the action once, not twice.
- **Duplicate labels / durations:** allowed (the agenda's stable sort — §5.2 — keeps their order fixed).

---

## 9. Accessibility (first-class)

- Full keyboard operation; visible focus states; logical tab order.
- **The completion popup is fully keyboard-operable** despite never auto-stealing focus: a global hotkey focuses/cycles cards, Tab reaches +5 / Restart / Dismiss, and focus returns to the prior app on dismiss (see §5.3). It must never be a mouse-only surface.
- `AutomationProperties` (names/labels) on interactive controls for screen readers; the completion popup exposes an accessible name **and announces completion via a UIA Notification event** (§5.3), so even silent timers are perceivable without focus moving.
- Dark-theme contrast meets WCAG AA (tokens chosen for ≥4.5:1 on text).
- Respect **reduced-motion** (OS setting) — skip the fade when set.
- Comfortable hit targets; nothing conveyed by colour alone (tags use text + colour).

---

## 10. Privacy & security

- **Fully local.** No network calls, no accounts — your data stays on your machine, in `%AppData%\Tidsro`. (Cloud sync, when added later, will be opt-in and specified separately.)
- **No telemetry, ever.** Labels are free text and routinely personal (medication, appointments, sensitive meetings), so **labels are never written to any diagnostic/crash log** — logging, if added, records IDs and trigger types only. The `data.json` is treated as untrusted on load (§5.6).
- The startup toggle writes a single, fully-quoted user-scope (`HKCU`) registry Run entry; no admin rights required (§5.7).

---

## 11. Testing

- Unit tests (`dotnet test`) for Model/Services: scheduler firing, recurrence next-occurrence math (weekday selection, DST boundary), persistence round-trip, missed-while-asleep logic.
- **Fire-dedup:** an occurrence crossed by both tick-gap detection and power-resume fires exactly once (`LastFired` guard).
- **Crash-safe persistence:** an atomic save survives a simulated mid-write kill; a corrupt `data.json` loads to defaults (quarantined to `data.json.corrupt`) instead of throwing on launch; loaded values are range-validated.
- ViewModels tested where logic warrants; Views are thin and verified by hand. The completion popup's keyboard path (hotkey focus, Tab to actions, focus return) and its UIA Notification announcement are verified by hand against a screen reader.

---

## 12. README requirement

The English README must explain the name: **Tidsro = Norwegian *tid* (time) + *ro* (calm / peace) → "calm time"**, including the meaning and the intent behind it (calm, focused, unflashy). Plus a one-line description, the stack listed plainly, minimal setup, and no badges or marketing bloat.

---

## 13. Open decisions (resolve in planning / research)

- Tray library: **H.NotifyIcon.Wpf** vs built-in WinForms `NotifyIcon`.
- Sound API: `SoundPlayer` vs `MediaPlayer`; source 3–5 gentle, licence-clear WAVs.
- .NET version & nullable settings from the `csharp-wpf` scaffold.
- Exact bottom-right offset & multi-monitor handling: which screen's working area, and the **rule that every card is clamped into the current working area on show** so unplugging a monitor (or a resolution change) can never strand a card off-screen and unreachable. Default: follow the screen the main window is on.
- Global hotkey default chord for focusing the completion card (§5.3), and whether it's user-rebindable in v1.
