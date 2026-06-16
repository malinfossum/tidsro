using System.ComponentModel;
using System.Windows;
using Tidsro.Models;
using Tidsro.ViewModels;

namespace Tidsro.Views;

public partial class MainWindow : Window
{
    private readonly Func<SettingsWindow> _settingsFactory;
    private readonly AppSettings _settings;
    private readonly Action _persist;

    public MainWindow(MainViewModel vm, Func<SettingsWindow> settingsFactory,
                      AppSettings settings, Action persist)
    {
        InitializeComponent();
        DataContext = vm;
        _settingsFactory = settingsFactory;
        _settings = settings;
        _persist = persist;
        ApplyPlacement();
    }

    // First show: restore the last on-screen position, or centre on first run.
    private void ApplyPlacement()
    {
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
