using CommunityToolkit.Mvvm.ComponentModel;
using Tidsro.Models;
using Tidsro.Services;

namespace Tidsro.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly StartupService _startup;
    private readonly PersistenceService _persistence;
    private readonly Action<SoundChoice> _onDefaultSoundChanged;

    [ObservableProperty] private bool _launchAtStartup;
    [ObservableProperty] private SoundChoice _defaultSound;

    public SoundChoice[] SoundOptions { get; } =
        { SoundChoice.None, SoundChoice.SoftChime, SoundChoice.Marimba, SoundChoice.Bell };

    public SettingsViewModel(AppSettings settings, StartupService startup,
        PersistenceService persistence, Action<SoundChoice> onDefaultSoundChanged)
    {
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

    private void Persist() =>
        _persistence.Save(new AppSettings { LaunchAtStartup = LaunchAtStartup, DefaultSound = DefaultSound });
}
