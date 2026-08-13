# Tidsro visual redesign — walnut, brass, and a voice

**Date:** 2026-08-13
**Status:** design agreed, awaiting implementation plan
**Slice:** visual redesign of the existing app. No behaviour, schema, or layout-structure change.

---

## 1. Why

Malin's assessment was that the app looks "dull and boring" and needs to feel professional and
visually appealing. That is a taste statement, but investigating it turned up concrete,
verifiable causes rather than a matter of preference.

**The palette contradicts itself.** Surfaces are cool blue-grey slate (`PageBg #0E141A`,
`CardBg #161D27`, `ElevatedBg #232C38`) while the accent is warm gold (`#E3B341`). A warm accent
on a cool base never resolves; it reads as a generic dark app with a yellow highlight. Malin's
recorded design preference is the opposite arrangement — near-black base, a single warm accent,
walnut and gold.

**The specified font never loads.** `FontSans` is `"Inter, Segoe UI, sans-serif"`, but Inter is
not installed on the development machine, does not ship with Windows, and the project embeds no
font resources. Verified 2026-08-13: `InstalledFontCollection` reports Inter absent. Every user
has therefore been seeing plain Segoe UI. "The typography has no voice" is literally true — the
intended typeface has never rendered.

**Most of the design system is dead.** Of the tokens defined in `Resources/tokens.xaml`,
`Success`, `Warning`, `Info`, `AccentSoft`, `BorderSoft`, `BorderStrong` and `CardShadow` have
**zero consumers** across all XAML. Only `Danger` is used, three times. The app therefore renders
with exactly one border colour, no shadows, and no semantic colour. The flatness is not a
styling subtlety; it is the measurable state of the system.

**Nothing dominates.** Every type size sits between 12 and 28px. There is no hero element, so the
eye has nowhere to land first.

## 2. Goals

1. Rebase the app on a warm near-black palette with a brass accent, so surfaces and accent agree.
2. Give the app a real typographic voice by embedding fonts, so it renders identically everywhere.
3. Introduce genuine depth: a working surface ladder, a visible control boundary, one hero element.
4. Restyle the scrollbar, which currently uses stock WPF chrome.
5. Fix the accessibility defects found while designing, rather than carrying them forward.

## 3. Non-goals

- **The tab structure.** Quick timers / Schedule and the running-timer strip shipped in PR #18
  and are not reopened.
- **Any `AppSettings` or `TidsroData` schema change.** This slice is purely visual; schema stays
  at 4 and no migration is written.
- **Behaviour.** No scheduling, persistence, sound, or command logic changes.
- **Accessible names.** Every `AutomationProperties.Name` stays byte-identical. That work was
  expensive to get right (see `37c2f25` and the tab-shell pass) and is easy to break in a restyle.
- **A light theme.** Tidsro is dark-mode-only and stays that way.
- **New screenshots and the release itself.** Those belong to the release pass that follows.

## 4. Direction

Chosen from four candidates: the current cool slate, a Claude-style true black with warm orange,
walnut with gold, and pine green with brass.

**Walnut + brass.** It delivers the warm-on-black arrangement Malin consistently prefers, but
unlike the Claude-style option it is not a reskin of another product's identity — the wood tones
connect to Tidsro's own pine-in-hourglass mark, so the app ends up looking like itself. Pine green
was the most on-brand conceptually but carried the highest risk of reading muted again, which is
the exact complaint being fixed.

## 5. Palette

Replaces the surface, text, line and accent tokens in `Resources/tokens.xaml`.

### Surfaces — four distinguishable layers

| Token | Old | New | Role |
|---|---|---|---|
| `PageBg` | `#0E141A` | `#0B0908` | Window background. Warm near-black; near pixel-off on OLED. |
| `PanelBg` | `#0A0F14` | `#16100C` | Quiet chrome panels (the running strip). |
| `CardBg` | `#161D27` | `#1F1712` | Cards and alarm rows. |
| `InteractiveBg` | `#1F2832` | `#2F231B` | Text boxes, combo boxes, quiet buttons. |
| `InteractiveHover` | `#28323E` | `#3A2B21` | Hover state for the above. |
| `ElevatedBg` | `#232C38` | `#34261D` | Hero card, next-alarm row, popup surfaces. |

