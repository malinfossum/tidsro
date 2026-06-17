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

    public ObservableCollection<AlarmItemViewModel> Alarms { get; } = new();

    [ObservableProperty] private string _alarmTimeInput = "";
    [ObservableProperty] private string _alarmLabel = "";
    [ObservableProperty] private string? _alarmError;
    [ObservableProperty] private SoundChoice _alarmSound;
    [ObservableProperty] private bool _isEditingAlarm;

    private Guid? _editingId;   // the alarm being edited in place (wired further in a later task)

    /// <summary>Raised when the armed alarm set changes (add/edit/delete-commit) so the App persists.</summary>
    public event EventHandler? AlarmsChanged;
    /// <summary>Raised with a short message for the View to announce via UIA (no focus change).</summary>
    public event EventHandler<string>? Announcement;

    public string AddOrSaveLabel => IsEditingAlarm ? "Save" : "Add";
    partial void OnIsEditingAlarmChanged(bool value) => OnPropertyChanged(nameof(AddOrSaveLabel));

    public bool IsDayEmpty => Alarms.Count == 0;

    public MainViewModel(SchedulerService scheduler, ISoundService sound, SoundChoice defaultSound)
    {
        _scheduler = scheduler;
        _sound = sound;
        _selectedSound = defaultSound;   // seed the picker from the global default; per-timer override lives here after
        _alarmSound = defaultSound;   // the alarm sound picker starts at the global default too
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

    [RelayCommand]
    private void AddOrSaveAlarm()
    {
        if (!ClockTimeRules.TryParse(AlarmTimeInput, out var hour, out var minute, out var error))
        { AlarmError = error; return; }
        AlarmError = null;

        var label = string.IsNullOrWhiteSpace(AlarmLabel) ? null : AlarmLabel.Trim();
        var fireAt = ClockTimeRules.ComputeFireAt(_scheduler.Now, hour, minute);

        if (_editingId is { } id)                                   // edit in place (wired further later)
        {
            var existing = _scheduler.Alarms.FirstOrDefault(a => a.Id == id);
            if (existing is not null) _scheduler.RemoveAlarm(existing);
            _scheduler.ArmClockAlarm(fireAt, label, AlarmSound, id);
            ExitEditMode();
            Announce($"Alarm updated for {fireAt:HH\\:mm}");
        }
        else
        {
            _scheduler.ArmClockAlarm(fireAt, label, AlarmSound);
            Announce($"Alarm added for {fireAt:HH\\:mm}");
        }

        RebuildAgenda();
        ClearEditor();
        AlarmsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearEditor()
    {
        AlarmTimeInput = "";
        AlarmLabel = "";
        AlarmError = null;
    }

    private void ExitEditMode()
    {
        _editingId = null;
        IsEditingAlarm = false;
    }

    private void Announce(string message) => Announcement?.Invoke(this, message);

    /// <summary>Rebuild the agenda from the scheduler's armed alarms: sorted, with tomorrow/next cues.</summary>
    private void RebuildAgenda()
    {
        var today = _scheduler.Now.Date;
        var ordered = _scheduler.Alarms
            .OrderBy(a => a.EndsAt)
            .ThenBy(a => a.Label)
            .ThenBy(a => a.Id)
            .ToList();

        Alarms.Clear();
        for (var i = 0; i < ordered.Count; i++)
        {
            var a = ordered[i];
            var isTomorrow = a.EndsAt is { } e && e.Date != today;
            Alarms.Add(new AlarmItemViewModel(a, isTomorrow, isNext: i == 0));
        }
        OnPropertyChanged(nameof(IsDayEmpty));
    }
}
