using System.Windows;
using Tidsro.Services;
using Tidsro.ViewModels;

namespace Tidsro.Views;

public partial class EditAlarmWindow : Window
{
    public EditAlarmWindow(EditAlarmViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        // Save sets DialogResult=true, Cancel/✕ false; either way the modal closes.
        vm.CloseRequested += (_, saved) => { DialogResult = saved; Close(); };
        Closed += (_, _) => vm.StopPreview();   // a previewed song does not outlive the dialog
        // LiveSetting on the text raises no event by itself, so say the skip warning out loud.
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditAlarmViewModel.SkipWarning) && vm.SkipWarning is { } warning)
                UiaNotifier.Announce(this, warning);
        };
    }
}
