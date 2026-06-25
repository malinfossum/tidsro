using System.IO;
using System.Linq;
using Tidsro.Services;
using Xunit;

namespace Tidsro.Tests;

public class LogServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;
    private readonly FakeClock _clock = new();

    public LogServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "TidsroTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "tidsro.log");
    }

    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private static int CountEntries(string text) =>
        text.Split(Environment.NewLine).Count(line => line.StartsWith("====="));

    [Fact]
    public void Format_includes_timestamp_version_source_type_and_message()
    {
        var now = new DateTimeOffset(2026, 6, 25, 14, 32, 1, TimeSpan.FromHours(2));
        var text = LogService.Format(now, new InvalidOperationException("boom"),
            "DispatcherUnhandledException", new Version(1, 4, 0));

        Assert.Contains("2026-06-25 14:32:01", text);
        Assert.Contains("+02:00", text);
        Assert.Contains("v1.4.0", text);
        Assert.Contains("DispatcherUnhandledException", text);
        Assert.Contains("System.InvalidOperationException", text);
        Assert.Contains("boom", text);
    }

    [Fact]
    public void Log_appends_an_entry_to_the_file()
    {
        new LogService(_path, _clock).Log(new InvalidOperationException("boom"), "Test");

        var text = File.ReadAllText(_path);
        Assert.Contains("System.InvalidOperationException", text);
        Assert.Contains("boom", text);
    }

    [Fact]
    public void Log_two_distinct_errors_writes_two_entries()
    {
        var svc = new LogService(_path, _clock);
        svc.Log(new InvalidOperationException("first"), "Test");
        svc.Log(new InvalidOperationException("second"), "Test");

        Assert.Equal(2, CountEntries(File.ReadAllText(_path)));
    }

    [Fact]
    public void Log_suppresses_an_identical_error_within_the_window()
    {
        var svc = new LogService(_path, _clock);

        Assert.True(svc.Log(new InvalidOperationException("boom"), "Test"));
        Assert.False(svc.Log(new InvalidOperationException("boom"), "Test"));   // same signature, same instant
        Assert.Equal(1, CountEntries(File.ReadAllText(_path)));
    }

    [Fact]
    public void Log_writes_again_after_the_window_elapses()
    {
        var svc = new LogService(_path, _clock);

        Assert.True(svc.Log(new InvalidOperationException("boom"), "Test"));
        _clock.Advance(TimeSpan.FromSeconds(6));
        Assert.True(svc.Log(new InvalidOperationException("boom"), "Test"));
        Assert.Equal(2, CountEntries(File.ReadAllText(_path)));
    }

    [Fact]
    public void Log_writes_a_different_signature_within_the_window()
    {
        var svc = new LogService(_path, _clock);

        Assert.True(svc.Log(new InvalidOperationException("boom"), "Test"));
        Assert.True(svc.Log(new InvalidOperationException("boom"), "Other"));   // different source -> different signature
        Assert.Equal(2, CountEntries(File.ReadAllText(_path)));
    }

    [Fact]
    public void Log_does_not_throw_on_an_unwritable_path()
    {
        var fileInTheWay = Path.Combine(_dir, "blocker");
        File.WriteAllText(fileInTheWay, "x");                       // a file where a directory is needed
        var badPath = Path.Combine(fileInTheWay, "nested", "tidsro.log");

        var thrown = Record.Exception(() => new LogService(badPath, _clock)
            .Log(new InvalidOperationException("boom"), "Test"));
        Assert.Null(thrown);
    }

    [Fact]
    public void Log_rolls_the_file_over_to_old_past_the_cap()
    {
        File.WriteAllText(_path, new string('x', 600 * 1024));      // > 512 KB
        new LogService(_path, _clock).Log(new InvalidOperationException("boom"), "Test");

        Assert.True(File.Exists(_path + ".old"));
        Assert.True(new FileInfo(_path + ".old").Length > 512 * 1024);   // the archive holds the prior log, not the new entry
        Assert.True(new FileInfo(_path).Length < 512 * 1024);      // live file is fresh and small
    }
}
