# Tidsro Slice 3 — Design Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give Tidsro a cohesive visual/interaction polish — deeper dark palette with a disciplined gold accent, generous spacing, unified icons, consistent depth, gentle motion, and a resizable window — with zero behaviour changes.

**Architecture:** Almost entirely WPF View/XAML work. Most of the palette change rides for free through `tokens.xaml` `StaticResource` lookups; the rest is per-view layout, a shared gold button style, storyboard motion (with a central reduced-motion duration override), and a small `AppSettings` addition for window size. No view-model or business-logic changes.

**Tech Stack:** .NET 10 WPF, `tokens.xaml` resource dictionary, `CommunityToolkit.Mvvm`, xUnit (regression only).

**Spec:** [`2026-06-17-tidsro-slice3-design-polish-design.md`](../specs/2026-06-17-tidsro-slice3-design-polish-design.md)

---

## Conventions (read once before Task 1)

- **Branch:** cut `feature/design-polish` from `main` (`2a80f77`) before Task 1. One PR / merge at the end (Malin merges; never merge or push without her OK).
- **This is a view slice — manual acceptance, not TDD.** Per the spec and Tidsro's "backend TDD / View manual-acceptance" pattern, tasks are verified by building, running, and eyeballing against the acceptance bullet. There is **no new testable logic**, so no new tests. The existing **110 tests are the regression guard** — they must stay green after every task.
- **Build gotcha:** a running `Tidsro.exe` locks `bin/.../Tidsro.exe`. Before any build, stop it:
  `Get-Process Tidsro -ErrorAction SilentlyContinue | Stop-Process -Force`
- **Commands** (from repo root `GitHub/repos/tidsro`):
  - Build: `dotnet build`
  - Regression tests: `dotnet test` → expect **110 passing**
  - Run: `dotnet run --project src/Tidsro/Tidsro.csproj`
- **Commits:** conventional prefix (`style:`/`feat:`/`refactor:`). **NO `Co-Authored-By` trailer, no Claude attribution** — Malin is the sole contributor.
- **Animation colours are literals.** WPF animations can't bind to brush resources, so `ColorAnimation To="#…"` values are duplicated from `tokens.xaml`. Keep them in sync if a token changes.
- **Surface-ramp refinement.** The spec licensed eyes-on tuning of surface values. Final ramp used here: `ElevatedBg #232C38`, `InteractiveBg #1F2832`, plus a new `InteractiveHover #28323E` (hover needs a distinct target), instead of the spec's first-pass unified `#1F2832`.

## File structure

| File | Responsibility | Tasks |
|---|---|---|
| `src/Tidsro/Resources/tokens.xaml` | Palette, gold accent, focus ring, `GoldAction` button style, hover/press motion | 1, 7 |
| `src/Tidsro/Views/MainWindow.xaml` | Spacing, editor containers, gold buttons, focal indicators, depth, icons, entrance/paused motion | 2, 3, 8 |
| `src/Tidsro/Views/MainWindow.xaml.cs` | Restore/save window size | 4 |
| `src/Tidsro/Models/AppSettings.cs` | Persist `WindowWidth`/`WindowHeight` | 4 |
| `src/Tidsro/Views/CompletionPopup.xaml` | Inherited palette, glyphs, softened shadow, slide transform | 2, 5, 8 |
| `src/Tidsro/Views/CompletionPopup.xaml.cs` | Slide-up on show | 8 |
| `src/Tidsro/Views/SettingsWindow.xaml` | Spacing, gold Save button | 6 |
| `src/Tidsro/App.xaml.cs` | Reduced-motion duration override at startup | 7 |

---

## Task 1: Deeper palette, gold accent, gold button style

**Files:**
- Modify: `src/Tidsro/Resources/tokens.xaml`

- [ ] **Step 1: Swap the surface, border, and accent brush values**

Replace the `<!-- Surfaces -->` block (currently `PageBg`…`InteractiveBg`):

```xml
  <!-- Surfaces -->
  <SolidColorBrush x:Key="PageBg"        Color="#0E141A"/>
  <SolidColorBrush x:Key="PanelBg"       Color="#0A0F14"/>
  <SolidColorBrush x:Key="CardBg"        Color="#161D27"/>
  <SolidColorBrush x:Key="ElevatedBg"    Color="#232C38"/>
  <SolidColorBrush x:Key="InteractiveBg" Color="#1F2832"/>
  <SolidColorBrush x:Key="InteractiveHover" Color="#28323E"/>
```

Replace the `<!-- Lines -->` block:

```xml
  <!-- Lines -->
  <SolidColorBrush x:Key="Border"       Color="#2B3440"/>
  <SolidColorBrush x:Key="BorderStrong" Color="#3A4552"/>
  <SolidColorBrush x:Key="BorderSoft"   Color="#1A222B"/>
```

In the `<!-- Accent + semantic -->` block, change only these four (leave `Success`/`Warning`/`Danger`/`Info`):

```xml
  <SolidColorBrush x:Key="Accent"       Color="#E3B341"/>
  <SolidColorBrush x:Key="AccentStrong" Color="#ECC25A"/>
  <SolidColorBrush x:Key="AccentSoft"   Color="#29E3B341"/>  <!-- 0.16 -->
  <SolidColorBrush x:Key="FocusRing"    Color="#99E3B341"/>  <!-- 0.60 -->
```

(`Text`/`TextMuted`/`TextFaint` are unchanged.)

- [ ] **Step 2: Add a shared `GoldAction` button style**

