# Tidsro tab shell and running-timer strip — implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the main window's single scrolling page with two real tabs — Quick timers and Schedule — plus a pinned strip that keeps the running countdown visible from either tab.

**Architecture:** A header-only `TabControl` provides tab semantics for keyboard and screen-reader users; the two content panels live beside it in their own grid row, both permanently loaded, with visibility driven by the selected index. The selected tab is a view-model property persisted in `AppSettings`. The strip derives from `Running[0]` and refreshes off `Running.CollectionChanged`.

**Tech Stack:** C# / .NET 10 (`net10.0-windows`), WPF, CommunityToolkit.Mvvm (`[ObservableProperty]` / `[RelayCommand]`), System.Text.Json, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-11-tidsro-tab-shell-design.md`

## Global Constraints

- Target framework `net10.0-windows`; WPF with MVVM and Services as the I/O boundary. No new packages.
- All colours, spacing, radii and font sizes come from `src/Tidsro/Resources/tokens.xaml`. No literal hex in `MainWindow.xaml`.
- Selected state must read from more than colour alone — the gold underline carries it.
- The strip shows and hides without animation. Do not add a storyboard.
- The strip's `AutomationProperties.Name` is a **static string**, never a binding, and it carries no `LiveSetting`.
- `AppSettings.Sanitized()` rebuilds the object property by property. Any property added to `AppSettings` **must** be added there or it silently resets on every load.
- Do not disturb the `ItemContainerStyle` carrying `AutomationProperties.Name` on the alarms `ItemsControl` (commit `37c2f25`). Regressing it makes every alarm row announce its class name.
- Running `Tidsro.exe` locks the build output (MSB3027). Before any build or test run: `Get-Process Tidsro | Stop-Process -Force` — this errors harmlessly when Tidsro is not running.
- Commit messages: **no `Co-Authored-By` trailer and no Claude attribution of any kind.** Malin is the sole listed contributor.
- Full test command, from the repo root: `dotnet test`. Single test: `dotnet test --filter "FullyQualifiedName~TestName"`.
- Baseline before starting: 301 tests passing.

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `src/Tidsro/Models/AppSettings.cs` | Modify | Adds `SelectedTab` + its clamp in `Sanitized` |
| `src/Tidsro/ViewModels/MainViewModel.cs` | Modify | Owns `SelectedTabIndex` and the three derived strip members |
| `src/Tidsro/Views/Converters.cs` | Modify | Adds `IndexToVisibleConverter` |
| `src/Tidsro/Resources/tokens.xaml` | Modify | Adds `ShellTabs` / `ShellTabItem` styles and registers the converter |
| `src/Tidsro/Views/MainWindow.xaml` | Modify | Five-row shell: headers, panels, strip, undo bar, Settings |
| `src/Tidsro/Views/MainWindow.xaml.cs` | Modify | Drops the responsive layout; seeds/saves the tab; focus handling |
| `src/Tidsro/App.xaml.cs` | Modify | Captures window state on exit; resets the tab alongside placement |
| `src/Tidsro/ViewModels/SettingsViewModel.cs` | Modify | Resets `SelectedTab` with the other defaults |
| `tests/Tidsro.Tests/TidsroDataTests.cs` | Modify | Sanitising and back-compat for `SelectedTab` |
| `tests/Tidsro.Tests/PersistenceServiceTests.cs` | Modify | Round-trip for `SelectedTab` |
| `tests/Tidsro.Tests/MainViewModelTests.cs` | Modify | Strip behaviour and default tab |
| `tests/Tidsro.Tests/SettingsViewModelTests.cs` | Modify | Reset returns the tab to 0 |
| `tests/Tidsro.Tests/IndexToVisibleConverterTests.cs` | Create | Converter unit tests |

---

### Task 1: Persist the selected tab in `AppSettings`

