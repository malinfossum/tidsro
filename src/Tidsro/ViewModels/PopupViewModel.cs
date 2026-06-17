using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidsro.Models;

namespace Tidsro.ViewModels;

public partial class PopupViewModel : ObservableObject
{
    private readonly TimerItem _item;
    private readonly Func<TimerItem, TimerItem> _onSnooze;
    private readonly Func<TimerItem, TimerItem> _onRestart;
    private readonly Action<TimerItem> _onDismiss;
    private bool _handled;   // debounce: one action per card

    [ObservableProperty] private string _title;

    public PopupViewModel(TimerItem item,
        Func<TimerItem, TimerItem> onSnooze,
        Func<TimerItem, TimerItem> onRestart,
        Action<TimerItem> onDismiss)
    {
        _item = item; _onSnooze = onSnooze; _onRestart = onRestart; _onDismiss = onDismiss;
        _title = string.IsNullOrWhiteSpace(item.Label) ? "Timer complete" : item.Label!;
    }

    public TimerItem Item => _item;
    /// <summary>Restart re-runs a duration; it has no meaning for a one-shot alarm, so the card hides it.</summary>
    public bool ShowRestart => _item.TriggerType == TriggerType.Countdown;
    public event EventHandler? CloseRequested;

    [RelayCommand] private void Plus5()   { if (Begin()) { _onSnooze(_item);  Close(); } }
    [RelayCommand] private void Restart() { if (Begin()) { _onRestart(_item); Close(); } }
    [RelayCommand] private void Dismiss() { if (Begin()) { _onDismiss(_item); Close(); } }

    private bool Begin() { if (_handled) return false; _handled = true; return true; }
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
