using System.Globalization;
using System.IO;
using System.Reflection;

namespace Tidsro.Services;

/// <summary>
/// Appends unhandled-exception records to a log file so a crash is never silent. Mirrors
/// PersistenceService: a path in, all I/O self-contained, tested against a temp file. Never throws —
/// a logger that crashes while logging a crash is useless.
/// </summary>
public sealed class LogService
{
    private const long MaxBytes = 512 * 1024;                              // roll the log past ~512 KB
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromSeconds(5);

    private readonly string _path;
    private readonly IClock _clock;
    private string? _lastSignature;
    private DateTimeOffset _lastWritten;

    public LogService(string path, IClock clock)
    {
        _path = path;
        _clock = clock;
    }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tidsro", "tidsro.log");

    /// <summary>The exact text of one log entry. Pure, so it can be asserted directly in tests.</summary>
    public static string Format(DateTimeOffset now, Exception ex, string source, Version? version)
    {
        var stamp = now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
        var ver = version is null ? "?" : $"v{version.Major}.{version.Minor}.{version.Build}";
        return $"===== {stamp} · {ver} · {source} ====={Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";
    }
}
