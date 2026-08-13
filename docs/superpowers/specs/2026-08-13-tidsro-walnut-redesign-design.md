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
3. Introduce genuine depth: shadow, spacing and hue separating the surfaces (see §5 — an interim
   revision tried to do this with lightness and made the palette worse), a visible control boundary,
   one hero element.
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

### Surfaces — separated by shadow, spacing and hue, not by lightness

| Token | Old (cool slate) | New | Role |
|---|---|---|---|
| `PageBg` | `#0E141A` | `#0B0908` | Window background. Warm near-black; near pixel-off on OLED. |
| `PanelBg` | `#0A0F14` | `#100B08` | Quiet chrome panels (the running strip). |
| `CardBg` | `#161D27` | `#170F0A` | Cards and alarm rows. The neutral walnut. |
| `InteractiveBg` | `#1F2832` | `#211810` | Text boxes, combo boxes, quiet buttons. A touch cooler than cards. |
| `InteractiveHover` | `#28323E` | `#2B2116` | Hover state for the above. |
| `ElevatedBg` | `#232C38` | `#251712` | Hero card, next-alarm row, popup surfaces. A touch warmer than cards. |

**This section previously specified per-step contrast minima (`CardBg` ≥ 1.12 on `PageBg`, and so
on). Those minima were the defect, and they are deleted.** A review correctly found that the first
draft of the walnut palette had shipped a *flatter* ladder than the cool slate it replaced; the fix
wave that followed chased separation up the **lightness** axis, lightening the mid surfaces until
each step cleared its ratio. Lifting lightness out of a warm near-black desaturates it, and the
result read as washed-out brown-pink rather than walnut. Driving the app confirmed it: *"the colours
look really bad — like washed out brown/pink."*

The recorded rule is the opposite of what those minima encode: **when a dark palette reads muted,
raise saturation, not brightness.** The shipped values follow it — `CardBg` went from 26.5%
saturation at 9.6% lightness to **39.4% saturation at 6.5% lightness**. Every surface is deeper and
more saturated than the values it replaces, and none of them is trying to out-bright its neighbour.

Separation instead comes from three things, in this order:

1. **Shadow.** `CardShadow` (§7.5) puts a soft dark halo under every card, the hero and every row.
2. **Spacing.** The gaps between major blocks widened to `Space5` (24) with a `Space6` (32) window
   margin, so blocks read as separate objects rather than a stack of stripes.
3. **Hue.** A few degrees between section types, so surfaces read as different *materials* rather
   than different brightnesses: `ElevatedBg` sits ~7° warmer (redder, hue 16°) than `CardBg` (23°),
   `InteractiveBg` ~5° cooler (more taupe, hue 28°). **Keep this under about 10°** — past that it
   stops being one wood and becomes two colours, which is not what was asked for.

The residual surface steps are informational only, and deliberately below the old minima:
`PanelBg`/`PageBg` 1.016, `CardBg`/`PageBg` 1.050, `InteractiveBg`/`CardBg` 1.084,
`ElevatedBg`/`CardBg` 1.091, `InteractiveHover`/`InteractiveBg` 1.107. **Do not reintroduce lightness
gates on these numbers.** Anyone who does will walk the palette back to dusty pink.

"Popup surfaces" in the table means the completion card (`CompletionPopup`), which shipped on
`CardBg` through the first pass and was moved onto `ElevatedBg` to match. It is the only surface in
the app that floats over other windows, so if anything belongs at the top of the ladder it does.

`PageBg` stays pinned at `#0B0908`: the near-black is deliberate and is what delivers the OLED
benefit. `PanelBg` is quiet chrome a hair *above* the page rather than a well — pure black is only
1.06:1 from `#0B0908`, so no recessed value can be meaningfully darker without collapsing to a
neutral near-black and losing the walnut hue. That is what its only consumer (the running strip)
actually needs.

### Text — warm, not blue-white

| Token | Old | New |
|---|---|---|
| `Text` | `#F4F7FA` | `#F9F3EA` |
| `TextMuted` | `#B4BDC7` | `#C0AB93` |
| `TextFaint` | `#87919C` | `#9E7F67` |

Blue-white text on warm surfaces is a large part of what makes a warm palette still feel clinical.
`TextFaint` is the one text token the surfaces still bind: it renders on `ElevatedBg`, the lightest
surface that carries body text, and has to clear 4.5:1 there. It measures **4.70 on `ElevatedBg`**
and 4.73 on `InteractiveBg`; those two are its floor. Darker, more saturated candidates were tried
first — `#8A7360` sits at the right saturation but peaks at 4.45:1 even on `PageBg`, so it fails AA
on *every* surface. Text luminance is a hard gate; it is the one place the "saturation, not
brightness" rule has to yield.

