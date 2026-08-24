using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidsro.Models;
using Tidsro.Services;
using Tidsro.Views;

namespace Tidsro.ViewModels;

/// <summary>Everything the two data-transfer commands need, bundled so the constructor does not grow
/// a tenth, eleventh and twelfth positional callback.</summary>
/// <param name="BuildData">The live state to export — not a copy of data.json, so an export still
/// captures good data when saves have been failing.</param>
/// <param name="ApplyImport">(document, includeSettings) — replaces the schedule, and the settings
/// too when the user chose a full restore.</param>
public sealed record DataPorts(
    IFileDialogService Dialogs,
    DataTransferService Transfer,
    Func<TidsroData> BuildData,
    Func<string, ImportChoice> AskImportChoice,
    Action<TidsroData, bool> ApplyImport,
    Action<string, string> ShowMessage,
    Func<DateTime> Today);

public partial class SettingsViewModel : ObservableObject
{
    private readonly IStartupService _startup;      // interface came from Task 2
    private readonly Action _save;                              // bundles settings + alarms at the App level
    private readonly Action<SoundChoice> _onDefaultSoundChanged;
    private readonly AppSettings _settings;   // the in-memory snapshot App reuses to open this window; keep it current
    private readonly Action _clearAllAlarms;
    private readonly Func<int> _alarmCount;
    private readonly Func<bool> _hasAnythingToClear;
    private readonly Action _resetWindowPlacement;
    private readonly Func<string, string, bool> _confirm;   // (title, message) -> confirmed
    private readonly DataPorts? _data;   // null only in older view-model tests; App always supplies it

    [ObservableProperty] private bool _launchAtStartup;
    [ObservableProperty] private SoundChoice _defaultSound;

    public SoundChoice[] SoundOptions { get; } =
        { SoundChoice.None, SoundChoice.SoftChime, SoundChoice.Marimba, SoundChoice.Bell,
          SoundChoice.PianoJingle, SoundChoice.ElectricPianoJingle, SoundChoice.BellJingle };

    public SettingsViewModel(AppSettings settings, IStartupService startup,
        Action save, Action<SoundChoice> onDefaultSoundChanged,
        Action clearAllAlarms, Func<int> alarmCount, Func<bool> hasAnythingToClear,
        Action resetWindowPlacement, Func<string, string, bool> confirm,
        DataPorts? dataPorts = null)
    {
        _settings = settings;
        _startup = startup; _save = save; _onDefaultSoundChanged = onDefaultSoundChanged;
        _clearAllAlarms = clearAllAlarms; _alarmCount = alarmCount; _hasAnythingToClear = hasAnythingToClear;
        _resetWindowPlacement = resetWindowPlacement; _confirm = confirm; _data = dataPorts;
        _launchAtStartup = settings.LaunchAtStartup;
        _defaultSound = settings.DefaultSound;
    }

    // Apply the draft to the shared snapshot and disk. Called by the Save button; closing without it
    // discards the draft (App rebuilds this VM from the shared snapshot each time Settings opens).
    // Startup is the only change with external reach, so only touch the HKCU Run key when it actually
    // changed. Persisting is best-effort: a locked/unwritable file must never crash Save.
    public void Save()
    {
        if (LaunchAtStartup != _settings.LaunchAtStartup)
        {
            if (LaunchAtStartup) _startup.Enable(); else _startup.Disable();
        }

        _onDefaultSoundChanged(DefaultSound);

        _settings.LaunchAtStartup = LaunchAtStartup;
        _settings.DefaultSound = DefaultSound;
        _save();   // App's SaveData handles IO errors; settings remain non-critical
    }

    // All four of these act at once and are outside the Save/Cancel draft — Cancel does not undo them,
    // which is why the view keeps them in their own separated section.

