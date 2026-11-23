namespace Tidsro.Models;

/// <summary>One logged stretch of time: a stopwatch run (start to pause, reset or removal) or a
/// countdown that finished. Kept in <see cref="TidsroData.TimeLog"/> and exportable as CSV.</summary>
public sealed class TimeLogEntry
{
    public const string StopwatchKind = "Stopwatch";
    public const string TimerKind = "Timer";

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = StopwatchKind;
    public string? Label { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public long DurationMs { get; set; }

    public TimeSpan Duration => TimeSpan.FromMilliseconds(DurationMs);
}
