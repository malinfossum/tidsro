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
| `PanelBg` | `#0A0F14` | `#070505` | Recessed areas. |
| `CardBg` | `#161D27` | `#17110D` | Cards and alarm rows. |
| `InteractiveBg` | `#1F2832` | `#1E1611` | Text boxes, combo boxes, quiet buttons. |
| `InteractiveHover` | `#28323E` | `#2A1F17` | Hover state for the above. |
| `ElevatedBg` | `#232C38` | `#241A14` | Hero card, next-alarm row, popup surfaces. |

The old `CardBg` and `ElevatedBg` were close enough to read as one plane. The new ladder makes
each step visible, so depth comes from layering rather than from shadows — consistent with the
established rule that dark UIs should layer chrome rather than paint everything one value.

### Text — warm, not blue-white

| Token | Old | New |
|---|---|---|
| `Text` | `#F4F7FA` | `#F7F1E8` |
| `TextMuted` | `#B4BDC7` | `#BCA894` |
| `TextFaint` | `#87919C` | `#94826F` |

Blue-white text on warm surfaces is a large part of what makes a warm palette still feel clinical.
`TextFaint` is set at `#94826F` rather than the first-drafted `#8A7867`, which measured 4.42:1 on
`CardBg` and would have failed AA for body text.

### Lines — split by job

| Token | Old | New | Role |
|---|---|---|---|
| `Border` | `#2B3440` | `#33251D` | Decorative card and divider edges. Name kept — it has 16 consumers, and renaming it to `BorderSubtle` would mean 16 edits for no functional gain. |
| `BorderControl` | *(new)* | `#7E5F48` | Text boxes, toggles, buttons, scrollbar thumb — anywhere the outline *is* the affordance. |

Rationale in §8.

### Accent and semantics

| Token | Old | New | Note |
|---|---|---|---|
| `Accent` | `#E3B341` | `#E0A93C` | Brass. |
| `AccentStrong` | `#ECC25A` | `#F2C05A` | Hover and focus ring base. |
| `FocusRing` | `#99E3B341` | `#99E0A93C` | Unchanged role, retuned to the new accent. |
| `Danger` | `#A1837F` | `#C4685C` | The one semantic token with real consumers. |

**Deleted:** `Success`, `Warning`, `Info`, `AccentSoft`, `BorderSoft`, `CardShadow`. All have zero
consumers today. Re-saturating colours nothing renders is busywork, and leaving them invites a
future reader to assume the app has a semantic colour system when it does not. A later slice that
genuinely needs a semantic colour should add it together with its consumer. This is the one
decision in this spec that is purely a judgement call and is easy to reverse.

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
rounding error on download size.

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

Weight becomes a hierarchy tool for the first time: SemiBold 600 for the selected tab, headings,
and primary buttons; Regular 400 everywhere else.

## 7. Depth and hierarchy

Four targeted view changes. Everything else inherits from tokens.

1. **Hero countdown.** The running timer's remaining time renders at `TextHero` in Plex Mono on an
   `ElevatedBg` card at the top of the Quick timers panel, with its label above in `TextFaint`
   uppercase and its finish time below in `TextMuted`. One element dominates the screen.
2. **The next alarm earns emphasis.** Today it carries a small accent dot. It additionally takes
   `ElevatedBg` and a `BorderControl` edge, so "what is coming next" is legible at a glance rather
   than being one row among six.
3. **The surface ladder is applied deliberately** — page, card, control, elevated — rather than
   every container defaulting to `CardBg` with one border.
4. **Scrollbar restyle.** A `ScrollBar` template in `tokens.xaml`: slim track on `PanelBg`, rounded
   thumb in `BorderControl`, hover to `TextFaint`, no stepper buttons. `ScrollBar` is an ordinary
   WPF control and retemplates exactly like the `TextBox`, `ComboBox` and `TabItem` styles already
   in the file — it is not native Windows chrome.

## 8. Accessibility

Contrast ratios computed against the new tokens (WCAG 2.1 relative luminance):

| Pair | Ratio | Level |
|---|---|---|
| `Text` on `PageBg` | 17.70 | AAA |
| `Text` on `CardBg` | 16.66 | AAA |
| `TextMuted` on `CardBg` | 8.17 | AAA |
| `TextFaint` on `CardBg` | 5.06 | AA |
| `Accent` on `PageBg` | 9.38 | AAA |
| Button text `#160F0B` on `Accent` | 8.95 | AAA |
| `Danger` on `CardBg` | 4.88 | AA |

**Why borders are split.** WCAG 1.4.11 requires 3:1 for visual information that identifies a user
interface component. Meeting that on every decorative card edge requires roughly `#7A5B45`, a
prominent tan outline that fights the calm the app is built around. The requirement applies to
boundaries that identify controls, not to ornament — so `BorderControl #7E5F48` (3.04:1 on
`CardBg`) is used for text boxes, toggles, buttons and the scrollbar thumb, while `Border`
stays quiet on card edges, where the surface step and the content already identify the container.

This **fixes a pre-existing failure**: today's `Border #2B3440` on `InteractiveBg #1F2832` measures
about 1.3:1, so input outlines are effectively invisible. The redesign is the natural moment.

Unchanged and protected: every accessible name; the keyboard-only focus ring (`ActionFocusVisual`,
retuned to the new accent but structurally identical); the reduced-motion gating in code-behind;
and state being conveyed by more than colour — the selected tab keeps its underline, the toggle
keeps its thumb position, the next alarm keeps its dot.

## 9. Files touched

- `src/Tidsro/Resources/tokens.xaml` — palette, fonts, `TextHero`, scrollbar style, token deletions.
- `src/Tidsro/Views/MainWindow.xaml` — hero countdown, next-alarm emphasis, surface ladder.
- `src/Tidsro/Assets/fonts/` *(new)* — four font files plus `OFL.txt`.
- `src/Tidsro/Tidsro.csproj` — font resource glob.
- `installer/` — ship the licence file.
- Popup, settings and dialog views — only where a hard-coded value bypasses a token.

## 10. Verification

- The 321 existing tests must stay green. This slice changes no view-model logic, so any failure
  means something was touched that should not have been.
- **A manual pass is mandatory before merge, and it must click things.** The tab-shell slice merged
  a control that did not respond to the mouse past every per-task review, the whole-branch review,
  and a green suite; only driving the app caught it. Restyling `TabItem`, `ScrollBar`, `CheckBox`
  and `Button` templates is exactly the kind of change that can silently break hit-testing or
  focus. Every restyled control gets clicked, and every input gets keyboard focus.
- Re-read the UIA tree and diff the accessible names against the current build. They must match
  exactly.
- Confirm the embedded fonts actually load — verify by rendering, not by reading the XAML, since
  the entire premise of this redesign is that a font reference silently fell back for months.
- Recompute the contrast table against the values as implemented.

## 11. Open questions

None. The one reversible judgement call is the deletion of unused tokens in §5.
