using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidsro.Models;
using Tidsro.Services;

namespace Tidsro.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SchedulerService _scheduler;
    private readonly ISoundService _sound;

    public ObservableCollection<TimerItemViewModel> Running { get; } = new();
    public int[] Presets { get; } = { 15, 30, 60 };

    public SoundChoice[] SoundOptions { get; } =
        { SoundChoice.None, SoundChoice.SoftChime, SoundChoice.Marimba, SoundChoice.Bell };

    [ObservableProperty] private string _customInput = "";
    [ObservableProperty] private string _label = "";
    [ObservableProperty] private string? _customError;
    [ObservableProperty] private SoundChoice _selectedSound;

    /// <summary>Your day agenda is empty until Slice 2 (clock-time alarms).</summary>
    public bool IsDayEmpty => true;

    public MainViewModel(SchedulerService scheduler, ISoundService sound, SoundChoice defaultSound)
    {
        _scheduler = scheduler;
        _sound = sound;
        _selectedSound = defaultSound;   // seed the picker from the global default; per-timer override lives here after
    }

    // The Settings "default sound" changed: move the picker to match (last edit wins with a manual per-timer pick).
    public void SetDefaultSound(SoundChoice sound) => SelectedSound = sound;

    [RelayCommand(CanExecute = nameof(CanPreviewSound))]
    private void PreviewSound() => _sound.Play(SelectedSound);
    private bool CanPreviewSound() => SelectedSound != SoundChoice.None;   // nothing to hear when silent

    partial void OnSelectedSoundChanged(SoundChoice value) => PreviewSoundCommand.NotifyCanExecuteChanged();

    [RelayCommand] private void StartPreset(int minutes) =>
        Add(TimeSpan.FromMinutes(minutes));

    [RelayCommand] private void StartCustom()
    {
        if (!CountdownRules.TryParse(CustomInput, out var d, out var error))
        { CustomError = error; return; }
        CustomError = null;
        Add(d);
        CustomInput = ""; Label = "";
    }

    private void Add(TimeSpan duration)
    {
        var label = string.IsNullOrWhiteSpace(Label) ? null : Label.Trim();
        var item = _scheduler.StartCountdown(duration, label, SelectedSound);
        Running.Add(new TimerItemViewModel(item, _scheduler));
    }

    public void RefreshAll()
    {
        // drop rows whose underlying timer is no longer running (cancelled/fired+dismissed)
        for (var i = Running.Count - 1; i >= 0; i--)
        {
            if (!_scheduler.Running.Contains(Running[i].Item)) Running.RemoveAt(i);
            else Running[i].Refresh();
        }

        // reconcile: Snooze/Restart add items to the scheduler directly (no row),
        // so give every running timer without a row a fresh one — otherwise a
        // +5/Restart countdown runs headless until it fires (can't see/pause/cancel)
        foreach (var item in _scheduler.Running)
            if (!Running.Any(vm => vm.Item == item))
                Running.Add(new TimerItemViewModel(item, _scheduler));
    }
}
