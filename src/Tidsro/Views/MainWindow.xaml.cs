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