**Each step carries a measured minimum**, because the first draft of this palette did the opposite
of what this section claimed: moving the base toward black compressed every step (`CardBg` on
`PageBg` fell from 1.09 to 1.06, `ElevatedBg` on `CardBg` from 1.20 to 1.10), so the ladder shipped
*flatter* than the cool slate it replaced. The values above are the corrected ones and hold to:

| Step | Minimum | Actual |
|---|---|---|
| `CardBg` on `PageBg` | 1.12 | **1.13** |
| `ElevatedBg` on `CardBg` | 1.20 | **1.21** |
| `InteractiveBg` on `CardBg` | 1.14 | **1.16** |
| `InteractiveHover` on `InteractiveBg` | 1.10 | **1.12** |
| `PanelBg` on `PageBg` | 1.05 | **1.05** |

Depth therefore comes from layering rather than from shadows — consistent with the established rule
that dark UIs should layer chrome rather than paint everything one value.

"Popup surfaces" in that table means the completion card (`CompletionPopup`), which shipped on
`CardBg` through the first pass and was moved onto `ElevatedBg` to match. It is the only surface in
the app that floats over other windows, so if anything belongs at the top of the ladder it does, and
every pair the move introduces is already measured in §8 and already shipping on the next-alarm row —
the same surface carrying the same `QuietAction` buttons.

`PageBg` stays pinned at `#0B0908`: the near-black is deliberate and is what delivers the OLED
benefit. That pin is also why **`PanelBg` now sits above the page rather than below it**. Pure black
is only 1.06:1 away from `#0B0908`, so a recessed surface cannot clear 1.05 without collapsing to a
neutral near-black and losing the walnut hue — the one thing this palette exists for. `PanelBg` is
consequently redefined from "recessed areas" to "quiet chrome a hair above the page", which is what
its only consumer (the running-timer strip) actually needs.

Lightening the surfaces forced two dependent tokens up with them, both recorded in the tables below:
`TextFaint` (`#94826F` → `#A28E7A`), which would otherwise have fallen to 3.94:1 on the new
`ElevatedBg`, and `BorderControl` (`#7E5F48` → `#947055`), which would otherwise have fallen to
2.63:1 on the new `InteractiveBg`. `Border` moved too (`#33251D` → `#413025`) for a non-contrast
reason: the old value was *darker* than the new `ElevatedBg`, so the hero card's edge would have
inverted into a near-invisible dark hairline.

### Text — warm, not blue-white

| Token | Old | New |
|---|---|---|
| `Text` | `#F4F7FA` | `#F7F1E8` |
| `TextMuted` | `#B4BDC7` | `#BCA894` |
| `TextFaint` | `#87919C` | `#A28E7A` |

Blue-white text on warm surfaces is a large part of what makes a warm palette still feel clinical.
`TextFaint` is the token the surface ladder binds hardest: it lands on `ElevatedBg`, the lightest
surface that carries body text, so every lift of `ElevatedBg` pushes it. First drafted at `#8A7867`,
which measured 4.42:1 on `CardBg` and would have failed AA for body text; raised to `#94826F`, which
the corrected surface ladder then dropped to 3.94:1 on `ElevatedBg`. It ships at `#A28E7A`, which
clears 4.5:1 on every surface it renders on.

### Lines — split by job

| Token | Old | New | Role |
|---|---|---|---|
| `Border` | `#2B3440` | `#413025` | Decorative card and divider edges. Name kept — it has 16 consumers, and renaming it to `BorderSubtle` would mean 16 edits for no functional gain. |
| `BorderControl` | *(new)* | `#947055` | Text boxes, toggles, buttons, scrollbar thumb — anywhere the outline *is* the affordance. |

Rationale in §8.

### Accent and semantics

