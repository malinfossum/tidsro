namespace Tidsro.Models;

/// <summary>A persisted stopwatch. Elapsed time is stored as the banked total plus, for a running
/// stopwatch, the instant it last started — so a stopwatch left running keeps counting across a
/// restart, the same way a wall clock would.</summary>
public sealed class StopwatchRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Label { get; set; }

    /// <summary>Time banked by earlier runs, in milliseconds. Excludes the current run.</summary>
    public long ElapsedMs { get; set; }

    /// <summary>When the current run began, or null while stopped.</summary>
    public DateTimeOffset? StartedAt { get; set; }
}