### Lines — split by job

| Token | Old | New | Role |
|---|---|---|---|
| `Border` | `#2B3440` | `#2A1B14` | Decorative card and divider edges. Name kept — it has 16 consumers, and renaming it to `BorderSubtle` would mean 16 edits for no functional gain. |
| `BorderControl` | *(new)* | `#8A6A4E` | Text boxes, toggles, buttons, scrollbar thumb — anywhere the outline *is* the affordance. |

Rationale in §8.

### Accent and semantics

| Token | Old | New | Note |
|---|---|---|---|
| `Accent` | `#E3B341` | `#E3B341` | Brass — *the gold already in `Assets/icons/tidsro.svg`*. |
| `AccentStrong` | `#ECC25A` | `#F0C55C` | Hover, and the keyboard focus ring (`ActionFocusVisual`). |
| `Danger` | `#A1837F` | `#C4685C` | The one semantic token with real consumers. |

**`Accent` is now the icon's gold, not a retuned one.** An interim revision moved it to `#E0A93C`,
which left the title-bar, taskbar and tray icon a visibly different gold from the UI they sit
beside. The mark is brand and the UI follows it, not the reverse — confirmed by the owner. That
also closes the open question the progress ledger carried about `tidsro.svg`: nothing in the SVG
changes, because the UI came to it. Do not drift one without the other.

**Deleted:** `Success`, `Warning`, `Info`, `AccentSoft`, `BorderSoft`, `BorderStrong`,
`FocusRing`. All have zero consumers — `FocusRing` occurs exactly once, in its own definition; the
actual focus indicator is `ActionFocusVisual`, which draws a solid `AccentStrong` border.
Re-saturating colours nothing renders is busywork, and leaving them invites a future reader to
assume the app has a semantic colour system when it does not. A later slice needing a semantic
colour should add it together with its consumer. Confirmed by Malin 2026-08-13.

**`CardShadow` is back, and this time it has consumers** — §7.5. It was deleted with the others for
having none; that was correct at the time and is no longer true, because the surface ladder no
longer carries the whole job of separating surfaces.

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
   `TextFaint` uppercase (`RUNNING` / `PAUSED`), **its finish time beside the numerals**, and the
   timer's own label below in `TextMuted`. One element dominates the screen. The hero carries **no
   buttons** — see the row rule below.

   **The finish time sits beside the numerals, not under them.** `done HH:mm` answers "when am I
   free", which belongs next to the number it qualifies rather than a line below it competing with
   the label. It is `TextSm` in `TextMuted`, baseline-aligned to the 42px numerals — `StackPanel`
   has no baseline alignment of its own, so it is `VerticalAlignment="Bottom"` plus a bottom margin
   of roughly one small-text descender. It carries **no `AutomationProperties.Name`**: a bare
   `TextBlock` already reports its own text to UIA, and adding one would be a new accessible name.
   The row below keeps its own `done HH:mm` — every other row still needs one, and unlike the
   numerals a finish time is a short static string, not a value that changes every second.

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
5. **Shadow (`CardShadow`), and the rasterisation trap it must avoid.**

   > **WPF trap — read before touching this.** Setting `Effect` on an element rasterises that
   > element **and its entire subtree** into an intermediate bitmap. Every glyph inside loses
   > ClearType sub-pixel antialiasing and renders visibly softer. In a timer app whose numerals
   > *are* the product, hanging a `DropShadowEffect` on the content `Border` of each card is the
   > obvious implementation and the wrong one.

   The shipped shape is a **background-only sibling caster**: `CardShadowCaster` is a childless
   `Border` filled with `PageBg`, sharing a `Grid` cell (and therefore the exact geometry and corner
   radius) with the content `Border` that sits on top of it. The caster carries the effect and casts
   the halo; the content `Border` is effect-free and its text stays crisply rendered. Filling the
   caster with `PageBg` makes it invisible in its own right — where the content `Border`'s
   antialiased corners are partly transparent, what shows through is exactly the page colour that
   would have been there anyway. `IsHitTestVisible="False"` keeps it out of the input path.

   Consumers: the hero, both form cards, every running-timer row, every alarm row, the missed-alarm
   note and the undo bar. Two deliberate exclusions:
   - **The running strip.** It is recessed chrome on `PanelBg`, a hair *below* the page in intent. A
     drop shadow would claim it floats above the page, which is the opposite of its job.
   - **`CompletionPopup`**, which keeps its effect on the content `Border`. That window is
     `AllowsTransparency="True"`, which already disables ClearType for the whole window, so the
     caster would buy nothing — and an opaque `PageBg` rect behind a deliberately transparent
     surface would show at the corners against the desktop.

   Values: `BlurRadius 22`, `ShadowDepth 4`, `Direction 270`, `Opacity 0.8`, black. A black shadow
   on a `#0B0908` page can only darken by ~11 units, so this is a soft grounding halo rather than a
   Material elevation step — which is the intent. Adjust `Opacity` to taste; do **not** reach for
   the content `Border`'s `Effect` instead. Left and right spill is clipped by the enclosing
   `ScrollViewer` (cards fill the panel width), so the visible shadow is mostly the bottom edge.
