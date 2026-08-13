# Tidsro walnut redesign — implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebase Tidsro on a warm walnut-and-brass palette with embedded IBM Plex fonts, a hero countdown, and a restyled scrollbar — without changing behaviour, schema, or accessible names.

**Architecture:** Almost everything flows from `Resources/tokens.xaml`. Colours that a `ControlTemplate` animates are promoted to `Color` resources so storyboards can reference tokens instead of literals — that is the fix for the class of bug where a hard-coded `To="#E3B341"` silently keeps the old palette. Views change only where hierarchy needs it: a hero countdown on Quick timers, emphasis on the next alarm, and the running strip hiding when the hero already shows the same value.

**Tech Stack:** C# / .NET 10 WPF, CommunityToolkit.Mvvm, xUnit. No new package references.

Spec: `docs/superpowers/specs/2026-08-13-tidsro-walnut-redesign-design.md`

## Global Constraints

- **Accessible names are frozen.** Every existing `AutomationProperties.Name` keeps its exact current string. The hero countdown is the single permitted addition.
- **No schema change.** `TidsroData` stays at schema 4. No migration, no `AppSettings` field.
- **No behaviour change.** No scheduling, persistence, sound, or command logic is touched.
- **321 tests must stay green** except where this plan explicitly changes an assertion (Task 4).
- **No hex literal outside a token definition.** `#000000` inside a `DropShadowEffect` is the sole exception.
- **The app is dark-mode-only.** No light theme, no runtime theme switching.
- **Never force-kill Tidsro to test.** Close it from the tray so it saves; back up `%AppData%\Tidsro\data.json` first.
- Commits carry **no `Co-Authored-By` trailer and no Claude attribution**.

## File structure

| File | Responsibility |
|---|---|
| `src/Tidsro/Resources/tokens.xaml` | All colour, type, spacing, radius and motion tokens, plus every control template. The single place a palette change should need to touch. |
| `src/Tidsro/Assets/fonts/` *(new)* | The four IBM Plex font files and `OFL.txt`. |
| `src/Tidsro/Views/MainWindow.xaml` | Hero countdown, strip visibility, next-alarm emphasis. |
| `src/Tidsro/ViewModels/MainViewModel.cs` | `ShowStrip` gains a tab condition; `ShowHero` is added. Pure view-state, unit-testable. |
| `src/Tidsro/Views/SettingsWindow.xaml` | In-app font attribution, so the portable exe carries its own licence notice. |
| `tests/Tidsro.Tests/FontResourceTests.cs` *(new)* | Guards that the fonts are actually embedded — the failure mode that started this whole slice. |
| `THIRD-PARTY-NOTICES.md` *(new)* | Declares the OFL fonts against an Apache-2.0 repo. |

---

### Task 1: Embed the IBM Plex fonts

**Files:**
- Create: `src/Tidsro/Assets/fonts/IBMPlexSans-Regular.ttf`, `IBMPlexSans-SemiBold.ttf`, `IBMPlexMono-Regular.ttf`, `IBMPlexMono-Medium.ttf`, `OFL.txt`
- Create: `tests/Tidsro.Tests/FontResourceTests.cs`
- Modify: `src/Tidsro/Tidsro.csproj`
- Modify: `src/Tidsro/Resources/tokens.xaml:55-56`

**Interfaces:**
- Consumes: nothing.
- Produces: `FontSans` and `FontMono` resource keys resolving to embedded IBM Plex families. Every later task and every existing view uses these two keys unchanged.

- [ ] **Step 1: Fetch the fonts**

Download the IBM Plex release from `https://github.com/IBM/plex/releases` — pin one release tag rather than taking `master`. From that archive take exactly four files and the licence:

```
IBMPlexSans-Regular.ttf
IBMPlexSans-SemiBold.ttf
IBMPlexMono-Regular.ttf
IBMPlexMono-Medium.ttf
OFL.txt
```

Place them in `src/Tidsro/Assets/fonts/`.

- [ ] **Step 2: Record what shipped**

```bash
cd src/Tidsro/Assets/fonts && sha256sum *.ttf
```

Paste the four hashes and the release tag into the spec's §6, replacing the sentence that says to record them.

- [ ] **Step 3: Register the fonts as WPF resources**

Fonts need build action `Resource` (compiled into `Tidsro.g.resources`, reachable by pack URI), **not** `EmbeddedResource` like the chimes — a pack URI cannot see a manifest resource. Name the four files explicitly rather than globbing, so nothing else dropped into that folder is silently embedded in a distributed binary.

In `src/Tidsro/Tidsro.csproj`, after the existing `Assets\icons\tidsro.ico` `ItemGroup`:

```xml
  <ItemGroup>
    <!-- Fonts are Resource (not EmbeddedResource): pack:// URIs read Tidsro.g.resources, and a
         manifest resource is invisible to them. Listed by name so a stray file in this folder
         cannot end up inside a distributed binary. -->
    <Resource Include="Assets\fonts\IBMPlexSans-Regular.ttf" />
    <Resource Include="Assets\fonts\IBMPlexSans-SemiBold.ttf" />
    <Resource Include="Assets\fonts\IBMPlexMono-Regular.ttf" />
    <Resource Include="Assets\fonts\IBMPlexMono-Medium.ttf" />
  </ItemGroup>
```