**Files:**
- Modify: `src/Tidsro/Models/AppSettings.cs`
- Test: `tests/Tidsro.Tests/TidsroDataTests.cs`, `tests/Tidsro.Tests/PersistenceServiceTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `AppSettings.SelectedTab` (`int`, default 0) and `AppSettings.TabCount` (`const int`, value 2). Every later task binds to or resets `SelectedTab`.

- [ ] **Step 1: Write the failing tests**

Add to `tests/Tidsro.Tests/TidsroDataTests.cs`. It needs `using System.Text.Json;` at the top of the file:

```csharp
    [Fact]
    public void Sanitized_keeps_a_valid_selected_tab()
    {
        var data = new TidsroData { Settings = new AppSettings { SelectedTab = 1 } };
        Assert.Equal(1, data.Sanitized().Settings!.SelectedTab);
    }

    [Fact]
    public void Sanitized_resets_a_selected_tab_outside_the_range()
    {
        var high = new TidsroData { Settings = new AppSettings { SelectedTab = 7 } };
        var low = new TidsroData { Settings = new AppSettings { SelectedTab = -1 } };
        Assert.Equal(0, high.Sanitized().Settings!.SelectedTab);
        Assert.Equal(0, low.Sanitized().Settings!.SelectedTab);
    }

    [Fact]
    public void A_file_written_before_the_tab_shell_loads_on_quick_timers()
    {
        // A v4 file has no SelectedTab key at all — the absent-key case must land on 0, not throw.
        var json = """
        {"SchemaVersion":4,"Settings":{"LaunchAtStartup":false,"DefaultSound":0},
         "Alarms":[],"RecurringAlarms":[]}
        """;
        var data = JsonSerializer.Deserialize<TidsroData>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal(0, data.Sanitized().Settings!.SelectedTab);
    }
```

Add to `tests/Tidsro.Tests/PersistenceServiceTests.cs` (the class already provides `_path` and cleans up in `Dispose`):

```csharp
    [Fact]
    public void SelectedTab_survives_a_save_and_load()
    {
        var svc = new PersistenceService(_path);
        svc.Save(new TidsroData { Settings = new AppSettings { SelectedTab = 1 } });
        Assert.Equal(1, svc.Load().Settings!.SelectedTab);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~selected_tab|FullyQualifiedName~tab_shell_loads"`
Expected: FAIL — compile error, `AppSettings` has no `SelectedTab`.

- [ ] **Step 3: Add the property and its clamp**

In `src/Tidsro/Models/AppSettings.cs`, add below `DefaultSound`:

```csharp
    /// <summary>Index of the tab the main window opens on. 0 = Quick timers, 1 = Schedule.</summary>
    public int SelectedTab { get; set; }

    /// <summary>Tabs the shell has. The weekly timetable slice makes this 3 and needs no other change here.</summary>
    public const int TabCount = 2;
```

In `Sanitized()`, add a line inside the object initialiser, after `DefaultSound`:

```csharp
        SelectedTab = SelectedTab >= 0 && SelectedTab < TabCount ? SelectedTab : 0,
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test`
Expected: PASS, 301 → 305 tests.

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/Models/AppSettings.cs tests/Tidsro.Tests/TidsroDataTests.cs tests/Tidsro.Tests/PersistenceServiceTests.cs
git commit -m "feat(settings): remember which tab the main window opens on"
```

---

### Task 2: `SelectedTabIndex` and the strip on `MainViewModel`

**Files:**
- Modify: `src/Tidsro/ViewModels/MainViewModel.cs`
- Test: `tests/Tidsro.Tests/MainViewModelTests.cs`

**Interfaces:**
- Consumes: `AppSettings.SelectedTab` from Task 1 (read by `MainWindow` in Task 5, not here).
- Produces: `MainViewModel.SelectedTabIndex` (`int`, get/set, observable); `MainViewModel.StripTimer` (`TimerItemViewModel?`); `MainViewModel.ShowStrip` (`bool`); `MainViewModel.StripExtraText` (`string?`). Tasks 5, 6 and 7 all bind to or set these.

- [ ] **Step 1: Write the failing tests**

Add to `tests/Tidsro.Tests/MainViewModelTests.cs`:

```csharp
    [Fact]
    public void The_window_opens_on_quick_timers_by_default()
    {
        Assert.Equal(0, New(out _, out _).SelectedTabIndex);
    }

    [Fact]
    public void Strip_is_empty_when_nothing_is_counting_down()
    {
        var vm = New(out _, out _);
        Assert.Null(vm.StripTimer);
        Assert.False(vm.ShowStrip);
        Assert.Null(vm.StripExtraText);
    }

    [Fact]
    public void Strip_shows_the_countdown_that_finishes_soonest()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "30:00"; vm.Label = "long";  vm.StartCustomCommand.Execute(null);
        vm.CustomInput = "5:00";  vm.Label = "short"; vm.StartCustomCommand.Execute(null);

        Assert.True(vm.ShowStrip);
        Assert.Equal("Short", vm.StripTimer!.Label);
        Assert.Equal("+1 more", vm.StripExtraText);   // counts what the strip is NOT showing
    }

    [Fact]
    public void Strip_shows_a_paused_timer_when_none_are_active()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00"; vm.StartCustomCommand.Execute(null);
        vm.Running[0].PauseResumeCommand.Execute(null);
        vm.RefreshAll();

        Assert.NotNull(vm.StripTimer);          // an IsNext-based strip would go blank here
        Assert.True(vm.StripTimer!.IsPaused);
    }

    [Fact]
    public void Strip_extra_text_is_null_for_a_single_timer()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00"; vm.StartCustomCommand.Execute(null);
        Assert.Null(vm.StripExtraText);
    }

    [Fact]
    public void Cancelling_the_shown_timer_moves_the_strip_to_the_next_one()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "30:00"; vm.Label = "long";  vm.StartCustomCommand.Execute(null);
        vm.CustomInput = "5:00";  vm.Label = "short"; vm.StartCustomCommand.Execute(null);

        vm.CancelTimerCommand.Execute(vm.StripTimer);

        Assert.Equal("Long", vm.StripTimer!.Label);   // no tick needed
        Assert.Null(vm.StripExtraText);
    }

    [Fact]
    public void ClearAllAlarms_empties_the_strip_without_waiting_for_a_tick()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00"; vm.StartCustomCommand.Execute(null);
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.ClearAllAlarms();

        Assert.Null(vm.StripTimer);
        Assert.Contains(nameof(MainViewModel.StripTimer), raised);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~Strip|FullyQualifiedName~quick_timers_by_default"`