After the `QuietAction` style closes (`</Style>`), add:

```xml
  <!-- Primary action: the one gold button per surface (Start, Add/Save). -->
  <Style x:Key="GoldAction" TargetType="Button">
    <Setter Property="FocusVisualStyle" Value="{StaticResource ActionFocusVisual}"/>
    <Setter Property="Background" Value="{StaticResource Accent}"/>
    <Setter Property="Foreground" Value="{StaticResource PageBg}"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Padding" Value="14,7"/>
    <Setter Property="FontSize" Value="{StaticResource TextSm}"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border x:Name="b" Background="{TemplateBinding Background}" CornerRadius="8" Padding="{TemplateBinding Padding}">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="b" Property="Background" Value="{StaticResource AccentStrong}"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
```

- [ ] **Step 3: Build**

Stop a running app, then `dotnet build`. Expected: build succeeds, 0 errors.

- [ ] **Step 4: Run and eyeball**

`dotnet run --project src/Tidsro/Tidsro.csproj`. Confirm: base reads deep `#0E141A`; the existing Add button and the agenda "next" dot are now gold; pressing Tab shows a gold keyboard focus ring. (Contrast is pre-computed and passes AA: `TextFaint` on base ≈ 5.8:1; gold-on-base ≈ 9.5:1.)

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/Resources/tokens.xaml
git commit -m "style: deeper palette, gold accent, gold button style"
```

---

## Task 2: Unify action icons on the icon font

Replace every raw-Unicode glyph with a `Segoe Fluent Icons` glyph (fallback `Segoe MDL2 Assets`). Verify each renders at run time; fallbacks noted if a glyph shows as a blank box.

**Files:**
- Modify: `src/Tidsro/Views/MainWindow.xaml`
- Modify: `src/Tidsro/Views/CompletionPopup.xaml`

- [ ] **Step 1: MainWindow — cancel/reset/delete/edit/dismiss glyphs**

Make these five replacements. For each, set the new `Content` and ensure `FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"` is present on the `Button`.

- Running-row **cancel** `Content="✕"` → `Content="&#xE711;"` (+ FontFamily). Fallback: `&#xE894;`.
- Running-row **reset** `Content="↺"` → `Content="&#xE72C;"` (+ FontFamily). Fallback: `&#xE149;`.
- Agenda **delete** `Content="✕"` → `Content="&#xE711;"` (+ FontFamily).
- Agenda **edit** `Content="✎"` → `Content="&#xE70F;"` (+ FontFamily). Fallback: `&#xE104;`.
- Missed-note **dismiss** `Content="✕"` → `Content="&#xE711;"` (+ FontFamily).

- [ ] **Step 2: CompletionPopup — check + dismiss glyphs**

Replace the header `TextBlock` (`Text="✓ complete"`) with glyph + text runs:

```xml
        <TextBlock Foreground="{StaticResource TextFaint}" FontSize="{StaticResource TextXs}" VerticalAlignment="Center">
          <Run FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" Text="&#xE73E;"/><Run Text=" complete"/>
        </TextBlock>
```

Change the dismiss button `Content="✕"` → `Content="&#xE711;"` and add `FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"`.

- [ ] **Step 3: Build, run, eyeball**

Build and run. Confirm every action button shows a crisp icon-font glyph (cancel, reset, edit, dismiss, the popup check) — no raw `✕`/`↺`/`✎`/`✓` and no blank boxes. If any box appears, use the listed fallback codepoint.

- [ ] **Step 4: Commit**

```bash
git add src/Tidsro/Views/MainWindow.xaml src/Tidsro/Views/CompletionPopup.xaml
git commit -m "style: unify action icons on the Segoe icon font"
```

---

## Task 3: MainWindow visual restructure (spacing, containers, gold, depth, focal)

Restructure both sections: generous spacing, each section's create-controls grouped in a card container, gold primary buttons via `GoldAction`, card depth (`RadiusMd` + border), and focal indicators (gold dot on active timers; gold dot + gold border on the "next" alarm). **All existing bindings, command names, and `AutomationProperties` are preserved exactly** — this is layout only.

**Files:**
- Modify: `src/Tidsro/Views/MainWindow.xaml`

- [ ] **Step 1: Root margin and Quick-timers block**

Set the root `<Grid Margin="16">` → `<Grid Margin="24">`. Replace the Quick-timers heading + controls (Grid rows 0 and 1) with:

