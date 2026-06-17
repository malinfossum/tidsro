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

    [ObservableProperty] private string? _missedNote;

    private TimerItem? _pendingDelete;
    private TimeSpan? _pendingDeleteRemaining;   // non-null when the pending item is a cancelled countdown
    [ObservableProperty] private string? _pendingDeleteLabel;
    public bool HasPendingDelete => _pendingDelete is not null;

    /// <summary>Raised when the armed alarm set changes (add/edit/delete-commit) so the App persists.</summary>
    public event EventHandler? AlarmsChanged;
    /// <summary>Raised with a short message for the View to announce via UIA (no focus change).</summary>
    public event EventHandler<string>? Announcement;
    /// <summary>Raised when the user picks an agenda row to edit, so the View opens the modal Edit-alarm dialog.</summary>
    public event EventHandler<AlarmItemViewModel>? EditAlarmRequested;

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
        var label = string.IsNullOrWhiteSpace(Label) ? null : CapitalizeFirst(Label.Trim());
        var item = _scheduler.StartCountdown(duration, label, SelectedSound);
        Running.Add(new TimerItemViewModel(item, _scheduler));
    }

    private static string CapitalizeFirst(string s) =>
        s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    [RelayCommand]
    private void CancelTimer(TimerItemViewModel? row)
    {
        if (row is null) return;
        CommitPendingDelete();                       // only one outstanding undo at a time
        var item = row.Item;
        var remaining = _scheduler.Remaining(item);  // capture BEFORE cancelling
        _scheduler.Cancel(item);
        Running.Remove(row);                         // instant removal — no 1s tick lag
        _pendingDelete = item;
        _pendingDeleteRemaining = remaining;
        PendingDeleteLabel = $"Timer cancelled{(string.IsNullOrEmpty(item.Label) ? "" : $" · {item.Label}")}";
        OnPropertyChanged(nameof(HasPendingDelete));
        Announce("Timer cancelled");
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

    // Add-only now: editing happens in the modal Edit-alarm dialog (see BeginEditAlarm / ApplyAlarmEdit).
    [RelayCommand]
    private void AddAlarm()
    {
        CommitPendingDelete();
        if (!ClockTimeRules.TryParse(AlarmTimeInput, out var hour, out var minute, out var error))
        { AlarmError = error; return; }
        AlarmError = null;

        var label = string.IsNullOrWhiteSpace(AlarmLabel) ? null : CapitalizeFirst(AlarmLabel.Trim());
        var fireAt = ClockTimeRules.ComputeFireAt(_scheduler.Now, hour, minute);
        _scheduler.ArmClockAlarm(fireAt, label, AlarmSound);

        RebuildAgenda();
        ClearEditor();
        AlarmsChanged?.Invoke(this, EventArgs.Empty);
        Announce($"Alarm added for {fireAt:HH\\:mm}");
    }

    [RelayCommand]
    private void BeginEditAlarm(AlarmItemViewModel? row)
    {
        if (row is null) return;
        CommitPendingDelete();                 // settle any outstanding undo first
        EditAlarmRequested?.Invoke(this, row);
    }

    // Called by the Edit-alarm dialog on Save. Replaces the alarm in place (same Id), normalizing the
    // label like the add path. Mirrors the former in-place edit branch.
    public void ApplyAlarmEdit(Guid id, int hour, int minute, string? label, SoundChoice sound)
    {
        var existing = _scheduler.Alarms.FirstOrDefault(a => a.Id == id);
        if (existing is not null) _scheduler.RemoveAlarm(existing);
        var clean = string.IsNullOrWhiteSpace(label) ? null : CapitalizeFirst(label.Trim());
        var fireAt = ClockTimeRules.ComputeFireAt(_scheduler.Now, hour, minute);
        _scheduler.ArmClockAlarm(fireAt, clean, sound, id);
        RebuildAgenda();
        Announce($"Alarm updated for {fireAt:HH\\:mm}");
        AlarmsChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void DeleteAlarm(AlarmItemViewModel? row)
    {
        if (row is null) return;
        CommitPendingDelete();                 // only one outstanding undo at a time

        var item = row.Item;
        _scheduler.RemoveAlarm(item);          // disarm at once: it can't fire during the undo window
        _pendingDelete = item;
        _pendingDeleteRemaining = null;        // this is an alarm (re-armed on undo, not a countdown)
        PendingDeleteLabel = $"Deleted {row.TimeText}{(string.IsNullOrEmpty(row.Item.Label) ? "" : $" · {row.Item.Label}")}";
        OnPropertyChanged(nameof(HasPendingDelete));

        RebuildAgenda();
        Announce($"Alarm at {row.TimeText} deleted");
        // Note: not persisted yet. The on-disk record survives until CommitPendingDelete (auto-timeout / quit).
    }

    [RelayCommand]
    private void UndoDelete()
    {
        if (_pendingDelete is not { } item) return;
        if (item.TriggerType == TriggerType.Countdown)
        {
            var restored = _scheduler.StartCountdown(_pendingDeleteRemaining ?? TimeSpan.Zero, item.Label, item.Sound);
            Running.Add(new TimerItemViewModel(restored, _scheduler));
            Announce("Timer restored");
        }
        else if (item.EndsAt is { } fireAt)
        {
            _scheduler.ArmClockAlarm(fireAt, item.Label, item.Sound, item.Id);   // re-arm; next tick re-checks grace if past
            RebuildAgenda();
            Announce("Alarm restored");
            // No persist needed: the record was never removed from disk.
        }
        _pendingDelete = null;
        _pendingDeleteRemaining = null;
        PendingDeleteLabel = null;
        OnPropertyChanged(nameof(HasPendingDelete));
    }

    /// <summary>Finalise an outstanding delete: for alarms, it leaves disk now. Called on timeout, on quit, or before another action.</summary>
    public void CommitPendingDelete()
    {
        if (_pendingDelete is not { } item) return;
        var wasAlarm = item.TriggerType == TriggerType.ClockTime;
        _pendingDelete = null;
        _pendingDeleteRemaining = null;
        PendingDeleteLabel = null;
        OnPropertyChanged(nameof(HasPendingDelete));
        if (wasAlarm) AlarmsChanged?.Invoke(this, EventArgs.Empty);   // disk now reflects the alarm removal
    }

    private void ClearEditor()
    {
        AlarmTimeInput = "";
        AlarmLabel = "";
        AlarmError = null;
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
