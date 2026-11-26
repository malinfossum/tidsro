using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Tidsro.Models;
using Tidsro.Services;
using Tidsro.ViewModels;

namespace Tidsro.Views;

public partial class MainWindow : Window
{
    private readonly Func<SettingsWindow> _settingsFactory;
    private readonly AppSettings _settings;
    private readonly Action _persist;
    private readonly DispatcherTimer _undoTimer;
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm, Func<SettingsWindow> settingsFactory,
                      Func<AlarmItemViewModel, EditAlarmWindow> editAlarmFactory,
                      AppSettings settings, Action persist)
    {
        InitializeComponent();
        DataContext = vm;
        _vm = vm;
        vm.SelectedTabIndex = settings.SelectedTab;   // sanitized on load, so always in range

        vm.Announcement += (_, message) => UiaNotifier.Announce(this, message);
        vm.EditAlarmRequested += (_, row) => { var dlg = editAlarmFactory(row); dlg.Owner = this; dlg.ShowDialog(); };
        // After the rows are rebuilt and laid out, not before: the new containers must exist.
        vm.AlarmRowFocusRequested += (_, f) => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => FocusAlarmRow(f));

        _undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(9) };   // comfortable undo floor (spec §3.1)
        _undoTimer.Tick += (_, _) => { _undoTimer.Stop(); vm.CommitPendingDelete(); };
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(MainViewModel.PendingDeleteLabel)) return;
            _undoTimer.Stop();
            if (vm.HasPendingDelete) _undoTimer.Start();   // restart the window on each new delete
        };

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedTabIndex)) RescueFocusFromHiddenPanel();
        };

        _settingsFactory = settingsFactory;
        _settings = settings;
        _persist = persist;
        ApplyPlacement();
    }

    // A row action rebuilds the agenda, which destroys the focused button. Put focus back on the same
    // control in the same alarm's new row, so a keyboard user can press Skip again or reach Undo skip.
    // Undo skip hides itself once the last skip is taken back; Skip is then the nearest control.
    // Mouse users see no ring: WPF draws focus visuals only after keyboard input.
    private void FocusAlarmRow(AlarmRowFocus focus)
    {
        if (!IsActive) return;   // the user has moved to another window; leave focus alone
        var row = AlarmList.Items.OfType<AlarmItemViewModel>().FirstOrDefault(r => r.Item.Id == focus.AlarmId);
        if (row is null || AlarmList.ItemContainerGenerator.ContainerFromItem(row) is not DependencyObject container)
            return;
        ICommand command = focus.Action switch
        {
            AlarmRowAction.Toggle => _vm.ToggleAlarmCommand,
            AlarmRowAction.Skip => _vm.SkipAlarmCommand,
            _ => _vm.UndoSkipAlarmCommand,
        };
        var target = FindCommandTarget(container, command)
            ?? (focus.Action == AlarmRowAction.UndoSkip ? FindCommandTarget(container, _vm.SkipAlarmCommand) : null);
        target?.Focus();
    }

    private static UIElement? FindCommandTarget(DependencyObject root, ICommand command)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ButtonBase { IsVisible: true } button && ReferenceEquals(button.Command, command)) return button;
            if (FindCommandTarget(child, command) is { } found) return found;
        }
        return null;
    }

    // First show: restore the last on-screen position, or center on first run. Centering is done here
    // rather than by WindowStartupLocation.CenterScreen, which does not clamp: a saved height taller than
    // the screen put the title bar above its top edge.
    private void ApplyPlacement()
    {
        var plan = ScreenHelper.PlanLaunchPlacement(_settings.WindowLeft, _settings.WindowTop,
            _settings.WindowWidth ?? Width, _settings.WindowHeight ?? Height,
            SystemParameters.MaximumWindowTrackWidth, ScreenHelper.Monitors(), ScreenHelper.CursorPosition());
        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = plan.Width;
        Height = plan.Height;
        Left = plan.Left;
        Top = plan.Top;
    }

    /// <summary>Return to the XAML defaults after a settings reset, so OnClosing can't re-save the
    /// coordinates the reset just cleared. WindowStartupLocation only takes effect before a window is
    /// first shown, so this window (already visible) is centered manually — within the work area of the
    /// monitor it's actually on, not SystemParameters.WorkArea (always the primary monitor).</summary>
    public void ResetPlacement()
    {
        Width = DefaultWidth;
        Height = DefaultHeight;
        CenterOnCurrentMonitor();
    }

    // The XAML defaults. ResetPlacement and the import fallback both return to them.
    private const double DefaultWidth = 440;
    private const double DefaultHeight = 600;

    // Center at the current size. Clamped to the top-left of the work area, so a window larger than
    // this monitor (an imported size from a bigger screen) still opens with its title bar on screen.
    private void CenterOnCurrentMonitor()
    {
        var work = ScreenHelper.WorkAreaForWindow(this);
        Left = Math.Max(work.Left, (work.Width - Width) / 2 + work.Left);
        Top = Math.Max(work.Top, (work.Height - Height) / 2 + work.Top);
    }

    /// <summary>Move an already-visible window to imported coordinates. Writing the settings alone is
    /// not enough: OnClosing writes the current placement back on every close, so the restore would
    /// silently revert. Off-screen coordinates fall back to centering at the imported size. The launch
    /// path applies the same off-screen guard for the same reason (an unplugged monitor or a lower
    /// resolution), and also shrinks a centered window to fit its work area; this fallback does not.</summary>
    public void ApplyPlacement(AppSettings settings)
    {
        if (settings.WindowWidth is double w) Width = w;
        if (settings.WindowHeight is double h) Height = h;
        if (settings.WindowLeft is double left && settings.WindowTop is double top && IsOnScreen(left, top))
        {
            Left = left;
            Top = top;
        }
        else
        {
            // A size the backup carries stays. One it lacks returns to the default, as it always did.
            if (settings.WindowWidth is null) Width = DefaultWidth;
            if (settings.WindowHeight is null) Height = DefaultHeight;
            CenterOnCurrentMonitor();
        }
    }

    // Guard against an imported position stranded off-screen by an unplugged monitor, a lower resolution,
    // or the gap beside a smaller monitor. Width is already the size this window is about to take, but
    // Windows shrinks a window wider than its maximum track size, so only that much title bar exists.
    // This window is already shown, so WPF turns the new Left and Top into device pixels at the scale of
    // the monitor it is on now, and the check has to use that scale too.
    private bool IsOnScreen(double left, double top) =>
        ScreenHelper.IsTitleBarOnScreen(left, top, Math.Min(Width, SystemParameters.MaximumWindowTrackWidth),
                                        ScreenHelper.Monitors(), VisualTreeHelper.GetDpi(this).DpiScaleX);

    /// <summary>A collapsed panel cannot hold keyboard focus, so switching tabs while focus sits in
    /// the panel content — which Ctrl+Tab allows from anywhere in the window — drops focus to the
    /// window itself: the next Tab restarts from the top and a screen reader loses its place. Move
    /// focus to the headers instead.
    ///
    /// The panels' Visibility bindings subscribe to PropertyChanged in the constructor before this
    /// handler does, so whether WPF has already reassigned focus off the collapsing panel by the time
    /// this runs is unknowable from the headless test suite. Handling both outcomes — focus still
    /// inside Panels, or already dropped to the window (including no focused element at all) — keeps
    /// the rescue correct either way. A header click leaves focus on a TabItem, neither of those
    /// states, so the normal click path is untouched.
    ///
    /// Gated on IsActive because ResetSettings changes the tab while the modal Settings dialog owns
    /// focus. Without the gate this would pull the user out of the dialog to a header behind it.</summary>
    private void RescueFocusFromHiddenPanel()
    {
        if (!IsActive) return;
        var focused = Keyboard.FocusedElement;
        var insidePanels = focused is Visual visual && Panels.IsAncestorOf(visual);
        var droppedToWindow = focused is null || ReferenceEquals(focused, this);
        if (!insidePanels && !droppedToWindow) return;
        // TabControl is Focusable with IsTabStop false: Focus() on it parks focus on the container
        // (UIA announces the tab strip, and the gold ring — set on ShellTabItem — never appears), so
        // focus the selected header itself, falling back to the control if it isn't generated yet.
        // _vm.SelectedTabIndex, not Tabs.SelectedIndex: this runs from the view model's PropertyChanged,
        // before the SelectedIndex binding has pushed the new value onto the control. Reading the control
        // here parks focus on the header of the tab just left — and because a TabItem selects itself only
        // when it *receives* keyboard focus, a later click on that header is a no-op, stranding the user
        // on the current tab.
        if (Tabs.ItemContainerGenerator.ContainerFromIndex(_vm.SelectedTabIndex) is TabItem header) header.Focus();
        else Tabs.Focus();
    }

    // ✕ on the window hides to tray instead of quitting (real Quit is in the tray menu).
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        SavePlacement();
        _vm.StopPreview();   // a previewed song does not play on in the tray
        Hide();
    }

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
        if (WindowState != WindowState.Normal) return;    // store a usable position, not minimized/maximized
        _settings.WindowWidth = Width;
        _settings.WindowHeight = Height;
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
    }

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        var w = _settingsFactory();
        w.Owner = this;
        w.ShowDialog();
    }

    /// <summary>Widen the window until the Week grid fits, once, because the reader asked for it.
    /// The window is never resized on its own — the responsive layout stays in the converters where
    /// the redesign put it, and this is a command that runs on a click, not a breakpoint. A window
    /// that resized itself on a tab change would keep overriding a width chosen on purpose.
    ///
    /// <para>Clamped to the monitor this window is actually on, which is also the one thing the
    /// converter deciding whether to show the line could not ask about.</para></summary>
    private void WidenForGrid_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState != WindowState.Normal) return;   // nothing to widen while maximized
        var week = _vm.Timetable.Week;
        var work = ScreenHelper.WorkAreaForWindow(this);

        if (TimetableLayout.WidthToRevealGrid(
                Panels.ActualWidth, ActualWidth, week.MaxLaneCount, week.GridColumnCount, work.Width)
            is not { } target) return;

        Width = target;
        // Growing rightwards can push the window past the edge it started near, so pull it back
        // rather than leave the columns it just revealed off the screen.
        if (Left + target > work.Right) Left = Math.Max(work.Left, work.Right - target);
    }
}