Expected: FAIL — compile error, `MainViewModel` has no `StripTimer`.

- [ ] **Step 3: Add the property, the derived members, and the subscription**

In `src/Tidsro/ViewModels/MainViewModel.cs`, add beside the other `[ObservableProperty]` declarations:

```csharp
    /// <summary>Which tab the shell shows. 0 = Quick timers, 1 = Schedule. Seeded from AppSettings
    /// by MainWindow and written back on the same path that saves window placement.</summary>
    [ObservableProperty] private int _selectedTabIndex;
```

Add below `IsDayEmpty`:

```csharp
    /// <summary>The countdown the bottom strip shows. SortRunning already puts active timers first in
    /// finish order and parks paused ones below, so Running[0] is "the soonest active timer, or the
    /// first paused one when nothing is active" — which is exactly what the strip should show. An
    /// IsNext-based strip would go blank the moment every timer was paused.</summary>
    public TimerItemViewModel? StripTimer => Running.FirstOrDefault();

    public bool ShowStrip => StripTimer is not null;

    /// <summary>The timers the strip is not showing, or null when there is only one.</summary>
    public string? StripExtraText => Running.Count > 1 ? $"+{Running.Count - 1} more" : null;

    // Driven off the collection, not off RefreshAll: Add, CancelTimer, UndoDelete and ClearAllAlarms
    // all mutate Running directly, so a tick-driven strip would keep showing a wiped timer for up to
    // 250 ms after "Clear all alarms" — exactly when the user is looking for confirmation. This also
    // catches the Move calls in SortRunning, which change Running[0] without changing the count.
    private void RefreshStrip()
    {
        OnPropertyChanged(nameof(StripTimer));
        OnPropertyChanged(nameof(ShowStrip));
        OnPropertyChanged(nameof(StripExtraText));
    }
```

In the constructor, after the two sound assignments:

```csharp
        Running.CollectionChanged += (_, _) => RefreshStrip();
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test`
Expected: PASS, 305 → 312 tests.

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/ViewModels/MainViewModel.cs tests/Tidsro.Tests/MainViewModelTests.cs
git commit -m "feat(main): add the selected tab and the running-timer strip to the view model"
```

---

### Task 3: `IndexToVisibleConverter`

**Files:**
- Modify: `src/Tidsro/Views/Converters.cs`
- Create: `tests/Tidsro.Tests/IndexToVisibleConverterTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `Tidsro.Views.IndexToVisibleConverter`, registered in `tokens.xaml` as `IndexToVisible` in Task 4 and used by `MainWindow.xaml` in Task 5. Value = the selected index (`int`), `ConverterParameter` = the panel's own index as a string.

- [ ] **Step 1: Write the failing test**

Create `tests/Tidsro.Tests/IndexToVisibleConverterTests.cs`:

```csharp
using System.Globalization;
using System.Windows;
using Tidsro.Views;
using Xunit;

namespace Tidsro.Tests;

public class IndexToVisibleConverterTests
{
    private static object Convert(object? value, object? parameter) =>
        new IndexToVisibleConverter().Convert(value, typeof(Visibility), parameter, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    public void The_selected_panel_is_visible(int selected, string ownIndex) =>
        Assert.Equal(Visibility.Visible, Convert(selected, ownIndex));

    [Theory]
    [InlineData(0, "1")]
    [InlineData(1, "0")]
    public void Every_other_panel_is_collapsed(int selected, string ownIndex) =>
        Assert.Equal(Visibility.Collapsed, Convert(selected, ownIndex));

    [Fact]
    public void A_missing_or_unparseable_parameter_collapses_rather_than_throws() =>
        Assert.Equal(Visibility.Collapsed, Convert(0, null));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~IndexToVisibleConverterTests"`
Expected: FAIL — compile error, `IndexToVisibleConverter` does not exist.

- [ ] **Step 3: Write the converter**

Append to `src/Tidsro/Views/Converters.cs`:

```csharp
/// <summary>Show a shell panel when its own index (ConverterParameter) matches the selected tab.
/// Both panels stay loaded so switching tabs cannot re-run their Loaded fade-in storyboards or lose
/// their scroll position — only visibility changes.</summary>
public sealed class IndexToVisibleConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is int selected
        && p is string s
        && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var own)
        && selected == own
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test`
Expected: PASS, 312 → 317 tests (two `Theory` cases each for the first two, plus one `Fact`).

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/Views/Converters.cs tests/Tidsro.Tests/IndexToVisibleConverterTests.cs
git commit -m "feat(views): add an index-to-visibility converter for the shell panels"
```

---

### Task 4: Tab styles in `tokens.xaml`

**Files:**
- Modify: `src/Tidsro/Resources/tokens.xaml`

**Interfaces:**
- Consumes: `IndexToVisibleConverter` from Task 3.
- Produces: `StaticResource IndexToVisible`, `StaticResource ShellTabs` (a `TabControl` style), `StaticResource ShellTabItem` (a `TabItem` style). Task 5 applies `ShellTabs` and the converter.

No unit test — XAML resources are verified by the app building and by the manual pass in Task 8.

- [ ] **Step 1: Register the converter**

In `src/Tidsro/Resources/tokens.xaml`, below the existing `BoolToSoundGlyphConverter` line:

```xml
  <v:IndexToVisibleConverter x:Key="IndexToVisible"/>
```

- [ ] **Step 2: Add the tab styles**

Append before the closing `</ResourceDictionary>`:

```xml
  <!-- Tab headers. The stock TabControl chrome is light-themed and unusable on PageBg, so both the
       control and its items are templated end to end. Selected state reads from the gold underline,
       not colour alone; 34px minimum height matches TextBox/ComboBox/DayChip so the app's only
       navigation control is not also its smallest mouse target. -->
  <Style x:Key="ShellTabItem" TargetType="TabItem">
    <Setter Property="FocusVisualStyle" Value="{StaticResource ActionFocusVisual}"/>
    <Setter Property="MinHeight" Value="34"/>
    <Setter Property="Padding" Value="14,6"/>
    <Setter Property="Margin" Value="0,0,4,0"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="FontSize" Value="{StaticResource TextSm}"/>
    <Setter Property="Foreground" Value="{StaticResource TextFaint}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="TabItem">
          <Border x:Name="bd" Background="Transparent" BorderThickness="0,0,0,2"
                  BorderBrush="Transparent" Padding="{TemplateBinding Padding}"
                  MinHeight="{TemplateBinding MinHeight}">
            <ContentPresenter ContentSource="Header" VerticalAlignment="Center"
                              TextElement.Foreground="{TemplateBinding Foreground}"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsSelected" Value="True">
              <Setter TargetName="bd" Property="BorderBrush" Value="{StaticResource Accent}"/>
              <Setter Property="Foreground" Value="{StaticResource Text}"/>
            </Trigger>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter Property="Foreground" Value="{StaticResource Text}"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Header-only: the template deliberately has no content host, so both shell panels can live
       outside the control and stay loaded. TabItem still reports as a tab to UIA. -->
  <Style x:Key="ShellTabs" TargetType="TabControl">
    <Setter Property="ItemContainerStyle" Value="{StaticResource ShellTabItem}"/>
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="TabControl">
          <Border BorderBrush="{StaticResource Border}" BorderThickness="0,0,0,1">
            <TabPanel IsItemsHost="True" HorizontalAlignment="Left"/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