| Token | Old | New | Note |
|---|---|---|---|
| `Accent` | `#E3B341` | `#E0A93C` | Brass. |
| `AccentStrong` | `#ECC25A` | `#F2C05A` | Hover, and the keyboard focus ring (`ActionFocusVisual`). |
| `Danger` | `#A1837F` | `#C4685C` | The one semantic token with real consumers. |

**Deleted:** `Success`, `Warning`, `Info`, `AccentSoft`, `BorderSoft`, `BorderStrong`, `CardShadow`,
`FocusRing`. All have zero consumers — `FocusRing` occurs exactly once, in its own definition; the
actual focus indicator is `ActionFocusVisual`, which draws a solid `AccentStrong` border.
Re-saturating colours nothing renders is busywork, and leaving them invites a future reader to
assume the app has a semantic colour system when it does not. A later slice needing a semantic
colour should add it together with its consumer. Confirmed by Malin 2026-08-13.

**Deleting a token is a runtime failure, not a build failure.** `StaticResource` to a missing key
throws `XamlParseException` when the *window* loads, and `CompletionPopup.xaml.cs:60` performs an
unchecked string lookup (`FindResource("DurationBase")`). Before deleting, grep both XAML and `.cs`
for each key. See §10 — every window must be opened during the manual pass.

### Hard-coded colours that bypass the tokens

Three literals sit inside control templates and defeat the token system:

| Site | Literal | Is |
|---|---|---|
| `tokens.xaml:100` | `#1F2832` | `QuietAction` button background |
| `tokens.xaml:151` | `#E3B341` | `GoldAction` button background |
| `MainWindow.xaml:118` | `#F4F7FA` | Countdown foreground |

The hover storyboards hard-code their endpoints too (`To="#28323E"`, `To="#ECC25A"`). They are
literals because `ColorAnimation` needs a concrete `SolidColorBrush` to animate `Background.Color`.

**Left as-is, changing `Accent` would leave every primary gold button on the old gold and every
quiet button on cool slate** — the most prominent controls in the app would silently keep the
palette this slice exists to replace. Each templated `Border` gets its own `SolidColorBrush` seeded
from the token, and the storyboards animate that brush rather than a literal.

Note that the old `Warning #A79A74` was effectively a dimmed version of the accent, so had it ever
been used, a warning would have read as a primary action.

## 6. Typography

**Embed IBM Plex Sans and IBM Plex Mono as application resources.** One family covering both
roles, humanist rather than neutral, and licensed under the SIL Open Font License 1.1 — verified
2026-08-13 against `github.com/IBM/plex/LICENSE.txt`. The OFL explicitly permits bundling and
embedding fonts in software, provided the licence accompanies them and reserved font names are not
applied to modified versions. Tidsro ships the fonts unmodified, so the obligation is simply to
include `OFL.txt` in the repository and in the installer alongside the font files.

- `FontSans` → `IBM Plex Sans` (Regular 400, SemiBold 600)
- `FontMono` → `IBM Plex Mono` (Regular 400, Medium 500)

Four font files, a few hundred KB against an already self-contained single-file binary — a
rounding error on download size. Sourced from `https://github.com/IBM/plex`, pinned at tag
`@ibm/plex-sans@1.1.0`, monorepo layout `packages/plex-{sans,mono}/fonts/complete/ttf/`.
`OFL.txt` is the repository `LICENSE.txt` at that same tag. SHA-256 of each file as embedded:

```
0bede3debdea8488bbb927f8f0650d915073209734a67fe8cd5a3320b572511c  IBMPlexMono-Medium.ttf
fe11304a5fe956d5744e9b6a246cc83d90425245e75a62230044966ca96a7f50  IBMPlexMono-Regular.ttf
975dcda37d80f038dcd143c22e33ca2d97a0cc5a929aace1c749153b0fe1afa5  IBMPlexSans-Regular.ttf
a20caf8286023a6a7a85e40b1d2a4ae9fc3e3b1f9eda8f4c542dd4986af67bb1  IBMPlexSans-SemiBold.ttf
7e6b2818edbd8f6a01ae80641cc8f16a51080d08fb4e532be3a0b6f74adb07da  OFL.txt
```

