using Tidsro.Models;

namespace Tidsro.ViewModels;

/// <summary>The row control an action came from, so focus can return to it after the rebuild.</summary>
public enum AlarmRowAction { Toggle, Skip, UndoSkip }

public sealed record AlarmRowFocus(Guid AlarmId, AlarmRowAction Action);

/// <summary>One row in the "Your day" agenda. Presentation only — the parent owns edit/delete.</summary>
public sealed class AlarmItemViewModel
{
    public TimerItem Item { get; }
    public bool IsTomorrow { get; }
    public bool IsNext { get; }

    /// <summary>How many occurrences this alarm will pass over before it rings again (skip-next).</summary>
    public int SkippedCount { get; }

    public AlarmItemViewModel(TimerItem item, bool isTomorrow, bool isNext, int skippedCount)
    {
        Item = item;
        IsTomorrow = isTomorrow;
        IsNext = isNext;
        SkippedCount = skippedCount;
    }

    public string TimeText => Item.EndsAt is { } e ? e.ToString("HH\\:mm") : "--:--";

    /// <summary>What the row prints: "09:00" for an instant, "09:00–10:30" for a timetable block, so
    /// the Schedule tab and the Week tab agree about what an alarm is. The command labels keep
    /// <see cref="TimeText"/> — "Edit alarm at 09:00" names the alarm, not its length.</summary>
    public string RangeText => Item.EndMinute is { } end
        ? $"{TimeText}–{end / 60:D2}:{end % 60:D2}"
        : TimeText;

    // Spoken rather than dashed: a screen reader reads an en dash unreliably, and a block's length
    // is exactly what the field is for.
    private string SpokenTime => Item.EndMinute is { } end
        ? $"{TimeText} to {end / 60:D2}:{end % 60:D2}"
        : TimeText;
    public string DisplayLabel => string.IsNullOrWhiteSpace(Item.Label) ? "No label" : Item.Label!;
    public bool HasSound => Item.Sound != SoundChoice.None;
    public string SoundText => HasSound ? "chime" : "silent";
    public string TomorrowText => IsTomorrow ? "tomorrow" : "";
    public bool WarnBefore => Item.WarnBefore;
    public string WarnText => WarnBefore ? "5-min warning" : "";
    public bool IsEnabled => Item.IsEnabled;
    public string ToggleLabel => $"Alarm at {TimeText}";

    // Recurring alarms show their cadence ("Daily", "Weekdays", "Mon Wed Fri"); a one-shot shows
    // "tomorrow" or nothing. This is the single tag the agenda row binds to.
    public string CadenceText => Item.RecurringDays is { } d
        ? RecurrenceRules.CadenceLabel(d)
        : (IsTomorrow ? "tomorrow" : "");

    // Sound state and the cadence/next cues are carried as text, never color alone (spec §7).
    public string AccessibleName =>
        $"Alarm at {SpokenTime}{CadencePhrase}, {DisplayLabel}, {SoundText}{(WarnBefore ? ", warns 5 minutes before" : "")}{(IsNext ? ", next" : "")}{(IsSkipped ? $", {SkipText.ToLowerInvariant()}" : "")}{(IsEnabled ? "" : ", off")}";

    private string CadencePhrase => Item.RecurringDays is { } d
        ? $" {RecurrenceRules.CadenceLabel(d).ToLowerInvariant()}"
        : (IsTomorrow ? " tomorrow" : "");

    // Skip-next: only a recurring alarm that is on has a next time to pass over. A one-shot alarm is
    // deleted instead, and an alarm that is off is already silent. A skip made before it was switched
    // off still shows, dimmed with the row, and Undo skip still takes it back.
    public bool CanSkip => Item.TriggerType == TriggerType.Recurring && Item.IsEnabled;
    public bool IsSkipped => SkippedCount > 0;

    /// <summary>"Skipped, next Fri 02.01" or "2 skipped, next Sat 03.01": the row says what will
    /// actually happen, so the skip is never a silent surprise.</summary>
    public string SkipText => !IsSkipped || Item.EndsAt is not { } next
        ? ""
        : $"{(SkippedCount == 1 ? "Skipped" : $"{SkippedCount} skipped")}, next {RecurrenceRules.ShortDate(next)}";

    public string SkipLabel => $"Skip the next alarm at {TimeText}";
    public string UndoSkipLabel => $"Undo skip for the alarm at {TimeText}";

    public string EditLabel => $"Edit alarm at {TimeText}";
    public string DeleteLabel => $"Delete alarm at {TimeText}";
}
