namespace Tidsro.Models;

/// <summary>Parse/validate a 24-hour "HH:MM" and compute the next absolute moment it fires.</summary>
public static class ClockTimeRules
{
    public static bool TryParse(string? input, out int hour, out int minute, out string? error)
    {
        hour = 0; minute = 0;
        if (string.IsNullOrWhiteSpace(input)) { error = "Enter a time as HH:MM."; return false; }

        var trimmed = input.Trim();

        if (trimmed.Contains(':'))
        {
            // Original colon path — behaviour unchanged.
            var parts = trimmed.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 2) { error = "Use HH:MM, e.g. 14:30."; return false; }

            if (!int.TryParse(parts[0], out hour) || !int.TryParse(parts[1], out minute))
            { error = "Use only numbers, e.g. 14:30."; return false; }

            if (hour is < 0 or > 23) { error = "Hour must be 0–23."; return false; }
            if (minute is < 0 or > 59) { error = "Minute must be 0–59."; return false; }

            error = null; return true;
        }

        // Bare-digit fast path — all characters must be digits.
        foreach (var c in trimmed)
            if (!char.IsAsciiDigit(c)) { error = "Use HH:MM, e.g. 14:30."; return false; }

        switch (trimmed.Length)
        {
            case 0:
                error = "Enter a time as HH:MM."; return false;
            case 1 or 2:
                // "9" → 09:00, "11" → 11:00
                hour = int.Parse(trimmed);
                minute = 0;
                break;
            case 3:
                // "930" → h=9, mm=30
                hour = int.Parse(trimmed[..1]);
                minute = int.Parse(trimmed[1..]);
                break;
            case 4:
                // "1430" → hh=14, mm=30
                hour = int.Parse(trimmed[..2]);
                minute = int.Parse(trimmed[2..]);
                break;
            default:
                error = "Use HH:MM, e.g. 14:30."; return false;
        }

        if (hour is < 0 or > 23) { error = "Hour must be 0–23."; return false; }
        if (minute is < 0 or > 59) { error = "Minute must be 0–59."; return false; }

        error = null; return true;
    }

    /// <summary>The next time HH:MM occurs: today if it is still ahead of <paramref name="now"/>, else tomorrow.</summary>
    public static DateTimeOffset ComputeFireAt(DateTimeOffset now, TimeZoneInfo zone, int hour, int minute)
    {
        var today = AtWallClock(now, zone, hour, minute);
        return today > now ? today : AtWallClock(now.AddDays(1), zone, hour, minute);
    }

    /// <summary>
    /// HH:MM on <paramref name="now"/>'s calendar day, with the offset <paramref name="zone"/> has at
    /// that moment. Copying <c>now.Offset</c> instead carries yesterday's offset across a DST switch —
    /// an alarm set on the last evening of summer time then fires an hour early.
    /// </summary>
    public static DateTimeOffset AtWallClock(DateTimeOffset now, TimeZoneInfo zone, int hour, int minute)
    {
        var local = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}