- [ ] **Step 4: Write the failing test**

Create `tests/Tidsro.Tests/FontResourceTests.cs`:

```csharp
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Resources;
using Tidsro.Services;

namespace Tidsro.Tests;

// Guards the premise of the walnut redesign: FontSans used to name "Inter", which is not installed
// and was never embedded, so every user silently got Segoe UI for months. A missing font file is a
// silent visual fallback, never an error - so it has to be a test.
public class FontResourceTests
{
    private static readonly Assembly App = typeof(SoundService).Assembly;

    // WPF Resource items live in Tidsro.g.resources, and WPF lower-cases every key.
    private static List<string> ResourceKeys()
    {
        using var stream = App.GetManifestResourceStream("Tidsro.g.resources");
        Assert.NotNull(stream);
        using var reader = new ResourceReader(stream!);
        return reader.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToList();
    }

    [Theory]
    [InlineData("assets/fonts/ibmplexsans-regular.ttf")]
    [InlineData("assets/fonts/ibmplexsans-semibold.ttf")]
    [InlineData("assets/fonts/ibmplexmono-regular.ttf")]
    [InlineData("assets/fonts/ibmplexmono-medium.ttf")]
    public void Each_font_is_embedded_as_a_wpf_resource(string key)
    {
        Assert.Contains(key, ResourceKeys());
    }
}
```

- [ ] **Step 5: Run the test and watch it fail**

Run: `dotnet test --filter FullyQualifiedName~FontResourceTests`
Expected: 4 failures. If the fonts are not yet in place it fails on the `Assert.Contains`; if `Tidsro.g.resources` cannot be found the `Assert.NotNull` fails instead.

- [ ] **Step 6: Build so the resources are compiled, then re-run**

Run: `dotnet build src/Tidsro/Tidsro.csproj -c Debug` then `dotnet test --filter FullyQualifiedName~FontResourceTests`
Expected: PASS, 4 tests.

If a key is not found, print what is actually there before guessing — the lower-casing rule is the usual culprit:

```bash
dotnet test --filter FullyQualifiedName~FontResourceTests -v n
```

- [ ] **Step 7: Point the font tokens at the embedded families**

In `src/Tidsro/Resources/tokens.xaml`, replace lines 55-56:

```xml
  <FontFamily x:Key="FontSans">Inter, Segoe UI, sans-serif</FontFamily>
  <FontFamily x:Key="FontMono">Consolas, Menlo, monospace</FontFamily>
```

with:

```xml
  <!-- pack:// so the embedded family is used rather than a machine-installed one. The trailing
       slash before # is required. Segoe UI / Consolas stay as fallbacks so a packaging mistake
       degrades instead of crashing - but a fallback is exactly how "Inter" went unnoticed, so
       FontResourceTests and the manual pass both check the real family is in use. -->
  <FontFamily x:Key="FontSans">pack://application:,,,/Assets/fonts/#IBM Plex Sans, Segoe UI</FontFamily>
  <FontFamily x:Key="FontMono">pack://application:,,,/Assets/fonts/#IBM Plex Mono, Consolas</FontFamily>
```

- [ ] **Step 8: Confirm the app renders in Plex, not the fallback**

```bash
dotnet build src/Tidsro/Tidsro.csproj -c Debug
```

Then launch the built exe and look at the window. Plex Sans has a distinctly different lowercase `g` and `a` from Segoe UI; the countdown digits are visibly narrower than Consolas. If it looks unchanged, the pack URI is wrong — do not proceed, because that is the exact failure this redesign exists to fix.

- [ ] **Step 9: Run the full suite**

Run: `dotnet test`
Expected: 325 passed (321 existing + 4 new).

- [ ] **Step 10: Commit**

```bash
git add src/Tidsro/Assets/fonts src/Tidsro/Tidsro.csproj src/Tidsro/Resources/tokens.xaml tests/Tidsro.Tests/FontResourceTests.cs
git commit -m "feat(type): embed IBM Plex Sans and Mono

FontSans named Inter, which is not installed on Windows and was never embedded,
so every user has been reading Segoe UI since the token was written. Embedding
the family makes the app render the same everywhere, and FontResourceTests
guards it - a missing font is a silent fallback, never an error."
```

---

### Task 2: Licence obligations

**Files:**
- Create: `THIRD-PARTY-NOTICES.md`
- Modify: `README.md`
- Modify: `Tidsro.iss`
- Modify: `src/Tidsro/Views/SettingsWindow.xaml`

**Interfaces:**
- Consumes: `src/Tidsro/Assets/fonts/OFL.txt` from Task 1.
- Produces: nothing other tasks depend on.

OFL 1.1 requires the licence to travel with the fonts. `publish.ps1` emits a **standalone portable `Tidsro.exe`** with no companion files, so the installer alone is not enough — the attribution has to exist inside the app too.

- [ ] **Step 1: Create the notices file**

Create `THIRD-PARTY-NOTICES.md`:

```markdown
# Third-party notices

Tidsro itself is licensed under Apache-2.0 (see `LICENSE`). It bundles the following
third-party components, which are licensed separately.

## IBM Plex Sans, IBM Plex Mono

Copyright © 2017 IBM Corp. Licensed under the SIL Open Font License, Version 1.1.

The fonts are embedded, unmodified, in the application binary. The full licence text ships
as `src/Tidsro/Assets/fonts/OFL.txt` in this repository, is installed alongside the
application, and is reproduced in the app under Settings.

Licence: <https://openfontlicense.org>
Source: <https://github.com/IBM/plex>
```

