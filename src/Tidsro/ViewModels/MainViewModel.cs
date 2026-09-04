using System.Collections.ObjectModel;
using System.Linq;
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
        { SoundChoice.None, SoundChoice.SoftChime, SoundChoice.Marimba, SoundChoice.Bell,
          SoundChoice.PianoJingle, SoundChoice.ElectricPianoJingle, SoundChoice.BellJingle };

    [ObservableProperty] private string _customInput = "";
    [ObservableProperty] private string _label = "";
    [ObservableProperty] private string? _customError;
    [ObservableProperty] private SoundChoice _selectedSound;

    public ObservableCollection<AlarmItemViewModel> Alarms { get; } = new();

    /// <summary>The Week tab's projection. Constructed once and held for the app's lifetime.</summary>
    public TimetableViewModel Timetable { get; }

    [ObservableProperty] private string _alarmTimeInput = "";
    [ObservableProperty] private string _alarmEndInput = "";
    [ObservableProperty] private string _alarmLabel = "";
    [ObservableProperty] private string? _alarmError;
    [ObservableProperty] private SoundChoice _alarmSound;

    public RepeatOption[] RepeatOptions { get; } =
        { RepeatOption.Once, RepeatOption.Daily, RepeatOption.Weekdays, RepeatOption.Weekends, RepeatOption.Custom };

    [ObservableProperty] private RepeatOption _alarmRepeat = RepeatOption.Once;

    /// <summary>Whether the add form offers an end time. Only a repeating alarm can have one: an end
    /// is a timetable block's length, and the Week tab draws recurring alarms alone.</summary>
    public bool ShowAlarmEndInput => AlarmRepeat != RepeatOption.Once;
    [ObservableProperty] private bool _alarmWarnBefore;
    [ObservableProperty] private int _selectedTabIndex;

    public IReadOnlyList<DayToggleViewModel> AlarmDayToggles { get; } = DayToggleViewModel.Week();

    public bool ShowCustomDays => AlarmRepeat == RepeatOption.Custom;

    partial void OnAlarmRepeatChanged(RepeatOption value)
    {
        OnPropertyChanged(nameof(ShowCustomDays));
        OnPropertyChanged(nameof(ShowAlarmEndInput));
    }

    // Both derived flags depend on the selected tab, and CommunityToolkit only raises
    // SelectedTabIndex itself.
    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(ShowHero));
        OnPropertyChanged(nameof(ShowStrip));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnythingToClear))]
    private string? _missedNote;

    // Snapshot of (id, fire-time) the agenda was last built from. A recurring alarm advances its
    // EndsAt on firing without changing its id, so reconciling on ids alone would leave a stale row.
    private HashSet<(Guid Id, DateTimeOffset? EndsAt)> _agendaSignature = new();
    private HashSet<(Guid Id, DateTimeOffset? EndsAt)> AgendaSignature() =>
        _scheduler.Alarms.Select(a => (a.Id, a.EndsAt)).ToHashSet();

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
    /// <summary>Raised before a bulk wipe so the View can close open completion cards — their Snooze
    /// would otherwise re-arm an alarm into the schedule we are emptying.</summary>
    public event EventHandler? ClosePopupsRequested;

    public bool IsDayEmpty => Alarms.Count == 0;

    /// <summary>The countdown the bottom strip shows. SortRunning already puts active timers first in
    /// finish order and parks paused ones below, so Running[0] is "the soonest active timer, or the
    /// first paused one when nothing is active" — which is exactly what the strip should show. An
    /// IsNext-based strip would go blank the moment every timer was paused.</summary>
    public TimerItemViewModel? StripTimer => Running.FirstOrDefault();

    /// <summary>The hero countdown at the top of Quick timers. Same timer the strip would show.</summary>
    public bool ShowHero => StripTimer is not null && SelectedTabIndex == QuickTimersTab;

    /// <summary>The bottom strip exists to keep a running timer visible from the OTHER tab. On Quick
    /// timers the hero already shows it, and rendering both repeats the value on screen and reports
    /// one piece of state twice to a screen reader.</summary>
    public bool ShowStrip => StripTimer is not null && SelectedTabIndex != QuickTimersTab;

    private const int QuickTimersTab = 0;

    /// <summary>The timers the strip is not showing, or null when there is only one.</summary>
    public string? StripExtraText => Running.Count > 1 ? $"+{Running.Count - 1} more" : null;

    // Driven off the collection, not off RefreshAll: Add, CancelTimer, UndoDelete and ClearAllAlarms
    // all mutate Running directly, so a tick-driven strip would keep showing a wiped timer for up to
    // 250 ms after "Clear all alarms" — exactly when the user is looking for confirmation. This also
    // catches the Move calls in SortRunning, which change Running[0] without changing the count.
    private void RefreshStrip()
    {
        OnPropertyChanged(nameof(StripTimer));
        OnPropertyChanged(nameof(ShowStrip));
        OnPropertyChanged(nameof(ShowHero));
        OnPropertyChanged(nameof(StripExtraText));
        MarkHero();
    }

    /// <summary>Flag the one timer whose countdown the hero card is already rendering, so its row in
    /// the list below can drop its own numerals. The hero and the Running list are bound to the SAME
    /// collection, so without this the countdown sits on screen twice on Quick timers — the
    /// duplication that collapsing the strip there was meant to prevent.
    ///
    /// Only the numerals go. The row itself stays, with its pause/reset/cancel buttons bound to their
    /// own item, its label, its finish time, its sound tag and its IsNext dot. Hiding the whole row
    /// cost all of that, and it collapsed a focusable subtree out from under the caret whenever a
    /// resumed timer sorted to the front — the failure MainWindow's RescueFocusFromHiddenPanel exists
    /// to prevent for panels. A TextBlock cannot hold focus, so hiding only that is inert.
    ///
    /// Marking at the item level is deliberate: re-projecting the ItemsSource as Running.Skip(1) would
    /// hand the ItemsControl a new collection on every one-second tick, rebuilding every container and
    /// restarting the rows' fade-in. Only Running[0] can be the hero's, so no tab check is needed —
    /// the hero and the list live in the same panel and appear together.</summary>
    private void MarkHero()
    {
        var hero = StripTimer;
        foreach (var vm in Running) vm.IsCountdownInHero = ReferenceEquals(vm, hero);
    }

    /// <summary>True when Settings' "Clear all alarms" would actually change anything: armed alarms,
    /// running timers, or a missed note left over from a one-shot alarm that fired while away (the
    /// note has no armed alarm behind it, so alarmCount alone would miss it).</summary>
    public bool HasAnythingToClear => _scheduler.Alarms.Count + _scheduler.Running.Count > 0 || MissedNote is not null;

    public MainViewModel(SchedulerService scheduler, ISoundService sound, SoundChoice defaultSound)
    {
        _scheduler = scheduler;
        _sound = sound;
        _selectedSound = defaultSound;   // seed the picker from the global default; per-timer override lives here after
        _alarmSound = defaultSound;   // the alarm sound picker starts at the global default too
        // No AlarmsChanged hook for the week. That event means "the alarm set is now worth writing to
        // disk", which is not the same question as "does the week still draw what the scheduler
        // holds" — DeleteAlarm deliberately does NOT raise it, because the delete is not committed
        // until the undo window closes, and the week was consequently stale for the whole nine
        // seconds. RebuildAgenda is the honest funnel: it runs on every path that changes what is
        // armed, including delete and undo. See RebuildAgenda.
        Timetable = new TimetableViewModel(scheduler);
        Running.CollectionChanged += (_, e) =>
        {
            // A row removed from the collection is not the hero's, and MarkHero only walks what is
            // still in Running — so unmark the departing rows here. (Clear() reports no OldItems,
            // but it discards every row anyway.)
            if (e.OldItems is not null)
                foreach (TimerItemViewModel row in e.OldItems) row.IsCountdownInHero = false;
            RefreshStrip();
        };
    }

    // The Settings "default sound" changed: move the picker to match (last edit wins with a manual per-timer pick).
    public void SetDefaultSound(SoundChoice sound) => SelectedSound = sound;

    // Restores Ctrl+Tab: the header-only TabControl template puts the panels beside Tabs rather than
    // inside it, so a keypress in a panel never reaches the control's own Ctrl+Tab handler. Bound from
    // a window-level KeyBinding instead, and testable here without a window. AppSettings.TabCount, not
    // a literal 2, so the Week tab needs no change to this wrap.
    [RelayCommand]
    private void AdvanceTab() => SelectedTabIndex = (SelectedTabIndex + 1) % AppSettings.TabCount;

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
        CustomInput = "";
    }

    private void Add(TimeSpan duration)
    {
        var label = string.IsNullOrWhiteSpace(Label) ? null : CapitalizeFirst(Label.Trim());
        var item = _scheduler.StartCountdown(duration, label, SelectedSound);
        Running.Add(new TimerItemViewModel(item, _scheduler));
        Label = "";   // consumed by this timer — clear so it can't carry into the next one (preset or custom)
        SortRunning();
        MarkNextRunning();
    }

    private static string CapitalizeFirst(string s) =>
        s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    // Flag the active timer that will reach zero soonest, so the View highlights it like the agenda's
    // "next" alarm. Only Running timers qualify — a paused or already-fired countdown isn't what fires next.
    private void MarkNextRunning()
    {
        var next = Running
            .Where(vm => vm.Item.State == TimerState.Running)
            .OrderBy(vm => _scheduler.Remaining(vm.Item))
            .FirstOrDefault();
        foreach (var vm in Running)
            vm.IsNext = ReferenceEquals(vm, next);
    }

    // Stack the active timers soonest-first, like the agenda; paused timers aren't counting down, so they
    // park below the active ones. Reorder in place with Move so a focused row keeps focus and its
    // screen-reader state — rebuilding the collection each tick would drop both. Running countdowns never
    // change relative order, so this is a no-op on almost every tick (only an add, pause, or resume shifts it).
    private void SortRunning()
    {
        var desired = Running
            .OrderBy(vm => vm.Item.State == TimerState.Running ? 0 : 1)
            .ThenBy(vm => _scheduler.Remaining(vm.Item))
            .ToList();
        for (var i = 0; i < desired.Count; i++)
        {
            var current = Running.IndexOf(desired[i]);
            if (current != i) Running.Move(current, i);
        }
    }

    [RelayCommand]
    private void CancelTimer(TimerItemViewModel? row)
    {
        if (row is null) return;
        CommitPendingDelete();                       // only one outstanding undo at a time
        var item = row.Item;
        var remaining = _scheduler.Remaining(item);  // capture BEFORE cancelling
        _scheduler.Cancel(item);
        Running.Remove(row);                         // instant removal — no 1s tick lag
        MarkNextRunning();                           // highlight jumps to the new soonest at once
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

        SortRunning();       // keep the soonest-first order current as timers are added, pause, or resume
        MarkNextRunning();   // keep the "next" highlight current as timers count down, pause, or fire

        // Reconcile the alarm agenda only when it actually changed — an add/remove/one-shot fire (ids)
        // or a recurring roll-forward (EndsAt). Otherwise leave the collection alone so focus and
        // announcements aren't disrupted every second. The same signature also catches an alarm armed
        // straight on the scheduler (Snooze/Restart-style, bypassing AlarmsChanged) for the Week tab.
        if (!AgendaSignature().SetEquals(_agendaSignature)) RebuildAgenda();   // rebuilds the week too

        Timetable.RefreshForTick();
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
        var days = ResolveDays();

        int? endMinute = null;
        if (days != Weekdays.None && !string.IsNullOrWhiteSpace(AlarmEndInput))
        {
            // Same parser as the start, and the same rule as the Edit dialog: reported here, because
            // here there is a person to tell.
            if (!ClockTimeRules.TryParse(AlarmEndInput, out var eh, out var em, out var endError))
            { AlarmError = endError; return; }

            endMinute = eh * 60 + em;
            if (endMinute <= hour * 60 + minute)
            { AlarmError = "The end must be after the start."; return; }
        }

        if (days == Weekdays.None)
        {
            var fireAt = ClockTimeRules.ComputeFireAt(_scheduler.Now, hour, minute);
            _scheduler.ArmClockAlarm(fireAt, label, AlarmSound, warnBefore: AlarmWarnBefore);
            Announce($"Alarm added for {fireAt:HH\\:mm}");
        }
        else
        {
            _scheduler.ArmRecurringAlarm(hour, minute, days, label, AlarmSound, warnBefore: AlarmWarnBefore,
                endMinute: endMinute);
            Announce($"Alarm added for {hour:00}:{minute:00}, {RecurrenceRules.CadenceLabel(days)}");
        }

        RebuildAgenda();
        ClearEditor();
        AlarmsChanged?.Invoke(this, EventArgs.Empty);
    }

    // Custom uses the picked toggles; every other option is a fixed preset (Once -> None -> one-shot).
    private Weekdays ResolveDays() => AlarmRepeat == RepeatOption.Custom
        ? AlarmDayToggles.Where(t => t.IsSelected).Aggregate(Weekdays.None, (acc, t) => acc | t.Flag)
        : RecurrenceRules.DaysFor(AlarmRepeat, Weekdays.None);

    [RelayCommand]
    private void BeginEditAlarm(AlarmItemViewModel? row)
    {
        if (row is null) return;
        CommitPendingDelete();                 // settle any outstanding undo first
        EditAlarmRequested?.Invoke(this, row);
    }

    // Called by the Edit-alarm dialog on Save. Replaces the alarm in place (same Id), normalizing the
    // label like the add path. Mirrors the former in-place edit branch.
    public void ApplyAlarmEdit(Guid id, int hour, int minute, Weekdays days, string? label, SoundChoice sound,
        bool warnBefore, int? endMinute = null)
    {
        var existing = _scheduler.Alarms.FirstOrDefault(a => a.Id == id);
        if (existing is not null) _scheduler.RemoveAlarm(existing);
        var clean = string.IsNullOrWhiteSpace(label) ? null : CapitalizeFirst(label.Trim());
        if (days == Weekdays.None)
        {
            var fireAt = ClockTimeRules.ComputeFireAt(_scheduler.Now, hour, minute);
            _scheduler.ArmClockAlarm(fireAt, clean, sound, id, warnBefore: warnBefore);
            Announce($"Alarm updated for {fireAt:HH\\:mm}");
        }
        else
        {
            _scheduler.ArmRecurringAlarm(hour, minute, days, clean, sound, id, warnBefore: warnBefore,
                endMinute: endMinute);
            Announce($"Alarm updated for {hour:00}:{minute:00}, {RecurrenceRules.CadenceLabel(days)}");
        }
        RebuildAgenda();
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

    /// <summary>Wipe every countdown, alarm and missed note. Walks the scheduler rather than the view:
    /// SaveData persists from _scheduler.Alarms, so anything the agenda hasn't caught up with would
    /// survive the wipe and be written straight back. Called from Settings; confirmation happens there.</summary>
    public void ClearAllAlarms()
    {
        CommitPendingDelete();                 // settle any outstanding undo first
        ClosePopupsRequested?.Invoke(this, EventArgs.Empty);

        foreach (var item in _scheduler.Running.ToList()) _scheduler.Cancel(item);
        foreach (var item in _scheduler.Alarms.ToList()) _scheduler.Cancel(item);

        Running.Clear();
        Alarms.Clear();
        MissedNote = null;
        // ClearAllAlarms manages Alarms itself rather than going through RebuildAgenda — clearing a
        // collection is cheaper than re-deriving an empty one — so it owes RebuildAgenda's two exit
        // duties by hand: re-project the week, and resync the signature so the next RefreshAll tick
        // does not mismatch against the now-empty scheduler and redo the whole thing 250 ms later.
        Timetable.Rebuild();
        _agendaSignature = AgendaSignature();

        OnPropertyChanged(nameof(IsDayEmpty));
        AlarmsChanged?.Invoke(this, EventArgs.Empty);
        Announce("All alarms cleared");
    }

    /// <summary>Replace the whole schedule with an imported one. This is ClearAllAlarms with an arming
    /// pass on the end, and it keeps that method's three invariants: walk the scheduler rather than the
    /// derived view collections (SaveData persists from the scheduler, so anything the agenda has not
    /// caught up with would survive and be written straight back), disarm before emptying so nothing
    /// can fire from the tick in between, and close open cards first — an open card's Snooze would
    /// re-arm into the set we are about to discard.</summary>
    public void ReplaceAllAlarms(IEnumerable<AlarmRecord> alarms, IEnumerable<RecurringAlarmRecord> recurring)
    {
        CommitPendingDelete();                 // settle any outstanding undo first
        ClosePopupsRequested?.Invoke(this, EventArgs.Empty);

        foreach (var item in _scheduler.Running.ToList()) _scheduler.Cancel(item);
        foreach (var item in _scheduler.Alarms.ToList()) _scheduler.Cancel(item);

        Running.Clear();
        Alarms.Clear();
        MissedNote = null;

        var armed = 0;
        foreach (var r in alarms)
        {
            // A residual bad record must never abort the import — the same posture as launch.
            try
            {
                _scheduler.ArmClockAlarm(LocalToOffset(r.FireAt), r.Label, r.Sound, r.Id, r.WarnBefore, r.Enabled);
                armed++;
            }
            catch { /* skip it and keep going */ }
        }

        foreach (var r in recurring)
        {
            try
            {
                _scheduler.ArmRecurringAlarm(r.Hour, r.Minute, r.Days, r.Label, r.Sound, r.Id,
                    LocalToOffset(r.NextFireAt), r.WarnBefore, r.Enabled, r.EndMinute);
                armed++;
            }
            catch { /* skip it and keep going */ }
        }

        RebuildAgenda();
        OnPropertyChanged(nameof(IsDayEmpty));
        AlarmsChanged?.Invoke(this, EventArgs.Empty);
        Announce(armed == 1 ? "Imported 1 alarm" : $"Imported {armed} alarms");
    }

    // A persisted alarm time is a wall-clock local time; tag it Local before lifting to DateTimeOffset
    // so the scheduler compares against the right instant. Mirrors App's loader.
    private static DateTimeOffset LocalToOffset(DateTime local) =>
        new(DateTime.SpecifyKind(local, DateTimeKind.Local));

    [RelayCommand]
    private void ToggleAlarm(AlarmItemViewModel? row)
    {
        if (row is null) return;
        CommitPendingDelete();                          // settle any outstanding undo first
        var item = row.Item;
        _scheduler.SetEnabled(item, !item.IsEnabled);   // re-enable rolls a stale recurring alarm forward
        RebuildAgenda();
        AlarmsChanged?.Invoke(this, EventArgs.Empty);   // the on/off change is persisted
        Announce($"Alarm at {row.TimeText} turned {(item.IsEnabled ? "on" : "off")}");
    }

    [RelayCommand]
    private void UndoDelete()
    {
        if (_pendingDelete is not { } item) return;
        if (item.TriggerType == TriggerType.Countdown)
        {
            var restored = _scheduler.StartCountdown(_pendingDeleteRemaining ?? TimeSpan.Zero, item.Label, item.Sound);
            Running.Add(new TimerItemViewModel(restored, _scheduler));
            SortRunning();
            MarkNextRunning();
            Announce("Timer restored");
        }
        else if (item.RecurringDays is { } days && item.EndsAt is { } next)
        {
            _scheduler.ArmRecurringAlarm(next.Hour, next.Minute, days, item.Label, item.Sound, item.Id, next,
                item.WarnBefore, item.IsEnabled, item.EndMinute);
            RebuildAgenda();
            Announce("Alarm restored");
            // No persist needed: the record was never removed from disk.
        }
        else if (item.EndsAt is { } fireAt)
        {
            _scheduler.ArmClockAlarm(fireAt, item.Label, item.Sound, item.Id, item.WarnBefore, item.IsEnabled);   // re-arm; next tick re-checks grace if past
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
        var wasAlarm = item.TriggerType is TriggerType.ClockTime or TriggerType.Recurring;
        _pendingDelete = null;
        _pendingDeleteRemaining = null;
        PendingDeleteLabel = null;
        OnPropertyChanged(nameof(HasPendingDelete));
        if (wasAlarm) AlarmsChanged?.Invoke(this, EventArgs.Empty);   // disk now reflects the alarm removal
    }

    private void ClearEditor()
    {
        AlarmTimeInput = "";
        AlarmEndInput = "";
        AlarmLabel = "";
        AlarmError = null;
        AlarmRepeat = RepeatOption.Once;
        foreach (var t in AlarmDayToggles) t.IsSelected = false;
        AlarmWarnBefore = false;
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

    /// <summary>Rebuild the agenda from the scheduler's armed alarms: sorted, with tomorrow/next cues.
    ///
    /// <para>Also re-projects the Week tab, and this is the ONLY place that does for anything short of
    /// a bulk wipe. Both views read the same scheduler set, so anything that moves one moves the
    /// other; hanging the week off AlarmsChanged instead left DeleteAlarm — which does not raise it,
    /// because the record stays on disk through the undo window — drawing a deleted alarm for nine
    /// seconds, with RefreshAll's signature gate comparing equal on every tick in between because
    /// this method resyncs it on its way out.</para></summary>
    private void RebuildAgenda()
    {
        var today = _scheduler.Now.Date;

        // Enabled alarms first, in fire-time order.
        var enabled = _scheduler.Alarms
            .Where(a => a.IsEnabled)
            .OrderBy(a => a.EndsAt)
            .ThenBy(a => a.Label)
            .ThenBy(a => a.Id);
        // Disabled alarms park below, ordered by time-of-day — a disabled recurring alarm's date can
        // be stale (frozen while off), so the full timestamp would mis-sort it.
        var disabled = _scheduler.Alarms
            .Where(a => !a.IsEnabled)
            .OrderBy(a => a.EndsAt?.TimeOfDay)
            .ThenBy(a => a.Label)
            .ThenBy(a => a.Id);
        var ordered = enabled.Concat(disabled).ToList();

        Alarms.Clear();
        for (var i = 0; i < ordered.Count; i++)
        {
            var a = ordered[i];
            var isTomorrow = a.EndsAt is { } e && e.Date != today;
            var isNext = i == 0 && a.IsEnabled;   // a disabled alarm is never "next"
            Alarms.Add(new AlarmItemViewModel(a, isTomorrow, isNext));
        }
        OnPropertyChanged(nameof(IsDayEmpty));
        Timetable.Rebuild();
        _agendaSignature = AgendaSignature();
    }
}
