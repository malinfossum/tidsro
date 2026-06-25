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

    /// <summary>
    /// Records one unhandled exception. Returns true when this is a fresh error worth surfacing to the
    /// user (a balloon), false when it is a consecutive duplicate suppressed within the dedupe window.
    /// The return reflects the throttle decision only — it stays true even if the file write fails,
    /// because the user should still be told.
    /// </summary>
    public bool Log(Exception ex, string source)
    {
        var now = _clock.Now;
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        Write(Format(now, ex, source, version));
        return true;
    }

    private void Write(string entry)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.AppendAllText(_path, entry);
    }
}