    [RelayCommand]
    private void ExportData()
    {
        if (_data is null) return;

        var path = _data.Dialogs.AskSavePath($"tidsro-backup-{_data.Today():yyyy-MM-dd}.json");
        if (path is null) return;                    // cancelled — nothing happens

        var data = _data.BuildData();
        try
        {
            _data.Transfer.Export(path, data);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Never silent: a failed export leaves the user believing they have a backup they do not.
            _data.ShowMessage("Export failed", "Tidsro couldn't write that file. "
                                             + "Try another folder, and check the drive is still connected.");
            return;
        }

        var count = data.Alarms.Count + data.RecurringAlarms.Count;
        _data.ShowMessage("Exported", $"Exported {Plural(count)} to {Path.GetFileName(path)}.");
    }

    [RelayCommand]
    private void ImportData()
    {
        if (_data is null) return;

        var path = _data.Dialogs.AskOpenPath();
        if (path is null) return;                    // cancelled

        var imported = _data.Transfer.Read(path);    // size, shape and sanitise gates
        if (imported is null)
        {
            _data.ShowMessage("Import failed", "That doesn't look like a Tidsro backup. "
                                             + "Pick a file Tidsro exported, or your data.json.");
            return;
        }

        var recurring = imported.RecurringAlarms.Count;
        var choice = _data.AskImportChoice(
            $"This file holds {Plural(imported.Alarms.Count)} and "
          + $"{recurring} recurring {(recurring == 1 ? "alarm" : "alarms")}. "
          + "Your current data is copied to data-before-import.json first.");
        if (choice == ImportChoice.Cancel) return;   // no snapshot: nothing is being replaced

        _data.Transfer.SnapshotBeforeImport();
        _data.ApplyImport(imported, choice == ImportChoice.Everything);

        if (choice == ImportChoice.Everything)
        {
            // Refresh the draft, or a following Save writes the pre-import values straight back.
            LaunchAtStartup = _settings.LaunchAtStartup;
            DefaultSound = _settings.DefaultSound;
        }
    }

    private static string Plural(int count) => count == 1 ? "1 alarm" : $"{count} alarms";

    [RelayCommand]
    private void ClearAlarms()
    {
        // alarmCount excludes a leftover missed note (no armed alarm behind it), so the early return
        // is gated on hasAnythingToClear instead — otherwise the button silently does nothing while
        // a missed note is still on screen.
        if (!_hasAnythingToClear()) return;

        var count = _alarmCount();
        var (title, message) = count switch
        {
            0 => ("Clear missed note?", "Clear the missed alarm note? This cannot be undone."),
            1 => ("Delete alarms?", "Delete this alarm? This cannot be undone."),
            _ => ("Delete alarms?", $"Delete all {count} alarms? This cannot be undone."),
        };
        if (!_confirm(title, message)) return;

        _clearAllAlarms();                          // raises AlarmsChanged, which persists via App
    }

    [RelayCommand]
    private void ResetSettings()
    {
        if (!_confirm("Reset settings?", "Reset all settings? Launch at startup will be turned off. "
                                       + "Your alarms and the diagnostic log are kept.")) return;

        var defaults = AppSettings.Defaults();
        _startup.Disable();                         // never leave the Run key behind a checkbox that reads off

        _settings.LaunchAtStartup = defaults.LaunchAtStartup;
        _settings.DefaultSound = defaults.DefaultSound;
        _settings.WindowLeft = null;
        _settings.WindowTop = null;
        _settings.WindowWidth = null;
        _settings.WindowHeight = null;
        _settings.SelectedTab = defaults.SelectedTab;

        // Refresh the draft, or a following Save writes the pre-reset values straight back.
        LaunchAtStartup = defaults.LaunchAtStartup;
        DefaultSound = defaults.DefaultSound;
        _onDefaultSoundChanged(defaults.DefaultSound);

        _resetWindowPlacement();                    // main window returns to 440x600 centred, so its
                                                    // OnClosing can't re-save the old coordinates
        _save();
    }
}
