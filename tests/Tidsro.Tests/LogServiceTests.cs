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
        Assert.Contains("v1.4.0", text);
        Assert.Contains("DispatcherUnhandledException", text);
        Assert.Contains("System.InvalidOperationException", text);
        Assert.Contains("boom", text);
    }
}