```

- [ ] **Step 3: Build to verify the XAML parses**

Run: `Get-Process Tidsro | Stop-Process -Force` then `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Tidsro/Resources/tokens.xaml
git commit -m "feat(theme): add dark tab header styles for the shell"
```

---

### Task 5: Restructure `MainWindow`

**Files:**
- Modify: `src/Tidsro/Views/MainWindow.xaml`
- Modify: `src/Tidsro/Views/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `MainViewModel.SelectedTabIndex`, `StripTimer`, `ShowStrip`, `StripExtraText` (Task 2); `ShellTabs` and `IndexToVisible` (Tasks 3–4); `AppSettings.SelectedTab` (Task 1).
- Produces: `MainWindow.CaptureWindowState()` (public, void) — mutates the shared `AppSettings` without persisting; called by `App.OnExit` in Task 7. Named elements `Tabs` (the `TabControl`) and `Panels` (the grid holding both panels), used by Task 6.

This task has no new unit tests: it is XAML restructuring plus the deletion of layout code. Its verification is that the existing 317 tests still pass and the app runs. Behaviour is checked in Task 8.

- [ ] **Step 1: Replace the root grid's row definitions and add the tab header row**

In `src/Tidsro/Views/MainWindow.xaml`, replace the opening of the root `Grid` (currently three rows) with:

```xml
  <Grid Margin="24">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>   <!-- tab headers -->
      <RowDefinition Height="*"/>      <!-- panels, both loaded -->
      <RowDefinition Height="Auto"/>   <!-- running-timer strip -->
      <RowDefinition Height="Auto"/>   <!-- undo bar -->
      <RowDefinition Height="Auto"/>   <!-- settings -->
    </Grid.RowDefinitions>

    <TabControl x:Name="Tabs" Grid.Row="0" Style="{StaticResource ShellTabs}"
                SelectedIndex="{Binding SelectedTabIndex, Mode=TwoWay}">
      <TabItem Header="Quick timers"/>
      <TabItem Header="Schedule"/>
    </TabControl>
```

- [ ] **Step 2: Replace the single ScrollViewer with the two-panel grid**

Delete the outer `<ScrollViewer Grid.Row="0">`, the `<Grid x:Name="Sections">` with its row definitions, and the `<Border x:Name="Divider" .../>`. In their place:

```xml
    <Grid x:Name="Panels" Grid.Row="1" Margin="0,16,0,0">
      <ScrollViewer VerticalScrollBarVisibility="Auto"
                    Visibility="{Binding SelectedTabIndex, Converter={StaticResource IndexToVisible}, ConverterParameter=0}">
        <StackPanel x:Name="QuickPanel">
          <!-- MainWindow.xaml lines 27-141 move here verbatim: the presets/custom-duration Border,
               then the Running ItemsControl. Line 25, the "Quick timers" heading, is deleted. -->
        </StackPanel>
      </ScrollViewer>

      <ScrollViewer VerticalScrollBarVisibility="Auto"
                    Visibility="{Binding SelectedTabIndex, Converter={StaticResource IndexToVisible}, ConverterParameter=1}">
        <StackPanel x:Name="DayPanel">
          <!-- MainWindow.xaml lines 149-293 move here verbatim: the add-alarm Border, the missed-note
               Border, the empty-state TextBlock, and the Alarms ItemsControl with its
               ItemContainerStyle and the comment above it intact. Line 147, the "Schedule" heading,
               is deleted. -->
        </StackPanel>
      </ScrollViewer>
    </Grid>
```

Move the existing markup unchanged. Two deletions only:

- the `<TextBlock Text="Quick timers" FontSize="{StaticResource TextXl}" Margin="0,0,0,14"/>` heading
- the `<TextBlock Text="Schedule" FontSize="{StaticResource TextXl}" Margin="0,0,0,14"/>` heading

Drop the `Margin="0,24,0,0"` that was on `DayPanel` — the panels no longer stack.

- [ ] **Step 3: Add the strip, and renumber the undo bar and Settings button**

Insert before the existing undo-bar `Border`, then change that `Border` to `Grid.Row="3"` and the Settings `Button` to `Grid.Row="4"`:

```xml
    <Border Grid.Row="2" Margin="0,12,0,0"
            Background="{StaticResource CardBg}" BorderBrush="{StaticResource Border}" BorderThickness="1"
            CornerRadius="{StaticResource RadiusMd}" Padding="12"
            AutomationProperties.Name="Running timer"
            Visibility="{Binding ShowStrip, Converter={StaticResource BoolToVisible}}">
      <!-- No LiveSetting and no bound Name: either would announce a new time every second.
           No storyboard either — this appears and disappears several times a day. -->
      <StackPanel Orientation="Horizontal">
        <Ellipse Width="9" Height="9" VerticalAlignment="Center" Margin="0,0,10,0" Fill="{StaticResource Accent}"/>
        <TextBlock Text="{Binding StripTimer.RemainingText}" FontFamily="{StaticResource FontMono}"
                   FontSize="{StaticResource TextLg}" Foreground="{StaticResource Text}" VerticalAlignment="Center"/>
        <TextBlock Text="{Binding StripTimer.Label}" Foreground="{StaticResource TextMuted}"
                   FontSize="{StaticResource TextXs}" VerticalAlignment="Center" Margin="10,0,0,0"
                   Visibility="{Binding StripTimer.Label, Converter={StaticResource NullToCollapsed}}"/>
        <TextBlock Text="{Binding StripExtraText}" Foreground="{StaticResource TextFaint}"
                   FontSize="{StaticResource TextXs}" VerticalAlignment="Center" Margin="10,0,0,0"
                   Visibility="{Binding StripExtraText, Converter={StaticResource NullToCollapsed}}"/>
      </StackPanel>
    </Border>
```

- [ ] **Step 4: Delete the responsive layout code**

In `src/Tidsro/Views/MainWindow.xaml.cs`, delete `WideBreakpoint`, `_wideApplied`, and the whole `ApplyLayout` method, plus these two lines from the constructor:

```csharp
        SizeChanged += (_, _) => ApplyLayout();
        Loaded += (_, _) => ApplyLayout();
```

Remove the now-unused `using System.Windows.Controls;` if nothing else in the file needs it.

- [ ] **Step 5: Seed the tab and split the window-state save**

Add a field beside the others:

```csharp
    private readonly MainViewModel _vm;
```

In the constructor, after `DataContext = vm;`:

```csharp
        _vm = vm;
        vm.SelectedTabIndex = settings.SelectedTab;   // sanitised on load, so always in range
```

Replace `SavePlacement` with:

```csharp
    private void SavePlacement()
    {
        CaptureWindowState();
        try { _persist(); } catch { /* position is a nicety; never block hiding */ }
    }

    /// <summary>Copy the live window state into the shared settings without persisting, so App.OnExit
    /// can fold it into the single save it already makes. The tray's Quit never runs OnClosing, so
    /// without this the session's tab and position are lost on every tray quit.</summary>
    public void CaptureWindowState()
    {
        _settings.SelectedTab = _vm.SelectedTabIndex;     // valid whatever the window state
        if (WindowState != WindowState.Normal) return;    // store a usable position, not minimised/maximised
        _settings.WindowWidth = Width;
        _settings.WindowHeight = Height;
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
    }
```

The tab assignment sits **above** the `WindowState` guard deliberately: a minimised window should still remember its tab.

- [ ] **Step 6: Build and run the full suite**

Run: `Get-Process Tidsro | Stop-Process -Force` then `dotnet test`
Expected: Build succeeded; PASS, 317 tests.

- [ ] **Step 7: Launch the app and confirm it opens**

Run: `dotnet build` then `Start-Process src/Tidsro/bin/Debug/net10.0-windows/Tidsro.exe`
Expected: the window opens with two tab headers, Quick timers selected and underlined in gold. Start a 5 minute timer and confirm the strip appears at the bottom and the tab headers do not move. Close the window when done — the full check is Task 8.

- [ ] **Step 8: Commit**

```bash
git add src/Tidsro/Views/MainWindow.xaml src/Tidsro/Views/MainWindow.xaml.cs
git commit -m "feat(main): replace the scrolling page with tabs and a running-timer strip"
```

---

### Task 6: Keep focus off a collapsed panel

**Files:**
- Modify: `src/Tidsro/Views/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `Tabs` and `Panels` from Task 5; `MainViewModel.SelectedTabIndex` from Task 2.
- Produces: nothing other tasks depend on.

WPF keyboard focus cannot be exercised from this project's headless xUnit suite — there is no STA test harness and no window to focus. Verification for this task is the two focus checks in Task 8, which is why they are called out explicitly there.

- [ ] **Step 1: Add the focus handler**

In `src/Tidsro/Views/MainWindow.xaml.cs`, add to the constructor after the existing `vm.PropertyChanged` subscription:

```csharp
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedTabIndex)) RescueFocusFromHiddenPanel();
        };