6. **Section spacing.** `Space1`–`Space4` are inside controls; `Space5` and up are between sections.
   Now that the ladder no longer leans on lightness, the gaps between major blocks are part of how
   surfaces separate, so they are a palette decision rather than only a layout one. Three
   `Thickness` tokens express the rhythm so `MainWindow` stops repeating magic numbers:
   `WindowPadding` (32), `SectionGap` (`0,24,0,0`) and `RowGap` (`0,0,0,16`). Padding *inside*
   controls — `CardPadding`, button padding, input padding — is deliberately unchanged; this is
   breathing room between blocks, not fatter controls.

## 8. Accessibility

Contrast ratios recomputed against the shipped tokens (WCAG 2.1 relative luminance). Darkening the
surfaces made almost every text pair easier; the accent change moved every accent pair. Every pair
below was recomputed, not carried forward.

| Pair | Ratio | Level |
|---|---|---|
| `Text` on `PageBg` | 18.01 | AAA |
| `Text` on `PanelBg` | 17.73 | AAA |
| `Text` on `CardBg` | 17.16 | AAA |
| `Text` on `InteractiveBg` | 15.83 | AAA |
| `Text` on `ElevatedBg` | 15.73 | AAA |
| `Text` on `InteractiveHover` | 14.29 | AAA |
| `TextMuted` on `PageBg` | 8.98 | AAA |
| `TextMuted` on `PanelBg` | 8.84 | AAA |
| `TextMuted` on `CardBg` | 8.55 | AAA |
| `TextMuted` on `InteractiveBg` | 7.89 | AAA |
| `TextMuted` on `ElevatedBg` | 7.84 | AAA |
| `TextMuted` on `InteractiveHover` | 7.12 | AAA |
| `TextFaint` on `PageBg` | 5.38 | AA |
| `TextFaint` on `PanelBg` | 5.30 | AA |
| `TextFaint` on `CardBg` | 5.13 | AA |
| `TextFaint` on `InteractiveBg` | 4.73 | AA |
| `TextFaint` on `ElevatedBg` | **4.70** | AA |
| `Danger` on `PageBg` | 5.19 | AA |
| `Danger` on `PanelBg` | 5.11 | AA |
| `Danger` on `CardBg` | 4.94 | AA |
| `Danger` on `InteractiveBg` | 4.56 | AA |
| `Danger` on `ElevatedBg` | **4.53** | AA |
| `Accent` on `PageBg` | 10.21 | AAA |
| `Accent` on `PanelBg` | 10.05 | AAA |
| `Accent` on `CardBg` | 9.73 | AAA |
| `Accent` on `ElevatedBg` | 8.92 | AAA |
| Button label `PageBg` on `Accent` | 10.21 | AAA |
| Button label `PageBg` on `AccentStrong` (hover) | 12.16 | AAA |
| `AccentStrong` (focus ring) on `PageBg` | 12.16 | passes 1.4.11 |
| `AccentStrong` (focus ring) on `PanelBg` | 11.97 | passes 1.4.11 |
| `AccentStrong` (focus ring) on `CardBg` | 11.58 | passes 1.4.11 |
| `AccentStrong` (focus ring) on `InteractiveBg` | 10.68 | passes 1.4.11 |
| `AccentStrong` (focus ring) on `InteractiveHover` | 9.65 | passes 1.4.11 |
| `AccentStrong` (focus ring) on `ElevatedBg` | 10.62 | passes 1.4.11 |
| `BorderControl` on `PageBg` | 4.02 | passes 1.4.11 |
| `BorderControl` on `PanelBg` | 3.96 | passes 1.4.11 |
| `BorderControl` on `CardBg` | 3.83 | passes 1.4.11 |
| `BorderControl` on `InteractiveBg` | 3.54 | passes 1.4.11 |
| `BorderControl` on `ElevatedBg` | 3.51 | passes 1.4.11 |
| `BorderControl` on `InteractiveHover` | **3.19** | passes 1.4.11 |

