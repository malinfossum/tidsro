using System.Windows;
using Tidsro.ViewModels;

namespace Tidsro.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(Func<Func<string, string, bool>, SettingsViewModel> vmFactory)
    {
        InitializeComponent();
        var vm = vmFactory((title, message) => ConfirmDialog.Ask(this, title, message));
        DataContext = vm;
        Closed += (_, _) => vm.StopPreview();   // a previewed song does not outlive the window
    }

    // Opens the OFL text embedded in this binary. Owned by Settings, which is itself modal, so the
    // license window nests under it rather than under the main window.
    private void ViewLicense_Click(object sender, RoutedEventArgs e) => LicenseDialog.Show(this);

    // Save applies the draft then closes; Cancel/✕ close without saving, which discards the draft
    // (App builds a fresh SettingsViewModel from the shared snapshot each time Settings opens).
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ((SettingsViewModel)DataContext).Save();
        DialogResult = true;   // closes the modal dialog
    }
}
