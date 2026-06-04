namespace Tidsro.Models;

public static class CountdownRules
{
    public static readonly TimeSpan Max = TimeSpan.FromHours(24);

    public static bool TryValidate(TimeSpan d, out string? error)
    {
        if (d <= TimeSpan.Zero) { error = "Duration must be greater than zero."; return false; }
        if (d > Max) { error = "Duration can be at most 24 hours."; return false; }
        error = null; return true;
    }

    /// <summary>"25" = minutes, "MM:SS" = minutes:seconds, "H:MM:SS" = hours:minutes:seconds.</summary>
    public static bool TryParse(string? input, out TimeSpan duration, out string? error)
    {
        duration = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(input)) { error = "Enter a duration."; return false; }

        var parts = input.Split(':', StringSplitOptions.TrimEntries);
        int h = 0, m, s = 0;
        try
        {
            switch (parts.Length)
            {
                case 1: m = int.Parse(parts[0]); break;
                case 2: m = int.Parse(parts[0]); s = int.Parse(parts[1]); break;
                case 3: h = int.Parse(parts[0]); m = int.Parse(parts[1]); s = int.Parse(parts[2]); break;
                default: error = "Use minutes, MM:SS, or H:MM:SS."; return false;
            }
        }
        catch (FormatException) { error = "Use only numbers and colons."; return false; }
        catch (OverflowException) { error = "That number is too large."; return false; }

        if (h < 0 || m < 0 || s < 0 || s > 59 || (parts.Length == 3 && m > 59))
        { error = "Minutes and seconds must be 0–59."; return false; }

        var d = new TimeSpan(h, m, s);
        if (!TryValidate(d, out error)) return false;
        duration = d; return true;
    }
}
