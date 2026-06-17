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

    // Sound state and the tomorrow/next cues are carried as text, never colour alone (spec §7).
    public string AccessibleName =>
        $"Alarm at {TimeText}{(IsTomorrow ? " tomorrow" : "")}, {DisplayLabel}, {SoundText}{(IsNext ? ", next" : "")}";

    public string EditLabel => $"Edit alarm at {TimeText}";
    public string DeleteLabel => $"Delete alarm at {TimeText}";
}
