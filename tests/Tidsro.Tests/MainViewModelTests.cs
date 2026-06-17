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
        Assert.Equal("tea", vm.Running[0].Label);
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

        vm.AddOrSaveAlarmCommand.Execute(null);

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
        vm.AddOrSaveAlarmCommand.Execute(null);
        Assert.True(Assert.Single(vm.Alarms).IsTomorrow);
    }

    [Fact]
    public void AddAlarm_with_bad_input_shows_an_error_and_adds_nothing()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "99:99";
        vm.AddOrSaveAlarmCommand.Execute(null);
        Assert.Empty(vm.Alarms);
        Assert.NotNull(vm.AlarmError);
    }

    [Fact]
    public void Alarms_are_sorted_by_fire_time()
    {
        var vm = New(out _, out _);                         // 09:00
        vm.AlarmTimeInput = "16:00"; vm.AddOrSaveAlarmCommand.Execute(null);
        vm.AlarmTimeInput = "11:00"; vm.AddOrSaveAlarmCommand.Execute(null);
        vm.AlarmTimeInput = "13:30"; vm.AddOrSaveAlarmCommand.Execute(null);

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
        vm.AddOrSaveAlarmCommand.Execute(null);

        Assert.Equal(SoundChoice.Bell, vm.Alarms[0].Item.Sound);
        Assert.NotNull(announced);
        Assert.Contains("10:00", announced);
    }

    [Fact]
    public void BeginEdit_loads_the_row_into_the_editor_and_enters_edit_mode()
    {
        var vm = New(out _, out _);                         // 09:00
        vm.AlarmTimeInput = "10:00"; vm.AlarmLabel = "Tea"; vm.AlarmSound = SoundChoice.Bell;
        vm.AddOrSaveAlarmCommand.Execute(null);
        var row = vm.Alarms[0];

        vm.BeginEditAlarmCommand.Execute(row);

        Assert.True(vm.IsEditingAlarm);
        Assert.Equal("Save", vm.AddOrSaveLabel);
        Assert.Equal("10:00", vm.AlarmTimeInput);
        Assert.Equal("Tea", vm.AlarmLabel);
        Assert.Equal(SoundChoice.Bell, vm.AlarmSound);
    }

    [Fact]
    public void Save_in_edit_mode_updates_the_alarm_in_place_keeping_its_id()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AlarmLabel = "Tea";
        vm.AddOrSaveAlarmCommand.Execute(null);
        var originalId = vm.Alarms[0].Item.Id;

        vm.BeginEditAlarmCommand.Execute(vm.Alarms[0]);
        vm.AlarmTimeInput = "11:15"; vm.AlarmLabel = "Coffee";
        vm.AddOrSaveAlarmCommand.Execute(null);

        var row = Assert.Single(vm.Alarms);                // still one alarm, not a duplicate
        Assert.Equal(originalId, row.Item.Id);
        Assert.Equal("11:15", row.TimeText);
        Assert.Equal("Coffee", row.DisplayLabel);
        Assert.False(vm.IsEditingAlarm);                   // back to add mode
        Assert.Equal("Add", vm.AddOrSaveLabel);
    }

    [Fact]
    public void Cancel_edit_leaves_the_alarm_unchanged_and_clears_the_editor()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AddOrSaveAlarmCommand.Execute(null);

        vm.BeginEditAlarmCommand.Execute(vm.Alarms[0]);
        vm.AlarmTimeInput = "23:00";                        // start changing...
        vm.CancelEditAlarmCommand.Execute(null);           // ...then bail

        Assert.Equal("10:00", Assert.Single(vm.Alarms).TimeText);   // unchanged
        Assert.False(vm.IsEditingAlarm);
        Assert.Equal("", vm.AlarmTimeInput);
    }

    [Fact]
    public void DeleteAlarm_disarms_immediately_but_does_not_commit_yet()
    {
        var vm = New(out _, out var sched);
        vm.AlarmTimeInput = "10:00"; vm.AddOrSaveAlarmCommand.Execute(null);
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
        vm.AlarmTimeInput = "10:00"; vm.AlarmLabel = "Tea"; vm.AddOrSaveAlarmCommand.Execute(null);
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
        vm.AlarmTimeInput = "10:00"; vm.AddOrSaveAlarmCommand.Execute(null);
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
        vm.AlarmTimeInput = "10:00"; vm.AddOrSaveAlarmCommand.Execute(null);
        vm.AlarmTimeInput = "11:00"; vm.AddOrSaveAlarmCommand.Execute(null);

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
        vm.AlarmTimeInput = "09:30"; vm.AddOrSaveAlarmCommand.Execute(null);   // clock is 09:00
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
        vm.AlarmTimeInput = "16:00"; vm.AddOrSaveAlarmCommand.Execute(null);
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
}
