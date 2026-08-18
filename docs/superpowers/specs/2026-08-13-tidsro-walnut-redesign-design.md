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

**Amended 2026-08-18 (§14): walnut was rejected twice on sight and the direction changed to true
black.** The objection recorded above — that a true-black base would be a reskin of another
product's identity — does not survive contact with what shipped: the accent is `#E3B341`, sampled
from Tidsro's own pine-in-hourglass mark, not Claude's orange. The surfaces carry no identity in
either scheme; the accent carries all of it, and this one is the app's own.

## 5. Palette

Replaces the surface, text, line and accent tokens in `Resources/tokens.xaml`.

### Surfaces — neutral, lifted off black so shadows render

| Token | Walnut (rejected) | True black (flat) | Shipped | Role |
|---|---|---|---|---|
| `PageBg` | `#0B0908` | `#000000` | `#0A0A0A` | Window background. Off pure black *so shadows exist*. |
| `PanelBg` | `#100B08` | `#0B0B0B` | `#141414` | Quiet chrome panels (the running strip). |
| `CardBg` | `#170F0A` | `#131313` | `#1E1E1E` + lit top edge | Cards and alarm rows. |
| `InteractiveBg` | `#211810` | `#171717` | `#0E0E0E` | Text boxes, combos, quiet buttons — **inset, below the card**. |
| `InteractiveHover` | `#2B2116` | `#222222` | `#191919` | Hover state for the above. |
| `ElevatedBg` | `#251712` | `#1C1C1C` | `#262626` + lit top edge | Hero card, next-alarm row, popup surfaces. |

**Walnut was rejected twice on sight (§14). True black was rejected as flat (§15).** Surfaces carry
no hue in either of the last two waves and that part was right — the gold accent does the warmth.
What was wrong with `#000000` is mechanical, not aesthetic: `CardShadow` casts `#000000`, so on a
`#000000` page every shadow in the app was arithmetically invisible and nothing lifted off anything.
`PageBg` is `#0A0A0A` to give those shadows somewhere to darken into.

**`InteractiveBg` now sits *below* `CardBg`, and that inversion is deliberate.** A field you type
into should read as a well cut into the card, not as another card stacked on top of it. Through both
earlier waves every input rendered lighter than its container, which is most of why the alarm
composer read as one flat slab. Inputs recessed, cards raised, hero raised further.

Two rules survive from the earlier waves and still hold:

- **No per-step lightness minima.** They are what drove wave one to dusty pink. Nothing below is a
  gate; the steps are informational.
- **Saturation before brightness** when a colour reads muted. It binds `Accent`, `Danger` and
  `StateOn` — the surfaces have no saturation to raise.

Separation comes from, in this order:

1. **Lightness**, running both ways from the card: `PanelBg`/`PageBg` 1.06, `CardBg`/`PanelBg` 1.08,
   `ElevatedBg`/`CardBg` 1.09, and `CardBg`/`InteractiveBg` 1.16 in the other direction.
2. **Shadow.** `CardShadow` (§7.5), which now renders. It did not before.
3. **The hairline.** `Border` lifted to `#303030` so a card edge is visible where shadow alone is
   subtle.
4. **Spacing.** Unchanged: `Space5` (24) between blocks, `Space6` (32) window margin.

"Popup surfaces" means the completion card (`CompletionPopup`), the only surface that floats over
other windows, so it sits at the top of the ladder.

### Text — warm greys at low chroma

| Token | Walnut (rejected) | New |
|---|---|---|
| `Text` | `#F9F3EA` | `#F4F1EC` |
| `TextMuted` | `#C0AB93` | `#B0A9A0` |
| `TextFaint` | `#9E7F67` | `#938D85` |

Text is where the warmth that used to be smeared across every surface now lives, together with the
accent — but at *low chroma*. The rejected `TextMuted` was `#C0AB93`, a tan carrying 45 points
between its red and blue channels; at body-copy size, over a whole window, that is what "washed
out" looked like. The new values stay under 15 points, which reads as warm white rather than beige.

`TextFaint` is still the floor token: it renders on `ElevatedBg`, the lightest surface carrying body
text. It now measures **5.19 on `ElevatedBg`** and 4.84 on `InteractiveHover`, so it clears AA on
every surface in the app — including the two that used to fail (see §8).

### Lines — split by job

| Token | Old | New | Role |
|---|---|---|---|
| `Border` | `#303030` | `#3A3A3A` | Decorative card and divider edges. Name kept — it has 16 consumers, and renaming it to `BorderSubtle` would mean 16 edits for no functional gain. |
| `BorderControl` | `#7A7A7A` (neutral) | `#78726B` (warm graphite) | Text boxes, toggles, buttons, scrollbar thumb — anywhere the outline *is* the affordance. |

