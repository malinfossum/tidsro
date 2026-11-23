using System.Globalization;
using System.IO;
using System.Text;
using Tidsro.Models;

namespace Tidsro.Services;

/// <summary>The time log as CSV: one row per entry, oldest first, in local time. Written with a UTF-8
/// BOM so Excel reads labels with non-ASCII characters (æ, ø, å) correctly when double-clicked.</summary>
public static class TimeLogCsv
{
    public const string Header = "Type,Label,Date,Start,End,Duration,Duration (seconds)";

    public static string Build(IEnumerable<TimeLogEntry> entries)
    {
        var sb = new StringBuilder();
        sb.Append(Header).Append("\r\n");
        foreach (var e in entries.OrderBy(e => e.StartedAt))
        {
            var start = e.StartedAt.ToLocalTime();
            var end = e.EndedAt.ToLocalTime();
            sb.Append(Escape(e.Kind)).Append(',')
              .Append(Escape(e.Label ?? "")).Append(',')
              .Append(start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
              .Append(start.ToString("HH:mm:ss", CultureInfo.InvariantCulture)).Append(',')
              .Append(end.ToString("HH:mm:ss", CultureInfo.InvariantCulture)).Append(',')
              .Append(FormatDuration(e.Duration)).Append(',')
              .Append(Math.Round(e.Duration.TotalSeconds, 1).ToString("0.0", CultureInfo.InvariantCulture))
              .Append("\r\n");
        }
        return sb.ToString();
    }

    public static void Write(string path, IEnumerable<TimeLogEntry> entries)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, Build(entries), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    /// <summary>h:mm:ss, hours unpadded and allowed past 24.</summary>
    public static string FormatDuration(TimeSpan d)
    {
        var whole = TimeSpan.FromSeconds(Math.Floor(Math.Max(0, d.TotalSeconds)));
        return $"{(int)whole.TotalHours}:{whole.Minutes:00}:{whole.Seconds:00}";
    }

    // RFC 4180 quoting, plus a leading apostrophe for text a spreadsheet would run as a formula.
    private static string Escape(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
