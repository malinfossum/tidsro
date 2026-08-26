using Tidsro.Models;
using Tidsro.Services;
using Tidsro.ViewModels;
using Xunit;

namespace Tidsro.Tests;

public class MainViewModelTests
{
    private static MainViewModel New(out FakeClock clock, out SchedulerService sched) =>
        New(SoundChoice.None, out clock, out sched, out _);

    private static MainViewModel New(SoundChoice defaultSound, out FakeClock clock,
        out SchedulerService sched, out FakeSoundService sound)
    {
        clock = new FakeClock();
        sched = new SchedulerService(clock);
        sound = new FakeSoundService();
        return new MainViewModel(sched, sound, defaultSound);
    }

    [Fact]
    public void StartPreset_adds_a_running_row()
    {
        var vm = New(out _, out _);
        vm.StartPresetCommand.Execute(30);   // 30 minutes
        Assert.Single(vm.Running);
        Assert.False(vm.IsDayEmpty == false); // Your day stays empty in Slice 1
    }

    [Fact]
    public void StartCustom_with_valid_input_adds_row_and_clears_error()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00";
        vm.Label = "tea";
        vm.StartCustomCommand.Execute(null);
        Assert.Single(vm.Running);
        Assert.Null(vm.CustomError);
        Assert.Equal("Tea", vm.Running[0].Label);   // first letter is capitalized
    }

    [Fact]
    public void StartCustom_with_bad_input_shows_error_and_adds_nothing()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "0";
        vm.StartCustomCommand.Execute(null);
        Assert.Empty(vm.Running);
        Assert.NotNull(vm.CustomError);
    }

    [Fact]
    public void Your_day_empty_state_is_true_in_slice_1()
    {
        var vm = New(out _, out _);
        Assert.True(vm.IsDayEmpty);   // agenda goes live in Slice 2
    }

    [Fact]
    public void RefreshAll_surfaces_a_scheduler_item_that_has_no_row()
    {
        var vm = New(out _, out var sched);
        // Snooze/Restart enqueue on the scheduler directly, bypassing the view-model's Add()
        var item = sched.StartCountdown(TimeSpan.FromMinutes(5), "pom", SoundChoice.None);
        Assert.Empty(vm.Running);
        vm.RefreshAll();
        Assert.Single(vm.Running);                 // reconciled into a visible row
        Assert.Equal(item.Id, vm.Running[0].Item.Id);
    }

    [Fact]
    public void SelectedSound_seeds_from_the_default()
    {
        var vm = New(SoundChoice.Bell, out _, out _, out _);
        Assert.Equal(SoundChoice.Bell, vm.SelectedSound);
    }

    [Fact]
    public void StartPreset_uses_the_selected_sound()
    {
        var vm = New(SoundChoice.None, out _, out _, out _);
        vm.SelectedSound = SoundChoice.Marimba;
        vm.StartPresetCommand.Execute(15);
        Assert.Equal(SoundChoice.Marimba, vm.Running[0].Item.Sound);
    }

    [Fact]
    public void StartCustom_uses_the_selected_sound()
    {
        var vm = New(SoundChoice.None, out _, out _, out _);
        vm.SelectedSound = SoundChoice.Bell;
        vm.CustomInput = "10";
        vm.StartCustomCommand.Execute(null);
        Assert.Equal(SoundChoice.Bell, vm.Running[0].Item.Sound);
    }

    [Fact]
    public void SetDefaultSound_updates_the_picker()
    {
        var vm = New(SoundChoice.None, out _, out _, out _);
        vm.SetDefaultSound(SoundChoice.Marimba);
        Assert.Equal(SoundChoice.Marimba, vm.SelectedSound);
    }

    [Fact]
    public void PreviewSound_plays_the_selected_sound()
    {
        var vm = New(SoundChoice.None, out _, out _, out var sound);
        vm.SelectedSound = SoundChoice.Bell;
        vm.PreviewSoundCommand.Execute(null);
        Assert.Equal(SoundChoice.Bell, sound.LastPlayed);
    }

    [Fact]
    public void Preview_is_disabled_when_the_sound_is_silent()
    {
        var vm = New(SoundChoice.None, out _, out _, out _);
        vm.SelectedSound = SoundChoice.None;
        Assert.False(vm.PreviewSoundCommand.CanExecute(null));
        vm.SelectedSound = SoundChoice.Bell;
        Assert.True(vm.PreviewSoundCommand.CanExecute(null));
    }

    [Fact]
    public void PreviewAlarmSound_plays_the_alarm_sound_independently_of_the_timer_sound()
    {
        var vm = New(SoundChoice.None, out _, out _, out var sound);
        vm.SelectedSound = SoundChoice.None;   // the Quick-timers sound is silent...
        vm.AlarmSound = SoundChoice.Bell;      // ...but the alarm has a sound
        Assert.True(vm.PreviewAlarmSoundCommand.CanExecute(null));   // gated by AlarmSound, not SelectedSound
        vm.PreviewAlarmSoundCommand.Execute(null);
        Assert.Equal(SoundChoice.Bell, sound.LastPlayed);           // plays the alarm sound, not the timer sound
    }

    [Fact]
    public void Preview_alarm_is_disabled_when_the_alarm_sound_is_silent()
    {
        var vm = New(SoundChoice.Bell, out _, out _, out _);   // even with the Quick-timers sound set...
        vm.AlarmSound = SoundChoice.None;                      // ...a silent alarm can't be previewed
        Assert.False(vm.PreviewAlarmSoundCommand.CanExecute(null));
        vm.AlarmSound = SoundChoice.Marimba;
        Assert.True(vm.PreviewAlarmSoundCommand.CanExecute(null));
    }

    [Fact]
    public void AddAlarm_with_a_future_time_inserts_a_row_and_clears_the_editor()
    {
        var vm = New(out var clock, out _);                 // FakeClock starts 2026-01-01 09:00
        vm.AlarmTimeInput = "10:30";
        vm.AlarmLabel = "Meeting";
        var changed = 0; vm.AlarmsChanged += (_, _) => changed++;

        vm.AddAlarmCommand.Execute(null);

        Assert.False(vm.IsDayEmpty);
        var row = Assert.Single(vm.Alarms);
        Assert.Equal("10:30", row.TimeText);
        Assert.Equal("Meeting", row.DisplayLabel);
        Assert.False(row.IsTomorrow);
        Assert.Equal("", vm.AlarmTimeInput);               // editor cleared
        Assert.Equal("", vm.AlarmLabel);
        Assert.Null(vm.AlarmError);
        Assert.Equal(1, changed);                          // persisted via the event
    }

    [Fact]
    public void AddAlarm_with_a_past_time_rolls_to_tomorrow()
    {
        var vm = New(out var clock, out _);                 // 09:00
        vm.AlarmTimeInput = "08:00";                        // already passed today
        vm.AddAlarmCommand.Execute(null);
        Assert.True(Assert.Single(vm.Alarms).IsTomorrow);
    }

    [Fact]
    public void AddAlarm_with_bad_input_shows_an_error_and_adds_nothing()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "99:99";
        vm.AddAlarmCommand.Execute(null);
        Assert.Empty(vm.Alarms);
        Assert.NotNull(vm.AlarmError);
    }

    [Fact]
    public void Alarms_are_sorted_by_fire_time()
    {
        var vm = New(out _, out _);                         // 09:00
        vm.AlarmTimeInput = "16:00"; vm.AddAlarmCommand.Execute(null);
        vm.AlarmTimeInput = "11:00"; vm.AddAlarmCommand.Execute(null);
        vm.AlarmTimeInput = "13:30"; vm.AddAlarmCommand.Execute(null);

        Assert.Collection(vm.Alarms,
            a => Assert.Equal("11:00", a.TimeText),
            a => Assert.Equal("13:30", a.TimeText),
            a => Assert.Equal("16:00", a.TimeText));
        Assert.True(vm.Alarms[0].IsNext);                  // earliest is the next to fire
        Assert.False(vm.Alarms[1].IsNext);
    }

    [Fact]
    public void AddAlarm_uses_the_selected_sound_and_announces()
    {
        var vm = New(SoundChoice.None, out _, out _, out _);
        vm.AlarmSound = SoundChoice.Bell;
        string? announced = null; vm.Announcement += (_, m) => announced = m;

        vm.AlarmTimeInput = "10:00";
        vm.AddAlarmCommand.Execute(null);

        Assert.Equal(SoundChoice.Bell, vm.Alarms[0].Item.Sound);
        Assert.NotNull(announced);
        Assert.Contains("10:00", announced);
    }

    [Fact]
    public void BeginEdit_raises_EditAlarmRequested_with_the_row()
    {
        var vm = New(out _, out _);                         // 09:00
        vm.AlarmTimeInput = "10:00"; vm.AlarmLabel = "Tea"; vm.AlarmSound = SoundChoice.Bell;
        vm.AddAlarmCommand.Execute(null);
        var row = vm.Alarms[0];
        AlarmItemViewModel? requested = null;
        vm.EditAlarmRequested += (_, r) => requested = r;

        vm.BeginEditAlarmCommand.Execute(row);

        Assert.Same(row, requested);                        // the View opens the dialog for this row
        Assert.Single(vm.Alarms);                           // nothing changed yet — that happens on dialog Save
    }

    [Fact]
    public void BeginEdit_commits_an_outstanding_pending_delete_first()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AddAlarmCommand.Execute(null);
        vm.AlarmTimeInput = "11:00"; vm.AddAlarmCommand.Execute(null);

        vm.DeleteAlarmCommand.Execute(vm.Alarms[0]);        // 10:00 now pending
        Assert.True(vm.HasPendingDelete);
        var committed = 0; vm.AlarmsChanged += (_, _) => committed++;

        vm.BeginEditAlarmCommand.Execute(vm.Alarms[0]);     // editing 11:00 settles the pending delete

        Assert.False(vm.HasPendingDelete);
        Assert.Equal(1, committed);
    }

    [Fact]
    public void BeginEdit_with_null_row_does_nothing()
    {
        var vm = New(out _, out _);
        var raised = false; vm.EditAlarmRequested += (_, _) => raised = true;

        vm.BeginEditAlarmCommand.Execute(null);

        Assert.False(raised);
    }

    [Fact]
    public void ApplyAlarmEdit_updates_the_alarm_in_place_keeping_its_id()
    {
        var vm = New(SoundChoice.None, out _, out var sched, out _);
        vm.AlarmTimeInput = "10:00"; vm.AlarmLabel = "Tea"; vm.AlarmSound = SoundChoice.Bell;
        vm.AddAlarmCommand.Execute(null);
        var originalId = vm.Alarms[0].Item.Id;
        var changed = 0; vm.AlarmsChanged += (_, _) => changed++;

        vm.ApplyAlarmEdit(originalId, 11, 15, Weekdays.None, "coffee", SoundChoice.Marimba, false);

        var row = Assert.Single(vm.Alarms);                // still one alarm, not a duplicate
        Assert.Single(sched.Alarms);                       // scheduler holds exactly one armed alarm
        Assert.Equal(originalId, row.Item.Id);             // same identity preserved
        Assert.Equal("11:15", row.TimeText);               // new time
        Assert.Equal("Coffee", row.DisplayLabel);          // label capitalized like the add path
        Assert.Equal(SoundChoice.Marimba, row.Item.Sound); // new sound
        Assert.Equal(11, row.Item.EndsAt!.Value.Hour);
        Assert.Equal(15, row.Item.EndsAt!.Value.Minute);
        Assert.Equal(1, changed);                          // persisted via the event
    }

    [Fact]
    public void ApplyAlarmEdit_announces_the_update()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AddAlarmCommand.Execute(null);
        var id = vm.Alarms[0].Item.Id;
        string? announced = null; vm.Announcement += (_, m) => announced = m;

        vm.ApplyAlarmEdit(id, 12, 45, Weekdays.None, null, SoundChoice.None, false);

        Assert.NotNull(announced);
        Assert.Contains("12:45", announced);
    }

    [Fact]
    public void ApplyAlarmEdit_clears_a_whitespace_label_to_null()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AlarmLabel = "Tea"; vm.AddAlarmCommand.Execute(null);
        var id = vm.Alarms[0].Item.Id;

        vm.ApplyAlarmEdit(id, 11, 0, Weekdays.None, "   ", SoundChoice.None, false);

        Assert.Null(vm.Alarms[0].Item.Label);
    }

    [Fact]
    public void DeleteAlarm_disarms_immediately_but_does_not_commit_yet()
    {
        var vm = New(out _, out var sched);
        vm.AlarmTimeInput = "10:00"; vm.AddAlarmCommand.Execute(null);
        var committed = 0; vm.AlarmsChanged += (_, _) => committed++;

        vm.DeleteAlarmCommand.Execute(vm.Alarms[0]);

        Assert.Empty(vm.Alarms);                 // row gone
        Assert.Empty(sched.Alarms);              // disarmed -> cannot fire during the undo window
        Assert.True(vm.HasPendingDelete);
        Assert.NotNull(vm.PendingDeleteLabel);
        Assert.Equal(0, committed);              // not persisted yet (disk still has it)
    }

    [Fact]
    public void UndoDelete_re_arms_the_alarm_with_its_original_id()
    {
        var vm = New(out _, out var sched);
        vm.AlarmTimeInput = "10:00"; vm.AlarmLabel = "Tea"; vm.AddAlarmCommand.Execute(null);
        var id = vm.Alarms[0].Item.Id;

        vm.DeleteAlarmCommand.Execute(vm.Alarms[0]);
        vm.UndoDeleteCommand.Execute(null);

        var row = Assert.Single(vm.Alarms);
        Assert.Equal(id, row.Item.Id);
        Assert.Equal("Tea", row.DisplayLabel);
        Assert.Single(sched.Alarms);
        Assert.False(vm.HasPendingDelete);
    }

    [Fact]
    public void CommitPendingDelete_persists_the_removal()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AddAlarmCommand.Execute(null);
        vm.DeleteAlarmCommand.Execute(vm.Alarms[0]);
        var committed = 0; vm.AlarmsChanged += (_, _) => committed++;

        vm.CommitPendingDelete();

        Assert.False(vm.HasPendingDelete);
        Assert.Equal(1, committed);              // now it leaves disk
    }

    [Fact]
    public void Deleting_a_second_alarm_commits_the_first_pending_delete()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AddAlarmCommand.Execute(null);
        vm.AlarmTimeInput = "11:00"; vm.AddAlarmCommand.Execute(null);

        vm.DeleteAlarmCommand.Execute(vm.Alarms[0]);   // pending: 10:00
        var committed = 0; vm.AlarmsChanged += (_, _) => committed++;
        vm.DeleteAlarmCommand.Execute(vm.Alarms[0]);   // now 11:00; should commit the 10:00 delete first

        Assert.Equal(1, committed);                    // the first delete committed
        Assert.True(vm.HasPendingDelete);              // the second is now pending
    }

    [Fact]
    public void RefreshAll_drops_a_row_for_an_alarm_that_fired()
    {
        var vm = New(out var clock, out var sched);
        vm.AlarmTimeInput = "09:30"; vm.AddAlarmCommand.Execute(null);   // clock is 09:00
        Assert.Single(vm.Alarms);

        clock.Advance(TimeSpan.FromMinutes(31));   // past 09:30, within grace
        sched.Tick();                              // fires + removes the alarm
        vm.RefreshAll();

        Assert.Empty(vm.Alarms);
        Assert.True(vm.IsDayEmpty);
    }

    [Fact]
    public void RefreshAll_does_not_rebuild_when_the_alarm_set_is_unchanged()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "16:00"; vm.AddAlarmCommand.Execute(null);
        var rowInstance = vm.Alarms[0];

        vm.RefreshAll();   // nothing changed

        Assert.Same(rowInstance, vm.Alarms[0]);   // same VM kept -> focus/announcements not disrupted
    }

    [Fact]
    public void AddMissed_builds_a_quiet_dismissible_note_and_announces()
    {
        var vm = New(out var clock, out var sched);
        var item = sched.ArmClockAlarm(clock.Now.AddMinutes(-30), "Lunch", SoundChoice.Bell);
        sched.RemoveAlarm(item);                    // simulate the scheduler having expired it
        string? announced = null; vm.Announcement += (_, m) => announced = m;

        vm.AddMissed(item);

        Assert.NotNull(vm.MissedNote);
        Assert.Contains("Lunch", vm.MissedNote);
        Assert.Contains("Missed while away", vm.MissedNote);
        Assert.NotNull(announced);

        vm.DismissMissedNoteCommand.Execute(null);
        Assert.Null(vm.MissedNote);
    }

    // ── CancelTimer: instant removal + undo ──────────────────────────────

    [Fact]
    public void CancelTimer_removes_the_row_immediately_without_waiting_for_a_tick()
    {
        var vm = New(out _, out _);
        vm.StartPresetCommand.Execute(30);
        var row = vm.Running[0];

        vm.CancelTimerCommand.Execute(row);

        Assert.Empty(vm.Running);              // gone at once, not deferred to the next RefreshAll
    }

    [Fact]
    public void CancelTimer_sets_HasPendingDelete_and_shows_a_label()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00"; vm.Label = "tea";
        vm.StartCustomCommand.Execute(null);
        var row = vm.Running[0];

        vm.CancelTimerCommand.Execute(row);

        Assert.True(vm.HasPendingDelete);
        Assert.NotNull(vm.PendingDeleteLabel);
        Assert.Contains("Tea", vm.PendingDeleteLabel);   // label is capitalized
    }

    [Fact]
    public void CancelTimer_announces_the_cancellation()
    {
        var vm = New(out _, out _);
        vm.StartPresetCommand.Execute(15);
        var row = vm.Running[0];
        string? announced = null; vm.Announcement += (_, m) => announced = m;

        vm.CancelTimerCommand.Execute(row);

        Assert.NotNull(announced);
        Assert.Contains("cancelled", announced, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UndoDelete_after_timer_cancel_restores_a_running_countdown_with_the_captured_remaining()
    {
        var vm = New(out var clock, out var sched);
        vm.StartPresetCommand.Execute(30);           // 30-minute countdown
        clock.Advance(TimeSpan.FromMinutes(10));     // 10 minutes elapsed → 20 remaining
        var row = vm.Running[0];

        vm.CancelTimerCommand.Execute(row);
        Assert.Empty(vm.Running);                    // confirm row gone

        vm.UndoDeleteCommand.Execute(null);

        Assert.Single(vm.Running);
        Assert.False(vm.HasPendingDelete);
        Assert.Null(vm.PendingDeleteLabel);

        // The restored timer should have ~20 minutes remaining (captured at cancel time)
        var restoredRemaining = sched.Remaining(vm.Running[0].Item);
        Assert.True(restoredRemaining >= TimeSpan.FromMinutes(19) && restoredRemaining <= TimeSpan.FromMinutes(20),
            $"Expected ~20 min remaining, got {restoredRemaining}");
    }

    [Fact]
    public void UndoDelete_after_timer_cancel_announces_restoration()
    {
        var vm = New(out _, out _);
        vm.StartPresetCommand.Execute(5);
        var row = vm.Running[0];
        vm.CancelTimerCommand.Execute(row);
        string? announced = null; vm.Announcement += (_, m) => announced = m;

        vm.UndoDeleteCommand.Execute(null);

        Assert.NotNull(announced);
        Assert.Contains("restored", announced, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CommitPendingDelete_after_timer_cancel_does_not_fire_AlarmsChanged()
    {
        var vm = New(out _, out _);
        vm.StartPresetCommand.Execute(5);
        vm.CancelTimerCommand.Execute(vm.Running[0]);
        var alarmsChangedCount = 0; vm.AlarmsChanged += (_, _) => alarmsChangedCount++;

        vm.CommitPendingDelete();

        Assert.Equal(0, alarmsChangedCount);   // cancelled countdowns are not persisted
        Assert.False(vm.HasPendingDelete);
    }

    [Fact]
    public void Existing_alarm_undo_still_works_after_timer_cancel_changes()
    {
        var vm = New(out _, out var sched);
        vm.AlarmTimeInput = "10:00"; vm.AlarmLabel = "Stand-up";
        vm.AddAlarmCommand.Execute(null);
        var id = vm.Alarms[0].Item.Id;

        vm.DeleteAlarmCommand.Execute(vm.Alarms[0]);
        vm.UndoDeleteCommand.Execute(null);

        var row = Assert.Single(vm.Alarms);
        Assert.Equal(id, row.Item.Id);               // same identity preserved
        Assert.Equal("Stand-up", row.DisplayLabel);
        Assert.Single(sched.Alarms);
        Assert.False(vm.HasPendingDelete);
    }

    // ── Quick-timer "next" highlight: the soonest active timer is flagged ──

    [Fact]
    public void The_soonest_finishing_running_timer_sorts_to_the_top_and_is_marked_next()
    {
        var vm = New(out _, out _);
        vm.StartPresetCommand.Execute(60);   // added first, finishes later
        vm.StartPresetCommand.Execute(15);   // added second, finishes first

        Assert.True(vm.Running[0].IsNext);    // the 15 sorts above the 60 and is next
        Assert.False(vm.Running[1].IsNext);
    }

    [Fact]
    public void Cancelling_the_next_timer_moves_the_highlight_to_the_remaining_timer()
    {
        var vm = New(out _, out _);
        vm.StartPresetCommand.Execute(60);
        vm.StartPresetCommand.Execute(15);

        vm.CancelTimerCommand.Execute(vm.Running[0]);    // the soonest (15) now sorts to the top — cancel it

        Assert.True(Assert.Single(vm.Running).IsNext);   // the 60 is now next
    }

    [Fact]
    public void A_paused_timer_parks_below_the_active_timer_and_is_not_marked_next()
    {
        var vm = New(out _, out _);
        vm.StartPresetCommand.Execute(15);   // soonest, but about to pause
        vm.StartPresetCommand.Execute(60);   // keeps running

        vm.Running[0].PauseResumeCommand.Execute(null);   // pause the 15 (currently on top)
        vm.RefreshAll();                                   // order + "next" re-evaluated on the tick

        Assert.Equal(TimerState.Running, vm.Running[0].Item.State);   // active timer rises to the top...
        Assert.True(vm.Running[0].IsNext);                            // ...and is marked next
        Assert.Equal(TimerState.Paused, vm.Running[1].Item.State);    // paused timer parks below
        Assert.False(vm.Running[1].IsNext);                           // and isn't next
    }

    // ── Quick-timer ordering: soonest-finishing on top, paused parked below ──

    [Fact]
    public void Running_timers_are_ordered_soonest_first()
    {
        var vm = New(out _, out var sched);
        vm.StartPresetCommand.Execute(60);   // added first, finishes last
        vm.StartPresetCommand.Execute(15);   // added second, finishes first
        vm.StartPresetCommand.Execute(30);

        Assert.Collection(vm.Running,
            r => Assert.Equal(TimeSpan.FromMinutes(15), sched.Remaining(r.Item)),
            r => Assert.Equal(TimeSpan.FromMinutes(30), sched.Remaining(r.Item)),
            r => Assert.Equal(TimeSpan.FromMinutes(60), sched.Remaining(r.Item)));
    }

    // ── Label auto-capitalization ─────────────────────────────────────────

    [Fact]
    public void StartCustom_capitalizes_first_letter_of_timer_label()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00"; vm.Label = "tea";
        vm.StartCustomCommand.Execute(null);
        Assert.Equal("Tea", vm.Running[0].Label);
    }

    [Fact]
    public void StartCustom_capitalizes_single_char_timer_label()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00"; vm.Label = "a";
        vm.StartCustomCommand.Execute(null);
        Assert.Equal("A", vm.Running[0].Label);
    }

    [Fact]
    public void StartCustom_leaves_rest_of_timer_label_unchanged()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00"; vm.Label = "morning tea";
        vm.StartCustomCommand.Execute(null);
        Assert.Equal("Morning tea", vm.Running[0].Label);
    }

    [Fact]
    public void StartCustom_empty_timer_label_stays_null()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00"; vm.Label = "";
        vm.StartCustomCommand.Execute(null);
        Assert.Null(vm.Running[0].Label);
    }

    // ── Label clears after starting, so it can't carry into the next timer ──

    [Fact]
    public void StartPreset_clears_the_label_after_starting()
    {
        var vm = New(out _, out _);
        vm.Label = "Tea";
        vm.StartPresetCommand.Execute(15);
        Assert.Equal("", vm.Label);
    }

    [Fact]
    public void StartCustom_clears_the_label_after_starting()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00"; vm.Label = "Tea";
        vm.StartCustomCommand.Execute(null);
        Assert.Equal("", vm.Label);
    }

    [Fact]
    public void AddAlarm_capitalizes_first_letter_of_alarm_label()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AlarmLabel = "tea";
        vm.AddAlarmCommand.Execute(null);
        Assert.Equal("Tea", vm.Alarms[0].DisplayLabel);
    }

    [Fact]
    public void AddAlarm_capitalizes_single_char_alarm_label()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AlarmLabel = "a";
        vm.AddAlarmCommand.Execute(null);
        Assert.Equal("A", vm.Alarms[0].DisplayLabel);
    }

    [Fact]
    public void AddAlarm_leaves_rest_of_alarm_label_unchanged()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AlarmLabel = "morning tea";
        vm.AddAlarmCommand.Execute(null);
        Assert.Equal("Morning tea", vm.Alarms[0].DisplayLabel);
    }

    [Fact]
    public void AddAlarm_empty_alarm_label_stays_null()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AlarmLabel = "";
        vm.AddAlarmCommand.Execute(null);
        Assert.Null(vm.Alarms[0].Item.Label);
    }

    [Fact]
    public void RefreshAll_reorders_when_a_recurring_alarm_fires_and_advances()
    {
        var vm = New(out var clock, out var sched);     // Thu 2026-01-01 09:00
        sched.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, "A", SoundChoice.None);
        sched.ArmRecurringAlarm(11, 0, RecurrenceRules.AllDays, "B", SoundChoice.None);
        vm.RefreshAll();
        Assert.Equal("A", vm.Alarms[0].DisplayLabel);   // 10:00 is the next to fire

        clock.Advance(TimeSpan.FromMinutes(61));          // 10:01 — A fires and advances to tomorrow 10:00
        sched.Tick();
        vm.RefreshAll();

        Assert.Equal("B", vm.Alarms[0].DisplayLabel);     // 11:00 today is now next
        Assert.True(vm.Alarms[0].IsNext);
        Assert.Equal("A", vm.Alarms[1].DisplayLabel);     // A (tomorrow) sorts after
    }

    [Fact]
    public void AddAlarm_with_a_weekdays_repeat_creates_a_recurring_alarm()
    {
        var vm = New(out _, out var sched);          // Thu 2026-01-01 09:00
        vm.AlarmTimeInput = "07:00";
        vm.AlarmLabel = "Stand-up";
        vm.AlarmRepeat = RepeatOption.Weekdays;

        vm.AddAlarmCommand.Execute(null);

        var row = Assert.Single(vm.Alarms);
        Assert.Equal("Weekdays", row.CadenceText);
        Assert.Equal(TriggerType.Recurring, row.Item.TriggerType);
        var weekdays = Weekdays.Mon | Weekdays.Tue | Weekdays.Wed | Weekdays.Thu | Weekdays.Fri;
        Assert.Equal(weekdays, row.Item.RecurringDays);
        Assert.Equal(weekdays, Assert.Single(sched.Alarms).RecurringDays);
    }

    [Fact]
    public void AddAlarm_with_a_custom_day_set_creates_a_recurring_alarm()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "08:00";
        vm.AlarmRepeat = RepeatOption.Custom;
        foreach (var t in vm.AlarmDayToggles)
            t.IsSelected = t.Flag is Weekdays.Mon or Weekdays.Wed or Weekdays.Fri;

        vm.AddAlarmCommand.Execute(null);

        Assert.Equal("Mon Wed Fri", Assert.Single(vm.Alarms).CadenceText);
    }

    [Fact]
    public void AddAlarm_with_once_still_creates_a_one_shot()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:30";
        vm.AlarmRepeat = RepeatOption.Once;   // the default
        vm.AddAlarmCommand.Execute(null);

        var row = Assert.Single(vm.Alarms);
        Assert.Equal(TriggerType.ClockTime, row.Item.TriggerType);
        Assert.Null(row.Item.RecurringDays);
    }

    [Fact]
    public void ShowCustomDays_tracks_the_repeat_choice()
    {
        var vm = New(out _, out _);
        Assert.False(vm.ShowCustomDays);
        vm.AlarmRepeat = RepeatOption.Custom;
        Assert.True(vm.ShowCustomDays);
        vm.AlarmRepeat = RepeatOption.Daily;
        Assert.False(vm.ShowCustomDays);
    }

    [Fact]
    public void AddAlarm_resets_the_repeat_editor_after_adding()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "07:00";
        vm.AlarmRepeat = RepeatOption.Custom;
        vm.AlarmDayToggles[0].IsSelected = true;

        vm.AddAlarmCommand.Execute(null);

        Assert.Equal(RepeatOption.Once, vm.AlarmRepeat);
        Assert.All(vm.AlarmDayToggles, t => Assert.False(t.IsSelected));
    }

    [Fact]
    public void ApplyAlarmEdit_can_turn_a_one_shot_into_a_recurring_alarm()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AddAlarmCommand.Execute(null);
        var id = vm.Alarms[0].Item.Id;
        var weekdays = Weekdays.Mon | Weekdays.Tue | Weekdays.Wed | Weekdays.Thu | Weekdays.Fri;

        vm.ApplyAlarmEdit(id, 7, 0, weekdays, "Stand-up", SoundChoice.None, false);

        var row = Assert.Single(vm.Alarms);
        Assert.Equal(id, row.Item.Id);                       // same identity
        Assert.Equal(TriggerType.Recurring, row.Item.TriggerType);
        Assert.Equal("Weekdays", row.CadenceText);
    }

    [Fact]
    public void UndoDelete_restores_a_recurring_alarm_as_recurring()
    {
        var vm = New(out _, out var sched);
        vm.AlarmTimeInput = "07:00"; vm.AlarmLabel = "Stand-up";
        vm.AlarmRepeat = RepeatOption.Weekdays;
        vm.AddAlarmCommand.Execute(null);
        var id = vm.Alarms[0].Item.Id;

        vm.DeleteAlarmCommand.Execute(vm.Alarms[0]);
        vm.UndoDeleteCommand.Execute(null);

        var row = Assert.Single(vm.Alarms);
        Assert.Equal(id, row.Item.Id);
        Assert.Equal(TriggerType.Recurring, row.Item.TriggerType);   // not downgraded to a one-shot
        Assert.Equal("Weekdays", row.CadenceText);
        Assert.Single(sched.Alarms);
    }

    [Fact]
    public void CommitPendingDelete_persists_a_recurring_alarm_removal()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "07:00"; vm.AlarmRepeat = RepeatOption.Daily;
        vm.AddAlarmCommand.Execute(null);
        vm.DeleteAlarmCommand.Execute(vm.Alarms[0]);
        var committed = 0; vm.AlarmsChanged += (_, _) => committed++;

        vm.CommitPendingDelete();

        Assert.Equal(1, committed);   // a committed recurring delete must reach disk
    }

    [Fact]
    public void ApplyAlarmEdit_carries_the_warn_before_flag()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AddAlarmCommand.Execute(null);
        var id = vm.Alarms[0].Item.Id;

        vm.ApplyAlarmEdit(id, 11, 0, Weekdays.None, "Tea", SoundChoice.Bell, warnBefore: true);

        Assert.True(vm.Alarms[0].Item.WarnBefore);
    }

    [Fact]
    public void AddAlarm_with_warning_on_arms_an_alarm_that_warns_before()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00";
        vm.AlarmWarnBefore = true;
        vm.AddAlarmCommand.Execute(null);
        Assert.True(vm.Alarms[0].Item.WarnBefore);
    }

    [Fact]
    public void AddAlarm_resets_the_warning_toggle_after_adding()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00";
        vm.AlarmWarnBefore = true;
        vm.AddAlarmCommand.Execute(null);
        Assert.False(vm.AlarmWarnBefore);   // editor cleared, like the rest
    }

    [Fact]
    public void UndoDelete_restores_an_alarm_with_its_warning_setting_intact()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AlarmWarnBefore = true;
        vm.AddAlarmCommand.Execute(null);

        vm.DeleteAlarmCommand.Execute(vm.Alarms[0]);
        vm.UndoDeleteCommand.Execute(null);

        Assert.True(Assert.Single(vm.Alarms).Item.WarnBefore);
    }

    [Fact]
    public void AddAlarm_with_warning_on_and_recurring_days_arms_a_recurring_alarm_that_warns_before()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "07:00";
        vm.AlarmRepeat = RepeatOption.Weekdays;
        vm.AlarmWarnBefore = true;
        vm.AddAlarmCommand.Execute(null);

        var row = Assert.Single(vm.Alarms);
        Assert.Equal(TriggerType.Recurring, row.Item.TriggerType);
        Assert.True(row.Item.WarnBefore);
    }

    [Fact]
    public void ToggleAlarm_off_persists_and_announces()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AddAlarmCommand.Execute(null);
        var changed = 0; vm.AlarmsChanged += (_, _) => changed++;
        string? announced = null; vm.Announcement += (_, m) => announced = m;

        vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);

        Assert.False(vm.Alarms[0].Item.IsEnabled);
        Assert.Equal(1, changed);                       // persisted via the event
        Assert.NotNull(announced);
        Assert.Contains("10:00", announced);
        Assert.Contains("off", announced);
    }

    [Fact]
    public void ToggleAlarm_back_on_re_enables_and_announces_on()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AddAlarmCommand.Execute(null);
        vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);    // off
        string? announced = null; vm.Announcement += (_, m) => announced = m;

        vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);    // on again

        Assert.True(vm.Alarms[0].Item.IsEnabled);
        Assert.NotNull(announced);
        Assert.Contains("on", announced);
    }

    [Fact]
    public void ToggleAlarm_with_null_row_does_nothing()
    {
        var vm = New(out _, out _);
        var changed = 0; vm.AlarmsChanged += (_, _) => changed++;
        vm.ToggleAlarmCommand.Execute(null);
        Assert.Equal(0, changed);
    }

    [Fact]
    public void ToggleAlarm_commits_an_outstanding_pending_delete_first()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AddAlarmCommand.Execute(null);
        vm.AlarmTimeInput = "11:00"; vm.AddAlarmCommand.Execute(null);
        vm.DeleteAlarmCommand.Execute(vm.Alarms[0]);    // 10:00 now pending-delete

        vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);    // toggling 11:00 settles the pending delete

        Assert.False(vm.HasPendingDelete);
        Assert.False(vm.Alarms.Single().Item.IsEnabled);   // only 11:00 remains, now off
    }

    [Fact]
    public void Undo_restores_a_disabled_alarm_still_disabled()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AddAlarmCommand.Execute(null);
        vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);    // off
        vm.DeleteAlarmCommand.Execute(vm.Alarms[0]);    // delete the disabled alarm
        vm.UndoDeleteCommand.Execute(null);             // undo

        Assert.False(Assert.Single(vm.Alarms).Item.IsEnabled);   // comes back still off
    }

    [Fact]
    public void A_disabled_alarm_parks_below_the_enabled_ones()
    {
        var vm = New(out _, out _);                       // 09:00
        vm.AlarmTimeInput = "11:00"; vm.AddAlarmCommand.Execute(null);
        vm.AlarmTimeInput = "16:00"; vm.AddAlarmCommand.Execute(null);

        vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);      // turn off the 11:00 (currently on top)

        Assert.Equal("16:00", vm.Alarms[0].TimeText);     // enabled 16:00 rises to the top...
        Assert.True(vm.Alarms[0].IsNext);                 // ...and is next
        Assert.Equal("11:00", vm.Alarms[1].TimeText);     // disabled 11:00 parks below
        Assert.False(vm.Alarms[1].IsEnabled);
        Assert.False(vm.Alarms[1].IsNext);
    }

    [Fact]
    public void With_every_alarm_off_none_is_marked_next()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "11:00"; vm.AddAlarmCommand.Execute(null);

        vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);      // the only alarm, now off

        var row = Assert.Single(vm.Alarms);
        Assert.False(row.IsEnabled);
        Assert.False(row.IsNext);                          // nothing is "next" when all are off
    }

    [Fact]
    public void Re_enabling_a_recurring_alarm_returns_it_to_the_active_group()
    {
        var vm = New(out var clock, out _);               // Thu 2026-01-01 09:00
        vm.AlarmTimeInput = "10:00"; vm.AlarmRepeat = RepeatOption.Daily;
        vm.AddAlarmCommand.Execute(null);

        vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);      // off
        Assert.False(vm.Alarms[0].Item.IsEnabled);

        clock.Advance(TimeSpan.FromDays(1));               // Fri 09:00 — its frozen 10:00 occurrence has passed
        vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);      // back on

        var row = Assert.Single(vm.Alarms);
        Assert.True(row.Item.IsEnabled);
        Assert.True(row.IsNext);                           // active again
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero), row.Item.EndsAt);   // rolled to Fri 10:00
    }

    [Fact]
    public void ClearAllAlarms_empties_every_list_and_disarms_the_scheduler()
    {
        var vm = New(out _, out var sched);
        vm.StartPresetCommand.Execute(30);                  // a running countdown
        vm.AlarmTimeInput = "14:30";
        vm.AddAlarmCommand.Execute(null);                   // a clock-time alarm
        vm.MissedNote = "Missed while away: Lunch · 11:30";

        vm.ClearAllAlarms();

        Assert.Empty(vm.Running);
        Assert.Empty(vm.Alarms);
        Assert.Null(vm.MissedNote);
        Assert.Empty(sched.Running);
        Assert.Empty(sched.Alarms);
    }

    [Fact]
    public void ClearAllAlarms_persists_once()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "14:30";
        vm.AddAlarmCommand.Execute(null);
        var saves = 0;
        vm.AlarmsChanged += (_, _) => saves++;

        vm.ClearAllAlarms();

        Assert.Equal(1, saves);
    }

    [Fact]
    public void ClearAllAlarms_settles_an_outstanding_undo_first()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "14:30";
        vm.AddAlarmCommand.Execute(null);
        vm.DeleteAlarmCommand.Execute(vm.Alarms.Count > 0 ? vm.Alarms[0] : null);

        vm.ClearAllAlarms();

        Assert.False(vm.HasPendingDelete);
        Assert.Null(vm.PendingDeleteLabel);
    }

    [Fact]
    public void ClearAllAlarms_disarms_what_the_agenda_has_not_caught_up_with()
    {
        var vm = New(out var clock, out var sched);
        // Armed straight on the scheduler: the agenda has never been rebuilt, so vm.Alarms is empty.
        sched.ArmClockAlarm(clock.Now.AddHours(2), "Ghost", SoundChoice.None, Guid.NewGuid());
        Assert.Empty(vm.Alarms);

        vm.ClearAllAlarms();

        Assert.Empty(sched.Alarms);   // the wipe follows the scheduler, not the view
    }

    [Fact]
    public void HasAnythingToClear_is_false_when_nothing_is_armed_or_missed()
    {
        var vm = New(out _, out _);
        Assert.False(vm.HasAnythingToClear);
    }

    [Fact]
    public void HasAnythingToClear_is_true_when_only_a_missed_note_remains()
    {
        // A one-shot alarm that expired while away leaves no armed alarms — only the missed note.
        var vm = New(out var clock, out var sched);
        var item = sched.ArmClockAlarm(clock.Now.AddMinutes(-30), "Lunch", SoundChoice.Bell);
        sched.RemoveAlarm(item);   // simulate the scheduler having expired and disarmed it

        vm.AddMissed(item);

        Assert.Empty(sched.Alarms);
        Assert.Empty(sched.Running);
        Assert.True(vm.HasAnythingToClear);
    }

    [Fact]
    public void HasAnythingToClear_is_true_when_an_alarm_is_armed()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "14:30";
        vm.AddAlarmCommand.Execute(null);

        Assert.True(vm.HasAnythingToClear);
    }

    [Fact]
    public void HasAnythingToClear_is_true_when_a_timer_is_running()
    {
        var vm = New(out _, out _);
        vm.StartPresetCommand.Execute(15);

        Assert.True(vm.HasAnythingToClear);
    }

    [Fact]
    public void ClearAllAlarms_asks_for_open_popups_to_close_before_disarming()
    {
        var vm = New(out _, out var sched);
        vm.AlarmTimeInput = "14:30";
        vm.AddAlarmCommand.Execute(null);
        var requests = 0;
        var armedWhenAsked = -1;
        vm.ClosePopupsRequested += (_, _) => { requests++; armedWhenAsked = sched.Alarms.Count; };

        vm.ClearAllAlarms();

        Assert.Equal(1, requests);
        Assert.Equal(1, armedWhenAsked);   // asked before anything was disarmed
    }

    // ── Tab selection and running-timer strip ────────────────────────────────

    [Fact]
    public void The_window_opens_on_quick_timers_by_default()
    {
        Assert.Equal(0, New(out _, out _).SelectedTabIndex);
    }

    [Fact]
    public void AdvanceTab_moves_from_the_first_tab_to_the_second()
    {
        var vm = New(out _, out _);
        vm.AdvanceTabCommand.Execute(null);
        Assert.Equal(1, vm.SelectedTabIndex);
    }

    [Fact]
    public void AdvanceTab_wraps_from_the_last_tab_back_to_the_first()
    {
        var vm = New(out _, out _);
        vm.SelectedTabIndex = AppSettings.TabCount - 1;
        vm.AdvanceTabCommand.Execute(null);
        Assert.Equal(0, vm.SelectedTabIndex);
    }

    [Fact]
    public void Strip_is_empty_when_nothing_is_counting_down()
    {
        var vm = New(out _, out _);
        Assert.Null(vm.StripTimer);
        Assert.False(vm.ShowStrip);
        Assert.Null(vm.StripExtraText);
    }

    [Fact]
    public void Strip_shows_the_countdown_that_finishes_soonest()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "30:00"; vm.Label = "long";  vm.StartCustomCommand.Execute(null);
        vm.CustomInput = "5:00";  vm.Label = "short"; vm.StartCustomCommand.Execute(null);
        vm.SelectedTabIndex = 1;   // the strip is for the Schedule tab; Quick timers has the hero

        Assert.True(vm.ShowStrip);
        Assert.Equal("Short", vm.StripTimer!.Label);
        Assert.Equal("+1 more", vm.StripExtraText);   // counts what the strip is NOT showing
    }

    // The hero on Quick timers shows the same countdown as the strip, so showing both would repeat
    // the value on screen and report one piece of state twice to a screen reader.
    [Fact]
    public void Strip_yields_to_the_hero_on_the_quick_timers_tab()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00"; vm.Label = "short"; vm.StartCustomCommand.Execute(null);

        vm.SelectedTabIndex = 0;
        Assert.True(vm.ShowHero);
        Assert.False(vm.ShowStrip);

        vm.SelectedTabIndex = 1;
        Assert.False(vm.ShowHero);
        Assert.True(vm.ShowStrip);
    }

    [Fact]
    public void Neither_hero_nor_strip_shows_without_a_countdown()
    {
        var vm = New(out _, out _);
        vm.SelectedTabIndex = 0;
        Assert.False(vm.ShowHero);
        Assert.False(vm.ShowStrip);
    }

    [Fact]
    public void Strip_shows_a_paused_timer_when_none_are_active()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00"; vm.StartCustomCommand.Execute(null);
        vm.Running[0].PauseResumeCommand.Execute(null);
        vm.RefreshAll();

        Assert.NotNull(vm.StripTimer);          // an IsNext-based strip would go blank here
        Assert.True(vm.StripTimer!.IsPaused);
    }

    [Fact]
    public void Strip_extra_text_is_null_for_a_single_timer()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00"; vm.StartCustomCommand.Execute(null);
        Assert.Null(vm.StripExtraText);
    }

    [Fact]
    public void Cancelling_the_shown_timer_moves_the_strip_to_the_next_one()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "30:00"; vm.Label = "long";  vm.StartCustomCommand.Execute(null);
        vm.CustomInput = "5:00";  vm.Label = "short"; vm.StartCustomCommand.Execute(null);

        vm.CancelTimerCommand.Execute(vm.StripTimer);

        Assert.Equal("Long", vm.StripTimer!.Label);   // no tick needed
        Assert.Null(vm.StripExtraText);
    }

    [Fact]
    public void ClearAllAlarms_empties_the_strip_without_waiting_for_a_tick()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00"; vm.StartCustomCommand.Execute(null);
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.ClearAllAlarms();

        Assert.Null(vm.StripTimer);
        Assert.Contains(nameof(MainViewModel.StripTimer), raised);
    }

    // ShowHero and ShowStrip are expression-bodied (computed from StripTimer + SelectedTabIndex), so
    // they always read live-correct regardless of whether a notification ever fired. Asserting the
    // property values alone (as above) can't catch a deleted OnPropertyChanged call - only watching
    // the PropertyChanged event itself can.
    [Fact]
    public void Switching_tabs_raises_notifications_for_hero_and_strip()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00"; vm.StartCustomCommand.Execute(null);
        vm.SelectedTabIndex = 0;

        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.SelectedTabIndex = 1;

        Assert.Contains(nameof(MainViewModel.ShowHero), raised);
        Assert.Contains(nameof(MainViewModel.ShowStrip), raised);
    }

    [Fact]
    public void Starting_a_countdown_raises_notifications_for_hero_and_strip()
    {
        var vm = New(out _, out _);
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.CustomInput = "5:00"; vm.StartCustomCommand.Execute(null);

        Assert.Contains(nameof(MainViewModel.ShowHero), raised);
        Assert.Contains(nameof(MainViewModel.ShowStrip), raised);
    }

    [Fact]
    public void Cancelling_the_last_countdown_raises_notifications_for_hero_and_strip()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00"; vm.StartCustomCommand.Execute(null);

        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.CancelTimerCommand.Execute(vm.StripTimer);

        Assert.Contains(nameof(MainViewModel.ShowHero), raised);
        Assert.Contains(nameof(MainViewModel.ShowStrip), raised);
    }

    // The bar must not linger on Schedule once the last quick timer stops. Every other cancellation
    // test above runs on the Quick timers tab, where ShowStrip is false either way and so cannot tell
    // a working teardown from a broken one. This one stays on Schedule across the cancel: the flag has
    // to go false AND say so, because a computed property reads correct whether or not the UI is told.
    [Fact]
    public void Cancelling_the_last_countdown_hides_the_strip_while_the_schedule_tab_is_showing()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00"; vm.StartCustomCommand.Execute(null);
        vm.SelectedTabIndex = 1;
        Assert.True(vm.ShowStrip);

        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.CancelTimerCommand.Execute(vm.StripTimer);

        Assert.False(vm.ShowStrip);
        Assert.Null(vm.StripTimer);
        Assert.Contains(nameof(MainViewModel.ShowStrip), raised);
    }

    // Same teardown by the other route: the timer leaves the scheduler behind the view model's back
    // (a completion card dismissed, or the tray), and RefreshAll — what the tick loop calls — is what
    // reaps the row. The strip must go with it rather than surviving until the next tab switch.
    [Fact]
    public void A_timer_reaped_by_the_tick_loop_hides_the_strip_while_the_schedule_tab_is_showing()
    {
        var vm = New(out _, out var sched);
        vm.CustomInput = "5:00"; vm.StartCustomCommand.Execute(null);
        vm.SelectedTabIndex = 1;
        Assert.True(vm.ShowStrip);

        sched.Cancel(vm.StripTimer!.Item);   // gone from the scheduler, row not yet reaped
        vm.RefreshAll();

        Assert.Null(vm.StripTimer);
        Assert.False(vm.ShowStrip);
    }

    [Fact]
    public void The_strip_stays_hidden_on_the_schedule_tab_when_nothing_is_running()
    {
        var vm = New(out _, out _);
        vm.SelectedTabIndex = 1;

        Assert.False(vm.ShowStrip);
        Assert.False(vm.ShowHero);
    }

    // The hero card and the Running list render the SAME collection, so the countdown was on screen
    // twice on Quick timers - the exact duplication that justified collapsing the strip there.
    // IsCountdownInHero is how the one row the hero borrows from drops its own numerals. The ROW
    // stays: its buttons, label, finish time, sound tag and "next" dot are all things the hero does
    // not carry, and a row that vanishes takes keyboard focus with it. Only the numerals go, and the
    // ItemsSource is never re-projected (which would rebuild every container on every one-second tick).
    [Fact]
    public void Only_the_timer_the_hero_shows_drops_its_own_countdown()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "30:00"; vm.Label = "long";  vm.StartCustomCommand.Execute(null);
        vm.CustomInput = "5:00";  vm.Label = "short"; vm.StartCustomCommand.Execute(null);

        Assert.Same(vm.StripTimer, vm.Running[0]);
        Assert.True(vm.Running[0].IsCountdownInHero);
        Assert.False(vm.Running[1].IsCountdownInHero);
        Assert.Equal(2, vm.Running.Count);   // both rows are still there - only a TextBlock hides
    }

    [Fact]
    public void Cancelling_the_heros_timer_moves_the_countdown_mark_to_the_next_one()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "30:00"; vm.Label = "long";  vm.StartCustomCommand.Execute(null);
        vm.CustomInput = "5:00";  vm.Label = "short"; vm.StartCustomCommand.Execute(null);

        vm.CancelTimerCommand.Execute(vm.StripTimer);

        var remaining = Assert.Single(vm.Running);
        Assert.Equal("Long", remaining.Label);
        Assert.True(remaining.IsCountdownInHero);
    }

    // Resuming a paused timer that then sorts to the front is the case that made hiding the whole row
    // untenable: the row moved, and under the old design it collapsed with keyboard focus inside it.
    // SortRunning reorders with Move, so the marks have to follow the reorder, not the insertion order.
    [Fact]
    public void Resuming_a_timer_that_sorts_to_the_front_moves_the_countdown_mark_with_it()
    {
        var vm = New(out _, out _);
        vm.StartPresetCommand.Execute(5);    // soonest, so the hero borrows this one first
        vm.StartPresetCommand.Execute(60);
        var soon = vm.Running[0];
        var late = vm.Running[1];
        Assert.True(soon.IsCountdownInHero);

        soon.PauseResumeCommand.Execute(null);   // paused timers park below the active ones
        vm.RefreshAll();

        Assert.Same(late, vm.Running[0]);
        Assert.True(late.IsCountdownInHero);
        Assert.False(soon.IsCountdownInHero);

        soon.PauseResumeCommand.Execute(null);   // resumed, and it finishes soonest again
        vm.RefreshAll();

        Assert.Same(soon, vm.Running[0]);
        Assert.True(soon.IsCountdownInHero);
        Assert.False(late.IsCountdownInHero);
        Assert.Equal(2, vm.Running.Count);       // reordered in place: neither row was ever removed
    }

    [Fact]
    public void No_row_drops_its_countdown_once_nothing_is_running()
    {
        var vm = New(out _, out _);
        vm.CustomInput = "5:00"; vm.StartCustomCommand.Execute(null);
        var only = vm.Running[0];
        Assert.True(only.IsCountdownInHero);

        vm.CancelTimerCommand.Execute(only);

        Assert.Null(vm.StripTimer);
        Assert.Empty(vm.Running);
        Assert.False(only.IsCountdownInHero);   // the mark leaves with the row, not after it
    }

    // --- ReplaceAllAlarms (import) ---------------------------------------------------------------

    private static AlarmRecord ImportedAlarm(string? label = null) => new()
    {
        Id = Guid.NewGuid(),
        FireAt = new DateTime(2026, 1, 1, 18, 0, 0, DateTimeKind.Local),
        Label = label,
        Sound = SoundChoice.None,
        Enabled = true,
    };

    private static RecurringAlarmRecord ImportedRecurring(int hour, string? label = null) => new()
    {
        Id = Guid.NewGuid(),
        Hour = hour,
        Minute = 0,
        Days = Weekdays.Mon,
        Label = label,
        Sound = SoundChoice.None,
        NextFireAt = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Local),
        Enabled = true,
    };

    [Fact]
    public void ReplaceAllAlarms_clears_the_scheduler_before_arming_the_imported_set()
    {
        var vm = New(out var clock, out var sched);
        sched.ArmClockAlarm(clock.Now.AddHours(1), "old", SoundChoice.None);
        vm.RefreshAll();

        vm.ReplaceAllAlarms(new[] { ImportedAlarm("new") }, Array.Empty<RecurringAlarmRecord>());

        var armed = Assert.Single(sched.Alarms);
        Assert.Equal("new", armed.Label);   // the old alarm left the scheduler, not just the view
    }

    [Fact]
    public void ReplaceAllAlarms_cancels_running_countdowns_too()
    {
        var vm = New(out _, out var sched);
        vm.CustomInput = "5:00";
        vm.StartCustomCommand.Execute(null);

        vm.ReplaceAllAlarms(Array.Empty<AlarmRecord>(), Array.Empty<RecurringAlarmRecord>());

        Assert.Empty(sched.Running);
        Assert.Empty(vm.Running);
    }

    [Fact]
    public void ReplaceAllAlarms_closes_open_popups_before_arming()
    {
        var vm = New(out _, out var sched);
        var armedWhenPopupsClosed = -1;
        vm.ClosePopupsRequested += (_, _) => armedWhenPopupsClosed = sched.Alarms.Count;

        vm.ReplaceAllAlarms(new[] { ImportedAlarm() }, Array.Empty<RecurringAlarmRecord>());

        Assert.Equal(0, armedWhenPopupsClosed);   // cards close before the imported set is armed
    }

    [Fact]
    public void ReplaceAllAlarms_raises_AlarmsChanged_once_so_the_import_is_persisted()
    {
        var vm = New(out _, out _);
        var changes = 0;
        vm.AlarmsChanged += (_, _) => changes++;

        vm.ReplaceAllAlarms(new[] { ImportedAlarm() }, Array.Empty<RecurringAlarmRecord>());

        Assert.Equal(1, changes);
    }

    [Fact]
    public void ReplaceAllAlarms_arms_both_kinds_of_record()
    {
        var vm = New(out _, out var sched);

        vm.ReplaceAllAlarms(new[] { ImportedAlarm("one-shot") }, new[] { ImportedRecurring(8, "weekly") });

        Assert.Equal(2, sched.Alarms.Count);
        Assert.Contains(sched.Alarms, a => a.Label == "one-shot");
        Assert.Contains(sched.Alarms, a => a.Label == "weekly");
    }

    [Fact]
    public void ReplaceAllAlarms_clears_the_missed_note()
    {
        var vm = New(out var clock, out var sched);
        var missed = sched.ArmClockAlarm(clock.Now.AddMinutes(1), "missed", SoundChoice.None);
        vm.AddMissed(missed);

        vm.ReplaceAllAlarms(Array.Empty<AlarmRecord>(), Array.Empty<RecurringAlarmRecord>());

        Assert.Null(vm.MissedNote);
    }

    [Fact]
    public void ReplaceAllAlarms_announces_the_count_for_a_screen_reader()
    {
        var vm = New(out _, out _);
        string? announced = null;
        vm.Announcement += (_, m) => announced = m;

        vm.ReplaceAllAlarms(new[] { ImportedAlarm() }, new[] { ImportedRecurring(8) });

        Assert.Equal("Imported 2 alarms", announced);
    }

    // ── Timetable: the Week tab's projection tracks the alarm set ──────────

    [Fact]
    public void Timetable_redraws_when_an_alarm_is_added()
    {
        var clock = new FakeClock { Now = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) };
        var scheduler = new SchedulerService(clock);
        var vm = new MainViewModel(scheduler, new FakeSoundService(), SoundChoice.None);

        Assert.True(vm.Timetable.Week.IsEmpty);

        scheduler.ArmRecurringAlarm(9, 0, Weekdays.Mon, "Class", SoundChoice.None);
        vm.RefreshAll();

        Assert.False(vm.Timetable.Week.IsEmpty);
    }

    [Fact]
    public void Timetable_empties_when_all_alarms_are_cleared()
    {
        var clock = new FakeClock { Now = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) };
        var scheduler = new SchedulerService(clock);
        var vm = new MainViewModel(scheduler, new FakeSoundService(), SoundChoice.None);
        scheduler.ArmRecurringAlarm(9, 0, Weekdays.Mon, "Class", SoundChoice.None);
        vm.RefreshAll();
        Assert.False(vm.Timetable.Week.IsEmpty);

        vm.ClearAllAlarms();

        Assert.True(vm.Timetable.Week.IsEmpty);
    }

    [Fact]
    public void Timetable_redraws_when_an_import_replaces_the_alarms()
    {
        var clock = new FakeClock { Now = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) };
        var scheduler = new SchedulerService(clock);
        var vm = new MainViewModel(scheduler, new FakeSoundService(), SoundChoice.None);

        vm.ReplaceAllAlarms(
            Array.Empty<AlarmRecord>(),
            new[]
            {
                new RecurringAlarmRecord
                {
                    Hour = 9, Minute = 0, Days = Weekdays.Mon, Label = "Imported",
                    NextFireAt = new DateTime(2026, 1, 5, 9, 0, 0),
                },
            });

        Assert.False(vm.Timetable.Week.IsEmpty);
        Assert.Equal("Imported", vm.Timetable.Week.Days.Single(d => d.Day == Weekdays.Mon).Entries.Single().Label);
    }
}