```

Add the method beside the other private helpers:

```csharp
    /// <summary>A collapsed panel cannot hold keyboard focus, so switching tabs while focus sits in
    /// the panel content — which Ctrl+Tab allows from anywhere in the window — drops focus to the
    /// window itself: the next Tab restarts from the top and a screen reader loses its place. Move
    /// focus to the headers instead.
    ///
    /// Gated on IsActive because ResetSettings changes the tab while the modal Settings dialog owns
    /// focus. Without the gate this would pull the user out of the dialog to a header behind it.</summary>
    private void RescueFocusFromHiddenPanel()
    {
        if (!IsActive) return;
        if (Keyboard.FocusedElement is not Visual focused) return;
        if (!Panels.IsAncestorOf(focused)) return;
        Tabs.Focus();
    }
```

Add the usings the file needs:

```csharp
using System.Windows.Input;
using System.Windows.Media;
```

- [ ] **Step 2: Build and run the full suite**

Run: `Get-Process Tidsro | Stop-Process -Force` then `dotnet test`
Expected: Build succeeded; PASS, 317 tests (this task adds none).

- [ ] **Step 3: Check the behaviour by hand**

Run: `Start-Process src/Tidsro/bin/Debug/net10.0-windows/Tidsro.exe`
Expected: click into the "Custom duration" box on Quick timers, press Ctrl+Tab, and the focus ring lands on a tab header rather than disappearing. Press Tab and confirm the next stop is inside the Schedule panel, not the top of the window.

- [ ] **Step 4: Commit**

```bash
git add src/Tidsro/Views/MainWindow.xaml.cs
git commit -m "fix(a11y): move focus to the tab headers instead of stranding it in a hidden panel"
```

---

### Task 7: Capture on exit, and reset the tab with the other settings

**Files:**
- Modify: `src/Tidsro/App.xaml.cs`
- Modify: `src/Tidsro/ViewModels/SettingsViewModel.cs`
- Test: `tests/Tidsro.Tests/SettingsViewModelTests.cs`

**Interfaces:**
- Consumes: `MainWindow.CaptureWindowState()` (Task 5), `MainViewModel.SelectedTabIndex` (Task 2), `AppSettings.SelectedTab` (Task 1).
- Produces: nothing later tasks depend on.

- [ ] **Step 1: Write the failing test**

Add to `tests/Tidsro.Tests/SettingsViewModelTests.cs`:

```csharp
    [Fact]
    public void Reset_returns_the_selected_tab_to_quick_timers()
    {
        var shared = new AppSettings { SelectedTab = 1 };
        var vm = new SettingsViewModel(shared, new FakeStartupService(),
            save: () => { }, _ => { }, clearAllAlarms: () => { }, alarmCount: () => 0,
            hasAnythingToClear: () => true, resetWindowPlacement: () => { }, confirm: (_, _) => true);

        vm.ResetSettingsCommand.Execute(null);

        Assert.Equal(0, shared.SelectedTab);
    }

    [Fact]
    public void A_declined_reset_leaves_the_selected_tab_alone()
    {
        var shared = new AppSettings { SelectedTab = 1 };
        var vm = new SettingsViewModel(shared, new FakeStartupService(),
            save: () => { }, _ => { }, clearAllAlarms: () => { }, alarmCount: () => 0,
            hasAnythingToClear: () => true, resetWindowPlacement: () => { }, confirm: (_, _) => false);

        vm.ResetSettingsCommand.Execute(null);

        Assert.Equal(1, shared.SelectedTab);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~selected_tab_to_quick_timers|FullyQualifiedName~declined_reset"`
Expected: FAIL — `Assert.Equal() Failure: Expected 0, Actual 1` on the first test.

- [ ] **Step 3: Reset the tab in `ResetSettings`**

In `src/Tidsro/ViewModels/SettingsViewModel.cs`, inside `ResetSettings`, add below `_settings.WindowTop = null;` (and the other two placement lines):

```csharp
        _settings.SelectedTab = defaults.SelectedTab;
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test`
Expected: PASS, 317 → 319 tests.

- [ ] **Step 5: Return the live window to the first tab on reset**

In `src/Tidsro/App.xaml.cs`, change the `resetWindowPlacement` argument (around line 270) from:

```csharp
                    resetWindowPlacement: () => _main?.ResetPlacement(),
```

to:

```csharp
                    // Also returns the live view to the first tab; clearing the stored value alone
                    // would leave the reset invisible until the next launch.
                    resetWindowPlacement: () => { _main?.ResetPlacement(); _mainVm.SelectedTabIndex = 0; },
```

- [ ] **Step 6: Capture the window state on exit**

In `src/Tidsro/App.xaml.cs`, in `OnExit`, add immediately **before** the existing `SaveData();` call:

```csharp
        _main?.CaptureWindowState();   // the tray's Quit never runs OnClosing; null when the window was never opened
```

- [ ] **Step 7: Build and run the full suite**

Run: `Get-Process Tidsro | Stop-Process -Force` then `dotnet test`
Expected: Build succeeded; PASS, 319 tests.

- [ ] **Step 8: Commit**

```bash
git add src/Tidsro/App.xaml.cs src/Tidsro/ViewModels/SettingsViewModel.cs tests/Tidsro.Tests/SettingsViewModelTests.cs
git commit -m "fix(settings): keep the selected tab through a reset and a tray quit"
```

---

### Task 8: Manual verification pass

**Files:** none changed unless a defect is found.

**Interfaces:** none.

**Before you start:** back up live state, because this pass edits real alarms and runs a Debug build. Copy `%AppData%\Tidsro\data.json` to a scratch folder and record the current `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Tidsro` value. **Close Tidsro gracefully — never force-kill it.** The live schedule is held in memory and only written on a clean exit, so a force-kill discards every unsaved edit.

- [ ] **Step 1: Confirm the tab semantics**

Read the UIA tree with Windows PowerShell and `UIAutomationClient`, walking `ControlViewWalker`. Confirm both headers appear with control type `TabItem` inside a `Tab`, and that their names are "Quick timers" and "Schedule". When filtering by name, AND the condition with a `ControlType` condition — matching on name alone finds the `TextBlock` inside the header, which has no selection pattern.

- [ ] **Step 2: Confirm keyboard navigation**

With focus on a tab header: left and right arrows move between the two. From anywhere in the window, Ctrl+Tab cycles. Both work without the mouse.

- [ ] **Step 3: Confirm the two focus rescues**

Click into the "Custom duration" box, press Ctrl+Tab: focus lands on a header, not nowhere. Then open Settings, click "Reset all settings", confirm: focus stays in the dialog throughout and the main window behind it switches to Quick timers.

- [ ] **Step 4: Confirm the strip**

Start a 5 minute timer. The strip appears at the bottom and **nothing above it moves**. Start a second: the strip still shows the 5 minute one and reads "+1 more". Pause both: the strip still shows a timer. Cancel both: it disappears. In the UIA tree, the strip carries the static name "Running timer" and no live-region setting.

- [ ] **Step 5: Confirm both panels stay loaded**

Scroll the Schedule tab down, switch to Quick timers, switch back: the scroll position is preserved and the alarm rows do **not** fade in again.

- [ ] **Step 6: Confirm the alarm rows still announce correctly**

Re-read the UIA tree for the alarm rows. Each `DataItem` must carry its composed accessible name — time, days, label, sound — and not `Tidsro.ViewModels.AlarmItemViewModel`. This is the `37c2f25` fix; the restructure moved the `ItemsControl` and must not have disturbed its `ItemContainerStyle`.

- [ ] **Step 7: Confirm the tab is remembered**

Switch to Schedule, quit from the tray menu, relaunch: it opens on Schedule. Repeat, closing the window with ✕ before quitting: still Schedule.

- [ ] **Step 8: Restore the backed-up state**

Close Tidsro gracefully, restore `data.json` and the Run key value, and relaunch the **installed** exe rather than the Debug build.

- [ ] **Step 9: Note the follow-ups this leaves open**

`docs/screenshots/main-window.png` now shows a layout the app no longer has, and the README references it. Refreshing the screenshots belongs to the release pass, not to this branch — record it so the release recipe picks it up.

---

## Out of scope for this plan

- The weekly timetable, the read-only week grid, and the optional end time on recurring alarms (schema 5). The shell leaves room for a third `TabItem` and a third panel; `AppSettings.TabCount` becomes 3.
- CHANGELOG and version bump. Those follow the existing release recipe, which runs at release time rather than on the feature branch.
- Lowering `MinWidth`/`MinHeight`, which would also mean moving the floors in `AppSettings.Sanitized`.