Rationale in §8.

### Accent and semantics

| Token | Old | New | Note |
|---|---|---|---|
| `Accent` | `#E3B341` | `#E3B341` | Brass — *the gold already in `Assets/icons/tidsro.svg`*. |
| `AccentStrong` | `#ECC25A` | `#F0C55C` | Hover, and the keyboard focus ring (`ActionFocusVisual`). |
| `Danger` | `#C4685C` (walnut) | `#D9736A` | The one semantic token with real consumers. Re-saturated so it still reads as a warning against neutral surfaces rather than as dulled brick. |

**`Accent` is now the icon's gold, not a retuned one.** An interim revision moved it to `#E0A93C`,
which left the title-bar, taskbar and tray icon a visibly different gold from the UI they sit
beside. The mark is brand and the UI follows it, not the reverse — confirmed by the owner. That
also closes the open question the progress ledger carried about `tidsro.svg`: nothing in the SVG
changes, because the UI came to it. Do not drift one without the other.

**`StateOn` `#3FB950` and `StateOnStrong` `#56D364` are new (§15), and they exist to take a job
*away* from the accent.** Gold used to fill every alarm toggle, so a six-alarm schedule rendered a
column of six gold pills and the gold stopped reading as "action" anywhere. Gold now marks the
primary button, the selected tab and the next-alarm pip only; `StateOn` marks an armed alarm;
`Danger` marks destructive intent on hover. Three hues, one job each — the hue-per-category rule the
rest of the dark-theme work follows. A fourth hue needs a fourth job, not a fourth mood.

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

Contrast ratios recomputed against the shipped tokens (WCAG 2.1 relative luminance). Every pair was
recomputed for the lifted palette, not carried forward.

| Pair | Ratio | Level |
|---|---|---|
| `Text` on `PageBg` | 17.57 | AAA |
| `Text` on `PanelBg` | 16.35 | AAA |
| `Text` on `CardBg` | 14.80 | AAA |
| `Text` on `InteractiveBg` | 17.14 | AAA |
| `Text` on `ElevatedBg` | 13.43 | AAA |
| `Text` on `InteractiveHover` | 15.61 | AAA |
| `TextMuted` on `PageBg` | 8.51 | AAA |
| `TextMuted` on `PanelBg` | 7.92 | AAA |
| `TextMuted` on `CardBg` | 7.17 | AAA |
| `TextMuted` on `InteractiveBg` | 8.30 | AAA |
| `TextMuted` on `ElevatedBg` | 6.51 | AA |
| `TextMuted` on `InteractiveHover` | 7.56 | AAA |
| `TextFaint` on `PageBg` | 6.02 | AA |
| `TextFaint` on `PanelBg` | 5.61 | AA |
| `TextFaint` on `CardBg` | 5.07 | AA |
| `TextFaint` on `InteractiveBg` | 5.87 | AA |
| `TextFaint` on `ElevatedBg` | 4.60 | AA |
| `TextFaint` on `InteractiveHover` | 5.35 | AA |
| `Danger` on `PageBg` | 6.23 | AA |
| `Danger` on `PanelBg` | 5.80 | AA |
| `Danger` on `CardBg` | 5.25 | AA |
| `Danger` on `InteractiveBg` | 6.08 | AA |
| `Danger` on `ElevatedBg` | 4.76 | AA |
| `Danger` on `InteractiveHover` | 5.53 | AA |
| `Accent` on `PageBg` | 10.17 | AAA |
| `Accent` on `PanelBg` | 9.47 | AAA |
| `Accent` on `CardBg` | 8.57 | AAA |
| `Accent` on `InteractiveBg` | 9.92 | AAA |
| `Accent` on `ElevatedBg` | 7.78 | AAA |
| Button label `PageBg` on `Accent` | 10.17 | AAA |
| Button label `PageBg` on `AccentStrong` (hover) | 12.11 | AAA |
| `AccentStrong` (focus ring) on `PageBg` | 12.11 | passes 1.4.11 |
| `AccentStrong` (focus ring) on `PanelBg` | 11.27 | passes 1.4.11 |
| `AccentStrong` (focus ring) on `CardBg` | 10.20 | passes 1.4.11 |
| `AccentStrong` (focus ring) on `InteractiveBg` | 11.81 | passes 1.4.11 |
| `AccentStrong` (focus ring) on `ElevatedBg` | 9.26 | passes 1.4.11 |
| `AccentStrong` (focus ring) on `InteractiveHover` | 10.76 | passes 1.4.11 |
| `BorderControl` on `PageBg` | 4.17 | passes 1.4.11 |
| `BorderControl` on `PanelBg` | 3.88 | passes 1.4.11 |
| `BorderControl` on `CardBg` | 3.51 | passes 1.4.11 |
| `BorderControl` on `InteractiveBg` | 4.06 | passes 1.4.11 |
| `BorderControl` on `ElevatedBg` | 3.18 | passes 1.4.11 |
| `BorderControl` on `InteractiveHover` | 3.70 | passes 1.4.11 |

