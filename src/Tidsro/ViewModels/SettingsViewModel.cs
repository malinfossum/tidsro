using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Tidsro.Models;
using Tidsro.Services;

namespace Tidsro.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly StartupService _startup;
    private readonly PersistenceService _persistence;
    private readonly Action<SoundChoice> _onDefaultSoundChanged;
    private readonly AppSettings _settings;   // the in-memory snapshot App reuses to open this window; keep it current

    [ObservableProperty] private bool _launchAtStartup;
    [ObservableProperty] private SoundChoice _defaultSound;

    public SoundChoice[] SoundOptions { get; } =
        { SoundChoice.None, SoundChoice.SoftChime, SoundChoice.Marimba, SoundChoice.Bell };

    public SettingsViewModel(AppSettings settings, StartupService startup,
        PersistenceService persistence, Action<SoundChoice> onDefaultSoundChanged)
    {
        _settings = settings;
        _startup = startup; _persistence = persistence; _onDefaultSoundChanged = onDefaultSoundChanged;
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
        try { _persistence.Save(_settings); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* settings are non-critical */ }
    }
}
