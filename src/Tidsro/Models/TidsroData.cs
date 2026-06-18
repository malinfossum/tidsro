namespace Tidsro.Models;

/// <summary>Root persistence document (schema v2): app settings plus the saved alarms.</summary>
public sealed class TidsroData
{
    public const int CurrentSchema = 3;
    private const int MaxLabel = 200;

    public int SchemaVersion { get; set; } = CurrentSchema;

    // Null after loading a v1.0 file (a bare AppSettings with no "Settings" key); that is the legacy signal.
    public AppSettings? Settings { get; set; }

    public List<AlarmRecord> Alarms { get; set; } = new();

    public List<RecurringAlarmRecord> RecurringAlarms { get; set; } = new();

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
            recurring.Add(new RecurringAlarmRecord
            {
                Id = r.Id,
                Hour = r.Hour,
                Minute = r.Minute,
                Days = days,
                Label = NormaliseLabel(r.Label),
                Sound = r.Sound,
                NextFireAt = r.NextFireAt,
            });
        }

        return new TidsroData
        {
            SchemaVersion = CurrentSchema,
            Settings = (Settings ?? AppSettings.Defaults()).Sanitized(),
            Alarms = alarms,
            RecurringAlarms = recurring,
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