```xml
    <TextBlock Grid.Row="0" Text="Quick timers" FontSize="{StaticResource TextXl}" Margin="0,0,0,14"/>
    <StackPanel Grid.Row="1">
      <!-- preset quick-starts -->
      <StackPanel Orientation="Horizontal">
        <Button Content="15" Width="64" Command="{Binding StartPresetCommand}" Style="{StaticResource QuietAction}">
          <Button.CommandParameter><sys:Int32>15</sys:Int32></Button.CommandParameter>
        </Button>
        <Button Content="30" Width="64" Command="{Binding StartPresetCommand}" Style="{StaticResource QuietAction}" Margin="10,0,0,0">
          <Button.CommandParameter><sys:Int32>30</sys:Int32></Button.CommandParameter>
        </Button>
        <Button Content="60" Width="64" Command="{Binding StartPresetCommand}" Style="{StaticResource QuietAction}" Margin="10,0,0,0">
          <Button.CommandParameter><sys:Int32>60</sys:Int32></Button.CommandParameter>
        </Button>
      </StackPanel>

      <!-- custom-timer editor container -->
      <Border Background="{StaticResource CardBg}" BorderBrush="{StaticResource Border}" BorderThickness="1"
              CornerRadius="{StaticResource RadiusMd}" Padding="16" Margin="0,14,0,0">
        <StackPanel>
          <Grid>
            <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
            <TextBox Grid.Column="0" Text="{Binding CustomInput, UpdateSourceTrigger=PropertyChanged}"
                     AutomationProperties.Name="Custom duration" ToolTip="e.g. 25, 5:00, 1:30:00"/>
            <TextBox Grid.Column="1" Text="{Binding Label, UpdateSourceTrigger=PropertyChanged}"
                     AutomationProperties.Name="Label" Margin="10,0,0,0"/>
          </Grid>
          <Grid Margin="0,12,0,0">
            <Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
            <TextBlock Grid.Column="0" Text="Sound" VerticalAlignment="Center" Foreground="{StaticResource TextMuted}" Margin="0,0,8,0"/>
            <ComboBox Grid.Column="1" ItemsSource="{Binding SoundOptions}" SelectedItem="{Binding SelectedSound}"
                      AutomationProperties.Name="Sound for new timers">
              <ComboBox.ItemTemplate><DataTemplate><TextBlock Text="{Binding Converter={StaticResource SoundLabel}}"/></DataTemplate></ComboBox.ItemTemplate>
            </ComboBox>
            <Button Grid.Column="2" Content="&#xE768;" Command="{Binding PreviewSoundCommand}"
                    FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                    AutomationProperties.Name="Preview sound" ToolTip="Preview sound" Style="{StaticResource QuietAction}" Margin="8,0,0,0"/>
          </Grid>
          <Button Content="Start" Command="{Binding StartCustomCommand}" Style="{StaticResource GoldAction}"
                  HorizontalAlignment="Right" Margin="0,14,0,0" AutomationProperties.Name="Start timer"/>
          <TextBlock Text="{Binding CustomError}" Foreground="{StaticResource Danger}" FontSize="{StaticResource TextXs}"
                     Margin="0,8,0,0" TextWrapping="Wrap" AutomationProperties.LiveSetting="Assertive"
                     Visibility="{Binding CustomError, Converter={StaticResource NullToCollapsed}}"/>
        </StackPanel>
      </Border>

      <!-- running timers -->
      <ItemsControl ItemsSource="{Binding Running}" Margin="0,12,0,0">
        <ItemsControl.ItemTemplate>
          <DataTemplate>
            <Border Background="{StaticResource CardBg}" BorderBrush="{StaticResource Border}" BorderThickness="1"
                    CornerRadius="{StaticResource RadiusMd}" Padding="16" Margin="0,0,0,12">
              <DockPanel>
                <Button DockPanel.Dock="Right" Content="&#xE711;" Command="{Binding CancelCommand}"
                        FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                        AutomationProperties.Name="Cancel timer" ToolTip="Cancel timer" Style="{StaticResource QuietAction}"/>
                <Button DockPanel.Dock="Right" Content="&#xE72C;" Command="{Binding ResetCommand}"
                        FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                        AutomationProperties.Name="Reset timer" ToolTip="Reset to full time" Style="{StaticResource QuietAction}" Margin="0,0,8,0"/>
                <Button DockPanel.Dock="Right" Content="{Binding PauseResumeGlyph}" Command="{Binding PauseResumeCommand}"
                        FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                        AutomationProperties.Name="{Binding PauseResumeLabel}" ToolTip="{Binding PauseResumeLabel}" Style="{StaticResource QuietAction}" Margin="0,0,8,0"/>
                <Ellipse DockPanel.Dock="Left" Width="9" Height="9" Margin="0,2,12,0" VerticalAlignment="Top" Fill="{StaticResource Accent}"/>
                <StackPanel>
                  <TextBlock Text="{Binding Label}" Foreground="{StaticResource TextMuted}" FontSize="{StaticResource TextXs}"/>
                  <TextBlock Text="{Binding RemainingText}" FontFamily="{StaticResource FontMono}" FontSize="{StaticResource Text2xl}">
                    <TextBlock.Style>
                      <Style TargetType="TextBlock">
                        <Setter Property="Foreground" Value="{StaticResource Text}"/>
                        <Style.Triggers>
                          <DataTrigger Binding="{Binding IsPaused}" Value="True">
                            <Setter Property="Foreground" Value="{StaticResource TextMuted}"/>
                          </DataTrigger>
                        </Style.Triggers>
                      </Style>
                    </TextBlock.Style>
                  </TextBlock>
                  <TextBlock Text="{Binding SoundTag}" Foreground="{StaticResource TextFaint}" FontSize="{StaticResource TextXs}"/>
                </StackPanel>
              </DockPanel>
            </Border>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </StackPanel>
```

- [ ] **Step 2: Your-day block (editor container + agenda)**

Replace the `<ScrollViewer Grid.Row="2" …>` and its contents with:

```xml
    <ScrollViewer Grid.Row="2" VerticalScrollBarVisibility="Auto" Margin="0,28,0,0">
      <StackPanel>
        <TextBlock Text="Your day" FontSize="{StaticResource TextXl}" Margin="0,0,0,14"/>

        <!-- alarm editor container -->
        <Border Background="{StaticResource CardBg}" BorderBrush="{StaticResource Border}" BorderThickness="1"
                CornerRadius="{StaticResource RadiusMd}" Padding="16">
          <StackPanel>
            <Grid>
              <Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
              <TextBox Grid.Column="0" Width="64" x:Name="AlarmTimeBox"
                       Text="{Binding AlarmTimeInput, UpdateSourceTrigger=PropertyChanged}"
                       AutomationProperties.Name="Time" ToolTip="24-hour time, e.g. 14:30"/>
              <TextBox Grid.Column="1" Text="{Binding AlarmLabel, UpdateSourceTrigger=PropertyChanged}"
                       AutomationProperties.Name="Alarm label" Margin="10,0,0,0"/>
            </Grid>
            <Grid Margin="0,12,0,0">
              <Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
              <TextBlock Grid.Column="0" Text="Sound" VerticalAlignment="Center" Foreground="{StaticResource TextMuted}" Margin="0,0,8,0"/>
              <ComboBox Grid.Column="1" ItemsSource="{Binding SoundOptions}" SelectedItem="{Binding AlarmSound}" AutomationProperties.Name="Alarm sound">
                <ComboBox.ItemTemplate><DataTemplate><TextBlock Text="{Binding Converter={StaticResource SoundLabel}}"/></DataTemplate></ComboBox.ItemTemplate>
              </ComboBox>
              <Button Grid.Column="2" Content="&#xE768;" Command="{Binding PreviewAlarmSoundCommand}"
                      FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" AutomationProperties.Name="Preview sound"
                      ToolTip="Preview sound" Style="{StaticResource QuietAction}" Margin="8,0,0,0"/>
            </Grid>
            <DockPanel Margin="0,14,0,0" LastChildFill="False">
              <Button DockPanel.Dock="Left" Content="Cancel" Command="{Binding CancelEditAlarmCommand}" Style="{StaticResource QuietAction}"
                      Visibility="{Binding IsEditingAlarm, Converter={StaticResource BoolToVisible}}"/>
              <Button DockPanel.Dock="Right" Content="{Binding AddOrSaveLabel}" Command="{Binding AddOrSaveAlarmCommand}"
                      Style="{StaticResource GoldAction}" AutomationProperties.Name="{Binding AddOrSaveLabel}"/>
            </DockPanel>
            <TextBlock Text="{Binding AlarmError}" Foreground="{StaticResource Danger}" FontSize="{StaticResource TextXs}"
                       Margin="0,8,0,0" TextWrapping="Wrap" AutomationProperties.LiveSetting="Assertive"
                       Visibility="{Binding AlarmError, Converter={StaticResource NullToCollapsed}}"/>
          </StackPanel>
        </Border>

        <!-- missed-while-away note -->
        <Border Background="{StaticResource CardBg}" BorderBrush="{StaticResource Border}" BorderThickness="1"
                CornerRadius="{StaticResource RadiusMd}" Padding="16" Margin="0,12,0,0"
                Visibility="{Binding MissedNote, Converter={StaticResource NullToCollapsed}}">
          <DockPanel>
            <Button DockPanel.Dock="Right" Content="&#xE711;" Command="{Binding DismissMissedNoteCommand}"
                    FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                    AutomationProperties.Name="Dismiss missed note" Style="{StaticResource QuietAction}"/>
            <TextBlock Text="{Binding MissedNote}" Foreground="{StaticResource TextMuted}" TextWrapping="Wrap"
                       VerticalAlignment="Center" AutomationProperties.LiveSetting="Polite"/>
          </DockPanel>
        </Border>

        <!-- undo-delete banner -->
        <Border Background="{StaticResource CardBg}" BorderBrush="{StaticResource Border}" BorderThickness="1"
                CornerRadius="{StaticResource RadiusMd}" Padding="16" Margin="0,12,0,0"
                Visibility="{Binding PendingDeleteLabel, Converter={StaticResource NullToCollapsed}}">
          <DockPanel>
            <Button DockPanel.Dock="Right" Content="Undo" Command="{Binding UndoDeleteCommand}"
                    AutomationProperties.Name="Undo delete" Style="{StaticResource QuietAction}"/>
            <TextBlock Text="{Binding PendingDeleteLabel}" Foreground="{StaticResource TextMuted}"
                       VerticalAlignment="Center" AutomationProperties.LiveSetting="Polite"/>
          </DockPanel>
        </Border>

        <!-- empty state -->
        <TextBlock Text="Nothing scheduled yet — add an alarm" Foreground="{StaticResource TextFaint}"
                   FontSize="{StaticResource TextSm}" Margin="0,14,0,0"
                   Visibility="{Binding IsDayEmpty, Converter={StaticResource BoolToVisible}}"/>

        <!-- agenda -->
        <ItemsControl ItemsSource="{Binding Alarms}" Margin="0,14,0,0">
          <ItemsControl.ItemTemplate>
            <DataTemplate>
              <Border Padding="16" Margin="0,0,0,12" CornerRadius="{StaticResource RadiusMd}"
                      Background="{StaticResource CardBg}" BorderThickness="1"
                      AutomationProperties.Name="{Binding AccessibleName}">
                <Border.Style>
                  <Style TargetType="Border">
                    <Setter Property="BorderBrush" Value="{StaticResource Border}"/>
                    <Style.Triggers>
                      <DataTrigger Binding="{Binding IsNext}" Value="True">
                        <Setter Property="BorderBrush" Value="{StaticResource Accent}"/>
                      </DataTrigger>
                    </Style.Triggers>
                  </Style>
                </Border.Style>
                <DockPanel>
                  <Button DockPanel.Dock="Right" Content="&#xE711;"
                          Command="{Binding DataContext.DeleteAlarmCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                          CommandParameter="{Binding}" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                          AutomationProperties.Name="{Binding DeleteLabel}" ToolTip="{Binding DeleteLabel}" Style="{StaticResource QuietAction}"/>
                  <Button DockPanel.Dock="Right" Content="&#xE70F;"
                          Command="{Binding DataContext.BeginEditAlarmCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                          CommandParameter="{Binding}" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                          AutomationProperties.Name="{Binding EditLabel}" ToolTip="{Binding EditLabel}" Style="{StaticResource QuietAction}" Margin="0,0,8,0"/>
                  <StackPanel Orientation="Horizontal">
                    <Ellipse Width="9" Height="9" Margin="0,2,12,0" VerticalAlignment="Top" Fill="{StaticResource Accent}"
                             Visibility="{Binding IsNext, Converter={StaticResource BoolToVisible}}"/>
                    <StackPanel>
                      <StackPanel Orientation="Horizontal">
                        <TextBlock Text="{Binding TimeText}" FontFamily="{StaticResource FontMono}" FontSize="{StaticResource TextLg}" Foreground="{StaticResource Text}"/>
                        <TextBlock Text="{Binding TomorrowText}" Foreground="{StaticResource TextFaint}" FontSize="{StaticResource TextXs}" VerticalAlignment="Center" Margin="8,0,0,0"/>
                      </StackPanel>
                      <TextBlock Text="{Binding DisplayLabel}" Foreground="{StaticResource TextMuted}" FontSize="{StaticResource TextXs}"/>
                      <StackPanel Orientation="Horizontal" Margin="0,2,0,0">
                        <TextBlock FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" FontSize="{StaticResource TextXs}" Foreground="{StaticResource TextFaint}" Margin="0,0,4,0">
                          <TextBlock.Text><Binding Path="HasSound"><Binding.Converter><StaticResource ResourceKey="SoundGlyph"/></Binding.Converter></Binding></TextBlock.Text>
                        </TextBlock>
                        <TextBlock Text="{Binding SoundText}" Foreground="{StaticResource TextFaint}" FontSize="{StaticResource TextXs}"/>
                      </StackPanel>
                    </StackPanel>
                  </StackPanel>
                </DockPanel>
              </Border>
            </DataTemplate>
          </ItemsControl.ItemTemplate>
        </ItemsControl>
      </StackPanel>
    </ScrollViewer>
```

