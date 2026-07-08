# Tidsro brand identity — implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Tidsro's stock-clock icon with the approved "sand into forest" mark and ship the surrounding brand assets (logo mark, wordmark lockup, social card, README header).

**Architecture:** Pure asset swap. The app already references one icon file (`Assets\icons\tidsro.ico`) from every touch-point, so replacing the source SVG + regenerating the `.ico` updates the whole app with **no code changes**. Brand-only assets (bare mark, lockup, social card) live under `docs/brand/`. Text-bearing assets (lockup, social card) are rendered to PNG from HTML artboards using Cinzel via Google Fonts, so no font install or path-outlining is needed.

**Tech Stack:** SVG, ImageMagick 7 (`magick`), Microsoft Edge headless (HTML→PNG), .NET 10 / WPF (build only, to verify the icon embeds).

## Global Constraints

- **Palette (exact):** page `#0E141A`, tile `#131820`, gold `#E3B341`, gold+ `#ECC25A`, text `#F4F7FA`. The mark uses gold only.
- **Zero application code changes.** Every icon reference already points at `Assets\icons\tidsro.ico` — keep that path. Do not edit `Tidsro.csproj`, `MainWindow.xaml`, `TrayBuilder.cs`, or `installer/Tidsro.iss`.
- **Mark geometry:** use the exact 256×256 SVGs in this plan (from the spec). Gold `#E3B341`; tile `#131820`.
- **Wordmark:** Cinzel (SIL OFL), uppercase → renders `TIDSRO`, colour `#F4F7FA`, letter-spacing `0.04em`. Loaded via Google Fonts in HTML artboards; shipped as PNG (no outlining, no system install).
- **Bare mark is dark-surface-only** (the gold goes faint on light). The tiled icon is for the OS.
- **Gotchas:** a running `Tidsro.exe` locks the build output — stop it before `dotnet build` (`Get-Process Tidsro | Stop-Process -Force`). Launch the built exe with `Start-Process`; background `dotnet run` is flaky.
- **Commits:** conventional style, no `Co-Authored-By`, no Claude attribution. Branch: `feat/brand-identity`.

---

### Task 1: Replace the app-icon source SVG (tiled)

**Files:**
- Modify: `src/Tidsro/Assets/icons/tidsro.svg` (overwrite the old clock)

- [ ] **Step 1: Overwrite the file with the tiled mark**

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

- [ ] **Step 2: Render a preview PNG and eyeball it**

