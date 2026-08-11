using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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
        vm.SelectedTabIndex = settings.SelectedTab;   // sanitised on load, so always in range

        vm.Announcement += (_, message) => UiaNotifier.Announce(this, message);
        vm.EditAlarmRequested += (_, row) => { var dlg = editAlarmFactory(row); dlg.Owner = this; dlg.ShowDialog(); };

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

    // First show: restore the last on-screen position, or centre on first run.
    private void ApplyPlacement()
    {
        if (_settings.WindowWidth is double w) Width = w;
        if (_settings.WindowHeight is double h) Height = h;
        if (_settings.WindowLeft is double left && _settings.WindowTop is double top && IsOnScreen(left, top))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    /// <summary>Return to the XAML defaults after a settings reset, so OnClosing can't re-save the
    /// coordinates the reset just cleared. WindowStartupLocation only takes effect before a window is
    /// first shown, so this window (already visible) is centred manually — within the work area of the
    /// monitor it's actually on, not SystemParameters.WorkArea (always the primary monitor).</summary>
    public void ResetPlacement()
    {
        Width = 440;
        Height = 600;
        var work = ScreenHelper.WorkAreaForWindow(this);
        Left = (work.Width - Width) / 2 + work.Left;
        Top = (work.Height - Height) / 2 + work.Top;
    }

    // Guard against a saved position stranded off-screen by an unplugged monitor or lower resolution.
    private static bool IsOnScreen(double left, double top)
    {
        var x = SystemParameters.VirtualScreenLeft;
        var y = SystemParameters.VirtualScreenTop;
        var right = x + SystemParameters.VirtualScreenWidth;
        var bottom = y + SystemParameters.VirtualScreenHeight;
        return left >= x - 8 && top >= y && left <= right - 40 && top <= bottom - 40;
    }

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
        if (Tabs.ItemContainerGenerator.ContainerFromIndex(Tabs.SelectedIndex) is TabItem header) header.Focus();
        else Tabs.Focus();
    }

    // ✕ on the window hides to tray instead of quitting (real Quit is in the tray menu).
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        SavePlacement();
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
        if (WindowState != WindowState.Normal) return;    // store a usable position, not minimised/maximised
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
}
