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
        var startup = new FakeStartupService();
        var saves = 0;
        var vm = new SettingsViewModel(shared, startup, save: () => saves++, _ => { },
            clearAllAlarms: () => { }, alarmCount: () => 0, hasAnythingToClear: () => true,
            resetWindowPlacement: () => { }, confirm: (_, _) => true);

        vm.DefaultSound = SoundChoice.Bell;   // edit the draft only

        Assert.Equal(SoundChoice.None, shared.DefaultSound);   // shared snapshot untouched
        Assert.Equal(0, saves);                                // nothing persisted yet
    }

    [Fact]
    public void Save_applies_changes_to_the_shared_AppSettings_and_persists()
    {
        var shared = new AppSettings { LaunchAtStartup = false, DefaultSound = SoundChoice.None };
        var startup = new FakeStartupService();
        var saves = 0;
        var vm = new SettingsViewModel(shared, startup, save: () => saves++, _ => { },
            clearAllAlarms: () => { }, alarmCount: () => 0, hasAnythingToClear: () => true,
            resetWindowPlacement: () => { }, confirm: (_, _) => true);

        vm.DefaultSound = SoundChoice.Bell;
        vm.Save();

        Assert.Equal(SoundChoice.Bell, shared.DefaultSound);   // reused snapshot reflects the change
        Assert.Equal(1, saves);                                // persisted exactly once via the injected action
    }

    [Fact]
    public void Clearing_alarms_asks_first_and_names_the_count()
    {
        var shared = new AppSettings { LaunchAtStartup = true, DefaultSound = SoundChoice.Bell };
        string? title = null;
        string? message = null;
        var cleared = 0;
        var vm = new SettingsViewModel(shared, new FakeStartupService(),
            () => { }, _ => { }, () => cleared++, () => 6, () => true, () => { },
            (t, m) => { title = t; message = m; return true; });

        vm.ClearAlarmsCommand.Execute(null);

        Assert.Equal("Delete alarms?", title);
        Assert.Equal("Delete all 6 alarms? This cannot be undone.", message);
        Assert.Equal(1, cleared);
        Assert.Equal(SoundChoice.Bell, shared.DefaultSound);   // clearing alarms leaves preferences untouched
        Assert.True(shared.LaunchAtStartup);
    }

    [Fact]
    public void Clearing_a_single_alarm_uses_singular_wording()
    {
        var shared = new AppSettings();
        string? message = null;
        var vm = new SettingsViewModel(shared, new FakeStartupService(),
            () => { }, _ => { }, () => { }, () => 1, () => true, () => { },
            (_, m) => { message = m; return true; });

        vm.ClearAlarmsCommand.Execute(null);

        Assert.Equal("Delete this alarm? This cannot be undone.", message);
    }

    [Fact]
    public void Clearing_with_only_a_missed_note_still_asks_and_clears()
    {
        // No armed alarms (alarmCount is 0), but a missed note remains — HasAnythingToClear says yes.
        var shared = new AppSettings();
        var asked = false;
        var cleared = 0;
        var vm = new SettingsViewModel(shared, new FakeStartupService(),
            () => { }, _ => { }, () => cleared++, () => 0, hasAnythingToClear: () => true,
            resetWindowPlacement: () => { }, confirm: (_, _) => { asked = true; return true; });

        vm.ClearAlarmsCommand.Execute(null);

        Assert.True(asked);
        Assert.Equal(1, cleared);
    }

    [Fact]
    public void The_reset_confirm_says_what_is_kept()
    {
        var shared = new AppSettings();
        string? message = null;
        var vm = new SettingsViewModel(shared, new FakeStartupService(),
            () => { }, _ => { }, () => { }, () => 6, () => true, () => { },
            (_, m) => { message = m; return false; });

        vm.ResetSettingsCommand.Execute(null);

        Assert.Equal("Reset all settings? Launch at startup will be turned off. "
                   + "Your alarms and the diagnostic log are kept.", message);
    }

    [Fact]
    public void Declining_the_confirm_clears_nothing()
    {
        var shared = new AppSettings();
        var cleared = 0;
        var vm = new SettingsViewModel(shared, new FakeStartupService(),
            () => { }, _ => { }, () => cleared++, () => 6, () => true, () => { }, (_, _) => false);

        vm.ClearAlarmsCommand.Execute(null);

        Assert.Equal(0, cleared);
    }

    [Fact]
    public void Clearing_with_nothing_to_clear_does_not_even_ask()
    {
        var shared = new AppSettings();
        var asked = false;
        var cleared = 0;
        var vm = new SettingsViewModel(shared, new FakeStartupService(),
            () => { }, _ => { }, () => cleared++, () => 0, hasAnythingToClear: () => false,
            resetWindowPlacement: () => { }, confirm: (_, _) => { asked = true; return true; });

        vm.ClearAlarmsCommand.Execute(null);

        Assert.False(asked);
        Assert.Equal(0, cleared);
    }

    [Fact]
    public void Resetting_restores_every_default_and_refreshes_the_draft()
    {
        var shared = new AppSettings
        {
            LaunchAtStartup = true, DefaultSound = SoundChoice.Bell,
            WindowLeft = 100, WindowTop = 200, WindowWidth = 900, WindowHeight = 900,
        };
        var startup = new FakeStartupService { Enabled = true };
        var placementResets = 0; var saves = 0; var cleared = 0;
        var vm = new SettingsViewModel(shared, startup,
            () => saves++, _ => { }, () => cleared++, () => 6, () => true,
            () => placementResets++, (_, _) => true);

        vm.ResetSettingsCommand.Execute(null);

        Assert.Equal(1, startup.DisableCalls);   // the Run key must go with the checkbox
        Assert.False(shared.LaunchAtStartup);
        Assert.Equal(SoundChoice.None, shared.DefaultSound);
        Assert.Null(shared.WindowLeft);
        Assert.Null(shared.WindowTop);
        Assert.Null(shared.WindowWidth);
        Assert.Null(shared.WindowHeight);
        Assert.False(vm.LaunchAtStartup);                 // draft refreshed...
        Assert.Equal(SoundChoice.None, vm.DefaultSound);  // ...so a later Save can't rewrite the old values
        Assert.Equal(1, placementResets);
        Assert.Equal(1, saves);
        Assert.Equal(0, cleared);                         // resetting settings leaves alarms untouched
    }

    [Fact]
    public void Saving_after_a_reset_keeps_the_defaults()
    {
        var shared = new AppSettings { LaunchAtStartup = true, DefaultSound = SoundChoice.Bell };
        var vm = new SettingsViewModel(shared, new FakeStartupService(),
            () => { }, _ => { }, () => { }, () => 6, () => true, () => { }, (_, _) => true);

        vm.ResetSettingsCommand.Execute(null);
        vm.Save();

        Assert.Equal(SoundChoice.None, shared.DefaultSound);
        Assert.False(shared.LaunchAtStartup);
    }

    [Fact]
    public void Declining_the_reset_changes_nothing()
    {
        var shared = new AppSettings { LaunchAtStartup = true, DefaultSound = SoundChoice.Bell };
        var vm = new SettingsViewModel(shared, new FakeStartupService(),
            () => { }, _ => { }, () => { }, () => 6, () => true, () => { }, (_, _) => false);

        vm.ResetSettingsCommand.Execute(null);

        Assert.True(shared.LaunchAtStartup);
        Assert.Equal(SoundChoice.Bell, shared.DefaultSound);
    }
}
