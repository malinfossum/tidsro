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

    partial void OnLaunchAtStartupChanged(bool value)
    {
        if (value) _startup.Enable(); else _startup.Disable();
        Persist();
    }

    partial void OnDefaultSoundChanged(SoundChoice value)
    {
        _onDefaultSoundChanged(value);
        Persist();
    }

    // Keep the shared in-memory snapshot current so reopening Settings shows live state (not the value
    // loaded at launch), then write that same object to disk. Best-effort: a locked/unwritable file must
    // never crash a toggle (symmetric with PersistenceService.Load).
    private void Persist()
    {
        _settings.LaunchAtStartup = LaunchAtStartup;
        _settings.DefaultSound = DefaultSound;
        try { _persistence.Save(_settings); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* settings are non-critical */ }
    }
}