### Licence obligations

OFL 1.1 requires the licence to accompany the font software wherever it is redistributed. The
installer can carry `OFL.txt`, but `publish.ps1` also produces a **standalone portable
`Tidsro.exe`** with no companion files — that binary embeds the fonts and travels alone, which is
the weak point. The repository root is Apache-2.0, so an unqualified `LICENSE` would also appear to
cover fonts it does not.

Therefore: add `THIRD-PARTY-NOTICES.md` at the repo root, ship `OFL.txt` through the installer,
state the font licence in the README, and **surface the attribution in-app** so the portable exe
carries its own — the Settings dialog is the natural home. Add the check to the release recipe.

Referenced through the WPF pack syntax, e.g. `pack://application:,,,/Fonts/#IBM Plex Sans`, with
`Segoe UI` retained as the fallback so a packaging mistake degrades rather than breaks.

Plex Mono replaces Consolas for all time and countdown display. Consolas renders correctly and
does not jitter — its digits are already monospaced, which was verified rather than assumed — but
it is generic system furniture. In a timer app the numerals are the product, so that is where the
voice belongs.

### Scale

Existing sizes are unchanged: 12, 14, 16, 18, 20, 28. One is added:

| Token | Value | Use |
|---|---|---|
| `TextHero` | 42 | The running timer's remaining time. |

Weight becomes a hierarchy tool for the first time: SemiBold 600 for the selected tab, section
headings, and primary buttons; Regular 400 everywhere else. As shipped that means the `ShellTabItem`
`IsSelected` trigger, the `GoldAction` button style, the Settings "Data" heading and the completion
card's title — and nothing else.

Asking for a weight a family lacks is not an error in WPF: it synthesises a fake bold from the
Regular face, which looks plausible while the real `.ttf` sits unused in the binary. So
`FontResourceTests` resolves each requested weight down to the physical face and asserts the face's
own weight, rather than trusting that the markup rendered what it asked for.

**`IBMPlexMono-Medium.ttf` currently has no consumer.** Mono renders at Regular everywhere; nothing
in the weight rule above is monospaced. The file is ~180 KB in every portable exe for nothing, so it
should either be given a job (the hero numerals are the obvious candidate) or dropped from the
`Resource` list. Left for a decision rather than settled here.

## 7. Depth and hierarchy

Four targeted view changes. Everything else inherits from tokens.

1. **Hero countdown.** The running timer's remaining time renders at `TextHero` in Plex Mono on an
   `ElevatedBg` card at the top of the Quick timers panel, with a state caption above it in
   `TextFaint` uppercase (`RUNNING` / `PAUSED`) and the timer's own label below in `TextMuted`. One
   element dominates the screen. The hero carries **no buttons** — see the row rule below.

   **The strip collapses while the hero is showing.** The strip exists so a running timer stays
   visible *from the Schedule tab*; on Quick timers it would repeat the hero's value verbatim —
   the same number twice on one screen, and two UIA elements reporting one piece of state. So the
   strip is visible only when the Quick timers tab is not selected. The hero carries the accessible
   name the strip would have had ("Running timer", or "Paused timer" when it is), is **not** a live
   region, and its numerals must not be announced on every tick.

   **The hero's timer keeps its row; the row drops its numerals.** The hero and the running-timer
   list render the same `Running` collection, so the soonest timer's countdown would otherwise be on
   screen twice — the same duplication the strip rule above exists to prevent. `MainViewModel` marks
   that one timer (`TimerItemViewModel.IsCountdownInHero`, derived from the same
   `Running.FirstOrDefault()` the hero binds, so the two cannot disagree) and its row hides its large
   `RemainingText` `TextBlock`. **Nothing else about the row changes**: it keeps pause/resume, reset
   and cancel, its label, its finish time, its sound tag and its `IsNext` dot.

   Hiding the *whole* row instead — the first attempt — cost three things:
   - the soonest timer lost every control, so pause/reset/cancel had to be rebuilt on the hero, where
     they could only bind `Running[0]` rather than a captured item. `CancelTimer` removes that item
     synchronously, so a double-click or key-repeat cancelled a **second** timer;
   - resuming a paused timer that then sorts to the front collapsed its row **with keyboard focus
     inside it**, dropping focus to the window — the failure `RescueFocusFromHiddenPanel` exists to
     prevent for tab panels;
   - the finish time, sound tag and "next" dot had nowhere left to live, so the Quick timers tab had
     no next-to-finish cue at all.

   A `TextBlock` takes no focus and owns no command, so hiding only the numerals has none of those
   consequences. The mark is applied per item and the `ItemsSource` stays bound to `Running` itself:
   re-projecting it as `Running.Skip(1)` would hand the `ItemsControl` a new collection on every
   one-second tick and rebuild every container.
