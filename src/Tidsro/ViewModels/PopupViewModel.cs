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
    private readonly bool _isWarning;
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

    // Heads-up (5-minute warning) variant: informational and close-only — no Snooze/Restart, and Dismiss
    // never disarms the alarm (it stays armed and still fires). App supplies the title (label, or "Alarm").
    public PopupViewModel(TimerItem item, string title)
    {
        _item = item;
        _title = title;
        _isWarning = true;
        _onSnooze = i => i;
        _onRestart = i => i;
        _onDismiss = _ => { };   // close-only
    }

    public TimerItem Item => _item;
    public bool IsWarning => _isWarning;
    /// <summary>Snooze (+5) is a completion action; the heads-up hides it.</summary>
    public bool ShowSnooze => !_isWarning;
    /// <summary>Restart re-runs a duration; meaningless for an alarm or a heads-up, so the card hides it.</summary>
    public bool ShowRestart => !_isWarning && _item.TriggerType == TriggerType.Countdown;
    /// <summary>The faint status line after the glyph (leading space matches the layout): " complete" / " in 5 minutes".</summary>
    public string HeaderText => _isWarning ? " in 5 minutes" : " complete";
    /// <summary>What the card announces to a screen reader on appear.</summary>
    public string AnnouncementText => _isWarning ? $"{Title} in 5 minutes" : $"{Title} complete";
    public event EventHandler? CloseRequested;

    [RelayCommand] private void Plus5()   { if (Begin()) { _onSnooze(_item);  Close(); } }
    [RelayCommand] private void Restart() { if (Begin()) { _onRestart(_item); Close(); } }
    [RelayCommand] private void Dismiss() { if (Begin()) { _onDismiss(_item); Close(); } }

    private bool Begin() { if (_handled) return false; _handled = true; return true; }
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
