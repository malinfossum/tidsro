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

    // Save applies the draft then closes; Cancel/✕ close without saving, which discards the draft
    // (App builds a fresh SettingsViewModel from the shared snapshot each time Settings opens).
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ((SettingsViewModel)DataContext).Save();
        DialogResult = true;   // closes the modal dialog
    }
}