2. **The next alarm earns emphasis.** Today it carries a small accent dot. It additionally takes
   `ElevatedBg` and a `BorderControl` edge, so "what is coming next" is legible at a glance rather
   than being one row among six.
3. **The surface ladder is applied deliberately** — page, card, control, elevated — rather than
   every container defaulting to `CardBg` with one border.
4. **Scrollbar restyle.** A `ScrollBar` template in `tokens.xaml`: slim track on `PanelBg`, rounded
   thumb in `BorderControl`, hover to `TextFaint`. `ScrollBar` is an ordinary WPF control and
   retemplates exactly like the `TextBox`, `ComboBox` and `TabItem` styles already in the file — it
   is not native Windows chrome.

   Stepper buttons are removed deliberately, which is a small loss for users who click the arrows
   to scroll; the alarm list is short and the panel is fully keyboard- and wheel-scrollable, so the
   affordance is redundant here. To keep the thumb a usable pointer target under WCAG 2.5.8, it
   takes a **minimum length of 40px** and the track keeps a **hit area at least 16px wide** even
   where the visible bar is slimmer — a transparent padded region, so the target grows without
   visual weight. Hover uses the existing duration tokens and stays inside the reduced-motion gate.

## 8. Accessibility

Contrast ratios computed against the new tokens (WCAG 2.1 relative luminance):

| Pair | Ratio | Level |
|---|---|---|
| `Text` on `PageBg` | 17.70 | AAA |
| `Text` on `PanelBg` | 16.80 | AAA |
| `Text` on `CardBg` | 15.72 | AAA |
| `Text` on `InteractiveBg` | 13.58 | AAA |
| `Text` on `ElevatedBg` | 12.98 | AAA |
| `Text` on `InteractiveHover` | 12.09 | AAA |
| `TextMuted` on `PageBg` | 8.67 | AAA |
| `TextMuted` on `CardBg` | 7.71 | AAA |
| `TextMuted` on `InteractiveBg` | 6.66 | AAA |
| `TextMuted` on `ElevatedBg` | 6.36 | AAA |
| `TextFaint` on `PageBg` | 6.33 | AA |
| `TextFaint` on `PanelBg` | 6.01 | AA |
| `TextFaint` on `CardBg` | 5.62 | AA |
| `TextFaint` on `InteractiveBg` | 4.86 | AA |
| `TextFaint` on `ElevatedBg` | **4.64** | AA |
| `Danger` on `CardBg` | **4.61** | AA |
| `Danger` on `PageBg` | 5.19 | AA |
| `Accent` on `PageBg` | 9.38 | AAA |
| Button label `PageBg` on `Accent` | 9.38 | AAA |
| Button label `PageBg` on `AccentStrong` (hover) | 11.79 | AAA |
| `AccentStrong` (focus ring) on `PageBg` | 11.79 | passes 1.4.11 |
| `AccentStrong` (focus ring) on `PanelBg` | 11.19 | passes 1.4.11 |
| `AccentStrong` (focus ring) on `CardBg` | 10.48 | passes 1.4.11 |
| `AccentStrong` (focus ring) on `InteractiveBg` | 9.05 | passes 1.4.11 |
| `AccentStrong` (focus ring) on `ElevatedBg` | 8.65 | passes 1.4.11 |
| `BorderControl` on `PageBg` | 4.46 | passes 1.4.11 |
| `BorderControl` on `CardBg` | 3.96 | passes 1.4.11 |
| `BorderControl` on `InteractiveBg` | 3.42 | passes 1.4.11 |
| `BorderControl` on `ElevatedBg` | **3.27** | passes 1.4.11 |
| `BorderControl` on `InteractiveHover` | **3.04** | passes 1.4.11 |

