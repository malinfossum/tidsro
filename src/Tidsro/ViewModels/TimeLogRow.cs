using Tidsro.Models;
using Tidsro.Services;

namespace Tidsro.ViewModels;

/// <summary>One time-log entry as the Stopwatch tab draws it. Immutable: the log only ever appends.</summary>
public sealed class TimeLogRow
{
    public TimeLogRow(TimeLogEntry entry)
    {
        Entry = entry;
        var start = entry.StartedAt.ToLocalTime();
        var end = entry.EndedAt.ToLocalTime();
        var isTimer = entry.Kind == TimeLogEntry.TimerKind;
        DisplayLabel = string.IsNullOrWhiteSpace(entry.Label) ? (isTimer ? "Timer" : "Stopwatch") : entry.Label!;
        KindText = isTimer ? "timer" : "stopwatch";
        // Segoe Fluent Icons: Timer (E916) for countdowns, Stopwatch-ish Clock (E917) for stopwatches.
        KindGlyph = ((char)(isTimer ? 0xE916 : 0xE917)).ToString();
        WhenText = $"{start:ddd d MMM} · {start:HH\\:mm}–{end:HH\\:mm}";
        DurationText = TimeLogCsv.FormatDuration(entry.Duration);
        AccessibleName = $"{DisplayLabel}, {KindText}, {DurationText}, {start:dddd d MMMM HH\\:mm} to {end:HH\\:mm}";
    }

    public TimeLogEntry Entry { get; }
    public string DisplayLabel { get; }
    public string KindText { get; }
    public string KindGlyph { get; }
    public string WhenText { get; }
    public string DurationText { get; }
    public string AccessibleName { get; }
}
