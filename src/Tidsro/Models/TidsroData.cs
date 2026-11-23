namespace Tidsro.Models;

/// <summary>Root persistence document: app settings plus the saved alarms. Current version is <see cref="CurrentSchema"/>.</summary>
public sealed class TidsroData
{
    public const int CurrentSchema = 5;
    private const int MaxLabel = 200;

    public int SchemaVersion { get; set; } = CurrentSchema;

    // Null after loading a v1.0 file (a bare AppSettings with no "Settings" key); that is the legacy signal.
    public AppSettings? Settings { get; set; }

    public List<AlarmRecord> Alarms { get; set; } = new();

    public List<RecurringAlarmRecord> RecurringAlarms { get; set; } = new();

    /// <summary>Stopwatches, in display order. Optional: older files have no key and load as none.</summary>
    public List<StopwatchRecord> Stopwatches { get; set; } = new();

    private const int MaxStopwatches = 100;

    /// <summary>Logged stopwatch runs and finished timers, oldest first. Optional, like Stopwatches.</summary>
    public List<TimeLogEntry> TimeLog { get; set; } = new();

    public const int MaxTimeLog = 5000;

    public static TidsroData Defaults() => new() { Settings = AppSettings.Defaults() };

    /// <summary>Harden untrusted input loaded from disk: sanitise settings, drop unusable alarms, normalise labels.</summary>
    public TidsroData Sanitized()
    {
        var seen = new HashSet<Guid>();
        var alarms = new List<AlarmRecord>();
        foreach (var a in Alarms ?? new List<AlarmRecord>())
        {
            if (a is null) continue;
            if (!Enum.IsDefined(a.Sound)) continue;            // unknown enum
            if (a.FireAt == default) continue;                 // never set
            if (!IsRepresentable(a.FireAt)) continue;          // extreme value would throw when armed
            if (!seen.Add(a.Id)) continue;                     // duplicate id -> keep the first only
            alarms.Add(new AlarmRecord
            {
                Id = a.Id,
                FireAt = a.FireAt,
                Label = NormaliseLabel(a.Label),
                Sound = a.Sound,
                WarnBefore = a.WarnBefore,
                Enabled = a.Enabled,
            });
        }

        var recSeen = new HashSet<Guid>();
        var recurring = new List<RecurringAlarmRecord>();
        foreach (var r in RecurringAlarms ?? new List<RecurringAlarmRecord>())
        {
            if (r is null) continue;
            var days = r.Days & RecurrenceRules.AllDays;       // strip any unknown bits
            if (days == Weekdays.None) continue;               // a recurring alarm needs at least one day
            if (r.Hour is < 0 or > 23) continue;
            if (r.Minute is < 0 or > 59) continue;
            if (!Enum.IsDefined(r.Sound)) continue;            // unknown enum
            if (r.NextFireAt == default || !IsRepresentable(r.NextFireAt)) continue;
            if (!recSeen.Add(r.Id)) continue;                  // duplicate id -> keep the first only

            // A bad end nulls the end; it never drops the alarm. Nothing about an unusable end
            // justifies losing an alarm out of the schedule — the same posture as
            // TimetableLayout.Build, which skips what it cannot place and renders the rest.
            var end = r.EndMinute;
            if (end is { } e && (e < 0 || e > 1439 || e <= r.Hour * 60 + r.Minute)) end = null;

            recurring.Add(new RecurringAlarmRecord
            {
                Id = r.Id,
                Hour = r.Hour,
                Minute = r.Minute,
                Days = days,
                Label = NormaliseLabel(r.Label),
                Sound = r.Sound,
                NextFireAt = r.NextFireAt,
                WarnBefore = r.WarnBefore,
                Enabled = r.Enabled,
                EndMinute = end,
            });
        }

        var swSeen = new HashSet<Guid>();
        var stopwatches = new List<StopwatchRecord>();
        foreach (var s in Stopwatches ?? new List<StopwatchRecord>())
        {
            if (s is null) continue;
            if (stopwatches.Count >= MaxStopwatches) break;
            if (!swSeen.Add(s.Id)) continue;                   // duplicate id -> keep the first only
            var started = s.StartedAt;
            // A start time that is not a sane instant would make elapsed nonsense: stop it instead.
            if (started is { } st && (st.UtcDateTime.Year < 2000 || st.UtcDateTime.Year > 9000)) started = null;
            stopwatches.Add(new StopwatchRecord
            {
                Id = s.Id,
                Label = NormaliseLabel(s.Label),
                ElapsedMs = Math.Clamp(s.ElapsedMs, 0, (long)TimeSpan.FromDays(3650).TotalMilliseconds),
                StartedAt = started,
            });
        }

        var logSeen = new HashSet<Guid>();
        var log = new List<TimeLogEntry>();
        foreach (var e in TimeLog ?? new List<TimeLogEntry>())
        {
            if (e is null) continue;
            if (e.StartedAt == default || e.EndedAt < e.StartedAt) continue;
            if (e.StartedAt.UtcDateTime.Year < 2000 || e.EndedAt.UtcDateTime.Year > 9000) continue;
            if (!logSeen.Add(e.Id)) continue;
            log.Add(new TimeLogEntry
            {
                Id = e.Id,
                Kind = e.Kind == TimeLogEntry.TimerKind ? TimeLogEntry.TimerKind : TimeLogEntry.StopwatchKind,
                Label = NormaliseLabel(e.Label),
                StartedAt = e.StartedAt,
                EndedAt = e.EndedAt,
                DurationMs = Math.Clamp(e.DurationMs, 0, (long)TimeSpan.FromDays(3650).TotalMilliseconds),
            });
        }
        log = log.OrderBy(e => e.StartedAt).ToList();
        if (log.Count > MaxTimeLog) log = log.Skip(log.Count - MaxTimeLog).ToList();   // keep the newest

        return new TidsroData
        {
            SchemaVersion = CurrentSchema,
            TimeLog = log,
            Settings = (Settings ?? AppSettings.Defaults()).Sanitized(),
            Alarms = alarms,
            RecurringAlarms = recurring,
            Stopwatches = stopwatches,
        };
    }

    // Values near DateTime.Min/MaxValue throw when converted to DateTimeOffset (the local offset
    // pushes them out of range), which would crash the launch arm pass. Reject them here instead.
    private static bool IsRepresentable(DateTime t) =>
        t > DateTime.MinValue.AddDays(2) && t < DateTime.MaxValue.AddDays(-2);

    private static string? NormaliseLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;
        var trimmed = label.Trim();
        return trimmed.Length > MaxLabel ? trimmed[..MaxLabel] : trimmed;
    }
}