**The binding constraints** (bold above) are `BorderControl` on `InteractiveHover` at 3.04 —
hover-only, but a `QuietAction`'s outline *is* its boundary while the fill animates underneath it —
and `BorderControl` on `ElevatedBg` at 3.27, which is persistent, since the next-alarm row sets both
together. For text they are `Danger` on `CardBg` at 4.61 and `TextFaint` on `ElevatedBg` at 4.64.
An earlier revision of this section named `BorderControl` on `InteractiveBg` as the tightest pair;
it was never the tightest and is now the third-loosest of the five. Recompute all four before
nudging any token darker.

### Constraint on future work: four pairs that pass only by not existing

Every pair in the table above renders somewhere in the app today. These four do not, and all four are
**below 4.5:1**. Nothing is broken — but nothing stops them either, and each is one plausible edit
away:

| Pair | Ratio | The edit that would create it |
|---|---|---|
| `TextFaint` on `InteractiveHover` | 4.32 | any faint text inside a `QuietAction` — its fill animates to `InteractiveHover` |
| `Danger` on `InteractiveBg` | 3.98 | error text on a text box, combo box or quiet button |
| `Danger` on `ElevatedBg` | 3.80 | a validation message on the hero card or the next-alarm row |
| `Danger` on `InteractiveHover` | 3.54 | a destructive button that colours its own label |

`Danger #C4685C` clears 4.5:1 on `PageBg` (5.19), `PanelBg` (4.92) and `CardBg` (4.61) only — the
three surfaces its current consumers (the two inline error `TextBlock`s) actually sit on. It is a
`CardBg`-and-below token. Putting error text on any interactive or elevated surface means raising
`Danger` first, and raising it is not free: it is already the tightest text pair in the palette at
4.61 on `CardBg`, so it has no headroom downward either.

`TextFaint` is the same story one step up. It clears 4.5:1 everywhere it is drawn today, including
`ElevatedBg` at 4.64, but `InteractiveHover` is not one of those places and would fail at 4.32.

**Why borders are split.** WCAG 1.4.11 requires 3:1 for visual information that identifies a user
interface component. The requirement applies to boundaries that identify controls, not to ornament —
so `BorderControl #947055` is used for text boxes, toggles, buttons and the scrollbar thumb and
clears 3:1 on every surface it is drawn on, in every state, while `Border #413025` stays quiet on
card edges, where the surface step and the content already identify the container.

This **fixes a pre-existing failure**: today's `Border #2B3440` on `InteractiveBg #1F2832` measures
about 1.3:1, so input outlines are effectively invisible. The redesign is the natural moment.

Unchanged and protected: every accessible name; the keyboard-only focus ring (`ActionFocusVisual`,
retuned to the new accent but structurally identical); the reduced-motion gating in code-behind;
and state being conveyed by more than colour — the selected tab keeps its underline, the toggle
keeps its thumb position, the next alarm keeps its dot.

### Accepted limitation: Windows High Contrast Mode

Tidsro retemplates `TextBox`, `ComboBox`, `CheckBox`, `TabItem` and `Button` with hard-coded
brushes and no `SystemColors` path, and the app contains **zero `DynamicResource` usage** — so
nothing can respond to a theme change at runtime. High Contrast users get an app that ignores their
settings entirely.