- [ ] **Step 3: Settings button spacing**

The `Grid.Row="3"` Settings button stays `QuietAction`; no change needed (it inherits the new margin from the root grid).

- [ ] **Step 4: Build, run, eyeball**

Build and run. Confirm: clear air between sections; each section's inputs sit in a bordered container; Start and Add are gold; the running timer shows a gold dot; the "next" agenda card has a gold dot **and** gold border, others are neutral; nothing is crowded. The `next` dot still has its text equivalent in the alarm's accessible name (unchanged).

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/Views/MainWindow.xaml
git commit -m "style: spacious MainWindow — editor containers, gold actions, card depth, focal indicators"
```

---

## Task 4: Resizable window — scroll, centered max-width, size persistence

**Files:**
- Modify: `src/Tidsro/Views/MainWindow.xaml`
- Modify: `src/Tidsro/Models/AppSettings.cs`
- Modify: `src/Tidsro/Views/MainWindow.xaml.cs`

> **Amendment (post-Task-3 visual feedback):** beyond the original size+persist scope, this task now also (a) unifies the whole content into a **single `ScrollViewer`** with Settings pinned below, so any number of running timers *and* alarms scroll (previously only "Your day" scrolled and the timers list grew unbounded), and (b) caps the content to a **centered `MaxWidth` (~640)** so a wide desktop window reads as an intentional centered column instead of stretching controls across the full width. The full restructured Grid is applied wholesale (see Step 1b). The adaptive side-by-side/tabs layout is explicitly a *future slice*, not this — see the spec's "What's next".

- [ ] **Step 1: Window attributes**

In `MainWindow.xaml`, change the `Window` opening tag: `Width="420" Height="560"` → `Width="440" Height="600"`, and add `MinWidth="380" MinHeight="480" ResizeMode="CanResize"`.

- [ ] **Step 2: Persist size in `AppSettings`**

Add the two properties after `WindowTop`:

```csharp
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
```

Add them to `Sanitized()` (a too-small saved size is dropped to default):

```csharp
        WindowWidth = WindowWidth is double w && double.IsFinite(w) && w >= 380 ? w : null,
        WindowHeight = WindowHeight is double h && double.IsFinite(h) && h >= 480 ? h : null,
```

- [ ] **Step 3: Restore and save size**

In `MainWindow.xaml.cs` `ApplyPlacement()`, add at the top of the method (before the position block):

```csharp
        if (_settings.WindowWidth is double w) Width = w;
        if (_settings.WindowHeight is double h) Height = h;
```

In `SavePlacement()`, inside the existing `WindowState == Normal` guard, add:

```csharp
        _settings.WindowWidth = Width;
        _settings.WindowHeight = Height;
```

- [ ] **Step 4: Build, test, run, eyeball**

`dotnet build`, then `dotnet test` (expect 110 passing — `AppSettings` change must not break persistence tests). Run: resize the window larger, close to tray (the ✕), reopen from the tray → it returns at the chosen size. Drag it down to the minimum → the layout holds and the agenda scrolls; nothing clips.

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/Views/MainWindow.xaml src/Tidsro/Models/AppSettings.cs src/Tidsro/Views/MainWindow.xaml.cs
git commit -m "feat: resizable window with remembered size"
```

