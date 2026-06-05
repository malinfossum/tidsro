using System.IO;
using Tidsro.Models;
using Tidsro.Services;
using Tidsro.ViewModels;
using Xunit;

namespace Tidsro.Tests;

public class SettingsViewModelTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public SettingsViewModelTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "TidsroTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "data.json");
    }

    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    // Repro for the "settings revert / won't save" bug: App reuses ONE AppSettings instance to
    // build the Settings window each time it opens. Changing a setting must update THAT shared
    // object, or a reopened window shows the stale startup value while disk has already moved on.
    // (Exercises the default-sound path only, so it never writes to the real HKCU Run key.)
    [Fact]
    public void Changing_a_setting_updates_the_shared_AppSettings_and_persists()
    {
        var shared = new AppSettings { LaunchAtStartup = false, DefaultSound = SoundChoice.None };
        var persistence = new PersistenceService(_path);
        var startup = new StartupService("Tidsro.exe");   // not exercised here
        var vm = new SettingsViewModel(shared, startup, persistence, _ => { });

        vm.DefaultSound = SoundChoice.Bell;

        Assert.Equal(SoundChoice.Bell, shared.DefaultSound);              // the reused snapshot must reflect the change
        Assert.Equal(SoundChoice.Bell, persistence.Load().DefaultSound);  // and it must be on disk
    }
}