This is pre-existing, but adding a `ScrollBar` template widens it, so it is recorded here rather
than left to be rediscovered. Supporting it properly means a `SystemParameters.HighContrast` check
and a swappable resource dictionary — a structural change, not a token change, and its own slice.
**Known debt, deliberately not fixed here.**

## 9. Files touched

- `src/Tidsro/Resources/tokens.xaml` — palette, fonts, `TextHero`, scrollbar style, token deletions,
  and converting the two hard-coded button-template literals plus their storyboard endpoints to
  token-seeded brushes.
- `src/Tidsro/Views/MainWindow.xaml` — hero countdown, strip visibility, next-alarm emphasis,
  surface ladder, and the hard-coded countdown foreground at line 118.
- `src/Tidsro/Assets/fonts/` *(new)* — four font files plus `OFL.txt`.
- `src/Tidsro/Tidsro.csproj` — font resource entries. Name the four files explicitly rather than
  globbing, so nothing else dropped in that folder is silently embedded in a distributed binary.
- `src/Tidsro/Views/SettingsWindow.xaml` — in-app font attribution (§6).
- `THIRD-PARTY-NOTICES.md` *(new)*, `README.md`, `installer/` — licence obligations.
- Popup, settings and dialog views — only where a hard-coded value bypasses a token.

## 10. Verification

- The 321 existing tests must stay green. This slice changes no view-model logic, so any failure
  means something was touched that should not have been.
- **A manual pass is mandatory before merge, and it must click things.** The tab-shell slice merged
  a control that did not respond to the mouse past every per-task review, the whole-branch review,
  and a green suite; only driving the app caught it. Restyling `TabItem`, `ScrollBar`, `CheckBox`
  and `Button` templates is exactly the kind of change that can silently break hit-testing or
  focus. Every restyled control gets clicked, and every input gets keyboard focus.
- **Open every window** — main, settings, confirm dialog, edit-alarm, and a fired completion popup.
  A `StaticResource` pointing at a deleted token throws only when that window loads, so a rarely
  opened view can ship green and crash on open. This is the same failure shape as the v1.3
  `Run.Text` crash: XAML-attach-time, invisible to the test suite.
- Re-read the UIA tree and diff the accessible names against the current build. They must match
  exactly, with the hero countdown as the one deliberate addition.
- **Prove the fonts loaded, objectively.** A wrong pack URI yields working, attractive Segoe UI and
  no error — precisely the failure that caused this redesign. Assert at runtime that
  `new FontFamily(uri).GetTypefaces()` is non-empty and that `FamilyNames` contains "IBM Plex Sans";
  a visual check is not sufficient, because the fallback looks fine.
- **Grep for hex literals outside token definitions.** The expected result is zero. Any hit is a
  control that kept the old palette.
- Recompute the contrast table against the values as implemented, starting with the four binding
  constraints named in §8: `BorderControl` on `InteractiveHover` and on `ElevatedBg`, and `Danger`
  and `TextFaint` on `CardBg` / `ElevatedBg`.

## 11. Stress test

Reviewed 2026-08-13 across security, privacy, accessibility and loopholes. Nine findings, all
folded in above: the hard-coded literals that would have half-applied the palette (§5), runtime
failure on token deletion (§5, §10), High Contrast Mode as named debt (§8), hero/strip duplication
(§7), OFL obligations for the portable exe (§6), silent font fallback (§10), the dead `FocusRing`
token and uncomputed focus contrast (§5, §8), scrollbar target size (§7), and pinning the font
source (§6).

Privacy passed clean: no network calls, no telemetry, no new logging, nothing leaves the device.

**Considered and rejected.** Windows text-scaling support — real, but pre-existing and a feature
slice of its own. `ElevatedBg` serving both the hero card and the next-alarm row — mild impurity,
better than inventing a fifth surface for one row. Light theme and runtime theme switching — out of
scope, and structural rather than token work. Lifting `Danger` above 4.88:1 — it passes AA, and
going further pushes it toward the accent's warmth and muddies the two.

## 12. Open questions

None. Token deletion in §5 was confirmed 2026-08-13.