- [ ] **Step 2: Note it in the README**

Add to the end of the README's licence section:

```markdown
Tidsro bundles IBM Plex Sans and IBM Plex Mono, licensed under the SIL Open Font License 1.1.
See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
```

- [ ] **Step 3: Ship the licence with the installer**

In `Tidsro.iss`, add to the `[Files]` section:

```
Source: "src\Tidsro\Assets\fonts\OFL.txt"; DestDir: "{app}"; DestName: "OFL-IBMPlex.txt"; Flags: ignoreversion
```

- [ ] **Step 4: Surface the attribution in the app**

The portable exe carries no files, so this is the only copy its users get. In `src/Tidsro/Views/SettingsWindow.xaml`, add above the Save/Cancel row:

```xml
    <TextBlock Text="Typeface: IBM Plex, © IBM Corp., SIL Open Font License 1.1"
               Foreground="{StaticResource TextFaint}" FontSize="{StaticResource TextXs}"
               TextWrapping="Wrap" Margin="0,16,0,0"
               AutomationProperties.Name="Typeface: IBM Plex, copyright IBM Corp., SIL Open Font License 1.1"/>
```

- [ ] **Step 5: Verify the installer still builds**

Run: `& './publish.ps1'`
Expected: `dist/Tidsro.exe` and `dist/Tidsro-Setup.exe` produced, no ISCC error. Confirm `OFL-IBMPlex.txt` appears in the install directory after running the installer.

Do **not** pass `-ExecutionPolicy Bypass`; the script runs directly.

- [ ] **Step 6: Commit**

```bash
git add THIRD-PARTY-NOTICES.md README.md Tidsro.iss src/Tidsro/Views/SettingsWindow.xaml
git commit -m "docs(licence): declare the bundled IBM Plex fonts

OFL 1.1 requires the licence to accompany the fonts wherever they are
redistributed, and publish.ps1 emits a portable exe with no companion files - so
the attribution also has to live inside the app, not only in the installer."
```

---

### Task 3: Repalette, and stop templates hard-coding colours

**Files:**
- Modify: `src/Tidsro/Resources/tokens.xaml:12-38` (tokens), `:70-71` (delete `CardShadow`), `:100`, `:108`, `:114`, `:151`, `:159`, `:165` (literals)
- Modify: `src/Tidsro/Views/MainWindow.xaml:118`, `:125`, `:130` (literals)

**Interfaces:**
- Consumes: nothing.
- Produces: token keys `PageBg`, `PanelBg`, `CardBg`, `ElevatedBg`, `InteractiveBg`, `InteractiveHover`, `Text`, `TextMuted`, `TextFaint`, `Border`, `BorderControl`, `Accent`, `AccentStrong`, `Danger`, plus `Color` resources `InteractiveBgColor`, `InteractiveHoverColor`, `TextColor`, `TextMutedColor`, `AccentColor`, `AccentStrongColor`. Tasks 4-6 use these names.

This task must land as one change: the palette and the literal conversion are the same deliverable. Swapping tokens alone leaves every gold button on the old gold.

- [ ] **Step 1: Check nothing looks up a doomed token by string**

`StaticResource` to a missing key throws `XamlParseException` when the *window loads*, not at build. `CompletionPopup.xaml.cs:60` already does `FindResource("DurationBase")`, which no compiler checks.

```bash
grep -rn "FindResource\|TryFindResource" src/Tidsro --include=*.cs
grep -rnE "StaticResource (Success|Warning|Info|AccentSoft|BorderSoft|BorderStrong|FocusRing|CardShadow)\}" src/Tidsro --include=*.xaml
```

Expected: the first prints only the `DurationBase` line (not on the deletion list). The second prints nothing. **If the second prints anything, stop** — that key has a live consumer and must not be deleted.

- [ ] **Step 2: Replace the colour tokens**

In `src/Tidsro/Resources/tokens.xaml`, replace everything from `<!-- Surfaces -->` through the `FocusRing` line (lines 12-38) with:

```xml
  <!-- Surfaces — four distinguishable layers. Depth comes from this ladder, not from shadows. -->
  <SolidColorBrush x:Key="PageBg"     Color="#0B0908"/>
  <SolidColorBrush x:Key="PanelBg"    Color="#070505"/>
  <SolidColorBrush x:Key="CardBg"     Color="#17110D"/>
  <SolidColorBrush x:Key="ElevatedBg" Color="#241A14"/>

  <!-- Any colour a ControlTemplate ANIMATES must exist as a Color resource. A Storyboard's To=
       takes a Color, so writing a literal there silently survives a palette change - which is
       exactly how the gold button kept its old gold. Colors are structs, so seeding an inline
       (unfrozen) brush from one keeps the brush animatable. -->
  <Color x:Key="InteractiveBgColor">#1E1611</Color>
  <Color x:Key="InteractiveHoverColor">#2A1F17</Color>
  <SolidColorBrush x:Key="InteractiveBg"    Color="{StaticResource InteractiveBgColor}"/>
  <SolidColorBrush x:Key="InteractiveHover" Color="{StaticResource InteractiveHoverColor}"/>

  <!-- Text — warm white, not blue-white: blue-white on warm surfaces is what keeps a "warm"
       palette feeling clinical. -->
  <Color x:Key="TextColor">#F7F1E8</Color>
  <Color x:Key="TextMutedColor">#BCA894</Color>
  <SolidColorBrush x:Key="Text"      Color="{StaticResource TextColor}"/>
  <SolidColorBrush x:Key="TextMuted" Color="{StaticResource TextMutedColor}"/>
  <SolidColorBrush x:Key="TextFaint" Color="#94826F"/>

  <!-- Lines, split by job. Border is ornament; BorderControl is the affordance, so it carries the
       3:1 non-text contrast WCAG 1.4.11 asks for. BorderControl on InteractiveBg is 3.07:1 - the
       tightest pair in the palette. Recompute it before nudging either value darker. -->
  <SolidColorBrush x:Key="Border"        Color="#33251D"/>
  <SolidColorBrush x:Key="BorderControl" Color="#7E5F48"/>

  <!-- Accent + semantic -->
  <Color x:Key="AccentColor">#E0A93C</Color>
  <Color x:Key="AccentStrongColor">#F2C05A</Color>
  <SolidColorBrush x:Key="Accent"       Color="{StaticResource AccentColor}"/>
  <SolidColorBrush x:Key="AccentStrong" Color="{StaticResource AccentStrongColor}"/>
  <SolidColorBrush x:Key="Danger"       Color="#C4685C"/>
```

`Success`, `Warning`, `Info`, `AccentSoft`, `BorderSoft`, `BorderStrong` and `FocusRing` are gone — all had zero consumers.

- [ ] **Step 3: Delete the unused shadow**

Remove lines 69-71 of `tokens.xaml`:

```xml
  <!-- Card drop shadow (shadow-sm) -->
  <DropShadowEffect x:Key="CardShadow" BlurRadius="24" ShadowDepth="8"
                    Direction="270" Opacity="0.18" Color="#000000"/>
```

`CompletionPopup.xaml:13` declares its own inline `DropShadowEffect` and is unaffected.

- [ ] **Step 4: Convert the six literals in tokens.xaml**

`QuietAction` template, line ~100:

```xml
            <Border.Background><SolidColorBrush Color="#1F2832"/></Border.Background>
```
becomes
```xml
            <Border.Background><SolidColorBrush Color="{StaticResource InteractiveBgColor}"/></Border.Background>
```

Line ~108, `To="#28323E"` becomes `To="{StaticResource InteractiveHoverColor}"`.
Line ~114, `To="#1F2832"` becomes `To="{StaticResource InteractiveBgColor}"`.

`GoldAction` template, line ~151:

```xml
            <Border.Background><SolidColorBrush Color="#E3B341"/></Border.Background>
```
becomes
```xml
            <Border.Background><SolidColorBrush Color="{StaticResource AccentColor}"/></Border.Background>
```

Line ~159, `To="#ECC25A"` becomes `To="{StaticResource AccentStrongColor}"`.
Line ~165, `To="#E3B341"` becomes `To="{StaticResource AccentColor}"`.

- [ ] **Step 5: Convert the three literals in MainWindow.xaml**

Line 118:

```xml
                      <TextBlock.Foreground><SolidColorBrush Color="#F4F7FA"/></TextBlock.Foreground>
```
becomes
```xml
                      <TextBlock.Foreground><SolidColorBrush Color="{StaticResource TextColor}"/></TextBlock.Foreground>
```

Line 125, `To="#B4BDC7"` becomes `To="{StaticResource TextMutedColor}"`.
Line 130, `To="#F4F7FA"` becomes `To="{StaticResource TextColor}"`.

- [ ] **Step 6: Give inputs the control border**

Input outlines currently sit at roughly 1.3:1 and are effectively invisible. Change these five to `{StaticResource BorderControl}` — note two are `Setter` form and three are attribute form:

| Location | Form | Control |
|---|---|---|
| `tokens.xaml:89` | `<Setter Property="BorderBrush" .../>` | `QuietAction` button |
| `tokens.xaml:181` | `<Setter Property="BorderBrush" .../>` | `TextBox` |
| `tokens.xaml:250` | attribute | `ComboBox` toggle |
| `tokens.xaml:295` | attribute | `DayChip` |
| `tokens.xaml:327` | attribute | `ToggleSwitch` track |