---

## Task 5: CompletionPopup polish

The popup inherits the new palette automatically and got its glyphs in Task 2. This task softens its shadow and opens up its spacing.

**Files:**
- Modify: `src/Tidsro/Views/CompletionPopup.xaml`

- [ ] **Step 1: Soften the shadow and open spacing**

The popup is the one place a shadow is correct (it floats over the desktop). It uses `{StaticResource CardShadow}`. Soften it locally by replacing the root border's `Effect="{StaticResource CardShadow}"` with an inline effect:

```xml
        Padding="{StaticResource CardPadding}" Width="320">
    <Border.Effect>
      <DropShadowEffect BlurRadius="30" ShadowDepth="10" Direction="270" Opacity="0.30" Color="#000000"/>
    </Border.Effect>
```

Increase the title's bottom margin from `Margin="0,4,0,12"` → `Margin="0,6,0,16"` so the actions sit with more air.

- [ ] **Step 2: Build, run, eyeball**

Build. To see the popup quickly, start a 1-minute preset and wait, or set a clock alarm a minute out. Confirm: the card reads on the new palette, the check + dismiss glyphs are crisp, the shadow is soft (not harsh), and the actions have breathing room. Actions stay neutral (`QuietAction`) — gold is reserved for the main window's primary actions.

- [ ] **Step 3: Commit**

```bash
git add src/Tidsro/Views/CompletionPopup.xaml
git commit -m "style: soften completion-popup shadow and spacing"
```

---

## Task 6: Settings window polish

**Files:**
- Modify: `src/Tidsro/Views/SettingsWindow.xaml`

- [ ] **Step 1: Spacing and gold Save**

Change the outer `StackPanel Margin="16"` → `Margin="24"`. Bump the checkbox/label gaps: the `CheckBox` `Margin="0,0,0,16"` → `Margin="0,0,0,20"`; the label `Margin="0,0,0,4"` → `Margin="0,0,0,8"`; the button row `Margin="0,16,0,0"` → `Margin="0,24,0,0"`.

Change the **Save** button from `Style="{StaticResource QuietAction}"` to `Style="{StaticResource GoldAction}"` (keep `IsDefault="True"`, `MinWidth="84"`, `Margin="0,0,8,0"`, `Click="Save_Click"`, the automation name). Leave **Cancel** as `QuietAction`.

- [ ] **Step 2: Build, run, eyeball**

Build and run; open Settings from the main window. Confirm: roomier layout; Save is gold (primary), Cancel neutral; Enter still saves (IsDefault), Esc still cancels.

- [ ] **Step 3: Commit**

```bash
git add src/Tidsro/Views/SettingsWindow.xaml
git commit -m "style: settings spacing and gold Save button"
```

---

## Task 7: Button hover/press motion

> **Amendment (WPF motion mechanism — supersedes the `DynamicResource`/duration-override approach written into Tasks 7–8):** `DynamicResource` on a `Storyboard` animation's `Duration` inside a sealed `ControlTemplate`/`Style` is unreliable (template storyboards get frozen). Corrected approach: (1) **drop** the App-startup duration-override step entirely — there's no resource indirection to override; (2) all XAML storyboards use **literal durations** (`0:0:0.12`); (3) reduced motion is honoured by keeping the only *movement* animation — the popup slide — in **code-behind**, gated by the existing `SystemParameters.ClientAreaAnimation` check, and making **card entrance fade-only** (no translate), so every always-on XAML transition is a non-movement fade. The concrete corrected code is in the executed dispatches; the `DynamicResource`/`App.xaml.cs`-override steps below are void.

Establish the reduced-motion override, then animate button hover/press. XAML storyboards use `{DynamicResource Duration…}` so the override makes them instant when the OS has animations off.

**Files:**
- Modify: `src/Tidsro/App.xaml.cs`
- Modify: `src/Tidsro/Resources/tokens.xaml`

- [ ] **Step 1: Reduced-motion duration override at startup**

In `App.xaml.cs` `OnStartup`, immediately after `base.OnStartup(e);` add:

```csharp
        if (!SystemParameters.ClientAreaAnimation)
        {
            Resources["DurationFast"] = new Duration(TimeSpan.Zero);
            Resources["DurationBase"] = new Duration(TimeSpan.Zero);
            Resources["DurationSlow"] = new Duration(TimeSpan.Zero);
        }
```

(`System.Windows` and `System` are already in scope via existing usings; `SystemParameters` is `System.Windows`.)

- [ ] **Step 2: Animate `QuietAction` hover (and a press dip)**

In `tokens.xaml`, in the `QuietAction` template, give the border its own mutable brush and replace the instant `IsMouseOver` trigger with animated enter/exit plus a press opacity dip. Replace the border line and the `ControlTemplate.Triggers` block with:

```xml
          <Border x:Name="b" CornerRadius="8"
                  BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"
                  Padding="{TemplateBinding Padding}">
            <Border.Background><SolidColorBrush Color="#1F2832"/></Border.Background>
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Trigger.EnterActions>
                <BeginStoryboard><Storyboard>
                  <ColorAnimation Storyboard.TargetName="b" Storyboard.TargetProperty="Background.Color"
                                  To="#28323E" Duration="{DynamicResource DurationFast}"/>
                </Storyboard></BeginStoryboard>
              </Trigger.EnterActions>
              <Trigger.ExitActions>
                <BeginStoryboard><Storyboard>
                  <ColorAnimation Storyboard.TargetName="b" Storyboard.TargetProperty="Background.Color"
                                  To="#1F2832" Duration="{DynamicResource DurationFast}"/>
                </Storyboard></BeginStoryboard>
              </Trigger.ExitActions>
            </Trigger>
            <Trigger Property="IsPressed" Value="True">
              <Trigger.EnterActions>
                <BeginStoryboard><Storyboard>
                  <DoubleAnimation Storyboard.TargetName="b" Storyboard.TargetProperty="Opacity"
                                   To="0.85" Duration="{DynamicResource DurationFast}"/>
                </Storyboard></BeginStoryboard>
              </Trigger.EnterActions>
              <Trigger.ExitActions>
                <BeginStoryboard><Storyboard>
                  <DoubleAnimation Storyboard.TargetName="b" Storyboard.TargetProperty="Opacity"
                                   To="1" Duration="{DynamicResource DurationFast}"/>
                </Storyboard></BeginStoryboard>
              </Trigger.ExitActions>
            </Trigger>
          </ControlTemplate.Triggers>
```

(The `Background` setter on `QuietAction` no longer drives the visual; the literal `#1F2832` mirrors `InteractiveBg`. Hover target `#28323E` mirrors `InteractiveHover`.)

- [ ] **Step 3: Animate `GoldAction` hover**

In the `GoldAction` template (Task 1), give the border a mutable brush and animate hover the same way:

```xml
          <Border x:Name="b" CornerRadius="8" Padding="{TemplateBinding Padding}">
            <Border.Background><SolidColorBrush Color="#E3B341"/></Border.Background>
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Trigger.EnterActions>
                <BeginStoryboard><Storyboard>
                  <ColorAnimation Storyboard.TargetName="b" Storyboard.TargetProperty="Background.Color"
                                  To="#ECC25A" Duration="{DynamicResource DurationFast}"/>
                </Storyboard></BeginStoryboard>
              </Trigger.EnterActions>
              <Trigger.ExitActions>
                <BeginStoryboard><Storyboard>
                  <ColorAnimation Storyboard.TargetName="b" Storyboard.TargetProperty="Background.Color"
                                  To="#E3B341" Duration="{DynamicResource DurationFast}"/>
                </Storyboard></BeginStoryboard>
              </Trigger.ExitActions>
            </Trigger>
          </ControlTemplate.Triggers>
```

- [ ] **Step 4: Build, run, eyeball (both motion states)**

Build and run. Hover a preset and a gold button → background eases (not a hard snap); press → a subtle dip. Then turn **off** "Show animations in Windows" (Settings → Accessibility → Visual effects → Animation effects off), restart the app, and confirm hovers are instant (no animation) — proving the reduced-motion override works.

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/App.xaml.cs src/Tidsro/Resources/tokens.xaml
git commit -m "feat: gentle button hover/press motion with reduced-motion honoring"
```

---

## Task 8: Card entrance, paused cross-fade, popup slide-up

**Files:**
- Modify: `src/Tidsro/Views/MainWindow.xaml`
- Modify: `src/Tidsro/Views/CompletionPopup.xaml`
- Modify: `src/Tidsro/Views/CompletionPopup.xaml.cs`

- [ ] **Step 1: Card entrance (running + agenda)**

To **both** item `DataTemplate` root `Border`s in `MainWindow.xaml` (the running-timer card and the agenda card), add a render transform and a Loaded storyboard that fades + slides the card in. Insert immediately inside each `<Border …>` (before its child content):

```xml
                <Border.RenderTransform><TranslateTransform Y="8"/></Border.RenderTransform>
                <Border.Triggers>
                  <EventTrigger RoutedEvent="Loaded">
                    <BeginStoryboard><Storyboard>
                      <DoubleAnimation Storyboard.TargetProperty="Opacity" From="0" To="1" Duration="{DynamicResource DurationBase}"/>
                      <DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(TranslateTransform.Y)"
                                       From="8" To="0" Duration="{DynamicResource DurationBase}">
                        <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction>
                      </DoubleAnimation>
                    </Storyboard></BeginStoryboard>
                  </EventTrigger>
                </Border.Triggers>
```

(Under reduced motion `DurationBase` is `0`, so the card just appears at its final state.)

- [ ] **Step 2: Paused readout cross-fade**

In the running-card readout `TextBlock` (Task 3), replace the instant `Foreground` style with a mutable brush + animated DataTrigger:

```xml
                  <TextBlock Text="{Binding RemainingText}" FontFamily="{StaticResource FontMono}" FontSize="{StaticResource Text2xl}">
                    <TextBlock.Foreground><SolidColorBrush Color="#F4F7FA"/></TextBlock.Foreground>
                    <TextBlock.Style>
                      <Style TargetType="TextBlock">
                        <Style.Triggers>
                          <DataTrigger Binding="{Binding IsPaused}" Value="True">
                            <DataTrigger.EnterActions>
                              <BeginStoryboard><Storyboard>
                                <ColorAnimation Storyboard.TargetProperty="(TextBlock.Foreground).(SolidColorBrush.Color)"
                                                To="#B4BDC7" Duration="{DynamicResource DurationBase}"/>
                              </Storyboard></BeginStoryboard>
                            </DataTrigger.EnterActions>
                            <DataTrigger.ExitActions>
                              <BeginStoryboard><Storyboard>
                                <ColorAnimation Storyboard.TargetProperty="(TextBlock.Foreground).(SolidColorBrush.Color)"
                                                To="#F4F7FA" Duration="{DynamicResource DurationBase}"/>
                              </Storyboard></BeginStoryboard>
                            </DataTrigger.ExitActions>
                          </DataTrigger>
                        </Style.Triggers>
                      </Style>
                    </TextBlock.Style>
                  </TextBlock>
