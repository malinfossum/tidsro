# Tidsro Slice 1 — Countdowns — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a usable, calm countdown timer — presets/custom durations with optional labels, multiple simultaneous countdowns, a non-focus-stealing completion card that is fully keyboard- and screen-reader-operable, tray-resident background running, and persisted settings.

**Architecture:** C# / WPF (`net10.0-windows`) in MVVM. Pure, testable Model + Service logic (scheduler driven by an injectable `IClock`, not a live timer) sits under thin ViewModels (CommunityToolkit.Mvvm) and hand-built XAML Views. Platform edges (registry, sound, global hotkey, monitor geometry) are isolated behind small services so the core stays unit-testable.

**Tech Stack:** .NET 10 / WPF · CommunityToolkit.Mvvm · H.NotifyIcon.Wpf · System.Text.Json · xUnit.

---

> **⚠ Before executing:** apply the corrections in **§ Stress-test findings & fixes** (near the end). Findings #1–#3 fix Task 1 setup (the build fails without them); #4–#6 fix feature/accessibility breaks. Apply each as you reach the named task.

## Resolved open decisions (spec §13)

| Decision | Resolution | Source |
|---|---|---|
| Tray library | **H.NotifyIcon.Wpf** (NuGet) | Malin, 2026-06-04 |
| MVVM glue | **CommunityToolkit.Mvvm** (NuGet) | Malin, 2026-06-04 |
| Test framework | **xUnit** | Malin, 2026-06-04 |
| .NET version | **net10.0-windows**, `Nullable=enable` | only SDK installed (10.0.300) |
| Sound API | **System.Media.SoundPlayer** (WAV, simplest) | spec §4 lean; flag to flip |
| Popup hotkey chord | **Ctrl+Alt+T** (`MOD_NOREPEAT`), not yet user-rebindable | default; rebinding deferred |
| Popup offset | **16 px** margin, clamped to the working area of the screen the main window is on | spec §13 default |

If any of the last three are wrong, they're each localised to one file — say so and only that task changes.

## Cross-cutting rules (every task)

