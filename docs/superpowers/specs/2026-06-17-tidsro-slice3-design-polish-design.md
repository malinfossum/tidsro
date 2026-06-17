# Tidsro Slice 3 — Design Polish

**Status:** Draft (awaiting review)
**Date:** 2026-06-17
**Builds on:** v1.1 (clock-time alarms, shipped `9919cb4` / tag `v1.1.0`)
**Ships as:** v1.2.0 (visual refresh — version confirmed at release)

## Summary

Slice 3 is a craft pass, not a feature slice. Tidsro already reads as clean and calm; the gap is polish, not concept. We keep the dark, quiet, accessible identity exactly and elevate the craft across three pillars:

1. **Visual system** — a deeper, warmer, more spacious look built around a single gold accent (`#E3B341`, lifted from the logo).
2. **Consistency** — one icon set everywhere, and one deliberate depth language.
3. **Motion** — gentle, calm transitions using the duration tokens that already exist but go unused.

No new functionality ships in Slice 3. Recurring alarms stay in the backlog.

## Goals

- A more premium, less "crowded" feel — confirmed against approved mockups (deeper `#0E141A` base, generous spacing, gold accent).
- Visual and interaction consistency: no mixed icon fonts, no accidental depth.
- The app should feel alive but calm — motion that reassures, never distracts.
- Zero behaviour regressions: all 110 existing tests stay green; screen-reader names and announcements unchanged.

## Non-goals (YAGNI)

- **No new features.** Quit-with-live-timer, cloud sync, etc. remain backlog. **Recurring alarms + a today-scoped "Your day" are Slice 4** (next, after this) — see *What's next* below. Deliberately kept out of this visual pass.
- **No custom window chrome.** The app keeps the default Windows title bar. The stylised dark title bar in the mockups was illustrative only; replacing WPF chrome (`WindowChrome`, hit-testing, snap, custom-button a11y) is a rabbit hole not worth Slice 3. Possible future polish.
- **No light mode.** Tidsro stays dark-mode-only.
- **No layout/feature restructuring** beyond grouping each section's create-controls into a container (see Pillar 1).

---

## Pillar 1 — Visual system

### Palette shift