Run (PowerShell):
```powershell
magick -background none src\Tidsro\Assets\icons\tidsro.svg -resize 256x256 "$env:TEMP\tidsro-check.png"; Start-Process "$env:TEMP\tidsro-check.png"
```
Expected: a gold hourglass with a small pine in the lower bulb on a dark rounded tile. If the strokes look broken (ImageMagick's SVG renderer can be weak), note it — Task 2 has an Edge-based fallback that also covers this.

- [ ] **Step 3: Commit**

```bash
git add src/Tidsro/Assets/icons/tidsro.svg
git commit -m "feat(brand): replace clock icon with the pine-in-hourglass mark"
```

---

### Task 2: Regenerate the multi-size `.ico`

**Files:**
- Modify: `src/Tidsro/Assets/icons/tidsro.ico` (regenerate from the new SVG)

**Interfaces:**
- Consumes: `tidsro.svg` from Task 1.
- Produces: a valid multi-size `.ico` at the unchanged path used by `Tidsro.csproj`, `MainWindow.xaml`, `TrayBuilder.cs`, and `installer/Tidsro.iss`.

- [ ] **Step 1: Build the `.ico` directly from the SVG**

Run (PowerShell, from repo root):
```powershell
magick -background none src\Tidsro\Assets\icons\tidsro.svg -define icon:auto-resize=256,128,64,48,32,16 src\Tidsro\Assets\icons\tidsro.ico
```

- [ ] **Step 2: Verify it contains all six sizes**

Run: `magick identify src\Tidsro\Assets\icons\tidsro.ico`
Expected: six lines, one per frame, at 256, 128, 64, 48, 32, and 16 px.

- [ ] **Step 3 (fallback, only if Step 1 output looks poor): build the `.ico` from a crisp Edge render**

If the strokes/pine are mangled, render a clean 256 PNG with Edge, then assemble the `.ico` from it:
```powershell
& "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe" --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 --window-size=256,256 --default-background-color=00000000 --screenshot="$env:TEMP\tidsro-256.png" "$((Resolve-Path src\Tidsro\Assets\icons\tidsro.svg).Path)"
magick "$env:TEMP\tidsro-256.png" -define icon:auto-resize=256,128,64,48,32,16 src\Tidsro\Assets\icons\tidsro.ico
```

- [ ] **Step 4: Confirm the app still builds with the new icon embedded**

Run (PowerShell):
```powershell
Get-Process Tidsro -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build src\Tidsro\Tidsro.csproj -c Debug -v minimal
```
Expected: `Build succeeded`, 0 errors (the `.ico` is embedded as both `ApplicationIcon` and a `Resource`; a malformed `.ico` fails the build).

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/Assets/icons/tidsro.ico
git commit -m "feat(brand): regenerate multi-size app icon from the new mark"
```

---

### Task 3: Add the bare logo mark

**Files:**
- Create: `docs/brand/tidsro-mark.svg`

- [ ] **Step 1: Create the file (mark without the tile)**

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

- [ ] **Step 2: Verify it renders (on a dark check background)**

Run: `magick -background "#0E141A" docs\brand\tidsro-mark.svg -resize 200x200 "$env:TEMP\mark-check.png"; Start-Process "$env:TEMP\mark-check.png"`
Expected: the gold hourglass-and-pine, no tile, on dark.

- [ ] **Step 3: Commit**

```bash
git add docs/brand/tidsro-mark.svg
git commit -m "docs(brand): add the box-less logo mark"
```

---

### Task 4: Build the horizontal lockup PNG

**Files:**
- Create: `docs/brand/tidsro-lockup.html` (editable source artboard)
- Create: `docs/brand/tidsro-lockup.png` (shipped asset, ~2000×520)

**Interfaces:**
- Consumes: the bare-mark geometry (inlined below).
- Produces: `docs/brand/tidsro-lockup.png`, used by Task 6 (README).

- [ ] **Step 1: Create the artboard HTML**

`docs/brand/tidsro-lockup.html`:
```html
<!doctype html>
<html><head><meta charset="utf-8">
<style>
@import url('https://fonts.googleapis.com/css2?family=Cinzel:wght@600&display=swap');
html,body{margin:0;padding:0;}
.stage{width:1000px;height:260px;background:#0E141A;display:flex;align-items:center;justify-content:center;gap:44px;}
.mk{width:148px;height:148px;}
.wm{font-family:'Cinzel',serif;font-weight:600;color:#F4F7FA;letter-spacing:.04em;font-size:104px;line-height:1;}
</style></head>
<body><div class="stage">
<svg class="mk" viewBox="0 0 256 256">
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
<span class="wm">Tidsro</span>
</div></body></html>
```

- [ ] **Step 2: Render to PNG at 2× (crisp)**

Run (PowerShell, from repo root):
```powershell
& "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe" --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=2 --window-size=1000,260 --screenshot="docs\brand\tidsro-lockup.png" "$((Resolve-Path docs\brand\tidsro-lockup.html).Path)"
```

- [ ] **Step 3: Verify the PNG**

Run: `magick identify docs\brand\tidsro-lockup.png`
Expected: `2000x520`. Then `Start-Process docs\brand\tidsro-lockup.png` — the bare gold mark beside `TIDSRO` in Cinzel, off-white on the dark banner. (If the wordmark shows in a fallback serif, Edge didn't fetch Google Fonts — confirm network and re-run.)

- [ ] **Step 4: Commit**

```bash
git add docs/brand/tidsro-lockup.html docs/brand/tidsro-lockup.png
git commit -m "docs(brand): add the horizontal wordmark lockup"
```

---

### Task 5: Build the social-preview card PNG

**Files:**
- Create: `docs/brand/social-preview.html` (editable source artboard)
- Create: `docs/brand/social-preview.png` (shipped asset, 1280×640)

- [ ] **Step 1: Create the artboard HTML**

`docs/brand/social-preview.html`:
```html
<!doctype html>
<html><head><meta charset="utf-8">
<style>
@import url('https://fonts.googleapis.com/css2?family=Cinzel:wght@600&display=swap');
html,body{margin:0;padding:0;}
.stage{width:1280px;height:640px;background:#0E141A;display:flex;flex-direction:column;align-items:center;justify-content:center;}
.mk{width:168px;height:168px;margin-bottom:28px;}
.wm{font-family:'Cinzel',serif;font-weight:600;color:#F4F7FA;letter-spacing:.04em;font-size:132px;line-height:1;margin:0;}
.t1{font-family:Georgia,serif;color:#F4F7FA;font-size:36px;margin:30px 0 6px;}
.t2{font-family:Georgia,serif;color:#87919C;font-size:25px;margin:0;}
.rule{width:84px;height:3px;background:#E3B341;margin:36px 0 20px;border-radius:2px;}
.repo{font-family:Consolas,monospace;color:#87919C;font-size:22px;}
</style></head>
<body><div class="stage">
<svg class="mk" viewBox="0 0 256 256">
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
<div class="wm">Tidsro</div>
<div class="t1">Calm time.</div>
<div class="t2">There when you need it, gone when you don't.</div>
<div class="rule"></div>
<div class="repo">github.com/malinfossum/tidsro</div>
</div></body></html>
```

- [ ] **Step 2: Render to PNG at exactly 1280×640**

Run (PowerShell, from repo root):
```powershell
& "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe" --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 --window-size=1280,640 --screenshot="docs\brand\social-preview.png" "$((Resolve-Path docs\brand\social-preview.html).Path)"
```

- [ ] **Step 3: Verify the PNG**

Run: `magick identify docs\brand\social-preview.png`
Expected: `1280x640`. Then `Start-Process docs\brand\social-preview.png` and eyeball: stacked mark + `TIDSRO`, "Calm time." tagline, gold rule, repo URL, all on `#0E141A`.

- [ ] **Step 4: Commit**

```bash
git add docs/brand/social-preview.html docs/brand/social-preview.png
git commit -m "docs(brand): add the palette-correct social preview card"
```

- [ ] **Step 5: (Manual, flag for Malin) Upload the social preview**

This is a GitHub web step, not scriptable here: repo **Settings → General → Social preview → Upload** `docs/brand/social-preview.png`. Leave a note in the PR description so it isn't missed.

---

### Task 6: Add the lockup to the README header

**Files:**
- Modify: `README.md` (top of file)

- [ ] **Step 1: Insert the centered lockup above the `# Tidsro` heading**

At the very top of `README.md`, before `# Tidsro`, add:
```markdown
<p align="center">
  <img src="docs/brand/tidsro-lockup.png" alt="Tidsro" width="480">
</p>
```
Leave the existing `# Tidsro` line and the description paragraph as they are.

- [ ] **Step 2: Verify the reference resolves**

Run: `Test-Path docs\brand\tidsro-lockup.png`
Expected: `True` (the image committed in Task 4 exists at the referenced path).

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: add brand lockup to the README header"
```

> Optional, only if Malin approves separately: the README status line still reads "v1.4.0 is released" (shipped is v1.5.0). Out of scope for this plan; do not change it here unless told to.

---

### Task 7: Visual acceptance (the real gate)

**Files:** none (verification only).

- [ ] **Step 1: Build and launch the app on the new icon**

Run (PowerShell):
```powershell
Get-Process Tidsro -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build src\Tidsro\Tidsro.csproj -c Debug -v minimal
Start-Process (Resolve-Path src\Tidsro\bin\Debug\net10.0-windows\Tidsro.exe).Path
```

- [ ] **Step 2: Eyeball the icon everywhere it appears**

Confirm the new pine-in-hourglass mark shows in: the **taskbar**, the **system tray** (bottom-right), and the **window title bar** (open the window from the tray). Confirm it reads as a small hourglass at tray size, not a smudge. Confirm the app otherwise behaves exactly as before (no code changed).

- [ ] **Step 3: Confirm the test suite is still green**

Run: `dotnet test -v minimal`
Expected: all tests pass (no code changed, so this is a regression guard, not new coverage).

- [ ] **Step 4: Open the PR**

```bash
git push -u origin feat/brand-identity
gh pr create --title "Brand identity: pine-in-hourglass mark, wordmark, README + social card" --body "Implements docs/superpowers/specs/2026-07-08-tidsro-brand-identity-design.md. Asset-only, no code changes. Manual step after merge: upload docs/brand/social-preview.png as the repo Social preview."
```
Malin reviews and merges on GitHub.

---

## Self-Review

**Spec coverage:** app-icon SVG → Task 1; `.ico` → Task 2; bare mark → Task 3; lockup (+PNG) → Task 4; social card (+PNG) → Task 5; README header → Task 6. Success criteria (reads as timer, palette agreement, builds unchanged, README + social) → Task 7 + Tasks 4–6. All spec deliverables mapped.

**Placeholder scan:** every step has exact content — full SVG/HTML, exact `magick`/Edge/`dotnet`/`gh` commands, expected outputs. No TODO/TBD.

**Type/path consistency:** the mark geometry is byte-identical across Tasks 1, 3, 4, 5 and the spec. All paths (`src/Tidsro/Assets/icons/tidsro.{svg,ico}`, `docs/brand/*`) are consistent between the producing task and its consumer (Task 6 uses Task 4's PNG; the `.ico` path matches the unchanged app references).

**Deviation from spec (noted):** the lockup and social card ship as PNG rendered from HTML artboards (Cinzel via Google Fonts) rather than outlined SVGs — this avoids a system font install and path-outlining tooling while producing pixel-identical Cinzel. The HTML artboards are the editable source, committed alongside the PNGs.
