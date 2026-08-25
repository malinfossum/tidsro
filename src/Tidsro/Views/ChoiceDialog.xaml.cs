using System.Windows;
using System.Windows.Automation;

namespace Tidsro.Views;

/// <summary>What an import should restore. Cancel is the answer in every ambiguous case — Esc, the
/// title-bar X and Enter all land here.</summary>
public enum ImportChoice { Cancel, AlarmsOnly, Everything }

// The three-way sibling of ConfirmDialog, doubling as the app's single-OK message box. Closing with
// the title-bar X leaves DialogResult null, which reads as Cancel.
public partial class ChoiceDialog : Window
{
    private ImportChoice _choice = ImportChoice.Cancel;

    private ChoiceDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;              // announced by screen readers when the modal opens
        MessageText.Text = message;
    }

    /// <summary>Ask what to restore. Returns Cancel unless the user picked a restore explicitly.</summary>
    public static ImportChoice AskImport(Window owner, string message)
    {
        var dialog = new ChoiceDialog("Import data", message) { Owner = owner };
        dialog.ShowDialog();
        return dialog._choice;
    }

    /// <summary>A single-OK message. Used for export results, import failures, and critical failures
    /// (a failed save, a caught crash) — never a tray balloon, which is invisible on machines with
    /// notifications turned off. <paramref name="owner"/> may be null when no window is available yet
    /// (e.g. very early in startup); the dialog then centres on the screen instead of an owner.</summary>
    public static void ShowMessage(Window? owner, string title, string message)
    {
        var dialog = new ChoiceDialog(title, message) { Owner = owner };
        if (owner is null) dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;   // CenterOwner with no owner lands top-left
        dialog.ChoiceButtons.Visibility = Visibility.Collapsed;
        dialog.CancelButton.Content = "OK";
        dialog.CancelButton.SetValue(AutomationProperties.NameProperty, "OK");
        dialog.ShowDialog();
    }

    private void AlarmsOnly_Click(object sender, RoutedEventArgs e)
    {
        _choice = ImportChoice.AlarmsOnly;
        DialogResult = true;
    }

    private void Everything_Click(object sender, RoutedEventArgs e)
    {
        _choice = ImportChoice.Everything;
        DialogResult = true;
    }
}
