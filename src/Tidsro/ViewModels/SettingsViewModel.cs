using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidsro.Models;
using Tidsro.Services;

namespace Tidsro.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IStartupService _startup;      // interface came from Task 2
    private readonly Action _save;                              // bundles settings + alarms at the App level
    private readonly Action<SoundChoice> _onDefaultSoundChanged;
    private readonly AppSettings _settings;   // the in-memory snapshot App reuses to open this window; keep it current
    private readonly Action _clearAllAlarms;
    private readonly Func<int> _alarmCount;
    private readonly Action _resetWindowPlacement;
    private readonly Func<string, string, bool> _confirm;   // (title, message) -> confirmed

    [ObservableProperty] private bool _launchAtStartup;
    [ObservableProperty] private SoundChoice _defaultSound;

    public SoundChoice[] SoundOptions { get; } =
        { SoundChoice.None, SoundChoice.SoftChime, SoundChoice.Marimba, SoundChoice.Bell,
          SoundChoice.PianoJingle, SoundChoice.ElectricPianoJingle, SoundChoice.BellJingle };

    public SettingsViewModel(AppSettings settings, IStartupService startup,
        Action save, Action<SoundChoice> onDefaultSoundChanged,
        Action clearAllAlarms, Func<int> alarmCount,
        Action resetWindowPlacement, Func<string, string, bool> confirm)
    {
        _settings = settings;
        _startup = startup; _save = save; _onDefaultSoundChanged = onDefaultSoundChanged;
        _clearAllAlarms = clearAllAlarms; _alarmCount = alarmCount;
        _resetWindowPlacement = resetWindowPlacement; _confirm = confirm;
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

    // Both of these act at once and are outside the Save/Cancel draft — Cancel does not undo them,
    // which is why the view keeps them in their own separated section.

    [RelayCommand]
    private void ClearAlarms()
    {
        var count = _alarmCount();
        if (count == 0) return;                     // nothing to lose: don't ask a pointless question
        if (!_confirm("Delete alarms?", $"Delete all {count} alarms? This cannot be undone.")) return;

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

        // Refresh the draft, or a following Save writes the pre-reset values straight back.
        LaunchAtStartup = defaults.LaunchAtStartup;
        DefaultSound = defaults.DefaultSound;
        _onDefaultSoundChanged(defaults.DefaultSound);

        _resetWindowPlacement();                    // main window returns to 440x600 centred, so its
                                                    // OnClosing can't re-save the old coordinates
        _save();
    }
}
