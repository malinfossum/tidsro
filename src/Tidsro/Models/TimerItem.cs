namespace Tidsro.Models;

public sealed class TimerItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string? Label { get; set; }
    public TriggerType TriggerType { get; init; } = TriggerType.Countdown;
    public SoundChoice Sound { get; set; } = SoundChoice.None;

    // Recurring runtime (Slice 3): the weekday set this alarm repeats on. Null for countdowns and one-shots.
    public Weekdays? RecurringDays { get; set; }

    // Countdown runtime (Slice 1)
    public TimeSpan OriginalDuration { get; set; }
    public TimeSpan? Duration { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public TimeSpan? PausedRemaining { get; set; }
    public TimerState State { get; set; } = TimerState.Idle;

    // Pre-alarm warning (heads-up): the persisted per-alarm opt-in, plus a transient per-occurrence guard
    // (managed inside SchedulerService; never persisted).
    public bool WarnBefore { get; set; }
    public bool WarningSent { get; set; }
}