No pair that renders is below its gate. **The binding constraint** is `BorderControl` on
`ElevatedBg` at **3.18**, and it binds by design: §16 took the control outline *down* to just above
the 3:1 gate rather than up, so the hero and next-alarm row are now what stops it going dimmer. The
tightest text pair is `TextFaint` on `ElevatedBg` at **4.60**. Recompute both before nudging any
token — there is no headroom left underneath either.

The armed toggle is a non-text indicator, so its gate is 1.4.11's 3:1 — the gold ring measures 8.57
on `CardBg` and the `AccentStrong` thumb 12.47 against the `#050505` well it sits in. State is never
carried by colour alone: the knob also travels left-to-right, which is what a colour-blind or
greyscale reading uses.

### Resolved: the two pairs that used to pass only by not existing

The walnut palette carried two pairs below 4.5:1 that were safe only because nothing rendered them
— `TextFaint` on `InteractiveHover` (4.27) and `Danger` on `InteractiveHover` (4.12). Both now
measure **5.35** and **5.53**. The constraint is lifted: faint text inside a `QuietAction`, or a
destructive button that colours its own label, are no longer edits that would silently break AA.

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

## 14. Third wave — the direction changed (2026-08-18)

§13 recorded a *tuning* mistake. This one records that the tuning was not the problem.

**What happened.** The corrected walnut palette — deeper, more saturated, exactly what the recorded
rule prescribed — was driven and rejected again: *"it looks horrible."* Two rejections of two
different tunings of the same hue is evidence about the hue, not the tuning.

**Why brown failed.** `dark-theme-design.md` records walnut as a *pairing Malin likes*, never as a
large-area surface. A warm brown reads rich in a 200px swatch and muddy across a full window, and
every attempt to rescue it moved along an axis that made it worse: lighter desaturates it into
dusty pink, darker collapses it into the page. The north star that file actually records is **true
black with one warm accent** — the Claude Code aesthetic — and the walnut waves had quietly
replaced "one warm accent" with "everything warm".

**What ships instead.** Neutral near-black surfaces from `#000000` up, warm greys for text at low
chroma, and `#E3B341` — the icon's own gold, unchanged and still settled — as the only saturated
colour in the app. `Danger` was re-saturated to `#D9736A` so the one semantic token still reads as
a warning beside the gold rather than as dulled brick.

**Nothing structural changed.** The tabs, hero countdown, spacing, shadows, scrollbar and typography
were approved on the first drive-through and are untouched by this wave. Only `Resources/tokens.xaml`
and this spec changed; 349 tests stay green.

**The rule to keep:** when a colour direction is rejected twice, change the direction. Re-tuning a
rejected hue is how this branch spent two waves.

## 15. Fourth wave — depth, and giving the gold its job back (2026-08-18)

§14 changed the hue and fixed the "washed out" complaint. Driving it surfaced the next one: *"way
better, but the colors still look off — it needs to separate further."* Two distinct causes, both
mechanical.

**Cause one: every shadow in the app was invisible.** `CardShadowCaster` fills with `PageBg` and
`CardShadow` casts `#000000`. §14 set `PageBg` to `#000000`. A black shadow on a black page darkens
nothing, so the depth model documented in §7.5 was silently doing zero work — cards, hero and rows
were separated by a 1.13 lightness step and a `#2A2A2A` hairline and nothing else. `PageBg` lifted
to `#0A0A0A`, `Border` to `#303030`. **`PageBg` and `CardShadow` are one decision**: a future wave
that wants true black back has to replace the drop shadow with a light rim in the same change.

**Cause two: inputs sat above their containers.** Every text box, combo and quiet button rendered
*lighter* than the card holding it, so the alarm composer read as one flat slab with faint outlines
on it. `InteractiveBg` now sits below `CardBg` — inputs are wells, cards are surfaces, the hero is
raised. The ladder runs both ways from the card instead of only up.

