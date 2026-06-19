using Tidsro.Models;

namespace Tidsro.ViewModels;

/// <summary>One row in the "Your day" agenda. Presentation only — the parent owns edit/delete.</summary>
public sealed class AlarmItemViewModel
{
    public TimerItem Item { get; }
    public bool IsTomorrow { get; }
    public bool IsNext { get; }

    public AlarmItemViewModel(TimerItem item, bool isTomorrow, bool isNext)
    {
        Item = item;
        IsTomorrow = isTomorrow;
        IsNext = isNext;
    }

    public string TimeText => Item.EndsAt is { } e ? e.ToString("HH\\:mm") : "--:--";
    public string DisplayLabel => string.IsNullOrWhiteSpace(Item.Label) ? "No label" : Item.Label!;
    public bool HasSound => Item.Sound != SoundChoice.None;
    public string SoundText => HasSound ? "chime" : "silent";
    public string TomorrowText => IsTomorrow ? "tomorrow" : "";
    public bool WarnBefore => Item.WarnBefore;
    public string WarnText => WarnBefore ? "5-min warning" : "";

    // Recurring alarms show their cadence ("Daily", "Weekdays", "Mon Wed Fri"); a one-shot shows
    // "tomorrow" or nothing. This is the single tag the agenda row binds to.
    public string CadenceText => Item.RecurringDays is { } d
        ? RecurrenceRules.CadenceLabel(d)
        : (IsTomorrow ? "tomorrow" : "");

    // Sound state and the cadence/next cues are carried as text, never colour alone (spec §7).
    public string AccessibleName =>
        $"Alarm at {TimeText}{CadencePhrase}, {DisplayLabel}, {SoundText}{(WarnBefore ? ", warns 5 minutes before" : "")}{(IsNext ? ", next" : "")}";

    private string CadencePhrase => Item.RecurringDays is { } d
        ? $" {RecurrenceRules.CadenceLabel(d).ToLowerInvariant()}"
        : (IsTomorrow ? " tomorrow" : "");

    public string EditLabel => $"Edit alarm at {TimeText}";
    public string DeleteLabel => $"Delete alarm at {TimeText}";
}
