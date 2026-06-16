using System.IO;
using Tidsro.Models;
using Tidsro.Services;
using Xunit;

namespace Tidsro.Tests;

public class PersistenceServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;
    public PersistenceServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "TidsroTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "data.json");
    }
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    [Fact]
    public void Load_missing_file_returns_defaults()
    {
        var svc = new PersistenceService(_path);
        var s = svc.Load();
        Assert.False(s.LaunchAtStartup);
        Assert.Equal(SoundChoice.None, s.DefaultSound);
    }

    [Fact]
    public void Save_then_Load_round_trips()
    {
        var svc = new PersistenceService(_path);
        svc.Save(new AppSettings { LaunchAtStartup = true, DefaultSound = SoundChoice.Bell });
        var s = svc.Load();
        Assert.True(s.LaunchAtStartup);
        Assert.Equal(SoundChoice.Bell, s.DefaultSound);
    }

    [Fact]
    public void Save_is_atomic_and_leaves_no_temp_file()
    {
        var svc = new PersistenceService(_path);
        svc.Save(new AppSettings());
        Assert.True(File.Exists(_path));
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public void Load_corrupt_file_quarantines_and_returns_defaults()
    {
        File.WriteAllText(_path, "{ this is not valid json ");
        var svc = new PersistenceService(_path);
        var s = svc.Load();
        Assert.Equal(SoundChoice.None, s.DefaultSound);          // defaults
        Assert.True(File.Exists(_path + ".corrupt"));            // quarantined, app still launches
    }

    [Fact]
    public void Load_unknown_enum_value_falls_back_to_none()
    {
        File.WriteAllText(_path, "{\"SchemaVersion\":1,\"LaunchAtStartup\":false,\"DefaultSound\":999}");
        var svc = new PersistenceService(_path);
        Assert.Equal(SoundChoice.None, svc.Load().DefaultSound);  // untrusted-input hardening
    }

    [Fact]
    public void Save_then_Load_round_trips_window_position()
    {
        var svc = new PersistenceService(_path);
        svc.Save(new AppSettings { WindowLeft = 123.5, WindowTop = 45.0 });
        var s = svc.Load();
        Assert.Equal(123.5, s.WindowLeft);
        Assert.Equal(45.0, s.WindowTop);
    }

    [Fact]
    public void Sanitized_nulls_non_finite_window_position()
    {
        var s = new AppSettings { WindowLeft = double.NaN, WindowTop = double.PositiveInfinity }.Sanitized();
        Assert.Null(s.WindowLeft);
        Assert.Null(s.WindowTop);
    }
}
