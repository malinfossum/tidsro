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

    // Apply-on-Save contract: editing a field only updates the in-memory draft. Until Save() is
    // called, the shared AppSettings snapshot and the file on disk must be untouched, so closing
    // the window without saving discards the edit.
    // (Exercises the default-sound path only, so it never writes to the real HKCU Run key.)
    [Fact]
    public void Editing_a_setting_does_not_apply_until_Save()
    {
        var shared = new AppSettings { LaunchAtStartup = false, DefaultSound = SoundChoice.None };
        var persistence = new PersistenceService(_path);
        var startup = new StartupService("Tidsro.exe");   // not exercised here
        var vm = new SettingsViewModel(shared, startup, persistence, _ => { });

        vm.DefaultSound = SoundChoice.Bell;   // edit the draft only

        Assert.Equal(SoundChoice.None, shared.DefaultSound);   // shared snapshot untouched
        Assert.False(File.Exists(_path));                      // nothing written to disk
    }

    // Save() is the apply step: it must update the shared AppSettings snapshot (so a reopened window
    // isn't stale — the bug fixed in Slice 1) and write the change to disk.
    // (Default-sound path only; LaunchAtStartup is unchanged, so the real HKCU Run key is never touched.)
    [Fact]
    public void Save_applies_changes_to_the_shared_AppSettings_and_persists()
    {
        var shared = new AppSettings { LaunchAtStartup = false, DefaultSound = SoundChoice.None };
        var persistence = new PersistenceService(_path);
        var startup = new StartupService("Tidsro.exe");   // not exercised: LaunchAtStartup unchanged
        var vm = new SettingsViewModel(shared, startup, persistence, _ => { });

        vm.DefaultSound = SoundChoice.Bell;
        vm.Save();

        Assert.Equal(SoundChoice.Bell, shared.DefaultSound);              // reused snapshot reflects the change
        Assert.Equal(SoundChoice.Bell, persistence.Load().DefaultSound);  // and it's on disk
    }
}
