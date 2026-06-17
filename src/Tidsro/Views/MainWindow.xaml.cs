using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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

    public MainWindow(MainViewModel vm, Func<SettingsWindow> settingsFactory,
                      AppSettings settings, Action persist)
    {
        InitializeComponent();
        DataContext = vm;

        vm.Announcement += (_, message) => UiaNotifier.Announce(this, message);

        _undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(9) };   // comfortable undo floor (spec §3.1)
        _undoTimer.Tick += (_, _) => { _undoTimer.Stop(); vm.CommitPendingDelete(); };
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(MainViewModel.PendingDeleteLabel)) return;
            _undoTimer.Stop();
            if (vm.HasPendingDelete) _undoTimer.Start();   // restart the window on each new delete
        };

        _settingsFactory = settingsFactory;
        _settings = settings;
        _persist = persist;
        ApplyPlacement();
        SizeChanged += (_, _) => ApplyLayout();
        Loaded += (_, _) => ApplyLayout();
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

    private const double WideBreakpoint = 760;

    // Narrow: sections stack with a horizontal divider. Wide: side by side with a vertical divider.
    private void ApplyLayout()
    {
        var wide = ActualWidth >= WideBreakpoint;
        Sections.ColumnDefinitions.Clear();
        Sections.RowDefinitions.Clear();
        if (wide)
        {
            Sections.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Sections.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Sections.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(QuickPanel, 0); Grid.SetColumn(QuickPanel, 0);
            Grid.SetRow(Divider, 0); Grid.SetColumn(Divider, 1);
            Grid.SetRow(DayPanel, 0); Grid.SetColumn(DayPanel, 2);
            QuickPanel.Margin = new Thickness(0, 0, 16, 0);
            DayPanel.Margin = new Thickness(16, 0, 0, 0);
            Divider.Width = 1; Divider.Height = double.NaN;
            Divider.HorizontalAlignment = HorizontalAlignment.Center;
            Divider.VerticalAlignment = VerticalAlignment.Stretch;
            Divider.Margin = new Thickness(0);
        }
        else
        {
            Sections.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Sections.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Sections.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(QuickPanel, 0); Grid.SetRow(QuickPanel, 0);
            Grid.SetColumn(Divider, 0); Grid.SetRow(Divider, 1);
            Grid.SetColumn(DayPanel, 0); Grid.SetRow(DayPanel, 2);
            QuickPanel.Margin = new Thickness(0);
            DayPanel.Margin = new Thickness(0);
            Divider.Width = double.NaN; Divider.Height = 1;
            Divider.HorizontalAlignment = HorizontalAlignment.Stretch;
            Divider.VerticalAlignment = VerticalAlignment.Center;
            Divider.Margin = new Thickness(0, 12, 0, 12);
        }
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

    // ✕ on the window hides to tray instead of quitting (real Quit is in the tray menu).
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        SavePlacement();
        Hide();
    }

    private void SavePlacement()
    {
        if (WindowState != WindowState.Normal) return;   // store a usable position, not minimised/maximised
        _settings.WindowWidth = Width;
        _settings.WindowHeight = Height;
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        try { _persist(); } catch { /* position is a nicety; never block hiding */ }
    }

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        var w = _settingsFactory();
        w.Owner = this;
        w.ShowDialog();
    }
}