The base lifts off pure black onto a deep, slightly-warm dark (a touch below the logo's tile), and the primary accent changes from blue-grey to gold. This is a deliberate, Tidsro-local divergence from the shared design-system blue-grey — gold is Tidsro's brand (the logo), and Malin has chosen to bring it into the UI. **No change to the shared `design-system/` folder; this is `Tidsro/Resources/tokens.xaml` only.**

| Token | v1.1 | Slice 3 | Note |
|---|---|---|---|
| `PageBg` | `#000000` | `#0E141A` | Deep base; off pure black, keeps text bright |
| `PanelBg` | `#0A0D10` | `#0A0F14` | Slightly under the base |
| `CardBg` | `#11161B` | `#161D27` | Lifted surface for cards + editor containers |
| `ElevatedBg` | `#171D24` | `#1F2832` | Inputs / interactive |
| `InteractiveBg` | `#1D252D` | `#1F2832` | Unify with ElevatedBg |
| `Text` | `#F4F7FA` | unchanged | |
| `TextMuted` | `#B4BDC7` | unchanged | Verify AA on new base |
| `TextFaint` | `#87919C` | unchanged | Verify AA on new base (deeper base helps; nudge lighter only if needed) |
| `Border` | `#232C35` | `#2B3440` | |
| `BorderStrong` | `#313C48` | `#3A4552` | |
| `BorderSoft` | `#1A2128` | `#1A222B` | |
| `Accent` | `#7C9AB3` | `#E3B341` | **Gold** — primary accent |
| `AccentStrong` | `#90ADC5` | `#ECC25A` | Lighter gold (hover/strong) |
| `AccentSoft` | `#297C9AB3` | `#29E3B341` | Gold @ ~16% |
| `FocusRing` | `#6190ADC5` | `#99E3B341` | Gold focus ring — verify ≥3:1 visibility |
| `Success` / `Warning` / `Danger` / `Info` | — | unchanged | Keep muted semantics |

Exact surface/alpha values are tunable eyes-on during the build — these are the starting ramp, verified against the approved mockup.

### Spacing system

Token values (`Space1`–`Space6`) stay; we apply the larger steps. The crowding came from `8px` everywhere — we move to a `12/16/24` rhythm.

| Use | v1.1 | Slice 3 |
|---|---|---|
| Window content padding | 16 | 24 (`Space5`) |
| Gap between sections (Quick timers ↔ Your day) | 16 | 24–28 |
| Editor-container padding | — | 16 (`Space4`) |
| Card padding | 12 | 16 (`Space4`) |
| Card vertical margin | 8 | 12 (`Space3`) |
| Card corner radius | `RadiusSm` (8) | `RadiusMd` (12) |
| Intra-control gaps | 8 | 10–12 |

**Structural change (the one approved tweak):** each section's create-controls group into a single calm container (`CardBg`, `Border`, `RadiusMd`, padding 16) — the pattern approved in the Your-day mockup, now applied to Quick timers too. Presets (15/30/60) stay as quick one-tap pills *outside* the container.

### Window sizing & resize

The window becomes comfortably resizable so it can grow on desktop — handy with a full agenda or while setting alarms up:

- Default size ~`440×600` (up from `420×560`), tuned eyes-on.
- `ResizeMode="CanResize"` with a sensible **minimum** (~`380×480`) so the layout never collapses.
- Content reflows gracefully at any size: sections stack, inputs and buttons stretch to width, and the agenda scrolls when tall (already handled by the `ScrollViewer`).
- The chosen size persists across launches — the existing window-placement memory (`App.xaml.cs`) stores a `WINDOWPLACEMENT` (size + position + state); confirm it round-trips the resized dimensions.

### Gold usage rules (the discipline)

Gold is powerful; unchecked it goes gaudy and breaks "calm." Gold appears **only** for:

1. **The primary action in each section** — Start (Quick timers), Add/Save alarm (Your day), Save (Settings). Gold fill, dark text (`#0E141A`, the page base).
2. **The single focal indicator per list** — the running timer and the next upcoming alarm get a gold dot; the *next* alarm additionally gets a gold card border + a `next` text label.
3. **The keyboard focus ring.**

Everything else stays neutral: presets, all secondary icon buttons (pause/reset/cancel/edit/delete), Settings entry, inputs, comboboxes, banners.

**Gold is never the sole signal.** Every gold cue is paired with a text label, a glyph, or position — so it carries no meaning for colour-blind users that isn't also carried another way. (next = dot + "next" word; running = dot + live countdown; primary buttons = gold + text.)

---

## Pillar 2 — Consistency

### Icon unification

Replace every raw-Unicode glyph with the icon font already in use (`Segoe Fluent Icons`, fallback `Segoe MDL2 Assets`). Codepoints below are proposed; verify each renders at build.

| Meaning | Current (raw Unicode) | Replace with | Locations |
|---|---|---|---|
| Cancel / dismiss / delete | `✕` U+2715 | `` E711 (ChromeClose) | MainWindow: cancel timer, delete alarm, dismiss missed-note; CompletionPopup: dismiss |
| Reset | `↺` U+21BA | `` E72C (Refresh) | MainWindow: running-row reset |
| Edit | `✎` U+270E | `` E70F (Edit) | MainWindow: agenda edit |
| Complete check | `✓` U+2713 | `` E73E (CheckMark) | CompletionPopup: header |

Glyphs already on the icon font stay as-is: Play `E768`, Pause `E769` (`PauseResumeGlyph`), chevron `E70D`, sound on/off (`BoolToSoundGlyphConverter`).

### Depth language

One deliberate rule instead of the current accident (shadow only on the popup, everything else flat):

- **Editor containers:** surface + 1px border, no shadow (flat grouping).
- **List cards** (running timer, agenda): lifted surface (`CardBg`) + 1px border, no shadow. Depth from surface + border.
- **Focal card** (running/active timer, next alarm): gold 1px border. One focal card per list, max.
- **Completion popup:** keeps its drop shadow — it floats over the desktop, so a shadow is correct here (optionally soften). **The only shadow in the app.**

No drop shadows inside the main window. Calm and flat in-window; elevation only for the floating popup.

---

## Pillar 3 — Motion

Use the existing duration tokens: `DurationFast` 120ms, `DurationBase` 180ms, `DurationSlow` 260ms. Easing: ease-out (`QuadraticEase`/`CubicEase`, `EaseOut`). Calm, never bouncy.

| Element | Behaviour | Duration | Priority |
|---|---|---|---|
| Timer/alarm card added | fade + slide-up (~8px → 0) | Base | Core |
| Button hover / press | background cross-fade | Fast | Core |
| Paused timer readout | cross-fade to dim (not instant) | Base | Core |
| Completion popup shown | slide-up from bottom-right + fade | Slow | Core |
| Completion popup dismissed | slide-down + fade | Base | Core |
| Undo / missed-note banner | fade + slide in | Base | Core |
| Timer/alarm card removed | fade-out + collapse | Base | Nice-to-have |

Entrance animations (Loaded storyboards), hover, paused cross-fade, popup slide, and banner reveals are all straightforward in WPF. True *exit* animation on list removal is harder (the item is already gone) and may need a small attached behaviour — implement only if cheap; otherwise the item simply removes without an exit transition.

**Reduced motion (a11y):** *movement* transitions — the completion-popup slide-up — are disabled when the OS has animations off (`SystemParameters.ClientAreaAnimation == false`), gated in code-behind (the popup already uses this check for its fade). The remaining transitions are subtle opacity/colour fades (≤180 ms, no translation) and always play — consistent with reduced-motion practice, which targets movement/parallax, not fades. Card entrance is therefore **fade-only** (no slide). (This supersedes the earlier "zero the durations via `DynamicResource`" idea, which is unreliable for storyboards inside sealed WPF templates.)

---

## Accessibility requirements

Accessibility is load-bearing for Tidsro and must hold through the polish:

- **Contrast (WCAG AA):** verify with a checker —
  - `Text` / `TextMuted` / `TextFaint` on the new `#0E141A` base. (Deeper than the first proposal, so contrast stays close to the old pure-black; still confirm `TextFaint` ≥ 4.5:1 and nudge lighter only if needed.)
  - Gold text (`next` label, focus ring) on dark.
  - Dark text (`#0E141A`) on gold buttons.
- **Colour never alone:** every gold cue paired with text/glyph/position (see gold rules).
- **Focus ring:** gold, keyboard-only (existing `ActionFocusVisual` pattern), ≥3:1 against adjacent surfaces.
- **Reduced motion:** honoured (above).
- **Screen reader:** no regression — all `AutomationProperties.Name` values and live regions (`announce()`, missed-note, undo) behave exactly as in v1.1.

---

## Surfaces to change

| File | Changes |
|---|---|
| `src/Tidsro/Resources/tokens.xaml` | Palette ramp, accent→gold, focus ring, radius usage, button hover transitions, motion resources |
| `src/Tidsro/Views/MainWindow.xaml` | Spacing, editor containers, gold primary buttons, gold focal indicators, card depth, icon glyphs, entrance/hover/paused motion, default size + resize (MinWidth/MinHeight, reflow) |
| `src/Tidsro/Views/CompletionPopup.xaml` | Palette inherit, `✓`/`✕` → glyphs, slide-up/down motion, neutral actions |
| `src/Tidsro/Views/SettingsWindow.xaml` | Spacing, palette inherit, Save → gold primary, Cancel neutral |
| `src/Tidsro/App.xaml` · `App.xaml.cs` | Shared motion/transition resources if needed; confirm window-placement persistence round-trips the resized size |

---

## Testing & acceptance

This slice is almost entirely View/XAML, so it is **manual-acceptance, not TDD** — consistent with Tidsro's "backend TDD / View manual-acceptance" pattern.

- **Unit tests** only for any new *logic* (e.g. a reduced-motion helper or a new value converter). No behaviour changes → no new behavioural tests.
- **Existing 110 tests stay green.**

**Manual UI acceptance checklist:**

- [ ] Base reads as `#0E141A`; gold appears only per the gold rules.
- [ ] No crowding; Quick timers and Your day clearly separated.
- [ ] Zero raw-Unicode glyphs remain; all icons from the icon font.
- [ ] Gold discipline holds: presets + secondary actions neutral.
- [ ] Motion: cards ease in; popup slides; hover/press soft; paused cross-fades; nothing flashy.
- [ ] Reduced motion: OS animations off → all transitions instant.
- [ ] Contrast: gold-on-dark, dark-on-gold, and faint/muted text on the new base all pass AA.
- [ ] Colour-never-alone: every gold cue has a text/glyph/position partner.
- [ ] Narrator: names and announcements unchanged from v1.1.
- [ ] Keyboard: gold focus ring visible, keyboard-only.

## What's next (future slices — not this one)

Captured here so the ideas aren't lost; each gets its own brainstorm → design → plan once Slice 3 ships:

- **Recurring schedules** — per-alarm repeat: Weekdays, Weekend, or specific days (e.g. Tue + Thu). Needs a recurrence model, a persistence bump (schema v2 → v3), and scheduler re-arm logic (after a recurring alarm fires, compute the next occurrence).
- **Today-scoped "Your day"** — show only the current day's alarms by default, with a toggle to see the full schedule. Pairs naturally with recurring (with one-shot alarms, everything is essentially today or tomorrow).
- **Adaptive layout** — on a wide desktop window, divide "Quick timers" and "Your day" into two side-by-side sections, plus a user toggle to switch between side-by-side and tabbed views (remembered across launches). A real layout feature: needs a layout-mode state, a toggle control, persistence, a responsive breakpoint, and UX calls (do tabs make sense for two sections? auto-split threshold? interaction with the tray/popup?). Slice 3 ships only the precursor — scroll + a centered max-width column so wide windows aren't stretched.

## Open notes

- Version `v1.2.0` proposed; confirm at release.
- Surface ramp + gold alpha values are starting points, tunable eyes-on.
- Keep an eye on gold (`#E3B341`) vs `Warning` (`#A79A74`) proximity; they rarely co-occur, but don't place them adjacent.