- **No telemetry. No labels in logs.** If you add any diagnostic logging, log IDs/types only — never `Label` text (spec §10).
- **Loaded JSON is untrusted.** No polymorphic/`$type` handling; range-validate on load (spec §5.6).
- **Commit style:** plain imperative subject, no `feat:`/`test:` prefix, **no `Co-Authored-By` trailer** (matches the repo and Malin's rule). Commit after every green step.
- **TDD:** for Model/Service/ViewModel logic, write the failing test first. XAML Views are thin and verified by hand (spec §11) — their tasks build + manually verify instead.

## File structure

```
Tidsro/
  Tidsro.sln
  .editorconfig                      # copied from _template/csharp-wpf
  src/Tidsro/
    Tidsro.csproj                    # net10.0-windows, UseWPF, packages
    App.xaml(.cs)                    # OnExplicitShutdown; boots services to tray, no window
    Models/
      TriggerType.cs                 # enum Countdown|ClockTime|Recurring
      SoundChoice.cs                 # enum None|SoftChime|Marimba|Bell
      TimerState.cs                  # enum Idle|Running|Paused|Fired
      TimerItem.cs                   # one timer record (Slice 1 uses Countdown fields)
      AppSettings.cs                 # persisted settings + Sanitized()
      CountdownRules.cs              # parse + bounds validation (>0, <=24h)
    Services/
      IClock.cs / SystemClock.cs     # time abstraction (testable scheduler)
      SchedulerService.cs            # holds running countdowns; Tick(); Fired event
      PersistenceService.cs          # atomic save + tolerant load (settings)
      SoundService.cs                # SoundPlayer; play once; None = silent
      StartupService.cs              # HKCU Run key, quoted path, refresh/remove
      HotkeyService.cs               # global hotkey -> Pressed event
      ScreenHelper.cs                # work area for a window + bottom-right clamp
      UiaNotifier.cs                 # raise UIA Notification (screen-reader announce)
    ViewModels/
      MainViewModel.cs               # presets/custom/label, running list, Your day empty-state
      TimerItemViewModel.cs          # one running countdown row (remaining, pause/cancel)
      PopupViewModel.cs              # completion card: +5/Restart/Dismiss, debounced
      SettingsViewModel.cs           # launch-at-startup, default sound
    Views/
      MainWindow.xaml(.cs)           # two zones
      CompletionPopup.xaml(.cs)      # non-activating card, keyboard path, UIA announce
      SettingsWindow.xaml(.cs)
    Resources/tokens.xaml            # brushes/sizes derived from design-system tokens
    Assets/icons/tidsro.ico
    Assets/sounds/{soft-chime,marimba,bell}.wav
  tests/Tidsro.Tests/
    Tidsro.Tests.csproj              # xUnit, UseWPF (for System.Windows geometry types)
    FakeClock.cs
    CountdownRulesTests.cs
    SchedulerServiceTests.cs
    PersistenceServiceTests.cs
    ScreenHelperTests.cs
    PopupViewModelTests.cs
    MainViewModelTests.cs
```

## Contracts (lock these names — later tasks depend on them)

```csharp
enum TriggerType { Countdown, ClockTime, Recurring }
enum SoundChoice { None, SoftChime, Marimba, Bell }   // None = silent (default)
enum TimerState { Idle, Running, Paused, Fired }

interface IClock { DateTimeOffset Now { get; } }

// TimerItem (Slice 1 fields; alarm fields added in Slice 2/3)
Guid Id; string? Label; TriggerType TriggerType; SoundChoice Sound;
TimeSpan OriginalDuration; TimeSpan? Duration; DateTimeOffset? EndsAt;
TimeSpan? PausedRemaining; TimerState State;

// SchedulerService
TimerItem StartCountdown(TimeSpan duration, string? label, SoundChoice sound);
TimeSpan Remaining(TimerItem item);
void Tick();                       // single-fire via State guard; raises Fired
void Pause(TimerItem); void Resume(TimerItem); void Cancel(TimerItem);
TimerItem Snooze(TimerItem item, TimeSpan by);   // +5: fresh countdown
TimerItem Restart(TimerItem item);               // re-run OriginalDuration
event EventHandler<TimerItem> Fired;
IReadOnlyList<TimerItem> Running { get; }

// PersistenceService
AppSettings Load();  void Save(AppSettings settings);  static string DefaultPath { get; }

// CountdownRules
static TimeSpan Max;                                              // 24h
static bool TryParse(string? input, out TimeSpan d, out string? error);
static bool TryValidate(TimeSpan d, out string? error);
```

---

## Phase 0 — Solution & project scaffolding

### Task 1: Create the solution, projects, and packages

**Files:**
- Create: `Tidsro.sln`, `src/Tidsro/Tidsro.csproj`, `tests/Tidsro.Tests/Tidsro.Tests.csproj`
- Create: `.editorconfig` (copy of `_template/csharp-wpf/.editorconfig`)

- [ ] **Step 1: Scaffold from the repo root** (`GitHub/repos/Tidsro`)

Run:
```powershell
dotnet new sln -n Tidsro
dotnet new wpf -n Tidsro -o src/Tidsro -f net10.0-windows
dotnet new xunit -n Tidsro.Tests -o tests/Tidsro.Tests -f net10.0-windows
dotnet sln add src/Tidsro/Tidsro.csproj tests/Tidsro.Tests/Tidsro.Tests.csproj
dotnet add tests/Tidsro.Tests/Tidsro.Tests.csproj reference src/Tidsro/Tidsro.csproj
```

- [ ] **Step 2: Add packages**

Run:
```powershell
dotnet add src/Tidsro/Tidsro.csproj package CommunityToolkit.Mvvm
dotnet add src/Tidsro/Tidsro.csproj package H.NotifyIcon.Wpf
```

- [ ] **Step 3: Set project properties**

Edit `src/Tidsro/Tidsro.csproj` so the first `<PropertyGroup>` reads:
```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net10.0-windows</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <UseWPF>true</UseWPF>
  <ApplicationManifest>app.manifest</ApplicationManifest>
  <AssemblyName>Tidsro</AssemblyName>
  <RootNamespace>Tidsro</RootNamespace>
</PropertyGroup>
```
Edit `tests/Tidsro.Tests/Tidsro.Tests.csproj` `<PropertyGroup>` to add (geometry types live in WPF assemblies):
```xml
<UseWPF>true</UseWPF>
<Nullable>enable</Nullable>
```

- [ ] **Step 4: Copy the editorconfig convention**

Copy `_template/csharp-wpf/.editorconfig` to the repo root `.editorconfig` (the repo already has `_template/csharp-wpf/.gitignore`'s rules via the existing `.gitignore`; do not overwrite the existing `.gitignore`).

- [ ] **Step 5: Verify it builds and the empty test project runs**

Run:
```powershell
dotnet build
dotnet test
```
Expected: build succeeds; `dotnet test` runs 0 tests (or the template's 1 sample) with exit code 0. Delete any template sample test file (`UnitTest1.cs`).

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "Scaffold solution, WPF app, and xUnit test project"
```

---

## Phase 1 — App shell to tray (no window on launch)

### Task 2: Boot to tray with Open/Quit, no window shown

**Files:**
- Modify: `src/Tidsro/App.xaml`, `src/Tidsro/App.xaml.cs`
- Create: `src/Tidsro/app.manifest`
- Delete: `src/Tidsro/MainWindow.xaml(.cs)` template content is replaced in Task 16; for now keep but don't show.

- [ ] **Step 1: Add a DPI-aware manifest**

Create `src/Tidsro/app.manifest`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

- [ ] **Step 2: Make App start to tray, not a window**

Replace `src/Tidsro/App.xaml`:
```xml
<Application x:Class="Tidsro.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="Resources/tokens.xaml"/>
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```
(`Resources/tokens.xaml` is created in Task 3 — build will fail until then; that's expected and fixed in Task 3.)

Replace `src/Tidsro/App.xaml.cs`:
```csharp
using System.Windows;
using H.NotifyIcon;
using Tidsro.Services;

namespace Tidsro;

public partial class App : Application
{
    private TaskbarIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _tray = TrayBuilder.Create(ShowMainWindow, Quit);
    }

    private void ShowMainWindow()
    {
        var win = MainWindow ??= new MainWindow();
        win.Show();
        win.WindowState = WindowState.Normal;
        win.Activate();
    }

    private void Quit()
    {
        _tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 3: Tray builder (H.NotifyIcon)**

Create `src/Tidsro/Services/TrayBuilder.cs`:
```csharp
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;

namespace Tidsro.Services;

public static class TrayBuilder
{
    public static TaskbarIcon Create(Action onOpen, Action onQuit)
    {
        var menu = new ContextMenu();
        var open = new MenuItem { Header = "Open" };
        open.Click += (_, _) => onOpen();
        var quit = new MenuItem { Header = "Quit" };
        quit.Click += (_, _) => onQuit();
        menu.Items.Add(open);
        menu.Items.Add(new Separator());
        menu.Items.Add(quit);

        var tray = new TaskbarIcon
        {
            ToolTipText = "Tidsro",
            ContextMenu = menu,
            IconSource = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Assets/icons/tidsro.ico"))
        };
        tray.TrayLeftMouseUp += (_, _) => onOpen();
        tray.ForceCreate();
        return tray;
    }
}
```

- [ ] **Step 4: Add a tray icon asset**

Add a licence-clear 32×32 `.ico` at `src/Tidsro/Assets/icons/tidsro.ico` (a simple calm glyph; sourcing/creation is an asset step, not code). Mark it in `Tidsro.csproj`:
```xml
<ItemGroup>
  <Resource Include="Assets\icons\tidsro.ico" />
</ItemGroup>
```

- [ ] **Step 5: Manual verify** (after Task 3 makes it build)

Run `dotnet run --project src/Tidsro`. Expected: **no window appears**, a tray icon shows; left-click opens the (empty) main window; tray menu has Open + Quit; Quit exits the process. Closing the window leaves the process running in the tray.

- [ ] **Step 6: Commit** (do this at the end of Task 3, once it builds)

---

### Task 3: Design tokens (`tokens.xaml`) from the design system

**Files:**
- Create: `src/Tidsro/Resources/tokens.xaml`

> Keep in sync with `_template/design-system/tokens/` via the **design-system-sync** skill. Values below are the dark theme (the only theme in v1), translated to WPF. `rgba(r,g,b,a)` → `#AARRGGBB`.

- [ ] **Step 1: Create the resource dictionary**

Create `src/Tidsro/Resources/tokens.xaml`:
```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:sys="clr-namespace:System;assembly=System.Runtime">

  <!-- Surfaces -->
  <SolidColorBrush x:Key="PageBg"      Color="#000000"/>
  <SolidColorBrush x:Key="PanelBg"     Color="#0A0D10"/>
  <SolidColorBrush x:Key="CardBg"      Color="#11161B"/>
  <SolidColorBrush x:Key="ElevatedBg"  Color="#171D24"/>
  <SolidColorBrush x:Key="InteractiveBg" Color="#1D252D"/>

  <!-- Text -->
  <SolidColorBrush x:Key="Text"        Color="#F4F7FA"/>
  <SolidColorBrush x:Key="TextMuted"   Color="#B4BDC7"/>
  <SolidColorBrush x:Key="TextFaint"   Color="#87919C"/>

  <!-- Lines -->
  <SolidColorBrush x:Key="Border"      Color="#232C35"/>
  <SolidColorBrush x:Key="BorderStrong" Color="#313C48"/>
  <SolidColorBrush x:Key="BorderSoft"  Color="#1A2128"/>

  <!-- Accent + semantic -->
  <SolidColorBrush x:Key="Accent"      Color="#7C9AB3"/>
  <SolidColorBrush x:Key="AccentStrong" Color="#90ADC5"/>
  <SolidColorBrush x:Key="AccentSoft"  Color="#297C9AB3"/>  <!-- 0.16 -->
  <SolidColorBrush x:Key="Success"     Color="#7D9E8A"/>
  <SolidColorBrush x:Key="Warning"     Color="#A79A74"/>
  <SolidColorBrush x:Key="Danger"      Color="#A1837F"/>
  <SolidColorBrush x:Key="Info"        Color="#7F92A3"/>
  <SolidColorBrush x:Key="FocusRing"   Color="#6190ADC5"/>  <!-- 0.38 -->

  <!-- Spacing (DIP) -->
  <sys:Double x:Key="Space1">4</sys:Double>
  <sys:Double x:Key="Space2">8</sys:Double>
  <sys:Double x:Key="Space3">12</sys:Double>
  <sys:Double x:Key="Space4">16</sys:Double>
  <sys:Double x:Key="Space5">24</sys:Double>
  <sys:Double x:Key="Space6">32</sys:Double>
  <Thickness x:Key="CardPadding">16</Thickness>

  <!-- Radius -->
  <CornerRadius x:Key="RadiusSm">8</CornerRadius>
  <CornerRadius x:Key="RadiusMd">12</CornerRadius>
  <CornerRadius x:Key="RadiusLg">16</CornerRadius>

  <!-- Typography -->
  <FontFamily x:Key="FontSans">Inter, Segoe UI, sans-serif</FontFamily>
  <FontFamily x:Key="FontMono">Consolas, Menlo, monospace</FontFamily>
  <sys:Double x:Key="TextXs">12</sys:Double>
  <sys:Double x:Key="TextSm">14</sys:Double>
  <sys:Double x:Key="TextMd">16</sys:Double>
  <sys:Double x:Key="TextLg">18</sys:Double>
  <sys:Double x:Key="TextXl">20</sys:Double>
  <sys:Double x:Key="Text2xl">28</sys:Double>

  <!-- Motion -->
  <Duration x:Key="DurationFast">0:0:0.12</Duration>
  <Duration x:Key="DurationBase">0:0:0.18</Duration>
  <Duration x:Key="DurationSlow">0:0:0.26</Duration>

  <!-- Card drop shadow (shadow-sm) -->
  <DropShadowEffect x:Key="CardShadow" BlurRadius="24" ShadowDepth="8"
                    Direction="270" Opacity="0.18" Color="#000000"/>
</ResourceDictionary>
```

- [ ] **Step 2: Build + manual verify the shell**

Run `dotnet build` then `dotnet run --project src/Tidsro`. Expected: builds; Task 2's tray behaviour now works (no window on launch, tray Open/Quit). 

- [ ] **Step 3: Commit**

```powershell
git add -A
git commit -m "Boot to tray with Open/Quit and design tokens; no window on launch"
```

---

## Phase 2 — Domain models & validation (TDD)

### Task 4: Enums and TimerItem

**Files:**
- Create: `src/Tidsro/Models/TriggerType.cs`, `SoundChoice.cs`, `TimerState.cs`, `TimerItem.cs`

- [ ] **Step 1: Add the enums**

`Models/TriggerType.cs`:
```csharp
namespace Tidsro.Models;
public enum TriggerType { Countdown, ClockTime, Recurring }
```
`Models/SoundChoice.cs`:
```csharp
namespace Tidsro.Models;
public enum SoundChoice { None, SoftChime, Marimba, Bell }
```
`Models/TimerState.cs`:
```csharp
namespace Tidsro.Models;
public enum TimerState { Idle, Running, Paused, Fired }
```

- [ ] **Step 2: Add TimerItem**

`Models/TimerItem.cs`:
```csharp
namespace Tidsro.Models;

public sealed class TimerItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string? Label { get; set; }
    public TriggerType TriggerType { get; init; } = TriggerType.Countdown;
    public SoundChoice Sound { get; set; } = SoundChoice.None;

    // Countdown runtime (Slice 1)
    public TimeSpan OriginalDuration { get; set; }
    public TimeSpan? Duration { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public TimeSpan? PausedRemaining { get; set; }
    public TimerState State { get; set; } = TimerState.Idle;
}
```

- [ ] **Step 3: Build**

Run `dotnet build`. Expected: PASS (no behaviour yet to test).

- [ ] **Step 4: Commit**

```powershell
git add -A
git commit -m "Add timer enums and TimerItem model"
```

### Task 5: CountdownRules — parse + bounds (TDD)

**Files:**
- Create: `src/Tidsro/Models/CountdownRules.cs`
- Test: `tests/Tidsro.Tests/CountdownRulesTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/Tidsro.Tests/CountdownRulesTests.cs`:
```csharp
using Tidsro.Models;
using Xunit;

namespace Tidsro.Tests;

public class CountdownRulesTests
{
    [Theory]
    [InlineData("25", 0, 25, 0)]        // bare minutes
    [InlineData("5:00", 0, 5, 0)]       // mm:ss
    [InlineData("1:30:00", 1, 30, 0)]   // h:mm:ss
    public void TryParse_accepts_valid_formats(string input, int h, int m, int s)
    {
        Assert.True(CountdownRules.TryParse(input, out var d, out var err));
        Assert.Null(err);
        Assert.Equal(new TimeSpan(h, m, s), d);
    }

    [Theory]
    [InlineData("0")]          // zero
    [InlineData("00:00:00")]   // zero
    [InlineData("25:00:00")]   // > 24h
    [InlineData("abc")]        // garbage
    [InlineData("")]           // empty
    [InlineData("5:99")]       // seconds out of range
    public void TryParse_rejects_invalid(string input)
    {
        Assert.False(CountdownRules.TryParse(input, out _, out var err));
        Assert.NotNull(err);
    }

    [Fact]
    public void TryValidate_rejects_zero_and_over_max()
    {
        Assert.False(CountdownRules.TryValidate(TimeSpan.Zero, out _));
        Assert.False(CountdownRules.TryValidate(TimeSpan.FromHours(25), out _));
        Assert.True(CountdownRules.TryValidate(TimeSpan.FromMinutes(25), out var err));
        Assert.Null(err);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test --filter CountdownRulesTests`
Expected: FAIL — `CountdownRules` does not exist.

- [ ] **Step 3: Implement**

`src/Tidsro/Models/CountdownRules.cs`:
```csharp
namespace Tidsro.Models;

public static class CountdownRules
{
    public static readonly TimeSpan Max = TimeSpan.FromHours(24);

    public static bool TryValidate(TimeSpan d, out string? error)
    {
        if (d <= TimeSpan.Zero) { error = "Duration must be greater than zero."; return false; }
        if (d > Max) { error = "Duration can be at most 24 hours."; return false; }
        error = null; return true;
    }

    /// <summary>"25" = minutes, "MM:SS" = minutes:seconds, "H:MM:SS" = hours:minutes:seconds.</summary>
    public static bool TryParse(string? input, out TimeSpan duration, out string? error)
    {
        duration = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(input)) { error = "Enter a duration."; return false; }

        var parts = input.Split(':', StringSplitOptions.TrimEntries);
        int h = 0, m, s = 0;
        try
        {
            switch (parts.Length)
            {
                case 1: m = int.Parse(parts[0]); break;
                case 2: m = int.Parse(parts[0]); s = int.Parse(parts[1]); break;
                case 3: h = int.Parse(parts[0]); m = int.Parse(parts[1]); s = int.Parse(parts[2]); break;
                default: error = "Use minutes, MM:SS, or H:MM:SS."; return false;
            }
        }
        catch (FormatException) { error = "Use only numbers and colons."; return false; }
        catch (OverflowException) { error = "That number is too large."; return false; }

        if (h < 0 || m < 0 || s < 0 || s > 59 || (parts.Length == 3 && m > 59))
        { error = "Minutes and seconds must be 0–59."; return false; }

        var d = new TimeSpan(h, m, s);
        if (!TryValidate(d, out error)) return false;
        duration = d; return true;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test --filter CountdownRulesTests`
Expected: PASS (all theories green).

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "Add countdown parsing and bounds validation"
```

---

## Phase 3 — Scheduler (TDD)

### Task 6: IClock + FakeClock

**Files:**
- Create: `src/Tidsro/Services/IClock.cs`, `src/Tidsro/Services/SystemClock.cs`
- Create: `tests/Tidsro.Tests/FakeClock.cs`

- [ ] **Step 1: Add the abstraction**

`src/Tidsro/Services/IClock.cs`:
```csharp
namespace Tidsro.Services;
public interface IClock { DateTimeOffset Now { get; } }
```
`src/Tidsro/Services/SystemClock.cs`:
```csharp
namespace Tidsro.Services;
public sealed class SystemClock : IClock { public DateTimeOffset Now => DateTimeOffset.Now; }
```
`tests/Tidsro.Tests/FakeClock.cs`:
```csharp
using Tidsro.Services;
namespace Tidsro.Tests;
public sealed class FakeClock : IClock
{
    public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
    public void Advance(TimeSpan by) => Now += by;
}
```

- [ ] **Step 2: Build**

Run `dotnet build`. Expected: PASS.

- [ ] **Step 3: Commit**

```powershell
git add -A
git commit -m "Add IClock time abstraction and test fake"
```

### Task 7: SchedulerService — start, tick, single-fire (TDD)

**Files:**
- Create: `src/Tidsro/Services/SchedulerService.cs`
- Test: `tests/Tidsro.Tests/SchedulerServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/Tidsro.Tests/SchedulerServiceTests.cs`:
```csharp
using Tidsro.Models;
using Tidsro.Services;
using Xunit;

namespace Tidsro.Tests;

public class SchedulerServiceTests
{
    private static (SchedulerService s, FakeClock c) New()
    {
        var c = new FakeClock();
        return (new SchedulerService(c), c);
    }

    [Fact]
    public void StartCountdown_adds_a_running_item()
    {
        var (s, c) = New();
        var item = s.StartCountdown(TimeSpan.FromMinutes(25), "focus", SoundChoice.None);
        Assert.Single(s.Running);
        Assert.Equal(TimerState.Running, item.State);
        Assert.Equal(c.Now + TimeSpan.FromMinutes(25), item.EndsAt);
    }

    [Fact]
    public void Tick_before_end_does_not_fire_and_remaining_counts_down()
    {
        var (s, c) = New();
        var item = s.StartCountdown(TimeSpan.FromMinutes(1), null, SoundChoice.None);
        var fired = 0; s.Fired += (_, _) => fired++;
        c.Advance(TimeSpan.FromSeconds(40)); s.Tick();
        Assert.Equal(0, fired);
        Assert.Equal(TimeSpan.FromSeconds(20), s.Remaining(item));
    }

    [Fact]
    public void Tick_at_or_after_end_fires_exactly_once()
    {
        var (s, c) = New();
        var item = s.StartCountdown(TimeSpan.FromMinutes(1), null, SoundChoice.None);
        var fired = 0; s.Fired += (_, _) => fired++;
        c.Advance(TimeSpan.FromSeconds(61));
        s.Tick(); s.Tick();              // tick twice past zero
        Assert.Equal(1, fired);          // single-fire guard
        Assert.Equal(TimerState.Fired, item.State);
        Assert.Equal(TimeSpan.Zero, s.Remaining(item));
    }

    [Fact]
    public void Pause_then_Resume_preserves_remaining()
    {
        var (s, c) = New();
        var item = s.StartCountdown(TimeSpan.FromMinutes(10), null, SoundChoice.None);
        c.Advance(TimeSpan.FromMinutes(4)); s.Pause(item);
        Assert.Equal(TimeSpan.FromMinutes(6), s.Remaining(item));
        c.Advance(TimeSpan.FromMinutes(3)); s.Resume(item);   // time passes while paused
        Assert.Equal(TimeSpan.FromMinutes(6), s.Remaining(item));
    }

    [Fact]
    public void Snooze_rearms_fresh_five_minutes_and_Restart_uses_original()
    {
        var (s, c) = New();
        var item = s.StartCountdown(TimeSpan.FromMinutes(25), "pom", SoundChoice.Bell);
        c.Advance(TimeSpan.FromMinutes(26)); s.Tick();        // fires
        var snoozed = s.Snooze(item, TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.FromMinutes(5), s.Remaining(snoozed));
        Assert.Equal("pom", snoozed.Label);
        var restarted = s.Restart(snoozed);
        Assert.Equal(TimeSpan.FromMinutes(5), restarted.OriginalDuration); // restart re-runs the snooze's 5m
    }

    [Fact]
    public void Cancel_removes_the_item()
    {
        var (s, _) = New();
        var item = s.StartCountdown(TimeSpan.FromMinutes(1), null, SoundChoice.None);
        s.Cancel(item);
        Assert.Empty(s.Running);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test --filter SchedulerServiceTests`
Expected: FAIL — `SchedulerService` does not exist.

- [ ] **Step 3: Implement**

`src/Tidsro/Services/SchedulerService.cs`:
```csharp
using Tidsro.Models;

namespace Tidsro.Services;

public sealed class SchedulerService
{
    private readonly IClock _clock;
    private readonly List<TimerItem> _running = new();

    public SchedulerService(IClock clock) => _clock = clock;

    public IReadOnlyList<TimerItem> Running => _running;
    public event EventHandler<TimerItem>? Fired;

    public TimerItem StartCountdown(TimeSpan duration, string? label, SoundChoice sound)
    {
        var item = new TimerItem
        {
            TriggerType = TriggerType.Countdown,
            Label = label,
            Sound = sound,
            OriginalDuration = duration,
            Duration = duration,
            EndsAt = _clock.Now + duration,
            State = TimerState.Running,
        };
        _running.Add(item);
        return item;
    }

    public TimeSpan Remaining(TimerItem item)
    {
        if (item.State == TimerState.Paused) return item.PausedRemaining ?? TimeSpan.Zero;
        if (item.EndsAt is not { } end) return TimeSpan.Zero;
        var rem = end - _clock.Now;
        return rem > TimeSpan.Zero ? rem : TimeSpan.Zero;
    }

    public void Tick()
    {
        var now = _clock.Now;
        foreach (var item in _running.ToList())   // snapshot: handlers may mutate _running
        {
            if (item.State == TimerState.Running && item.EndsAt is { } end && now >= end)
            {
                item.State = TimerState.Fired;     // guard: fire at most once
                Fired?.Invoke(this, item);
            }
        }
    }

    public void Pause(TimerItem item)
    {
        if (item.State != TimerState.Running) return;
        item.PausedRemaining = Remaining(item);
        item.State = TimerState.Paused;
    }

    public void Resume(TimerItem item)
    {
        if (item.State != TimerState.Paused) return;
        item.EndsAt = _clock.Now + (item.PausedRemaining ?? TimeSpan.Zero);
        item.PausedRemaining = null;
        item.State = TimerState.Running;
    }

    public void Cancel(TimerItem item) => _running.Remove(item);

    public TimerItem Snooze(TimerItem item, TimeSpan by)
    {
        Cancel(item);
        return StartCountdown(by, item.Label, item.Sound);
    }

    public TimerItem Restart(TimerItem item)
    {
        Cancel(item);
        return StartCountdown(item.OriginalDuration, item.Label, item.Sound);
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test --filter SchedulerServiceTests`
Expected: PASS (6 tests green).

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "Add scheduler: start, tick with single-fire, pause/resume, snooze/restart"
```

---

## Phase 4 — Persistence & settings (TDD)

### Task 8: AppSettings with sanitisation

**Files:**
- Create: `src/Tidsro/Models/AppSettings.cs`

- [ ] **Step 1: Implement**

`src/Tidsro/Models/AppSettings.cs`:
```csharp
namespace Tidsro.Models;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public bool LaunchAtStartup { get; set; }
    public SoundChoice DefaultSound { get; set; } = SoundChoice.None;

    public static AppSettings Defaults() => new();

    /// <summary>Harden untrusted input loaded from disk: unknown enum -> None.</summary>
    public AppSettings Sanitized() => new()
    {
        SchemaVersion = 1,
        LaunchAtStartup = LaunchAtStartup,
        DefaultSound = Enum.IsDefined(DefaultSound) ? DefaultSound : SoundChoice.None,
    };
}
```

- [ ] **Step 2: Build + commit**

```powershell
dotnet build
git add -A
git commit -m "Add AppSettings with untrusted-input sanitisation"
```

### Task 9: PersistenceService — atomic save, tolerant load (TDD)

**Files:**
- Create: `src/Tidsro/Services/PersistenceService.cs`
- Test: `tests/Tidsro.Tests/PersistenceServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/Tidsro.Tests/PersistenceServiceTests.cs`:
```csharp
using Tidsro.Models;
using Tidsro.Services;
using Xunit;

namespace Tidsro.Tests;

public class PersistenceServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;
    public PersistenceServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "TidsroTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "data.json");
    }
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    [Fact]
    public void Load_missing_file_returns_defaults()
    {
        var svc = new PersistenceService(_path);
        var s = svc.Load();
        Assert.False(s.LaunchAtStartup);
        Assert.Equal(SoundChoice.None, s.DefaultSound);
    }

    [Fact]
    public void Save_then_Load_round_trips()
    {
        var svc = new PersistenceService(_path);
        svc.Save(new AppSettings { LaunchAtStartup = true, DefaultSound = SoundChoice.Bell });
        var s = svc.Load();
        Assert.True(s.LaunchAtStartup);
        Assert.Equal(SoundChoice.Bell, s.DefaultSound);
    }

    [Fact]
    public void Save_is_atomic_and_leaves_no_temp_file()
    {
        var svc = new PersistenceService(_path);
        svc.Save(new AppSettings());
        Assert.True(File.Exists(_path));
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public void Load_corrupt_file_quarantines_and_returns_defaults()
    {
        File.WriteAllText(_path, "{ this is not valid json ");
        var svc = new PersistenceService(_path);
        var s = svc.Load();
        Assert.Equal(SoundChoice.None, s.DefaultSound);          // defaults
        Assert.True(File.Exists(_path + ".corrupt"));            // quarantined, app still launches
    }

    [Fact]
    public void Load_unknown_enum_value_falls_back_to_none()
    {
        File.WriteAllText(_path, "{\"SchemaVersion\":1,\"LaunchAtStartup\":false,\"DefaultSound\":999}");
        var svc = new PersistenceService(_path);
        Assert.Equal(SoundChoice.None, svc.Load().DefaultSound);  // untrusted-input hardening
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test --filter PersistenceServiceTests`
Expected: FAIL — `PersistenceService` does not exist.

- [ ] **Step 3: Implement**

`src/Tidsro/Services/PersistenceService.cs`:
```csharp
using System.IO;
using System.Text.Json;
using Tidsro.Models;

namespace Tidsro.Services;

public sealed class PersistenceService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        // No polymorphic/$type handling. Default, non-polymorphic contracts only.
    };

    private readonly string _path;
    public PersistenceService(string path) => _path = path;

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tidsro", "data.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return AppSettings.Defaults();
            var dto = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options);
            return dto?.Sanitized() ?? AppSettings.Defaults();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Quarantine();
            return AppSettings.Defaults();   // never fail to launch on a bad file
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Options));   // flushed on close
        if (File.Exists(_path)) File.Replace(tmp, _path, null);                // atomic, same volume
        else File.Move(tmp, _path);
    }

    private void Quarantine()
    {
        try { if (File.Exists(_path)) File.Copy(_path, _path + ".corrupt", overwrite: true); }
        catch { /* quarantine must never throw */ }
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test --filter PersistenceServiceTests`
Expected: PASS (5 tests green).

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "Add crash-safe, tolerant settings persistence with untrusted-JSON hardening"
```

---

## Phase 5 — Platform services

### Task 10: ScreenHelper — work area + bottom-right clamp (clamp math is TDD)

**Files:**
- Create: `src/Tidsro/Services/ScreenHelper.cs`
- Test: `tests/Tidsro.Tests/ScreenHelperTests.cs`

- [ ] **Step 1: Write the failing test (pure clamp math)**

`tests/Tidsro.Tests/ScreenHelperTests.cs`:
```csharp
using System.Windows;
using Tidsro.Services;
using Xunit;

namespace Tidsro.Tests;

public class ScreenHelperTests
{
    [Fact]
    public void Clamp_places_card_bottom_right_with_margin()
    {
        var work = new Rect(0, 0, 1920, 1040);
        var p = ScreenHelper.ClampBottomRight(work, new Size(320, 120), 16);
        Assert.Equal(1920 - 320 - 16, p.X);
        Assert.Equal(1040 - 120 - 16, p.Y);
    }

    [Fact]
    public void Clamp_keeps_card_on_a_small_work_area()
    {
        // work area smaller than card + margin must not push the card off the left/top edge
        var work = new Rect(100, 100, 200, 80);
        var p = ScreenHelper.ClampBottomRight(work, new Size(320, 120), 16);
        Assert.True(p.X >= work.Left);
        Assert.True(p.Y >= work.Top);
        Assert.True(p.X + 320 >= work.Left);   // never fully off-screen
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test --filter ScreenHelperTests`
Expected: FAIL — `ScreenHelper` does not exist.

- [ ] **Step 3: Implement**

`src/Tidsro/Services/ScreenHelper.cs`:
```csharp
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Tidsro.Services;

public static class ScreenHelper
{
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO mi);
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    /// <summary>Working area (DIPs) of the monitor the window is on; primary work area as fallback.</summary>
    public static Rect WorkAreaForWindow(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            var mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (mon != IntPtr.Zero && GetMonitorInfo(mon, ref mi))
            {
                var dpi = VisualTreeHelper.GetDpi(window);
                var w = mi.rcWork;
                return new Rect(w.Left / dpi.DpiScaleX, w.Top / dpi.DpiScaleY,
                                (w.Right - w.Left) / dpi.DpiScaleX, (w.Bottom - w.Top) / dpi.DpiScaleY);
            }
        }
        return SystemParameters.WorkArea;
    }

    /// <summary>Bottom-right position, clamped so the card can never land off-screen.</summary>
    public static Point ClampBottomRight(Rect work, Size card, double margin)
    {
        var x = work.Right - card.Width - margin;
        var y = work.Bottom - card.Height - margin;
        x = Math.Max(work.Left, Math.Min(x, work.Right - card.Width));
        y = Math.Max(work.Top, Math.Min(y, work.Bottom - card.Height));
        return new Point(x, y);
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test --filter ScreenHelperTests`
Expected: PASS (2 tests green).

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "Add monitor work-area lookup and off-screen popup clamp"
```

### Task 11: SoundService

**Files:**
- Create: `src/Tidsro/Services/SoundService.cs`
- Assets: `src/Tidsro/Assets/sounds/{soft-chime,marimba,bell}.wav`

- [ ] **Step 1: Add three licence-clear gentle WAVs**

Place `soft-chime.wav`, `marimba.wav`, `bell.wav` under `src/Tidsro/Assets/sounds/` (asset sourcing — short, soft, non-looping). Mark them as content in `Tidsro.csproj`:
```xml
<ItemGroup>
  <Content Include="Assets\sounds\*.wav" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [ ] **Step 2: Implement**

`src/Tidsro/Services/SoundService.cs`:
```csharp
using System.IO;
using System.Media;
using Tidsro.Models;

namespace Tidsro.Services;

public sealed class SoundService
{
    private static string File(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "sounds", name);

    private static string? PathFor(SoundChoice c) => c switch
    {
        SoundChoice.SoftChime => File("soft-chime.wav"),
        SoundChoice.Marimba   => File("marimba.wav"),
        SoundChoice.Bell      => File("bell.wav"),
        _ => null,   // None = silent
    };

    /// <summary>Play the chosen sound once. Silent and never throws.</summary>
    public void Play(SoundChoice choice)
    {
        var path = PathFor(choice);
        if (path is null || !System.IO.File.Exists(path)) return;
        try { new SoundPlayer(path).Play(); }   // async, fire-and-forget, plays once
        catch { /* sound must never crash a timer */ }
    }
}
```

- [ ] **Step 3: Build + commit**

```powershell
dotnet build
git add -A
git commit -m "Add sound service (silent default, gentle WAV played once)"
```

### Task 12: StartupService — quoted Run-key path

**Files:**
- Create: `src/Tidsro/Services/StartupService.cs`
- Test: `tests/Tidsro.Tests/StartupServiceTests.cs`

- [ ] **Step 1: Write the failing test (pure quoting helper)**

`tests/Tidsro.Tests/StartupServiceTests.cs`:
```csharp
using Tidsro.Services;
using Xunit;

namespace Tidsro.Tests;

public class StartupServiceTests
{
    [Fact]
    public void Run_value_is_fully_quoted_to_survive_spaces_in_path()
    {
        var v = StartupService.RunValueFor(@"C:\Program Files\Tidsro\Tidsro.exe");
        Assert.Equal("\"C:\\Program Files\\Tidsro\\Tidsro.exe\"", v);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test --filter StartupServiceTests`
Expected: FAIL — `StartupService` does not exist.

- [ ] **Step 3: Implement**

`src/Tidsro/Services/StartupService.cs`:
```csharp
using System.Diagnostics;
using Microsoft.Win32;

namespace Tidsro.Services;

public sealed class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Tidsro";

    private readonly string _exePath;
    public StartupService(string exePath) => _exePath = exePath;

    public static string CurrentExePath =>
        Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName;

    /// <summary>Fully-quoted command so a space in the path can't mis-parse.</summary>
    public static string RunValueFor(string exePath) => "\"" + exePath + "\"";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.SetValue(ValueName, RunValueFor(_exePath));
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>If enabled, repoint a stale path after an app move/update.</summary>
    public void RefreshIfEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(ValueName) is string existing && existing != RunValueFor(_exePath))
            key.SetValue(ValueName, RunValueFor(_exePath));
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test --filter StartupServiceTests`
Expected: PASS.

- [ ] **Step 5: Manual verify (real registry)**

In a throwaway run, call `Enable()`, check `HKCU\...\Run` has a `Tidsro` value wrapped in quotes; `Disable()` removes it. Do not leave a stray entry. (Covered live by the Settings toggle in Task 18.)

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "Add launch-at-startup service with quoted, self-healing Run entry"
```

### Task 13: HotkeyService — global hotkey to focus the latest card

**Files:**
- Create: `src/Tidsro/Services/HotkeyService.cs`

- [ ] **Step 1: Implement (platform; manual-verify in Task 17)**

`src/Tidsro/Services/HotkeyService.cs`:
```csharp
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Tidsro.Services;

/// <summary>Registers a system-wide hotkey (default Ctrl+Alt+T) on a message-only window.</summary>
public sealed class HotkeyService : IDisposable
{
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mod, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_NOREPEAT = 0x4000;
    private const uint VK_T = 0x54;
    private readonly int _id = 0x5444; // "TD"
    private readonly HwndSource _source;

    public event EventHandler? Pressed;

    public HotkeyService()
    {
        var p = new HwndSourceParameters("TidsroHotkey") { ParentWindow = new IntPtr(-3) }; // HWND_MESSAGE
        _source = new HwndSource(p);
        _source.AddHook(WndProc);
    }

    public bool Register() =>
        RegisterHotKey(_source.Handle, _id, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_T);

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == _id)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterHotKey(_source.Handle, _id);
        _source.Dispose();
    }
}
```

- [ ] **Step 2: Build + commit**

```powershell
dotnet build
git add -A
git commit -m "Add global hotkey service for focusing the completion card"
```

### Task 14: UiaNotifier — screen-reader announcement

**Files:**
- Create: `src/Tidsro/Services/UiaNotifier.cs`

- [ ] **Step 1: Implement**

`src/Tidsro/Services/UiaNotifier.cs`:
```csharp
using System.Windows;
using System.Windows.Automation.Peers;

namespace Tidsro.Services;

public static class UiaNotifier
{
    /// <summary>Announce via a UIA Notification event — no focus change (spec §5.3, §9).</summary>
    public static void Announce(UIElement element, string message)
    {
        var peer = UIElementAutomationPeer.FromElement(element)
                   ?? UIElementAutomationPeer.CreatePeerForElement(element);
        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.ActionCompleted,
            AutomationNotificationProcessing.MostRecent,
            message,
            "TidsroTimerComplete");
    }
}
```

- [ ] **Step 2: Build + commit**

```powershell
dotnet build
git add -A
git commit -m "Add UIA notification helper for focus-free completion announcements"
```

---

## Phase 6 — ViewModels (TDD where logic)

### Task 15: PopupViewModel — actions, debounce (TDD)

**Files:**
- Create: `src/Tidsro/ViewModels/PopupViewModel.cs`
- Test: `tests/Tidsro.Tests/PopupViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/Tidsro.Tests/PopupViewModelTests.cs`:
```csharp
using Tidsro.Models;
using Tidsro.ViewModels;
using Xunit;

namespace Tidsro.Tests;

public class PopupViewModelTests
{
    private static TimerItem Item(string? label = "focus") =>
        new() { Label = label, OriginalDuration = TimeSpan.FromMinutes(25) };

    [Fact]
    public void Title_falls_back_when_label_is_blank()
    {
        Assert.Equal("Timer complete", new PopupViewModel(Item(" "), _ => Item(), _ => Item(), _ => { }).Title);
        Assert.Equal("focus", new PopupViewModel(Item("focus"), _ => Item(), _ => Item(), _ => { }).Title);
    }

    [Fact]
    public void Plus5_is_debounced_against_double_trigger()
    {
        var snoozes = 0;
        var vm = new PopupViewModel(Item(), _ => { snoozes++; return Item(); }, _ => Item(), _ => { });
        vm.Plus5Command.Execute(null);
        vm.Plus5Command.Execute(null);     // fast double-click
        Assert.Equal(1, snoozes);          // performed once
    }

    [Fact]
    public void Dismiss_raises_CloseRequested_and_calls_callback_once()
    {
        var dismissed = 0; var closed = 0;
        var vm = new PopupViewModel(Item(), _ => Item(), _ => Item(), _ => dismissed++);
        vm.CloseRequested += (_, _) => closed++;
        vm.DismissCommand.Execute(null);
        vm.DismissCommand.Execute(null);
        Assert.Equal(1, dismissed);
        Assert.Equal(1, closed);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test --filter PopupViewModelTests`
Expected: FAIL — `PopupViewModel` does not exist.

- [ ] **Step 3: Implement**

`src/Tidsro/ViewModels/PopupViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidsro.Models;

namespace Tidsro.ViewModels;

public partial class PopupViewModel : ObservableObject
{
    private readonly TimerItem _item;
    private readonly Func<TimerItem, TimerItem> _onSnooze;
    private readonly Func<TimerItem, TimerItem> _onRestart;
    private readonly Action<TimerItem> _onDismiss;
    private bool _handled;   // debounce: one action per card

    [ObservableProperty] private string _title;

    public PopupViewModel(TimerItem item,
        Func<TimerItem, TimerItem> onSnooze,
        Func<TimerItem, TimerItem> onRestart,
        Action<TimerItem> onDismiss)
    {
        _item = item; _onSnooze = onSnooze; _onRestart = onRestart; _onDismiss = onDismiss;
        _title = string.IsNullOrWhiteSpace(item.Label) ? "Timer complete" : item.Label!;
    }

    public TimerItem Item => _item;
    public event EventHandler? CloseRequested;

    [RelayCommand] private void Plus5()   { if (Begin()) { _onSnooze(_item);  Close(); } }
    [RelayCommand] private void Restart() { if (Begin()) { _onRestart(_item); Close(); } }
    [RelayCommand] private void Dismiss() { if (Begin()) { _onDismiss(_item); Close(); } }

    private bool Begin() { if (_handled) return false; _handled = true; return true; }
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test --filter PopupViewModelTests`
Expected: PASS (3 tests green).

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "Add popup view-model with debounced +5/Restart/Dismiss"
```

### Task 16: TimerItemViewModel + MainViewModel (TDD)

**Files:**
- Create: `src/Tidsro/ViewModels/TimerItemViewModel.cs`, `src/Tidsro/ViewModels/MainViewModel.cs`
- Test: `tests/Tidsro.Tests/MainViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/Tidsro.Tests/MainViewModelTests.cs`:
```csharp
using Tidsro.Models;
using Tidsro.Services;
using Tidsro.ViewModels;
using Xunit;

namespace Tidsro.Tests;

public class MainViewModelTests
{
    private static MainViewModel New(out FakeClock clock, out SchedulerService sched)
    {
        clock = new FakeClock();
        sched = new SchedulerService(clock);
        return new MainViewModel(sched, SoundChoice.None);
    }

    [Fact]
    public void StartPreset_adds_a_running_row()
    {
        var vm = New(out _, out _);
        vm.StartPresetCommand.Execute(30);   // 30 minutes
        Assert.Single(vm.Running);
        Assert.False(vm.IsDayEmpty == false); // Your day stays empty in Slice 1
    }

    [Fact]
    public void StartCustom_with_valid_input_adds_row_and_clears_error()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00";
        vm.Label = "tea";
        vm.StartCustomCommand.Execute(null);
        Assert.Single(vm.Running);
        Assert.Null(vm.CustomError);
        Assert.Equal("tea", vm.Running[0].Label);
    }

    [Fact]
    public void StartCustom_with_bad_input_shows_error_and_adds_nothing()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "0";
        vm.StartCustomCommand.Execute(null);
        Assert.Empty(vm.Running);
        Assert.NotNull(vm.CustomError);
    }

    [Fact]
    public void Your_day_empty_state_is_true_in_slice_1()
    {
        var vm = New(out _, out _);
        Assert.True(vm.IsDayEmpty);   // agenda goes live in Slice 2
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test --filter MainViewModelTests`
Expected: FAIL — view-models do not exist.

- [ ] **Step 3: Implement TimerItemViewModel**

`src/Tidsro/ViewModels/TimerItemViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidsro.Models;
using Tidsro.Services;

namespace Tidsro.ViewModels;

public partial class TimerItemViewModel : ObservableObject
{
    private readonly SchedulerService _scheduler;
    public TimerItem Item { get; }

    [ObservableProperty] private string _remainingText = "00:00";
    [ObservableProperty] private bool _isPaused;

    public TimerItemViewModel(TimerItem item, SchedulerService scheduler)
    {
        Item = item; _scheduler = scheduler;
        Refresh();
    }

    public string? Label => Item.Label;
    public bool HasSound => Item.Sound != SoundChoice.None;
    public string SoundTag => HasSound ? "sound" : "silent";

    public void Refresh()
    {
        var r = _scheduler.Remaining(Item);
        RemainingText = r.Hours > 0 ? r.ToString(@"h\:mm\:ss") : r.ToString(@"mm\:ss");
        IsPaused = Item.State == TimerState.Paused;
    }

    [RelayCommand] private void PauseResume()
    {
        if (Item.State == TimerState.Running) _scheduler.Pause(Item);
        else if (Item.State == TimerState.Paused) _scheduler.Resume(Item);
        Refresh();
    }

    [RelayCommand] private void Cancel() => _scheduler.Cancel(Item);
}
```

- [ ] **Step 4: Implement MainViewModel**

`src/Tidsro/ViewModels/MainViewModel.cs`:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidsro.Models;
using Tidsro.Services;

namespace Tidsro.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SchedulerService _scheduler;
    private SoundChoice _defaultSound;

    public ObservableCollection<TimerItemViewModel> Running { get; } = new();
    public int[] Presets { get; } = { 15, 30, 60 };

    [ObservableProperty] private string _customInput = "";
    [ObservableProperty] private string _label = "";
    [ObservableProperty] private string? _customError;

    /// <summary>Your day agenda is empty until Slice 2 (clock-time alarms).</summary>
    public bool IsDayEmpty => true;

    public MainViewModel(SchedulerService scheduler, SoundChoice defaultSound)
    {
        _scheduler = scheduler;
        _defaultSound = defaultSound;
    }

    public void SetDefaultSound(SoundChoice sound) => _defaultSound = sound;

    [RelayCommand] private void StartPreset(int minutes) =>
        Add(TimeSpan.FromMinutes(minutes));

    [RelayCommand] private void StartCustom()
    {
        if (!CountdownRules.TryParse(CustomInput, out var d, out var error))
        { CustomError = error; return; }
        CustomError = null;
        Add(d);
        CustomInput = ""; Label = "";
    }

    private void Add(TimeSpan duration)
    {
        var label = string.IsNullOrWhiteSpace(Label) ? null : Label.Trim();
        var item = _scheduler.StartCountdown(duration, label, _defaultSound);
        Running.Add(new TimerItemViewModel(item, _scheduler));
    }

    public void RefreshAll()
    {
        // drop rows whose underlying timer is no longer running (cancelled/fired+dismissed)
        for (var i = Running.Count - 1; i >= 0; i--)
        {
            if (!_scheduler.Running.Contains(Running[i].Item)) Running.RemoveAt(i);
            else Running[i].Refresh();
        }
    }
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test --filter MainViewModelTests`
Expected: PASS (4 tests green). Then run the full suite: `dotnet test` → all green.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "Add main and timer-row view-models with preset/custom start and validation"
```

---

## Phase 7 — Views & wiring

### Task 17: CompletionPopup — non-activating card with full keyboard path + UIA announce

**Files:**
- Create: `src/Tidsro/Views/CompletionPopup.xaml`, `src/Tidsro/Views/CompletionPopup.xaml.cs`

> This is the spec's headline accessibility surface (§5.3, §9). The window never auto-activates, yet every action is keyboard-reachable on focus and announced to screen readers.

- [ ] **Step 1: Create the XAML**

`src/Tidsro/Views/CompletionPopup.xaml`:
```xml
<Window x:Class="Tidsro.Views.CompletionPopup"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        ShowActivated="False" Topmost="True" ShowInTaskbar="False"
        SizeToContent="WidthAndHeight" ResizeMode="NoResize"
        AutomationProperties.Name="Timer complete"
        FontFamily="{StaticResource FontSans}">
  <Border Background="{StaticResource CardBg}" CornerRadius="{StaticResource RadiusMd}"
          BorderBrush="{StaticResource Border}" BorderThickness="1"
          Padding="{StaticResource CardPadding}" Width="320" Effect="{StaticResource CardShadow}">
    <StackPanel>
      <DockPanel>
        <TextBlock Text="✓ complete" Foreground="{StaticResource TextFaint}"
                   FontSize="{StaticResource TextXs}" VerticalAlignment="Center"/>
        <Button x:Name="DismissX" DockPanel.Dock="Right" Content="✕"
                Command="{Binding DismissCommand}"
                AutomationProperties.Name="Dismiss" ToolTip="Dismiss"
                Background="Transparent" BorderThickness="0"
                Foreground="{StaticResource TextMuted}" FontSize="{StaticResource TextMd}"
                Padding="6" Cursor="Hand" HorizontalAlignment="Right"/>
      </DockPanel>
      <TextBlock Text="{Binding Title}" Foreground="{StaticResource Text}"
                 FontSize="{StaticResource TextLg}" TextWrapping="Wrap" Margin="0,4,0,12"/>
      <!-- Actions: hidden for mouse-at-rest, shown on hover OR when the card has keyboard focus -->
      <StackPanel x:Name="Actions" Orientation="Horizontal" Opacity="0">
        <Button Content="+5 min" Command="{Binding Plus5Command}"
                AutomationProperties.Name="Add 5 minutes" Style="{StaticResource QuietAction}"/>
        <Button Content="Restart" Command="{Binding RestartCommand}"
                AutomationProperties.Name="Restart timer" Style="{StaticResource QuietAction}" Margin="8,0,0,0"/>
        <Button Content="Dismiss" Command="{Binding DismissCommand}"
                AutomationProperties.Name="Dismiss" Style="{StaticResource QuietAction}" Margin="8,0,0,0"/>
      </StackPanel>
    </StackPanel>
  </Border>
</Window>
```

Add the `QuietAction` button style to `tokens.xaml` (within the `ResourceDictionary`):
```xml
<Style x:Key="QuietAction" TargetType="Button">
  <Setter Property="Background" Value="{StaticResource InteractiveBg}"/>
  <Setter Property="Foreground" Value="{StaticResource Text}"/>
  <Setter Property="BorderBrush" Value="{StaticResource Border}"/>
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="Padding" Value="10,6"/>
  <Setter Property="FontSize" Value="{StaticResource TextSm}"/>
  <Setter Property="Cursor" Value="Hand"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="Button">
        <Border x:Name="b" Background="{TemplateBinding Background}" CornerRadius="8"
                BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"
                Padding="{TemplateBinding Padding}">
          <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter TargetName="b" Property="Background" Value="{StaticResource ElevatedBg}"/>
          </Trigger>
          <Trigger Property="IsKeyboardFocused" Value="True">
            <Setter TargetName="b" Property="BorderBrush" Value="{StaticResource AccentStrong}"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

- [ ] **Step 2: Code-behind — non-activating window, reveal-on-focus/hover, focus return, UIA announce**

`src/Tidsro/Views/CompletionPopup.xaml.cs`:
```csharp
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Tidsro.Services;
using Tidsro.ViewModels;

namespace Tidsro.Views;

public partial class CompletionPopup : Window
{
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    private const int GWL_EXSTYLE = -20, WS_EX_NOACTIVATE = 0x08000000;

    private readonly PopupViewModel _vm;
    private IntPtr _previousForeground;

    public CompletionPopup(PopupViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        vm.CloseRequested += (_, _) => Close();

        // remember who had focus so we can return it on dismiss
        _previousForeground = NativeFocus.GetForegroundWindow();

        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            SetWindowLong(h, GWL_EXSTYLE, GetWindowLong(h, GWL_EXSTYLE) | WS_EX_NOACTIVATE);
        };

        // reveal actions on hover OR keyboard focus anywhere in the card
        MouseEnter += (_, _) => Actions.Opacity = 1;
        MouseLeave += (_, _) => { if (!IsKeyboardFocusWithin) Actions.Opacity = 0; };
        IsKeyboardFocusWithinChanged += (_, e) => { if ((bool)e.NewValue) Actions.Opacity = 1; else if (!IsMouseOver) Actions.Opacity = 0; };

        Loaded += (_, _) => UiaNotifier.Announce(this, $"{_vm.Title} complete");
        Closed += (_, _) => NativeFocus.Restore(_previousForeground);
    }

    /// <summary>Called by the global hotkey: pull this card into keyboard focus on demand.</summary>
    public void FocusForKeyboard()
    {
        Activate();
        Actions.Opacity = 1;
        DismissX.Focus();
    }
}
```

`src/Tidsro/Services/NativeFocus.cs`:
```csharp
using System.Runtime.InteropServices;

namespace Tidsro.Services;

internal static class NativeFocus
{
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);

    internal static void Restore(IntPtr hWnd)
    {
        if (hWnd != IntPtr.Zero) SetForegroundWindow(hWnd);
    }
}
```

- [ ] **Step 3: Build**

Run `dotnet build`. Expected: PASS.

- [ ] **Step 4: Manual verify (mouse, keyboard, screen reader)** — see §11 hand-checks

Wire a temporary test trigger (or wait for Task 19), then verify:
1. **Mouse:** card appears bottom-right, does NOT take focus (a text editor keeps the caret/typing). At rest only `✓ complete` + `✕`. Hover → +5 / Restart / Dismiss fade in.
2. **Keyboard:** with the card up, press **Ctrl+Alt+T** → focus lands on the card, actions visible, Tab cycles +5 → Restart → Dismiss, Enter activates; after Dismiss, focus returns to the prior app.
3. **Screen reader (Narrator):** on appear, hear "<label> complete" with no focus jump.

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "Add non-activating completion card: keyboard path, focus return, UIA announce"
```

### Task 18: MainWindow (two zones) + SettingsWindow

**Files:**
- Replace: `src/Tidsro/MainWindow.xaml(.cs)` → move to `src/Tidsro/Views/MainWindow.xaml(.cs)`
- Create: `src/Tidsro/Views/SettingsWindow.xaml(.cs)`, `src/Tidsro/ViewModels/SettingsViewModel.cs`

- [ ] **Step 1: SettingsViewModel**

`src/Tidsro/ViewModels/SettingsViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Tidsro.Models;
using Tidsro.Services;

namespace Tidsro.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly StartupService _startup;
    private readonly PersistenceService _persistence;
    private readonly Action<SoundChoice> _onDefaultSoundChanged;

    [ObservableProperty] private bool _launchAtStartup;
    [ObservableProperty] private SoundChoice _defaultSound;

    public SoundChoice[] SoundOptions { get; } =
        { SoundChoice.None, SoundChoice.SoftChime, SoundChoice.Marimba, SoundChoice.Bell };

    public SettingsViewModel(AppSettings settings, StartupService startup,
        PersistenceService persistence, Action<SoundChoice> onDefaultSoundChanged)
    {
        _startup = startup; _persistence = persistence; _onDefaultSoundChanged = onDefaultSoundChanged;
        _launchAtStartup = settings.LaunchAtStartup;
        _defaultSound = settings.DefaultSound;
    }

    partial void OnLaunchAtStartupChanged(bool value)
    {
        if (value) _startup.Enable(); else _startup.Disable();
        Persist();
    }

    partial void OnDefaultSoundChanged(SoundChoice value)
    {
        _onDefaultSoundChanged(value);
        Persist();
    }

    private void Persist() =>
        _persistence.Save(new AppSettings { LaunchAtStartup = LaunchAtStartup, DefaultSound = DefaultSound });
}
```

- [ ] **Step 2: MainWindow XAML (close = minimise to tray)**

`src/Tidsro/Views/MainWindow.xaml`:
```xml
<Window x:Class="Tidsro.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Tidsro" Width="420" Height="560"
        Background="{StaticResource PageBg}" Foreground="{StaticResource Text}"
        FontFamily="{StaticResource FontSans}">
  <Grid Margin="16">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>

    <!-- Quick timers -->
    <TextBlock Grid.Row="0" Text="Quick timers" FontSize="{StaticResource TextXl}" Margin="0,0,0,8"/>
    <StackPanel Grid.Row="1">
      <StackPanel Orientation="Horizontal">
        <Button Content="15" Width="56" Command="{Binding StartPresetCommand}" CommandParameter="15" Style="{StaticResource QuietAction}"/>
        <Button Content="30" Width="56" Command="{Binding StartPresetCommand}" CommandParameter="30" Style="{StaticResource QuietAction}" Margin="8,0,0,0"/>
        <Button Content="60" Width="56" Command="{Binding StartPresetCommand}" CommandParameter="60" Style="{StaticResource QuietAction}" Margin="8,0,0,0"/>
      </StackPanel>
      <Grid Margin="0,8,0,0">
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width="*"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        <TextBox Grid.Column="0" Text="{Binding CustomInput, UpdateSourceTrigger=PropertyChanged}"
                 AutomationProperties.Name="Custom duration" ToolTip="e.g. 25, 5:00, 1:30:00"/>
        <TextBox Grid.Column="1" Text="{Binding Label, UpdateSourceTrigger=PropertyChanged}"
                 AutomationProperties.Name="Label" Margin="8,0,0,0"/>
        <Button Grid.Column="2" Content="Start" Command="{Binding StartCustomCommand}" Style="{StaticResource QuietAction}" Margin="8,0,0,0"/>
      </Grid>
      <TextBlock Text="{Binding CustomError}" Foreground="{StaticResource Danger}"
                 FontSize="{StaticResource TextXs}" Margin="0,4,0,0"
                 Visibility="{Binding CustomError, Converter={StaticResource NullToCollapsed}}"/>
      <ItemsControl ItemsSource="{Binding Running}" Margin="0,12,0,0">
        <ItemsControl.ItemTemplate>
          <DataTemplate>
            <Border Background="{StaticResource CardBg}" CornerRadius="{StaticResource RadiusSm}"
                    Padding="12" Margin="0,0,0,8">
              <DockPanel>
                <Button DockPanel.Dock="Right" Content="✕" Command="{Binding CancelCommand}"
                        AutomationProperties.Name="Cancel timer" Style="{StaticResource QuietAction}"/>
                <Button DockPanel.Dock="Right" Content="⏸" Command="{Binding PauseResumeCommand}"
                        AutomationProperties.Name="Pause or resume" Style="{StaticResource QuietAction}" Margin="0,0,8,0"/>
                <StackPanel>
                  <TextBlock Text="{Binding Label}" Foreground="{StaticResource TextMuted}" FontSize="{StaticResource TextXs}"/>
                  <TextBlock Text="{Binding RemainingText}" FontFamily="{StaticResource FontMono}" FontSize="{StaticResource Text2xl}"/>
                  <TextBlock Text="{Binding SoundTag}" Foreground="{StaticResource TextFaint}" FontSize="{StaticResource TextXs}"/>
                </StackPanel>
              </DockPanel>
            </Border>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </StackPanel>

    <!-- Your day (empty until Slice 2) -->
    <StackPanel Grid.Row="2" VerticalAlignment="Top" Margin="0,16,0,0">
      <TextBlock Text="Your day" FontSize="{StaticResource TextXl}" Margin="0,0,0,8"/>
      <TextBlock Text="Nothing scheduled yet — add an alarm" Foreground="{StaticResource TextFaint}"
                 FontSize="{StaticResource TextSm}"
                 Visibility="{Binding IsDayEmpty, Converter={StaticResource BoolToVisible}}"/>
    </StackPanel>

    <Button Grid.Row="3" Content="Settings" HorizontalAlignment="Left"
            Click="OnSettings" Style="{StaticResource QuietAction}"/>
  </Grid>
</Window>
```

Add two value converters in `tokens.xaml` namespace imports + resources (or a small `Converters.cs`). Create `src/Tidsro/Views/Converters.cs`:
```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Tidsro.Views;

public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is null || (v is string s && s.Length == 0) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class BoolToVisibleConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
```
Register them in `tokens.xaml`:
```xml
<ResourceDictionary ... xmlns:v="clr-namespace:Tidsro.Views">
  <v:NullToCollapsedConverter x:Key="NullToCollapsed"/>
  <v:BoolToVisibleConverter x:Key="BoolToVisible"/>
  ...
```

- [ ] **Step 3: MainWindow code-behind (close minimises to tray)**

`src/Tidsro/Views/MainWindow.xaml.cs`:
```csharp
using System.ComponentModel;
using System.Windows;
using Tidsro.ViewModels;

namespace Tidsro.Views;

public partial class MainWindow : Window
{
    private readonly Func<SettingsWindow> _settingsFactory;

    public MainWindow(MainViewModel vm, Func<SettingsWindow> settingsFactory)
    {
        InitializeComponent();
        DataContext = vm;
        _settingsFactory = settingsFactory;
    }

    // ✕ on the window hides to tray instead of quitting (real Quit is in the tray menu)
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        var w = _settingsFactory();
        w.Owner = this;
        w.ShowDialog();
    }
}
```

- [ ] **Step 4: SettingsWindow XAML + code-behind**

`src/Tidsro/Views/SettingsWindow.xaml`:
```xml
<Window x:Class="Tidsro.Views.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Settings" Width="360" Height="220" WindowStartupLocation="CenterOwner"
        Background="{StaticResource PageBg}" Foreground="{StaticResource Text}"
        FontFamily="{StaticResource FontSans}">
  <StackPanel Margin="16">
    <CheckBox Content="Launch Tidsro at startup" IsChecked="{Binding LaunchAtStartup}"
              AutomationProperties.Name="Launch at startup" Margin="0,0,0,16"/>
    <TextBlock Text="Default sound for new timers" Margin="0,0,0,4"/>
    <ComboBox ItemsSource="{Binding SoundOptions}" SelectedItem="{Binding DefaultSound}"
              AutomationProperties.Name="Default sound"/>
  </StackPanel>
</Window>
```
`src/Tidsro/Views/SettingsWindow.xaml.cs`:
```csharp
using System.Windows;
using Tidsro.ViewModels;

namespace Tidsro.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
```

- [ ] **Step 5: Build**

Run `dotnet build`. Fix the `MainWindow` namespace move (it's now `Tidsro.Views.MainWindow`; update `App.xaml.cs` references in Task 19). Expected: build may fail until Task 19 wires App — that's expected; if building standalone, comment the App reference temporarily. Prefer to do Task 19 next, then build.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "Add main window (two zones), settings window, and view converters"
```

### Task 19: Wire it all together in App

**Files:**
- Modify: `src/Tidsro/App.xaml.cs`

- [ ] **Step 1: Compose the object graph and the 1-second tick**

Replace `src/Tidsro/App.xaml.cs`:
```csharp
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using H.NotifyIcon;
using Tidsro.Models;
using Tidsro.Services;
using Tidsro.ViewModels;
using Tidsro.Views;

namespace Tidsro;

public partial class App : Application
{
    private TaskbarIcon? _tray;
    private SchedulerService _scheduler = null!;
    private SoundService _sound = null!;
    private PersistenceService _persistence = null!;
    private MainViewModel _mainVm = null!;
    private AppSettings _settings = null!;
    private HotkeyService _hotkey = null!;
    private DispatcherTimer _timer = null!;
    private MainWindow? _main;
    private readonly List<CompletionPopup> _openPopups = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _persistence = new PersistenceService(PersistenceService.DefaultPath);
        _settings = _persistence.Load();
        _scheduler = new SchedulerService(new SystemClock());
        _sound = new SoundService();
        _mainVm = new MainViewModel(_scheduler, _settings.DefaultSound);

        var startup = new StartupService(StartupService.CurrentExePath);
        startup.RefreshIfEnabled();          // self-heal a stale Run-key path

        _scheduler.Fired += OnTimerFired;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => { _scheduler.Tick(); _mainVm.RefreshAll(); };
        _timer.Start();

        _hotkey = new HotkeyService();
        _hotkey.Pressed += (_, _) => _openPopups.LastOrDefault()?.FocusForKeyboard();
        _hotkey.Register();

        _tray = TrayBuilder.Create(ShowMainWindow, Quit);
    }

    private void OnTimerFired(object? sender, TimerItem item)
    {
        _sound.Play(item.Sound);

        var vm = new PopupViewModel(item,
            onSnooze: i => _scheduler.Snooze(i, TimeSpan.FromMinutes(5)),
            onRestart: i => _scheduler.Restart(i),
            onDismiss: i => _scheduler.Cancel(i));

        var popup = new CompletionPopup(vm);
        popup.Closed += (_, _) => { _openPopups.Remove(popup); RestackPopups(); };
        _openPopups.Add(popup);
        PositionPopup(popup, _openPopups.Count - 1);
        popup.Show();   // ShowActivated=false -> appears without stealing focus
    }

    private void PositionPopup(CompletionPopup popup, int indexFromBottom)
    {
        var anchor = _main ?? (Application.Current.MainWindow as MainWindow);
        var work = anchor is not null ? ScreenHelper.WorkAreaForWindow(anchor) : SystemParameters.WorkArea;
        popup.UpdateLayout();
        var size = new Size(popup.Width, popup.ActualHeight > 0 ? popup.ActualHeight : 140);
        var p = ScreenHelper.ClampBottomRight(work, size, 16);
        popup.Left = p.X;
        popup.Top = p.Y - indexFromBottom * (size.Height + 8);   // stack upward
    }

    private void RestackPopups()
    {
        for (var i = 0; i < _openPopups.Count; i++) PositionPopup(_openPopups[i], i);
    }

    private void ShowMainWindow()
    {
        _main ??= new MainWindow(_mainVm, () => new SettingsWindow(
            new SettingsViewModel(_settings, new StartupService(StartupService.CurrentExePath),
                _persistence, _mainVm.SetDefaultSound)));
        Application.Current.MainWindow = _main;
        _main.Show();
        _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    private void Quit()
    {
        _timer.Stop();
        _hotkey.Dispose();
        _tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e) { _tray?.Dispose(); base.OnExit(e); }
}
```

- [ ] **Step 2: Build and run the whole app**

Run:
```powershell
dotnet build
dotnet run --project src/Tidsro
```
Expected: builds; app boots to tray; open window; start a 15-min preset and a custom `0:05` (5-second) timer to see firing fast.

- [ ] **Step 3: Commit**

```powershell
git add -A
git commit -m "Wire app: tick loop, fired -> sound + stacked popups, tray, hotkey, settings"
```

---

## Phase 8 — Integration, accessibility & polish

### Task 20: Calm fade-in respecting reduced motion

**Files:**
- Modify: `src/Tidsro/Views/CompletionPopup.xaml.cs`

- [ ] **Step 1: Add an opacity fade gated on the OS reduced-motion setting**

In `CompletionPopup` constructor, after `Loaded` announce, add:
```csharp
Loaded += (_, _) =>
{
    UiaNotifier.Announce(this, $"{_vm.Title} complete");
    if (!SystemParameters.ClientAreaAnimation)   // reduced motion -> no fade
    { Opacity = 1; return; }
    Opacity = 0;
    var fade = new System.Windows.Media.Animation.DoubleAnimation(0, 1,
        (Duration)FindResource("DurationBase")) { EasingFunction = new System.Windows.Media.Animation.CubicEase() };
    BeginAnimation(OpacityProperty, fade);
};
```
(Remove the earlier bare `Loaded` announce so it's not registered twice.)

- [ ] **Step 2: Manual verify**

Run the app, fire a timer → gentle fade-in. Turn on Windows "Show animations" off (Settings → Accessibility → Visual effects) → the card appears instantly, no fade. No flashing in either mode.

- [ ] **Step 3: Commit**

```powershell
git add -A
git commit -m "Fade the completion card in, skipping it under reduced motion"
```

### Task 21: Full manual acceptance pass (spec §11 hand-checks) + README note

**Files:**
- Modify: `README.md` (add a short "Slice 1 — Countdowns" usage line)

- [ ] **Step 1: Walk the acceptance checklist** (record pass/fail; fix regressions before commit)

- [ ] App launches to the tray, no window; tray left-click opens the window; tray **Quit** exits; window **✕** hides to tray (process keeps running).
- [ ] Presets 15/30/60 start countdowns; custom accepts `25`, `5:00`, `1:30:00`; rejects `0`, `25:00:00`, `abc` with a calm inline message.
- [ ] Multiple countdowns run at once; each shows mm:ss (or h:mm:ss), pause/resume, cancel.
- [ ] On finish: a bottom-right card appears, **does not steal focus** (keep typing in another app), persists until dismissed, plays the chosen sound once (or is silent).
- [ ] **Keyboard:** Ctrl+Alt+T focuses the latest card; Tab reaches +5/Restart/Dismiss; Enter activates; focus returns to the prior app on dismiss.
- [ ] **+5** re-arms a 5-minute countdown; **Restart** re-runs the original; double-clicking either does it once.
- [ ] Multiple finished cards stack upward and dismiss independently; unplugging the second monitor (or lowering resolution) never strands a card off-screen.
- [ ] Settings: launch-at-startup writes a **quoted** `HKCU\…\Run\Tidsro` value and removes it when unticked; default sound persists across a restart.
- [ ] Kill the app mid-run, relaunch → no crash; corrupt `%AppData%\Tidsro\data.json` by hand, relaunch → app starts with defaults and a `data.json.corrupt` appears.
- [ ] **Screen reader (Narrator):** finishing a timer is announced without focus moving.

- [ ] **Step 2: Run the full test suite**

Run: `dotnet test`
Expected: all green.

- [ ] **Step 3: Add a short README usage note and commit**

```powershell
git add -A
git commit -m "Document Slice 1 usage and complete countdown acceptance pass"
```

---

## Self-review

**1. Spec coverage (§ → task):**
- §5.1 triggers/Countdown + bounds → Tasks 5, 16
- §5.2 main window, Quick timers live, Your day empty-state → Tasks 16, 18
- §5.3 completion alert (non-activating, keyboard path, reveal-on-focus, announce, stacking, clamp) → Tasks 10, 14, 17, 19, 20
- §5.4 sounds (silent default, play once) → Task 11
- §5.5 tray & background → Tasks 2, 19
- §5.6 persistence (settings, atomic, tolerant, untrusted) → Tasks 8, 9
- §5.7 settings (launch-at-startup quoted/self-healing, default sound) → Tasks 12, 18, 19
- §8 single-fire, +5/Restart debounce, off-screen clamp → Tasks 7, 10, 15
- §9 accessibility (keyboard, UIA name + notification, focus return, reduced motion) → Tasks 14, 17, 20
- §10 no telemetry / no labels in logs, quoted Run entry → cross-cutting + Task 12
- §11 unit tests + manual hand-checks → Tasks 5,7,9,10,12,15,16 + Task 21
- §6 Slice 1 scope (multiple simultaneous, stacking) → Tasks 16, 19

*Deferred to later slices (correctly out of Slice 1):* clock-time/recurring alarms, alarm delete-undo, agenda tie-break, sleep/resume dedup + grace window, edit-during-fire — all Slice 2/3 per §6.

**2. Placeholder scan:** No `TODO`/`TBD`/"add error handling" left. Asset files (`.ico`, `.wav`) are sourced, not coded — flagged as asset steps, not placeholders.

**3. Type consistency:** `SchedulerService` members (`StartCountdown`, `Tick`, `Remaining`, `Pause/Resume/Cancel`, `Snooze`, `Restart`, `Fired`, `Running`) are used identically in Tasks 15/16/19. `PopupViewModel` ctor signature `(TimerItem, Func<TimerItem,TimerItem> onSnooze, Func<TimerItem,TimerItem> onRestart, Action<TimerItem> onDismiss)` matches Task 15 tests and Task 19 wiring. `AppSettings` shape matches across Tasks 8/9/18. `ScreenHelper.ClampBottomRight(Rect, Size, double)` matches Task 10 tests and Task 19 usage.

---

## Stress-test findings & fixes

Adversarial review of this plan (2026-06-04). Apply each fix as you reach the named task. Severity: 🔴 build/feature-breaking · 🟠 real gap · 🟡 polish.

### 🔴 Must fix
1. **Task 1 — `dotnet new` framework value.** `-f net10.0-windows` is rejected by the templates (they accept `net10.0` and append `-windows`). **Fix:** use `-f net10.0` for both `dotnet new wpf` and `dotnet new xunit`.
2. **Task 1 — test project TFM.** `dotnet new xunit` yields `net10.0`; referencing the WPF app and using `System.Windows` geometry (and `UseWPF`) needs a `-windows` TFM. **Fix:** set `tests/Tidsro.Tests/Tidsro.Tests.csproj` to `<TargetFramework>net10.0-windows</TargetFramework>` (then `UseWPF` is valid).
3. **Task 1 — manifest ordering.** Step 3 sets `<ApplicationManifest>app.manifest</ApplicationManifest>` but the file is created in Task 2 → Step 5 build fails. **Fix:** create `app.manifest` (Task 2 Step 1 content) during Task 1, before the property references it.
4. **Task 18 — preset `CommandParameter` type.** `CommandParameter="15"` passes a **string** to `IRelayCommand<int>` → preset buttons throw/disable at runtime. The Task 16 unit test passes a real `int`, so it stays green while the app is broken. **Fix:** pass an int — add `xmlns:sys="clr-namespace:System;assembly=System.Runtime"` and `<Button.CommandParameter><sys:Int32>15</sys:Int32></Button.CommandParameter>` (×3), or bind the `Presets` items so the parameter is the bound int.
5. **Tasks 16/19 — re-armed countdowns are headless.** Snooze/Restart create scheduler items directly; `MainViewModel.RefreshAll` only refreshes/removes existing rows, never adds → a +5/Restart timer runs with no row (can't see/pause/cancel) until it fires. **Fix:** in `RefreshAll`, after pruning stale rows, reconcile — for each `_scheduler.Running` item with no matching `TimerItemViewModel`, add one.
6. **Task 17 — keyboard focus to a no-activate window.** `Activate()` on a `WS_EX_NOACTIVATE` window won't reliably take keyboard focus → the headline accessibility path may silently fail. **Fix:** in `FocusForKeyboard`, clear `WS_EX_NOACTIVATE` via `SetWindowLong`, call `SetForegroundWindow(hwnd)`, then `Keyboard.Focus(DismissX)`; re-apply NOACTIVATE on blur if needed. Add a Narrator/keyboard hand-check that focus actually lands and returns.

### 🟠 Important
7. **Tray fallback missing (spec §5.3).** `TrayBuilder` is Open/Quit only; the spec lists active cards in the tray as the keyboard fallback. **Fix:** add a "Focus latest alert" tray item (wired to the same handler as the hotkey), and/or an Alerts submenu.
8. **Task 13/19 — hotkey failure swallowed.** `Register()`'s bool is ignored; if Ctrl+Alt+T is taken the keyboard path dies silently. **Fix:** check the result; on failure rely on the tray fallback (#7) and don't pretend the hotkey works.
9. **Task 18 — settings `Save` can throw.** A locked/failed write propagates out of a property setter (toggle) → possible crash. **Fix:** wrap the `Persist()` call best-effort (try/catch, no labels logged).
10. **Task 19 — popup positioned before measured.** Placement/stacking use a 140 px estimate pre-`Show` → off-by/overlap. **Fix:** position in `ContentRendered`/first `SizeChanged` when `ActualHeight` is real.
11. **Task 12 — startup path under `dotnet run`.** `Environment.ProcessPath` is the dev `bin` exe; the Run entry only makes sense for a built/installed exe. **Fix:** note this; run the manual verify against a published build.

### 🟡 Worth addressing
12. **Task 2 — missing `.ico` crashes startup** (pack-URI `BitmapImage` on a missing file). **Fix:** source it before first run, or fall back to a generated icon in `TrayBuilder`.
13. **Task 16 — fired rows linger at 00:00** in Quick timers until the popup is dismissed. **Fix:** decide — drop the row on fire, or keep it; document the choice.
14. **Task 17 — popup `AutomationProperties.Name` is static** ("Timer complete") and ignores the label. **Fix:** bind it to `Title`.
15. **Task 16 — test gap + nit.** The int/string mismatch (#4) hides under green tests; `Assert.False(vm.IsDayEmpty == false)` is a confusing double-negative. **Fix:** add a note that presets need a XAML-level check; simplify the assert to `Assert.True(vm.IsDayEmpty)`.
16. **Task 19 — unbounded upward stacking** can push cards above the work area with many simultaneous fires. **Fix:** cap visible cards (collapse overflow into a count) or clamp the top.
- **Verify:** confirm the H.NotifyIcon left-click API name (`TrayLeftMouseUp` vs `LeftClickCommand`) against the installed package.

### Clean (stress-tested, no change)
Scheduler core (single-fire guard, pause/resume, snooze/restart), persistence (atomic save, tolerant load, enum sanitization), duration parse/bounds, clamp math, debounce — all correct and covered.

### Considered and rejected (accepted trade-offs)
- **SoundPlayer not retained** — `PlaySound`/SND_ASYNC plays independently of the managed object; accept.
- **`.corrupt` overwritten each load** — keeping only the latest copy is fine.
- **±1 s tick display drift** — acceptable for a calm timer.
- **`0:90` rejected** (seconds > 59) — users type `1:30`; accept.

---

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-06-04-tidsro-slice1-countdowns.md`. Two execution options:

1. **Subagent-Driven (recommended)** — a fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** — execute tasks in this session with checkpoints for review.

Which approach?
