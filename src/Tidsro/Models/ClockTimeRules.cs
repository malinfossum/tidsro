namespace Tidsro.Models;

/// <summary>Parse/validate a 24-hour "HH:MM" and compute the next absolute moment it fires.</summary>
public static class ClockTimeRules
{
    public static bool TryParse(string? input, out int hour, out int minute, out string? error)
    {
        hour = 0; minute = 0;
        if (string.IsNullOrWhiteSpace(input)) { error = "Enter a time as HH:MM."; return false; }

        var parts = input.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 2) { error = "Use HH:MM, e.g. 14:30."; return false; }

        if (!int.TryParse(parts[0], out hour) || !int.TryParse(parts[1], out minute))
        { error = "Use only numbers, e.g. 14:30."; return false; }

        if (hour is < 0 or > 23) { error = "Hour must be 0–23."; return false; }
        if (minute is < 0 or > 59) { error = "Minute must be 0–59."; return false; }

        error = null; return true;
    }

    /// <summary>The next time HH:MM occurs: today if it is still ahead of <paramref name="now"/>, else tomorrow.</summary>
    public static DateTimeOffset ComputeFireAt(DateTimeOffset now, int hour, int minute)
    {
        var today = new DateTimeOffset(now.Year, now.Month, now.Day, hour, minute, 0, now.Offset);
        return today > now ? today : today.AddDays(1);
    }
}