No pair that renders is below its gate. **The binding constraints** (bold above) are `BorderControl`
on `InteractiveHover` at 3.19 — hover-only, but a `QuietAction`'s outline *is* its boundary while the
fill animates underneath it — and, for text, `Danger` on `ElevatedBg` at 4.53 and `TextFaint` on
`ElevatedBg` at 4.70. Recompute those three before nudging any token darker.

### Constraint on future work: two pairs that pass only by not existing

Every pair in the table above renders somewhere in the app today. These two do not, and both are
**below 4.5:1**. Nothing is broken — but nothing stops them either:

| Pair | Ratio | The edit that would create it |
|---|---|---|
| `TextFaint` on `InteractiveHover` | 4.27 | any faint text inside a `QuietAction` — its fill animates to `InteractiveHover` |
| `Danger` on `InteractiveHover` | 4.12 | a destructive button that colours its own label |

This list was four pairs before the palette correction. Deepening the surfaces lifted `Danger` on
`InteractiveBg` (3.98 → 4.56) and on `ElevatedBg` (3.80 → 4.53) over the line, so error text is now
safe everywhere except a hovered quiet button. `Danger #C4685C` is nonetheless still the tightest
text token and has no headroom downward.

**Why borders are split.** WCAG 1.4.11 requires 3:1 for visual information that identifies a user
interface component. The requirement applies to boundaries that identify controls, not to ornament —
so `BorderControl #8A6A4E` is used for text boxes, toggles, buttons and the scrollbar thumb and
clears 3:1 on every surface it is drawn on, in every state, while `Border #2A1B14` stays quiet on
card edges, where the shadow, the gap and the content already identify the container.

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
  converting the two hard-coded button-template literals plus their storyboard endpoints to
  token-seeded brushes, and the `CardShadow` / `CardShadowCaster` / section-spacing tokens.
- `src/Tidsro/Views/MainWindow.xaml` — hero countdown and its finish time, strip visibility,
  next-alarm emphasis, surface ladder, shadow casters, section spacing, and the hard-coded countdown
  foreground at line 118.
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
- Recompute the contrast table against the values as implemented, starting with the three binding
  constraints named in §8: `BorderControl` on `InteractiveHover`, and `Danger` and `TextFaint` on
  `ElevatedBg`.
- **Assert the shadow is not on a content element.** Walk the live visual tree, collect every
  `FrameworkElement` with a non-null `Effect`, and assert each one is childless and contains no
  `TextBlock`. This is the cheap standing guard against someone "simplifying" the caster away by
  moving the effect onto the card — a change that looks identical in a screenshot and quietly
  softens every glyph in the app.
- **Verify the strip empirically, including the transition.** Reasoning from `ShowStrip`'s
  definition is not enough: drive the window to the Schedule tab with nothing running, start a
  timer, then cancel the last one *while Schedule is still showing*, and read the strip's visibility
  from the tree at each step. Note that the hero's caption deliberately carries the same accessible
  name ("Running timer") as the strip's, so a tree query matching on name alone finds the wrong
  element on Quick timers — match on the caption's literal text ("Running") too.

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

`tidsro.svg`'s gold is no longer an open thread: `Accent` moved to the icon's `#E3B341` rather than
the icon moving to the UI's, so the two agree with no change to the mark (§5).

`IBMPlexMono-Medium.ttf` still has no consumer — the weight rule has nothing monospaced in it. Keep
or drop, owner's call; unchanged by this wave.

## 13. Palette correction wave (2026-08-13, after the first drive-through)

Recorded because the mistake it fixes was made *inside this branch* and is easy to make again.

**What happened.** A review found the surface ladder had shipped flatter than the palette it
replaced. The fix wave read that as "the steps are too small" and lightened the mid surfaces until
each cleared a contrast minimum. That chased separation up the lightness axis. Lightening a warm
near-black desaturates it, so `CardBg` drifted from the approved `#1A1310` to `#1F1712` and
`InteractiveBg` from `#1E1611` to `#2F231B`, and the app read as washed-out brown-pink.

**Why it was wrong.** The ladder minima in §5 encoded the opposite of the recorded rule. A dark
palette that reads muted wants **more saturation**, not more brightness. The minima were the defect;
they are now deleted rather than retuned, so nobody re-derives them from the same reasoning.

**What ships instead.** Deeper, more saturated surfaces; separation from shadow (§7.5), spacing
(§7.6) and a few degrees of hue between section types (§5); `Accent` moved onto the icon's own gold.
`TextFaint` is the single token that had to be *lightened* rather than deepened, because text
luminance against a surface is a hard WCAG gate and no sufficiently dark value clears 4.5:1.

**The rule to keep:** separation in this app comes from shadow, spacing and hue. Do not add a
lightness gate to the surface tokens.