**And the accent had six jobs.** Gold filled the Add button, the selected tab, the next-alarm pip
*and* all six alarm toggles. At six alarms that is a column of gold pills down the right edge, which
is both visually loud and semantically empty — if everything is gold, gold marks nothing. `StateOn`
`#3FB950` takes the armed-alarm job, `Danger` takes destructive-on-hover, gold keeps action and
attention. `DangerAction` is hover-only on purpose: six permanently red delete buttons read as six
errors.

**Untouched again:** tabs, hero countdown, spacing, scrollbar and typography. This wave is
`Resources/tokens.xaml`, one style attribute in `MainWindow.xaml`, and this spec.

**The rule to keep:** before tuning a colour, check whether the mechanism that colour feeds is
running at all. Two waves of palette work sat on top of a shadow system that could not render.

## 16. Fifth wave — the gold comes back, and the grey comes down (2026-08-18)

Driven and reported as *"way better"*, with two specific objections. Both were previewed in HTML
before any code changed — three rejected waves was enough evidence that iterating through the
compiler is the slow way to make a colour decision.

**The gold toggle is restored, and §15's reasoning is withdrawn.** Splitting the armed-alarm state
onto a green was an argument about semantics that Malin never asked for, and she likes the gold. What
actually made six gold toggles read as wallpaper was the *shape*, not the hue: six filled pills. The
shipped toggle is a `#050505` well with a 2px gold ring, an `AccentStrong` knob and a soft gold halo
— six lit outlines rather than six gold slabs. `StateOn`/`StateOnStrong` are deleted rather than left
unused.

**Depth: deeper shadow plus a lit top edge.** `CardShadow` went to blur 30 / depth 8 / 95%, and
`CardBg` and `ElevatedBg` became gradients whose top 3% carries a highlight. Shadow says "below", the
highlight says "above"; together the card reads as an object catching light. The 3% stop is load
bearing — it keeps the highlight inside `CardPadding` so no text renders on it, which is what lets
the §8 table measure against the flat base colours. A softer wash was tried first and dropped
`TextFaint` to 4.37 on the card.

**`BorderControl` went DOWN, not up.** Every previous wave answered "the field border looks dull" by
brightening it, which is how it reached neutral `#7A7A7A` and still read washed out. That was the
same error as wave one, one axis over: 1.4.11 asks for 3:1 and the token was sitting at 4.50 against
its fill, loud enough to compete with the content it framed. It is now `#78726B` — warm graphite,
11 points between red and blue where the rejected tan had 45, at 3.18:1 on its tightest surface.
**Chrome sits just above the gate; the bright moment is the gold focus ring.**

A dimmer `#6B6560` was previewed and measured well against the field fill, but the same token is
also drawn on `ElevatedBg`, where it falls to 2.63 and fails 1.4.11. `#78726B` is the dimmest warm
graphite that clears every surface it renders on.

**The rule to keep:** a control-boundary token is measured against *every* surface it is drawn on,
not the one you happened to preview it against.

## 17. Sixth wave — one measure (2026-08-18)

Not a palette wave. With the colours settled, the remaining complaint was proportion: on a widened
window the `Sound` and `Repeat` combos grew to 400px to hold the word "Once", and the composer card
read as stretched.

**The cap moved down three layers before it landed.** Each attempt fixed the previous complaint and
created the next one, which is worth recording because the failure was always the same shape — the
cap was on the wrong thing:

| Layer | What it did | Why it failed |
|---|---|---|
| The **column** (`ColumnDefinition MaxWidth`) | fields stop growing | pinned them to the left edge with dead space beside them — "static and weird" |
| The **form** (`StackPanel MaxWidth`) | form centres in the card | left a full-width card with a small form floating in it — "not meant to be there" |
| The **content panel** ✔ | card *and* rows cap and centre together | the card hugs its contents; the leftover space is page |

**Shipped: `MaxWidth="502"` on `QuickPanel` and `DayPanel`** — the two content `StackPanel`s inside
the tab `ScrollViewer`s. 502 is the 470 the form wants plus `CardPadding` on both sides. Below that
width every surface fills the window exactly as it did before; above it, the column stops and the
window grows around it.

**`HorizontalAlignment` stays `Stretch`, and that is the mechanism, not an oversight.** A `Stretch`
element constrained by `MaxWidth` is *centred* by WPF's arrange pass. `Left` would size the panel to
its children's desired width instead, and every star column inside it would collapse — star widths
contribute nothing to desired width.

The tab strip, the running strip and the bottom note bars sit outside these panels and keep the full
window width, so the window still reads as a window rather than a floating panel.

**The rule to keep:** when a layout looks wrong after a cap, check which layer the cap is on before
changing its value. Three of the four attempts here used a perfectly reasonable number on the wrong
element.
