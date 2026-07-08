# Tidsro brand identity — design

**Date:** 2026-07-08
**Status:** Approved (design locked with Malin). Ready for implementation planning.
**Author:** Malin Fossum

## Goal

Give Tidsro a real, ownable brand identity: a distinctive mark, a wordmark, defined
lockups, and a locked palette — replacing the current stock-clock icon. The mark must read
**instantly as a timer/focus tool**, feel **calm and close to nature**, and stay **timeless**.
It must survive down to the 16px system tray, where Tidsro spends most of its life.

## Concept — "sand into forest"

A gold **hourglass** with a small **pine (spruce) growing in the lower bulb**, and a little
sand still falling from the top through the neck.

- The hourglass reads as *time / a timer* at a glance — the second-most-universal time symbol
  after the clock, but patient and contemplative where a clock is mechanical, and crucially
  **not** the stock-clock cliché every other timer app ships.
- The pine carries *calm, nature, and growth* — and the story of the product: **your focus
  time grows a forest** (the same emotional idea Forest built a focus app on).
- The name earns it: *Tidsro* = *tid* (time) + *ro* (calm/rest). The mark says both.

Rejected along the way (recorded so we don't revisit): a plain clock/progress ring (too
generic, "cold and mechanical"); still-water ripples (didn't read as a timer); meditation
hands (read as a wellness app, and the pressed-palm shape misread badly).

## The mark

Two forms of one mark. Both are gold `#E3B341` on the Tidsro palette. Geometry is defined on
a 256×256 canvas.

### App icon (tiled) — the OS icon

Used for the Windows application icon: `.ico`, taskbar, tray, installer, desktop shortcut,
and the window title bar. Keeps the dark rounded-square tile so the warm mid-toned gold
stays legible on **any** background (light taskbars, pale wallpapers) and reads instantly as
an app. This is the canonical source art for `tidsro.svg` → `tidsro.ico`.

```svg
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <rect x="10" y="10" width="236" height="236" rx="52" fill="#131820"/>
  <line x1="52" y1="46" x2="204" y2="46" stroke="#E3B341" stroke-width="10" stroke-linecap="round"/>
  <line x1="52" y1="210" x2="204" y2="210" stroke="#E3B341" stroke-width="10" stroke-linecap="round"/>
  <path d="M60,52 L196,52 L128,128 Z" fill="none" stroke="#E3B341" stroke-width="6" stroke-linejoin="round"/>
  <path d="M60,204 L196,204 L128,128 Z" fill="none" stroke="#E3B341" stroke-width="6" stroke-linejoin="round"/>
  <path d="M84,60 L172,60 L128,104 Z" fill="#E3B341" opacity="0.5"/>
  <circle cx="128" cy="114" r="3" fill="#E3B341"/>
  <circle cx="128" cy="124" r="2.5" fill="#E3B341"/>
  <path d="M118,158 L138,158 L128,140 Z" fill="#E3B341"/>
  <path d="M114,172 L142,172 L128,156 Z" fill="#E3B341"/>
  <path d="M109,186 L147,186 L128,170 Z" fill="#E3B341"/>
  <path d="M104,198 L152,198 L128,184 Z" fill="#E3B341"/>
  <line x1="128" y1="198" x2="128" y2="203" stroke="#E3B341" stroke-width="5" stroke-linecap="round"/>
</svg>
```

### Logo mark (bare) — the brand logo

The same mark with the tile removed (transparent). This is the primary **logo** for use on
dark surfaces: the README header, the social card, docs, and the wordmark lockup. Box-less,
it reads as a proper logo rather than a glyph-in-a-box.

Constraint: the bare gold mark is only for **dark** backgrounds. On light surfaces the warm
gold goes faint — there, use the tiled app icon instead. (This is why the OS icon keeps its
tile; do not ship a box-less OS icon.)

```svg
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <line x1="52" y1="46" x2="204" y2="46" stroke="#E3B341" stroke-width="10" stroke-linecap="round"/>
  <line x1="52" y1="210" x2="204" y2="210" stroke="#E3B341" stroke-width="10" stroke-linecap="round"/>
  <path d="M60,52 L196,52 L128,128 Z" fill="none" stroke="#E3B341" stroke-width="6" stroke-linejoin="round"/>
  <path d="M60,204 L196,204 L128,128 Z" fill="none" stroke="#E3B341" stroke-width="6" stroke-linejoin="round"/>
  <path d="M84,60 L172,60 L128,104 Z" fill="#E3B341" opacity="0.5"/>
  <circle cx="128" cy="114" r="3" fill="#E3B341"/>
  <circle cx="128" cy="124" r="2.5" fill="#E3B341"/>
  <path d="M118,158 L138,158 L128,140 Z" fill="#E3B341"/>
  <path d="M114,172 L142,172 L128,156 Z" fill="#E3B341"/>
  <path d="M109,186 L147,186 L128,170 Z" fill="#E3B341"/>
  <path d="M104,198 L152,198 L128,184 Z" fill="#E3B341"/>
  <line x1="128" y1="198" x2="128" y2="203" stroke="#E3B341" stroke-width="5" stroke-linecap="round"/>
</svg>
```

### Small-size behaviour

The mark is drawn to fill the tile so it holds up when tiny. Down to ~32px the spruce reads
as a tree; at 16px it simplifies to a filled base inside the hourglass — a clean fallback
that still reads as a timer. This is intended; do not add detail to "fix" 16px.

## Wordmark

**"Tidsro" set in Cinzel** (an inscriptional, carved-stone Roman face). Cinzel is
uppercase-only, so the wordmark renders **TIDSRO** — monumental, timeless, and carved,
echoing the calm/nature/old-world feeling without any fantasy-font costume.

- Colour: warm off-white `#F4F7FA`. Gold is reserved for the mark alone — the wordmark stays
  neutral. (Restraint is the point.)
- Tracking: ~0.04em letter-spacing (inscriptional caps want a little air).
- No flourishes. The sand-grain "i" idea was considered and **rejected** — cleaner without,
  and Cinzel is caps-only anyway.
- Licence: Cinzel is SIL Open Font License. In shipped SVG assets the wordmark is **converted
  to outlines (paths)** so there is no runtime font dependency and it renders identically
  everywhere (GitHub, browsers).

## Lockups

- **Horizontal (primary):** bare logo mark, then TIDSRO to its right, vertically centred.
  For the README header and any wide placement.
- **Stacked:** mark centred above TIDSRO. For square/vertical placements and the social card.
- **Clear space:** keep padding around the lockup of at least the cap-height of TIDSRO on all
  sides.
- The **box-less** mark is the logo; the **tiled** mark is the OS icon. Never use the tiled
  icon inside a lockup next to the wordmark.

## Palette (locked — Tidsro's own)

| Token | Hex | Use |
|---|---|---|
| Page | `#0E141A` | Brand background (dark surfaces, cards) |
| Tile | `#131820` | The app-icon rounded-square tile |
| Gold | `#E3B341` | The mark; the app's live UI accent |
| Gold+ | `#ECC25A` | Hover/emphasis variant |
| Text | `#F4F7FA` | Wordmark, primary text on dark |

The gold **is** the app's in-UI accent (`Accent` in `tokens.xaml`), so the brand and product
stay in sync — this is deliberately unchanged. Tidsro shares only a *family feeling* (warm
gold on deep dark, calm, restrained) with Malin's personal mark; it is **not** the personal
palette (`#0a0908` / `#c9a96e` + Palatino) and must not drift toward it.

## Deliverables

All within the `tidsro` repo. **No application code changes** — every icon reference already
points at `Assets\icons\tidsro.ico`, so swapping the asset files is sufficient.

1. **`src/Tidsro/Assets/icons/tidsro.svg`** — replace the old clock with the tiled app-icon
   art above.
2. **`src/Tidsro/Assets/icons/tidsro.ico`** — regenerate from the new `tidsro.svg`, multi-size
   (256/128/64/48/32/16), via ImageMagick:
   `magick tidsro.svg -define icon:auto-resize=256,128,64,48,32,16 tidsro.ico`.
   Consumed unchanged by: `Tidsro.csproj` (`ApplicationIcon` + `Resource`),
   `Views/MainWindow.xaml` (`Icon="pack://…/tidsro.ico"`), `Services/TrayBuilder.cs`
   (tray `BitmapImage`), and `installer/Tidsro.iss` (`SetupIconFile`).
3. **`docs/brand/tidsro-mark.svg`** — new: the bare logo mark (transparent).
4. **`docs/brand/tidsro-lockup.svg`** (+ a PNG export) — new: the horizontal lockup, mark +
   TIDSRO (Cinzel outlined to paths). Used in the README header.
5. **`docs/brand/social-preview.svg`** (+ a 1280×640 PNG) — new: stacked lockup + the
   tagline "Calm time — there when you need it, gone when you don't", in `#0E141A`/`#E3B341`.
   Uploaded as the GitHub repo social preview (Settings → Social preview).
6. **`README.md`** — add the lockup image to the top of the header.

## Out of scope (flag, don't fix here)

- The existing `social-previews\tidsro.png` (outside this repo, in `Development\social-previews\`)
  was generated from the **personal** palette by `generate.ps1`. We supersede it with the
  in-repo `docs/brand/social-preview` asset above rather than editing the external personal
  generator. Leaving the personal kit untouched is intentional.
- `README.md` currently says "**v1.4.0 is released**" (line ~18); the shipped version is
  v1.5.0. Pre-existing doc drift, unrelated to branding — offer to fix it in passing during
  the README edit, but it is not part of this design.
- A hand-simplified 16px `.ico` entry (bolder strokes) if the auto-resized 16px reads too
  thin — optional follow-up, decided after seeing the regenerated `.ico`.

## Success criteria

- The mark reads as a timer in a squint, and is clearly a pine-in-hourglass at ≥32px.
- Icon, app UI, and social card all agree on `#0E141A`/`#E3B341`.
- App builds and runs unchanged; the new icon shows in the window, tray, taskbar, and
  installer.
- The README header shows the box-less lockup; the repo social preview uses the true palette.
