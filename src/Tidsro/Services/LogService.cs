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
    private readonly object _gate = new();
    private readonly Version? _version;
    private string? _lastSignature;
    private DateTimeOffset _lastWritten;

    public LogService(string path, IClock clock)
    {
        _path = path;
        _clock = clock;
        _version = Assembly.GetExecutingAssembly().GetName().Version;
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
        lock (_gate)
        {
            var now = _clock.Now;
            var signature = $"{source}|{ex.GetType().FullName}|{ex.Message}";
            if (signature == _lastSignature && now - _lastWritten < DedupeWindow)
                return false;                                          // collapse a run of identical errors

            _lastSignature = signature;
            _lastWritten = now;

            Write(Format(now, ex, source, _version));
            return true;
        }
    }

    private void Write(string entry)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            RollIfTooLarge();
            File.AppendAllText(_path, entry);
        }
        catch { /* logging must never throw — a logger that crashes while logging a crash is useless */ }
    }

    private void RollIfTooLarge()
    {
        try
        {
            var info = new FileInfo(_path);
            if (info.Exists && info.Length > MaxBytes)
                File.Move(_path, _path + ".old", overwrite: true);    // keep one previous generation
        }
        catch { /* a failed roll must not stop logging */ }
    }
}
