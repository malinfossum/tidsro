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

        vm.ApplyAlarmEdit(originalId, 11, 15, "coffee", SoundChoice.Marimba);

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

        vm.ApplyAlarmEdit(id, 12, 45, null, SoundChoice.None);

        Assert.NotNull(announced);
        Assert.Contains("12:45", announced);
    }

    [Fact]
    public void ApplyAlarmEdit_clears_a_whitespace_label_to_null()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AlarmLabel = "Tea"; vm.AddAlarmCommand.Execute(null);
        var id = vm.Alarms[0].Item.Id;

        vm.ApplyAlarmEdit(id, 11, 0, "   ", SoundChoice.None);

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
}