```

- [ ] **Step 3: Popup slide-up**

In `CompletionPopup.xaml`, name the root border and give it a transform. Change `<Border Background="{StaticResource CardBg}" …>` to add `x:Name="Root"` and insert:

```xml
    <Border.RenderTransform><TranslateTransform Y="12"/></Border.RenderTransform>
```

In `CompletionPopup.xaml.cs` `Loaded`, replace the existing fade block with fade + slide, and reset the transform under reduced motion:

```csharp
            UiaNotifier.Announce(this, $"{_vm.Title} complete");
            if (!SystemParameters.ClientAreaAnimation)   // reduced motion -> no fade/slide
            {
                Opacity = 1;
                if (Root.RenderTransform is System.Windows.Media.TranslateTransform t0) t0.Y = 0;
                return;
            }
            Opacity = 0;
            var dur = (Duration)FindResource("DurationBase");
            BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, dur)
                { EasingFunction = new System.Windows.Media.Animation.CubicEase() });
            if (Root.RenderTransform is System.Windows.Media.TranslateTransform tt)
                tt.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(12, 0, dur)
                    { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } });
```

(Dismiss stays instant — animating Close would have to defer the existing close/focus-restore path; not worth it. Spec lists card-exit as nice-to-have; skipped.)

- [ ] **Step 4: Build, run, eyeball**

Build and run. Add an alarm → its card fades/slides in. Start then pause a timer → the readout cross-fades to dim and back. Fire a timer → the popup fades **and** slides up. Re-check with OS animations off → all of these are instant, app still fully usable.

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/Views/MainWindow.xaml src/Tidsro/Views/CompletionPopup.xaml src/Tidsro/Views/CompletionPopup.xaml.cs
git commit -m "feat: card entrance, paused cross-fade, and popup slide-up motion"
```

---

## Task 9: Accessibility & acceptance pass

No code unless a check fails. Run the full spec checklist; fix and re-verify any miss.

**Files:**
- Modify (only if a check fails): `src/Tidsro/Resources/tokens.xaml`

- [ ] **Step 1: Regression + build**

`dotnet build`; `dotnet test` → 110 passing.

- [ ] **Step 2: Contrast (WCAG AA)**

With a contrast checker, confirm on base `#0E141A`: `Text` `#F4F7FA` (huge), `TextMuted` `#B4BDC7`, `TextFaint` `#87919C` (pre-computed ≈ 5.8:1 — pass), gold `#E3B341` text (≈ 9.5:1), and dark `#0E141A` on the gold buttons (≈ 9.5:1). If `TextFaint` ever measures < 4.5:1, nudge it lighter (e.g. `#919BA6`) in `tokens.xaml` and re-check.

- [ ] **Step 3: Colour-never-alone**

Confirm every gold cue is also carried another way: gold primary buttons have text labels; the running-timer gold dot accompanies a live countdown; the "next" gold dot/border has its text equivalent in the alarm's accessible name. No information is gold-only.

- [ ] **Step 4: Keyboard + focus ring**

Tab through the window: focus order is sensible, the gold focus ring is visible on each control, and it appears only for keyboard (not mouse) focus.

- [ ] **Step 5: Reduced motion**

With OS animations off: hovers, card entrance, paused cross-fade, and popup show are all instant; the app is fully usable.

- [ ] **Step 6: Screen reader (Narrator)**

With Narrator on: control names and the live announcements (add/edit/delete + undo, missed-while-away, timer-complete) are read exactly as before — no regressions from the restructure.

- [ ] **Step 7: Final visual sweep**

Base reads `#0E141A`; sections are clearly separated and uncrowded; all icons are icon-font (no raw glyphs); gold appears only on primary actions + focal indicators; window resizes and remembers its size.

- [ ] **Step 8: Commit (only if Step 2 required a change)**

```bash
git add src/Tidsro/Resources/tokens.xaml
git commit -m "fix: nudge TextFaint for AA contrast on the new base"
```

---

## Self-review

**Spec coverage:** palette → T1; spacing system → T3; window resize → T4; gold rules → T1 (`GoldAction`) + T3 (application, focal dot/border) + T6 (Save); icon unification → T2; depth → T3 (cards) + T5 (popup shadow); motion (hover/press, entrance, paused, popup) → T7–T8 (card-exit intentionally skipped as nice-to-have); reduced motion → T7 override + T8 popup guard; accessibility → T9 + pre-computed contrast. Surfaces tokens/MainWindow/CompletionPopup/Settings/App all covered. No gaps.

**Placeholder scan:** every step has concrete code, exact values, exact commands, and a verification. No TBDs.

**Type/name consistency:** `GoldAction` defined T1, used T3/T6. `InteractiveHover #28323E` defined T1, used T7. Animation literals (`#1F2832`/`#28323E`/`#E3B341`/`#ECC25A`/`#F4F7FA`/`#B4BDC7`) match their tokens. `{DynamicResource DurationFast/Base/Slow}` used by all storyboards; overridden in `App.OnStartup`. `AlarmTimeBox` name preserved (focus target). All view-model command/property names reused verbatim from the current XAML.
