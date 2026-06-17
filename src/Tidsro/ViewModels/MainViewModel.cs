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

    private Guid? _editingId;   // the alarm being edited in place; null in add mode

    [ObservableProperty] private string? _missedNote;

    private TimerItem? _pendingDelete;
    [ObservableProperty] private string? _pendingDeleteLabel;
    public bool HasPendingDelete => _pendingDelete is not null;

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

    // The "Your day" sound picker has its own preview, gated on AlarmSound (independent of the timer sound above).
    [RelayCommand(CanExecute = nameof(CanPreviewAlarmSound))]
    private void PreviewAlarmSound() => _sound.Play(AlarmSound);
    private bool CanPreviewAlarmSound() => AlarmSound != SoundChoice.None;   // nothing to hear when silent

    partial void OnAlarmSoundChanged(SoundChoice value) => PreviewAlarmSoundCommand.NotifyCanExecuteChanged();

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

        // Reconcile the alarm agenda only when the armed set changed (fired/expired drops rows),
        // so the collection isn't rebuilt every second (which would disrupt focus and announcements).
        var live = _scheduler.Alarms.Select(a => a.Id).ToHashSet();
        var shown = Alarms.Select(a => a.Item.Id).ToHashSet();
        if (!live.SetEquals(shown)) RebuildAgenda();
    }

    [RelayCommand]
    private void AddOrSaveAlarm()
    {
        CommitPendingDelete();
        if (!ClockTimeRules.TryParse(AlarmTimeInput, out var hour, out var minute, out var error))
        { AlarmError = error; return; }
        AlarmError = null;

        var label = string.IsNullOrWhiteSpace(AlarmLabel) ? null : AlarmLabel.Trim();
        var fireAt = ClockTimeRules.ComputeFireAt(_scheduler.Now, hour, minute);

        if (_editingId is { } id)                                   // edit in place
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

    [RelayCommand]
    private void BeginEditAlarm(AlarmItemViewModel? row)
    {
        if (row?.Item.EndsAt is not { } fireAt) return;
        _editingId = row.Item.Id;
        AlarmTimeInput = fireAt.ToString("HH\\:mm");
        AlarmLabel = row.Item.Label ?? "";
        AlarmSound = row.Item.Sound;
        AlarmError = null;
        IsEditingAlarm = true;
    }

    [RelayCommand]
    private void CancelEditAlarm()
    {
        ExitEditMode();
        ClearEditor();
    }

    [RelayCommand]
    private void DeleteAlarm(AlarmItemViewModel? row)
    {
        if (row is null) return;
        CommitPendingDelete();                 // only one outstanding undo at a time

        var item = row.Item;
        _scheduler.RemoveAlarm(item);          // disarm at once: it can't fire during the undo window
        _pendingDelete = item;
        PendingDeleteLabel = $"Deleted {row.TimeText}{(string.IsNullOrEmpty(row.Item.Label) ? "" : $" · {row.Item.Label}")}";
        OnPropertyChanged(nameof(HasPendingDelete));

        RebuildAgenda();
        Announce($"Alarm at {row.TimeText} deleted");
        // Note: not persisted yet. The on-disk record survives until CommitPendingDelete (auto-timeout / quit).
    }

    [RelayCommand]
    private void UndoDelete()
    {
        if (_pendingDelete is not { EndsAt: { } fireAt } item) return;
        _scheduler.ArmClockAlarm(fireAt, item.Label, item.Sound, item.Id);   // re-arm; next tick re-checks grace if past
        _pendingDelete = null;
        PendingDeleteLabel = null;
        OnPropertyChanged(nameof(HasPendingDelete));

        RebuildAgenda();
        Announce("Alarm restored");
        // No persist needed: the record was never removed from disk.
    }

    /// <summary>Finalise an outstanding delete: it leaves disk now. Called on timeout, on quit, or before another action.</summary>
    public void CommitPendingDelete()
    {
        if (_pendingDelete is null) return;
        _pendingDelete = null;
        PendingDeleteLabel = null;
        OnPropertyChanged(nameof(HasPendingDelete));
        AlarmsChanged?.Invoke(this, EventArgs.Empty);   // disk now reflects the removal
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

    /// <summary>Record an alarm that expired beyond the grace (sleep or app-closed) as one quiet line.</summary>
    public void AddMissed(TimerItem item)
    {
        var time = item.EndsAt is { } e ? e.ToString("HH\\:mm") : "";
        var label = string.IsNullOrWhiteSpace(item.Label) ? "Alarm" : item.Label!.Trim();
        var line = $"{label} · {time}";
        MissedNote = MissedNote is null
            ? $"Missed while away: {line}"
            : $"{MissedNote}; {line}";
        Announce($"Missed while away: {line}");
    }

    [RelayCommand]
    private void DismissMissedNote() => MissedNote = null;

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
