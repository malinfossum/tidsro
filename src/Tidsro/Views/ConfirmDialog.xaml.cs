using System.Windows;

namespace Tidsro.Views;

// A dark, owner-centred yes/no in the app's own styling. Esc cancels via IsCancel, and closing with
// the title-bar X leaves DialogResult null — which Ask reads as "not confirmed".
public partial class ConfirmDialog : Window
{
    private ConfirmDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;              // announced by screen readers when the modal opens
        MessageText.Text = message;
    }

    /// <summary>Show the question modally. True only when the user explicitly confirms.</summary>
    public static bool Ask(Window owner, string title, string message) =>
        new ConfirmDialog(title, message) { Owner = owner }.ShowDialog() == true;

    private void Yes_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
