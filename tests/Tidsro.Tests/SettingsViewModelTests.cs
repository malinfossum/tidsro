using Tidsro.Models;
using Tidsro.Services;
using Tidsro.ViewModels;
using Xunit;

namespace Tidsro.Tests;

public class SettingsViewModelTests
{
    [Fact]
    public void Editing_a_setting_does_not_apply_until_Save()
    {
        var shared = new AppSettings { LaunchAtStartup = false, DefaultSound = SoundChoice.None };
        var startup = new StartupService("Tidsro.exe");   // not exercised here
        var saves = 0;
        var vm = new SettingsViewModel(shared, startup, save: () => saves++, _ => { });

        vm.DefaultSound = SoundChoice.Bell;   // edit the draft only

        Assert.Equal(SoundChoice.None, shared.DefaultSound);   // shared snapshot untouched
        Assert.Equal(0, saves);                                // nothing persisted yet
    }

    [Fact]
    public void Save_applies_changes_to_the_shared_AppSettings_and_persists()
    {
        var shared = new AppSettings { LaunchAtStartup = false, DefaultSound = SoundChoice.None };
        var startup = new StartupService("Tidsro.exe");   // not exercised: LaunchAtStartup unchanged
        var saves = 0;
        var vm = new SettingsViewModel(shared, startup, save: () => saves++, _ => { });

        vm.DefaultSound = SoundChoice.Bell;
        vm.Save();

        Assert.Equal(SoundChoice.Bell, shared.DefaultSound);   // reused snapshot reflects the change
        Assert.Equal(1, saves);                                // persisted exactly once via the injected action
    }
}
