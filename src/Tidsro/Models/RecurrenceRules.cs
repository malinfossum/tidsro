namespace Tidsro.Models;

/// <summary>
/// Weekday-set recurrence: compute the next / most-recent occurrence of a time on a set of days,
/// resolve a RepeatOption to a day-set (and back), and render a cadence label.
/// Builds occurrences in local wall-clock, mirroring <see cref="ClockTimeRules.ComputeFireAt"/>.
/// </summary>
public static class RecurrenceRules
{
    public const Weekdays AllDays =
        Weekdays.Mon | Weekdays.Tue | Weekdays.Wed | Weekdays.Thu | Weekdays.Fri | Weekdays.Sat | Weekdays.Sun;
    private const Weekdays MonToFri = Weekdays.Mon | Weekdays.Tue | Weekdays.Wed | Weekdays.Thu | Weekdays.Fri;
    private const Weekdays SatSun = Weekdays.Sat | Weekdays.Sun;

    /// <summary>The soonest matching weekday at HH:MM strictly after <paramref name="now"/>.</summary>
    public static DateTimeOffset NextOccurrence(DateTimeOffset now, TimeZoneInfo zone, int hour, int minute, Weekdays days)
    {
        for (var add = 0; add <= 7; add++)
        {
            var candidate = ClockTimeRules.AtWallClock(now.AddDays(add), zone, hour, minute);
            if (candidate > now && Includes(days, candidate.DayOfWeek)) return candidate;
        }
        throw new ArgumentException("A recurring alarm must repeat on at least one day.", nameof(days));
    }

    /// <summary>The latest matching weekday at HH:MM at or before <paramref name="now"/>.</summary>
    public static DateTimeOffset MostRecentOccurrence(DateTimeOffset now, TimeZoneInfo zone, int hour, int minute, Weekdays days)
    {
        for (var sub = 0; sub <= 7; sub++)
        {
            var candidate = ClockTimeRules.AtWallClock(now.AddDays(-sub), zone, hour, minute);
            if (candidate <= now && Includes(days, candidate.DayOfWeek)) return candidate;
        }
        throw new ArgumentException("A recurring alarm must repeat on at least one day.", nameof(days));
    }

    public static Weekdays DaysFor(RepeatOption option, Weekdays custom) => option switch
    {
        RepeatOption.Once => Weekdays.None,
        RepeatOption.Daily => AllDays,
        RepeatOption.Weekdays => MonToFri,
        RepeatOption.Weekends => SatSun,
        RepeatOption.Custom => custom,
        _ => Weekdays.None,
    };

    public static RepeatOption OptionFor(Weekdays days) => days switch
    {
        Weekdays.None => RepeatOption.Once,
        AllDays => RepeatOption.Daily,
        MonToFri => RepeatOption.Weekdays,
        SatSun => RepeatOption.Weekends,
        _ => RepeatOption.Custom,
    };

    public static string CadenceLabel(Weekdays days)
    {
        if (days == Weekdays.None) return "once";
        if (days == AllDays) return "Daily";
        if (days == MonToFri) return "Weekdays";
        if (days == SatSun) return "Weekends";

        var parts = new List<string>();
        foreach (var (flag, name) in Ordered)
            if ((days & flag) != 0) parts.Add(name);
        return string.Join(" ", parts);
    }

    /// <summary>"Mon".."Sun", from the same table as the cadence label, so no culture decides it.</summary>
    public static string DayName(DayOfWeek day)
    {
        var flag = DayFlag(day);
        foreach (var (f, name) in Ordered)
            if (f == flag) return name;
        return "";
    }

    /// <summary>A short date for a cue: "Fri 02.01".</summary>
    public static string ShortDate(DateTimeOffset when) => ShortDate(DateOnly.FromDateTime(when.DateTime));

    /// <summary>The same short date for a calendar day.</summary>
    public static string ShortDate(DateOnly day) => $"{DayName(day.DayOfWeek)} {day:dd.MM}";

    private static bool Includes(Weekdays set, DayOfWeek day) => (set & DayFlag(day)) != 0;

    private static Weekdays DayFlag(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => Weekdays.Mon,
        DayOfWeek.Tuesday => Weekdays.Tue,
        DayOfWeek.Wednesday => Weekdays.Wed,
        DayOfWeek.Thursday => Weekdays.Thu,
        DayOfWeek.Friday => Weekdays.Fri,
        DayOfWeek.Saturday => Weekdays.Sat,
        _ => Weekdays.Sun,
    };

    private static readonly (Weekdays flag, string name)[] Ordered =
    {
        (Weekdays.Mon, "Mon"), (Weekdays.Tue, "Tue"), (Weekdays.Wed, "Wed"),
        (Weekdays.Thu, "Thu"), (Weekdays.Fri, "Fri"), (Weekdays.Sat, "Sat"), (Weekdays.Sun, "Sun"),
    };
}