**Leave on `Border`:** `tokens.xaml:269` (the combo popup edge) and `tokens.xaml:395` (the tab strip's bottom rule). Both are ornament, not affordance.

Verify you caught the `Setter` form too — an attribute-only grep misses it:

```bash
grep -nE 'BorderBrush="\{StaticResource Border\}"|Property="BorderBrush" Value="\{StaticResource Border\}"' src/Tidsro/Resources/tokens.xaml
```

Expected after the change: only lines 269 and 395.

- [ ] **Step 7: Prove no literal survives**

```bash
grep -rn 'Color="#\|Background="#\|Foreground="#\|BorderBrush="#\|Fill="#\|To="#' src/Tidsro --include=*.xaml | grep -v 'x:Key=' | grep -v 'DropShadowEffect'
```

Expected: **no output.** Any hit is a control still wearing the old palette.

- [ ] **Step 8: Build, run the suite, open the app**

Run: `dotnet build src/Tidsro/Tidsro.csproj -c Debug && dotnet test`
Expected: build clean, 325 passed.

Then launch the built exe and confirm the primary **Start** button is brass `#E0A93C`, not the old `#E3B341`, and that quiet buttons are warm rather than blue-grey. A gold button still on the old gold means a literal was missed.

- [ ] **Step 9: Commit**

```bash
git add src/Tidsro/Resources/tokens.xaml src/Tidsro/Views/MainWindow.xaml
git commit -m "feat(theme): rebase on the walnut and brass palette

Cool blue-grey surfaces under a warm gold accent never resolved. Surfaces,
text and lines move to warm near-black, and the accent becomes brass.

Colours that ControlTemplates animate are promoted to Color resources: a
Storyboard's To= takes a Color, so the literals at tokens.xaml:108/114/159/165
would have kept the old palette on the most prominent controls in the app.
Border is split - ornament stays quiet, BorderControl carries the 3:1 the
affordance needs, which also fixes input outlines that sat near 1.3:1.

Drops eight tokens that had no consumers."
```

---

### Task 4: Hero countdown, and the strip that steps aside for it

**Files:**
- Modify: `src/Tidsro/ViewModels/MainViewModel.cs:77-92`
- Modify: `src/Tidsro/Views/MainWindow.xaml:33` (insert hero), `:296-300` (strip binding)
- Modify: `src/Tidsro/Resources/tokens.xaml` (add `TextHero`)
- Modify: `tests/Tidsro.Tests/MainViewModelTests.cs:1143-1152`

**Interfaces:**
- Consumes: `Accent`, `ElevatedBg`, `TextFaint`, `TextMuted`, `FontMono` from Tasks 1 and 3.
- Produces: `MainViewModel.ShowHero` (`bool`, true when a countdown is running) and a redefined `MainViewModel.ShowStrip` (`bool`, true when a countdown is running **and** the Quick timers tab is not selected).

On Quick timers the hero and the strip would show the same value — the same number twice, and two UIA elements reporting one piece of state. The strip exists to keep a running timer visible *from the Schedule tab*, so it hides when the hero is on screen.

- [ ] **Step 1: Write the failing test**

Add to `tests/Tidsro.Tests/MainViewModelTests.cs`, after `Strip_shows_the_countdown_that_finishes_soonest`:

```csharp
    // The hero on Quick timers shows the same countdown as the strip, so showing both would repeat
    // the value on screen and report one piece of state twice to a screen reader.
    [Fact]
    public void Strip_yields_to_the_hero_on_the_quick_timers_tab()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00"; vm.Label = "short"; vm.StartCustomCommand.Execute(null);

        vm.SelectedTabIndex = 0;
        Assert.True(vm.ShowHero);
        Assert.False(vm.ShowStrip);

        vm.SelectedTabIndex = 1;
        Assert.False(vm.ShowHero);
        Assert.True(vm.ShowStrip);
    }

    [Fact]
    public void Neither_hero_nor_strip_shows_without_a_countdown()
    {
        var vm = New(out _, out _);
        vm.SelectedTabIndex = 0;
        Assert.False(vm.ShowHero);
        Assert.False(vm.ShowStrip);
    }
```

- [ ] **Step 2: Fix the existing test that this redefines**

`Strip_shows_the_countdown_that_finishes_soonest` (line 1143) asserts `ShowStrip` is true while `SelectedTabIndex` is its default 0 — which now means the hero is showing instead. Add the tab switch so it tests the strip on the tab the strip is for:

```csharp
    [Fact]
    public void Strip_shows_the_countdown_that_finishes_soonest()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "30:00"; vm.Label = "long";  vm.StartCustomCommand.Execute(null);
        vm.CustomInput = "5:00";  vm.Label = "short"; vm.StartCustomCommand.Execute(null);
        vm.SelectedTabIndex = 1;   // the strip is for the Schedule tab; Quick timers has the hero

        Assert.True(vm.ShowStrip);
        Assert.Equal("Short", vm.StripTimer!.Label);
        Assert.Equal("+1 more", vm.StripExtraText);   // counts what the strip is NOT showing
    }
```

- [ ] **Step 3: Run the tests and watch them fail**

Run: `dotnet test --filter FullyQualifiedName~MainViewModelTests`
Expected: FAIL — `ShowHero` does not exist, so the test project does not compile. That is the expected red.

- [ ] **Step 4: Add the view-model properties**

In `src/Tidsro/ViewModels/MainViewModel.cs`, replace line 79:

```csharp
    public bool ShowStrip => StripTimer is not null;
```

with:

```csharp
    /// <summary>The hero countdown at the top of Quick timers. Same timer the strip would show.</summary>
    public bool ShowHero => StripTimer is not null && SelectedTabIndex == QuickTimersTab;

    /// <summary>The bottom strip exists to keep a running timer visible from the OTHER tab. On Quick
    /// timers the hero already shows it, and rendering both repeats the value on screen and reports
    /// one piece of state twice to a screen reader.</summary>
    public bool ShowStrip => StripTimer is not null && SelectedTabIndex != QuickTimersTab;

    private const int QuickTimersTab = 0;
```

`RefreshStrip()` (line 88) already raises `StripTimer`, `ShowStrip` and `StripExtraText`. Add one line to it:

```csharp
        OnPropertyChanged(nameof(ShowHero));
```

And add the tab-change hook next to the other `partial void On...Changed` methods:

```csharp
    // Both derived flags depend on the selected tab, and CommunityToolkit only raises
    // SelectedTabIndex itself.
    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(ShowHero));
        OnPropertyChanged(nameof(ShowStrip));
    }
```

If `RefreshStrip` already raises `ShowStrip`, keep it — just add `ShowHero` beside it.

- [ ] **Step 5: Run the tests and watch them pass**

Run: `dotnet test --filter FullyQualifiedName~MainViewModelTests`
Expected: PASS.

- [ ] **Step 6: Add the hero type size**

In `tokens.xaml`, after `<sys:Double x:Key="Text2xl">28</sys:Double>`:

```xml
  <sys:Double x:Key="TextHero">42</sys:Double>
```

- [ ] **Step 7: Add the hero card**

In `src/Tidsro/Views/MainWindow.xaml`, immediately inside `<StackPanel x:Name="QuickPanel">` (line 33), before the existing first `<Border>`:

```xml
        <!-- Hero countdown. The accessible name lives on the caption TextBlock: a Border has no
             automation peer. Deliberately NOT a live region - a countdown that announces every
             tick is unusable with a screen reader. -->
        <Border Background="{StaticResource ElevatedBg}" BorderBrush="{StaticResource Border}"
                BorderThickness="1" CornerRadius="{StaticResource RadiusMd}" Padding="16"
                Margin="0,0,0,12"
                Visibility="{Binding ShowHero, Converter={StaticResource BoolToVisible}}">
          <StackPanel>
            <TextBlock Text="RUNNING" Foreground="{StaticResource TextFaint}"
                       FontSize="{StaticResource TextXs}"
                       AutomationProperties.Name="Running timer"/>
            <TextBlock Text="{Binding StripTimer.RemainingText}" FontFamily="{StaticResource FontMono}"
                       FontSize="{StaticResource TextHero}" Foreground="{StaticResource Text}"
                       Margin="0,4,0,0"/>
            <TextBlock Text="{Binding StripTimer.Label}" Foreground="{StaticResource TextMuted}"
                       FontSize="{StaticResource TextXs}" TextTrimming="CharacterEllipsis"
                       Margin="0,2,0,0"
                       Visibility="{Binding StripTimer.Label, Converter={StaticResource NullToCollapsed}}"/>
          </StackPanel>
        </Border>
```

- [ ] **Step 8: Point the strip at the new flag**

`MainWindow.xaml:299` already reads `Visibility="{Binding ShowStrip, Converter={StaticResource BoolToVisible}}"`. `ShowStrip` now carries the tab condition, so **no markup change is needed** — confirm the line is unchanged and move on.

- [ ] **Step 9: Build, test, and look at it**

Run: `dotnet build src/Tidsro/Tidsro.csproj -c Debug && dotnet test`
Expected: build clean, 327 passed (325 + 2 new).

Launch the exe, start a 5-minute timer, and confirm: the hero shows on Quick timers with no strip beneath it, and switching to Schedule hides the hero and shows the strip.

- [ ] **Step 10: Commit**

```bash
git add src/Tidsro/ViewModels/MainViewModel.cs src/Tidsro/Views/MainWindow.xaml src/Tidsro/Resources/tokens.xaml tests/Tidsro.Tests/MainViewModelTests.cs
git commit -m "feat(main): give the running countdown a hero, and let the strip step aside

Every type size sat between 12 and 28, so nothing dominated and the eye had
nowhere to land. The running countdown now renders at 42 in an elevated card
at the top of Quick timers.

The strip exists to keep a running timer visible from the Schedule tab; on
Quick timers it would repeat the hero's value verbatim and report one piece of
state twice to a screen reader, so it hides while the hero is up."
```

---

### Task 5: Next-alarm emphasis and the surface ladder

**Files:**
- Modify: `src/Tidsro/Views/MainWindow.xaml` (alarm row template, card backgrounds)

**Interfaces:**
- Consumes: `ElevatedBg`, `CardBg`, `BorderControl`, `Border` from Task 3.
- Produces: nothing other tasks depend on.

- [ ] **Step 1: Find the alarm row template and its next-alarm trigger**

```bash
grep -n "IsNext" src/Tidsro/Views/MainWindow.xaml
```

The row already reacts to `IsNext` with an accent dot. That dot stays — it is the non-colour cue that keeps the state readable without relying on hue.

- [ ] **Step 2: Add surface and border emphasis to the next alarm**

In the alarm row's `ItemContainerStyle`/`DataTemplate` `Border`, add a trigger so the next alarm lifts a surface step:

```xml
                <DataTrigger Binding="{Binding IsNext}" Value="True">
                  <Setter TargetName="row" Property="Background" Value="{StaticResource ElevatedBg}"/>
                  <Setter TargetName="row" Property="BorderBrush" Value="{StaticResource BorderControl}"/>
                </DataTrigger>
```

Use the actual `x:Name` of the row `Border` in place of `row`. If the `Border` has no name, add `x:Name="row"` to it.

- [ ] **Step 3: Apply the surface ladder**

`MainWindow.xaml` sets `Background="{StaticResource CardBg}"` on 7 containers, which is why everything reads as one plane:

```bash
grep -n 'Background="{StaticResource CardBg}"' src/Tidsro/Views/MainWindow.xaml
```

Walk those 7 and assign by role rather than by default:

- **`CardBg`** — the ordinary content cards and alarm rows. Most stay here.
- **`ElevatedBg`** — anything that should sit above its neighbours: the hero card (already set in Task 4) and the next-alarm row (Step 2).
- **`PanelBg`** — recessed regions that frame content rather than being content. The running-timer strip is the candidate: it is chrome along the bottom edge, not a card.

Change only where the role is clearly different from `CardBg`; a container that genuinely is an ordinary card stays put. The goal is that page, card and elevated are each visibly distinct, not that every container gets a different value.

- [ ] **Step 4: Confirm the accessible name did not change**

The `IsNext` state is already carried in the composed row name (it ends with `, next`). Adding visual emphasis must not touch that string.

```bash
grep -n "AutomationProperties.Name" src/Tidsro/Views/MainWindow.xaml | head -20
```

- [ ] **Step 5: Build and look**

Run: `dotnet build src/Tidsro/Tidsro.csproj -c Debug && dotnet test`
Expected: build clean, 327 passed.

Launch and confirm the next alarm reads as lifted rather than as one row among six, that the dot is still present, and that page, card and elevated surfaces are each distinguishable.

- [ ] **Step 6: Commit**

```bash
git add src/Tidsro/Views/MainWindow.xaml
git commit -m "feat(schedule): lift the next alarm out of the list

A single accent dot was the only thing marking what fires next. The row now
also takes an elevated surface and the control border, so the most useful
piece of information on the tab is legible at a glance. The dot stays, so the
state is never carried by colour alone."
```

---

### Task 6: Restyle the scrollbar

**Files:**
- Modify: `src/Tidsro/Resources/tokens.xaml` (append a `ScrollBar` style)

**Interfaces:**
- Consumes: `PanelBg`, `BorderControl`, `TextFaint`, `DurationFast` from Task 3 and the existing motion tokens.
- Produces: nothing other tasks depend on.

`ScrollBar` is an ordinary WPF control, not native Windows chrome, so it retemplates exactly like the `TextBox` and `TabItem` styles already in this file.

- [ ] **Step 1: Add the style**

Append to `tokens.xaml`, before `</ResourceDictionary>`:

```xml
  <!-- Scrollbar. Stepper buttons are dropped deliberately: the lists are short and both the wheel
       and the keyboard scroll the panel, so the arrows are redundant here. The thumb keeps a 40px
       minimum length and the track a 16px hit area (wider than it looks) so it stays a usable
       pointer target under WCAG 2.5.8. -->
  <Style x:Key="ScrollThumb" TargetType="Thumb">
    <Setter Property="OverridesDefaultStyle" Value="True"/>
    <Setter Property="IsTabStop" Value="False"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Thumb">
          <!-- Transparent padding grows the target without adding visual weight. -->
          <Border Background="Transparent" Padding="5,0">
            <Border x:Name="bar" CornerRadius="3" Background="{StaticResource BorderControl}"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="bar" Property="Background" Value="{StaticResource TextFaint}"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="ScrollBar">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Width" Value="16"/>
    <Setter Property="MinWidth" Value="16"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ScrollBar">
          <Grid Background="{TemplateBinding Background}">
            <Track x:Name="PART_Track" IsDirectionReversed="True">
              <Track.Thumb>
                <Thumb Style="{StaticResource ScrollThumb}" MinHeight="40"/>
              </Track.Thumb>
              <Track.IncreaseRepeatButton>
                <RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0" Focusable="False"/>
              </Track.IncreaseRepeatButton>
              <Track.DecreaseRepeatButton>
                <RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0" Focusable="False"/>
              </Track.DecreaseRepeatButton>
            </Track>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
```

- [ ] **Step 2: Build and drive it**

Run: `dotnet build src/Tidsro/Tidsro.csproj -c Debug && dotnet test`
Expected: build clean, 327 passed.

Launch, go to Schedule, and with six alarms present: drag the thumb, click the track above and below the thumb (page up/down must still work — the invisible `RepeatButton`s provide that), and scroll with the wheel.

A `Track` without `PART_Track` as its name silently stops scrolling — if dragging does nothing, that is the first thing to check.

- [ ] **Step 3: Commit**

```bash
git add src/Tidsro/Resources/tokens.xaml
git commit -m "feat(theme): restyle the scrollbar

The one piece of stock Windows chrome left in the window. Slim track, rounded
thumb on the control border, hover to the faint text colour. The thumb keeps a
40px minimum and the track a 16px hit area behind a transparent pad, so
slimming it down does not shrink the target."
```

---

### Task 7: Manual verification pass

**Files:** none changed unless a defect is found.

**Interfaces:** none.

**Before you start:** back up live state. Copy `%AppData%\Tidsro\data.json` to a scratch folder and record `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Tidsro`. **Close Tidsro from the tray, never force-kill it** — the live schedule is held in memory and only written on a clean exit.

- [ ] **Step 1: Open every window**

Main, Settings, the licence viewer (Settings ▸ View font licence), the confirm dialog (Settings ▸ Reset all settings, then Cancel), edit-alarm (the pencil on any row), and a fired completion popup (arm a one-shot a minute out).

This step exists because a `StaticResource` pointing at a deleted token throws only when that window loads — Task 3 removed eight tokens, and a missed reference in a rarely opened view ships green and crashes on open. Same failure shape as the v1.3 `Run.Text` crash: XAML-attach-time, invisible to the suite.

- [ ] **Step 2: Click every restyled control**

Both tab headers, several times alternating — a switch must happen every time, not just the first. Then: Start, the preset buttons, pause/reset/cancel on **both** running-timer rows with two timers going, the alarm on/off toggle, edit and delete on an alarm row, day chips in the editor, both combo boxes, Settings, View font licence, and the scrollbar thumb.

The hero card has no buttons — it is caption, numerals and label only. Every running timer is reached from its own row, including the one the hero is counting: that row is fully present and shows everything except its own large numerals. With two timers going, pause the top one and watch the rows swap: the numerals should move to whichever row the hero is not showing, both rows should stay put otherwise, and **keyboard focus should not move** — tab to the second row's pause button, press Space, and confirm focus is still on a button afterwards rather than on the window. Watch the tab headers as you switch, too: the selected one is SemiBold, and a weight change on an auto-width `TabPanel` can shift the header row.

The tab-shell slice merged a control that did not respond to the mouse past three reviews and a green suite. Restyling `TabItem`, `Button`, `CheckBox` and `ScrollBar` templates is exactly the change that can silently break hit-testing.

- [ ] **Step 3: Confirm the fonts actually loaded**

A wrong pack URI yields working, attractive Segoe UI and no error — the precise failure that caused this redesign, so a glance is not enough. In the running app, compare the countdown digits against a Consolas sample; Plex Mono's digits are visibly narrower and its zero is unslashed. If there is any doubt, temporarily set `FontSans` to a deliberate nonsense family and confirm the app looks *different* — proving the token is live — then restore it.

- [ ] **Step 4: Re-read the UIA tree and diff the accessible names**

Walk `ControlViewWalker` from the Tidsro window with Windows PowerShell and `UIAutomationClient`. Every name must match the pre-redesign build exactly, except for the deliberate additions below. There are **six** of them, not one — an earlier version of this step expected only the hero's, which would have made the second read as a regression:

| Name | Where | Added by |
|---|---|---|
| `Running timer` | hero caption, on the Quick timers tab | Task 4 |
| `Paused timer` | the same caption, while that timer is paused | fix wave (F3) |
| `Typeface: IBM Plex, copyright IBM Corp., SIL Open Font License 1.1` | Settings | Task 2 |
| `View font licence` | Settings | fix wave (F5) |
| `Font licence text` | licence viewer | fix wave (F5) |
| `Close licence` | licence viewer | fix wave (F5) |

A short-lived fix-wave version of the hero carried its own pause/resume, reset and cancel buttons, because that design collapsed the whole row. It no longer does — the row stays and keeps its controls — so those buttons are **gone** and with them one extra instance each of `Pause`/`Resume`, `Reset timer` and `Cancel timer`. No string disappears from the app: N running timers expose exactly N of each, from N rows, which is what the pre-redesign build did. If the tree shows N+1 of any of them, the hero's buttons came back.

Traps worth remembering: a `Border` gets no automation peer, so a name set on one never reaches the tree; owned modal windows (Settings, confirm, the licence viewer under Settings) nest *under* the owner rather than as siblings at root; and `Start-Process -ArgumentList` does not quote values containing spaces.

- [ ] **Step 5: Confirm no literal survived**

```bash
grep -rn 'Color="#\|Background="#\|Foreground="#\|BorderBrush="#\|Fill="#\|To="#' src/Tidsro --include=*.xaml | grep -v 'x:Key=' | grep -v 'DropShadowEffect'
```

Expected: no output.

- [ ] **Step 6: Recompute the contrast table**

Against the values as implemented, starting with the palette's four binding constraints — `BorderControl` on `InteractiveHover` (3.04:1, hover-only) and on `ElevatedBg` (3.27:1, persistent), and for text `Danger` on `CardBg` (4.61:1) and `TextFaint` on `ElevatedBg` (4.64:1). `BorderControl` on `InteractiveBg` was named here as the tightest pair; it never was, and after the surface-ladder correction it sits at 3.42:1.

- [ ] **Step 7: Restore the backed-up state**

Close Tidsro from the tray, restore `data.json` and the Run key, and relaunch the **installed** exe rather than the Debug build.

- [ ] **Step 8: Record what this leaves open**

- `docs/screenshots/main-window.png` and `alarm-dialog.png` now show both the old layout *and* the old palette. They are retaken at release, not here.
- Running-timer rows still announce as `TimerItemViewModel`; pre-existing, and untouched by this slice.
- High Contrast Mode is unsupported and now more thoroughly so — recorded as known debt in the spec's §8, its own future slice.

---

## Out of scope for this plan

- The version bump, CHANGELOG entry, fresh screenshots and the release itself. Those follow the existing release recipe once this merges.
- High Contrast Mode support, which needs a `SystemParameters.HighContrast` check and a swappable dictionary — structural, not token work.
- Windows text-scaling support.
- A light theme or any runtime theme switching.
- Giving the running-timer rows composed accessible names.
