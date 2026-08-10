using System.Windows;
using Tidsro.ViewModels;

namespace Tidsro.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(Func<Func<string, string, bool>, SettingsViewModel> vmFactory)
    {
        InitializeComponent();
        DataContext = vmFactory((title, message) => ConfirmDialog.Ask(this, title, message));
    }

    // Save applies the draft then closes; Cancel/✕ close without saving, which discards the draft
    // (App builds a fresh SettingsViewModel from the shared snapshot each time Settings opens).
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ((SettingsViewModel)DataContext).Save();
        DialogResult = true;   // closes the modal dialog
    }
}
