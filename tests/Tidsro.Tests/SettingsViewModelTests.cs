using System.IO;
using Tidsro.Views;
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
        string? title = null;
        string? message = null;
        var vm = new SettingsViewModel(shared, new FakeStartupService(),
            () => { }, _ => { }, () => cleared++, () => 0, hasAnythingToClear: () => true,
            resetWindowPlacement: () => { }, confirm: (t, m) => { asked = true; title = t; message = m; return true; });

        vm.ClearAlarmsCommand.Execute(null);

        Assert.True(asked);
        Assert.Equal("Clear missed note?", title);
        Assert.Equal("Clear the missed alarm note? This cannot be undone.", message);
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

    [Fact]
    public void Reset_returns_the_selected_tab_to_quick_timers()
    {
        var shared = new AppSettings { SelectedTab = 1 };
        var vm = new SettingsViewModel(shared, new FakeStartupService(),
            save: () => { }, _ => { }, clearAllAlarms: () => { }, alarmCount: () => 0,
            hasAnythingToClear: () => true, resetWindowPlacement: () => { }, confirm: (_, _) => true);

        vm.ResetSettingsCommand.Execute(null);

        Assert.Equal(0, shared.SelectedTab);
    }

    [Fact]
    public void A_declined_reset_leaves_the_selected_tab_alone()
    {
        var shared = new AppSettings { SelectedTab = 1 };
        var vm = new SettingsViewModel(shared, new FakeStartupService(),
            save: () => { }, _ => { }, clearAllAlarms: () => { }, alarmCount: () => 0,
            hasAnythingToClear: () => true, resetWindowPlacement: () => { }, confirm: (_, _) => false);

        vm.ResetSettingsCommand.Execute(null);

        Assert.Equal(1, shared.SelectedTab);
    }

    // --- Export / import -------------------------------------------------------------------------

    private const string ValidBackup =
        """{"SchemaVersion":4,"Settings":{},"Alarms":[],"RecurringAlarms":[]}""";

    private sealed class Harness : IDisposable
    {
        public string Dir { get; }
        public FakeFileDialogService Dialogs { get; } = new();
        public List<(string Title, string Message)> Messages { get; } = new();
        public List<(TidsroData Data, bool IncludeSettings)> Applied { get; } = new();
        public List<string> Asked { get; } = new();
        public DataTransferService Transfer { get; }
        public SettingsViewModel Vm { get; }

        public Harness(ImportChoice choice, AppSettings? shared = null, IStartupService? startup = null)
        {
            Dir = Path.Combine(Path.GetTempPath(), "TidsroTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Dir);
            Transfer = new DataTransferService(Path.Combine(Dir, "data.json"));

            var ports = new DataPorts(
                Dialogs: Dialogs,
                Transfer: Transfer,
                BuildData: TidsroData.Defaults,
                AskImportChoice: m => { Asked.Add(m); return choice; },
                ApplyImport: (d, includeSettings) => Applied.Add((d, includeSettings)),
                ShowMessage: (t, m) => Messages.Add((t, m)),
                Today: () => new DateTime(2026, 8, 24));

            Vm = new SettingsViewModel(shared ?? new AppSettings(), startup ?? new FakeStartupService(),
                save: () => { }, _ => { }, clearAllAlarms: () => { }, alarmCount: () => 0,
                hasAnythingToClear: () => true, resetWindowPlacement: () => { },
                confirm: (_, _) => true, dataPorts: ports);
        }

        public string WriteFile(string name, string contents)
        {
            var p = Path.Combine(Dir, name);
            File.WriteAllText(p, contents);
            return p;
        }

        public void Dispose() { try { Directory.Delete(Dir, true); } catch { } }
    }

    [Fact]
    public void Export_with_a_cancelled_dialog_writes_nothing()
    {
        using var h = new Harness(ImportChoice.Cancel);
        h.Dialogs.SavePath = null;   // user cancelled

        h.Vm.ExportDataCommand.Execute(null);

        Assert.Empty(h.Messages);    // no success message, no error
        Assert.Empty(Directory.GetFiles(h.Dir, "*.json"));
    }

    [Fact]
    public void Export_suggests_a_dated_file_name_and_reports_success()
    {
        using var h = new Harness(ImportChoice.Cancel);
        h.Dialogs.SavePath = Path.Combine(h.Dir, "backup.json");

        h.Vm.ExportDataCommand.Execute(null);

        Assert.Equal("tidsro-backup-2026-08-24.json", h.Dialogs.LastSuggestedName);
        Assert.True(File.Exists(h.Dialogs.SavePath));
        var (title, message) = Assert.Single(h.Messages);
        Assert.Equal("Exported", title);
        Assert.Contains("backup.json", message);
    }

    [Fact]
    public void Export_reports_a_failure_instead_of_failing_silently()
    {
        using var h = new Harness(ImportChoice.Cancel);
        var target = Path.Combine(h.Dir, "taken.json");
        Directory.CreateDirectory(target);   // a directory in the file's place: the write throws
        h.Dialogs.SavePath = target;

        h.Vm.ExportDataCommand.Execute(null);

        var (title, _) = Assert.Single(h.Messages);
        Assert.Equal("Export failed", title);
    }

    [Fact]
    public void Import_with_a_cancelled_open_dialog_changes_nothing()
    {
        using var h = new Harness(ImportChoice.AlarmsOnly);
        h.Dialogs.OpenPath = null;

        h.Vm.ImportDataCommand.Execute(null);

        Assert.Empty(h.Applied);
        Assert.Empty(h.Messages);
    }

    [Fact]
    public void Import_of_a_valid_json_file_that_is_not_a_Tidsro_document_is_refused()
    {
        using var h = new Harness(ImportChoice.Everything);
        h.Dialogs.OpenPath = h.WriteFile("package.json", """{"name":"something"}""");

        h.Vm.ImportDataCommand.Execute(null);

        Assert.Empty(h.Applied);                              // the data-loss guard
        Assert.Empty(h.Asked);                                // never even asked the question
        Assert.False(File.Exists(h.Transfer.SnapshotPath));
        var (title, _) = Assert.Single(h.Messages);
        Assert.Equal("Import failed", title);
    }

    [Fact]
    public void Import_cancelled_at_the_choice_dialog_takes_no_snapshot_and_changes_nothing()
    {
        using var h = new Harness(ImportChoice.Cancel);
        File.WriteAllText(Path.Combine(h.Dir, "data.json"), ValidBackup);
        h.Dialogs.OpenPath = h.WriteFile("good.json", ValidBackup);

        h.Vm.ImportDataCommand.Execute(null);

        Assert.Empty(h.Applied);
        Assert.Empty(h.Messages);
        Assert.False(File.Exists(h.Transfer.SnapshotPath));   // the snapshot comes after the question
    }

    [Fact]
    public void Import_alarms_only_applies_without_settings_and_snapshots_first()
    {
        using var h = new Harness(ImportChoice.AlarmsOnly);
        File.WriteAllText(Path.Combine(h.Dir, "data.json"), ValidBackup);
        h.Dialogs.OpenPath = h.WriteFile("good.json", ValidBackup);

        h.Vm.ImportDataCommand.Execute(null);

        var (_, includeSettings) = Assert.Single(h.Applied);
        Assert.False(includeSettings);
        Assert.True(File.Exists(h.Transfer.SnapshotPath));
    }

    [Fact]
    public void Import_everything_applies_with_settings_and_refreshes_the_draft()
    {
        var shared = new AppSettings { LaunchAtStartup = false, DefaultSound = SoundChoice.None };
        using var h = new Harness(ImportChoice.Everything, shared);
        h.Dialogs.OpenPath = h.WriteFile("good.json", ValidBackup);
        // ApplyImport is App's job; stand in for it so the draft-refresh step has something to read.
        shared.LaunchAtStartup = true;
        shared.DefaultSound = SoundChoice.Bell;

        h.Vm.ImportDataCommand.Execute(null);

        var (_, includeSettings) = Assert.Single(h.Applied);
        Assert.True(includeSettings);
        Assert.True(h.Vm.LaunchAtStartup);                     // draft follows the restored settings,
        Assert.Equal(SoundChoice.Bell, h.Vm.DefaultSound);     // or a later Save would undo the import
    }

    [Fact]
    public void Import_names_the_counts_and_the_recovery_file_in_the_question()
    {
        using var h = new Harness(ImportChoice.Cancel);
        h.Dialogs.OpenPath = h.WriteFile("good.json", """
            {"SchemaVersion":4,"Settings":{},"Alarms":[],
             "RecurringAlarms":[{"Id":"11111111-1111-1111-1111-111111111111","Hour":8,"Minute":0,
                                 "Days":1,"Sound":0,"NextFireAt":"2026-09-01T08:00:00"}]}
            """);

        h.Vm.ImportDataCommand.Execute(null);

        var asked = Assert.Single(h.Asked);
        Assert.Contains("0 alarms", asked);
        Assert.Contains("1 recurring alarm.", asked);
        Assert.Contains("data-before-import.json", asked);
    }

    [Fact]
    public void The_data_commands_no_op_when_no_ports_were_supplied()
    {
        var vm = new SettingsViewModel(new AppSettings(), new FakeStartupService(), () => { }, _ => { },
            () => { }, () => 0, () => true, () => { }, (_, _) => true);

        vm.ExportDataCommand.Execute(null);   // must not throw
        vm.ImportDataCommand.Execute(null);
    }
}
