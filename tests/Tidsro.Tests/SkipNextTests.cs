using Tidsro.Models;
using Tidsro.Services;
using Tidsro.ViewModels;
using Xunit;

namespace Tidsro.Tests;

// The fake clock starts Thursday 01.01.2026 09:00 UTC, so a 10:00 daily alarm's next occurrence
// is today at 10:00 and a skip moves it to Friday 02.01.
public class SkipNextTests
{
    private static readonly Weekdays MonToFri =
        Weekdays.Mon | Weekdays.Tue | Weekdays.Wed | Weekdays.Thu | Weekdays.Fri;

    private static (SchedulerService s, FakeClock c) New()
    {
        var c = new FakeClock();
        return (new SchedulerService(c), c);
    }

    private static DateTimeOffset At(int day, int hour) => new(2026, 1, day, hour, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SkipNext_moves_a_daily_alarm_to_the_occurrence_after()
    {
        var (s, _) = New();
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, "Talk", SoundChoice.Bell);

        Assert.True(s.SkipNext(alarm));

        Assert.Equal(At(2, 10), alarm.EndsAt);
        Assert.Equal(1, s.SkippedCount(alarm));
    }

    [Fact]
    public void SkipNext_twice_skips_two_occurrences()
    {
        var (s, _) = New();
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None);

        s.SkipNext(alarm);
        s.SkipNext(alarm);

        Assert.Equal(At(3, 10), alarm.EndsAt);
        Assert.Equal(2, s.SkippedCount(alarm));
    }

    [Fact]
    public void SkipNext_on_a_weekday_alarm_jumps_the_weekend()
    {
        var (s, c) = New();
        c.Now = At(2, 11);   // Friday after 10:00: the next occurrence is Monday 05.01
        var alarm = s.ArmRecurringAlarm(10, 0, MonToFri, null, SoundChoice.None);

        s.SkipNext(alarm);

        Assert.Equal(At(6, 10), alarm.EndsAt);   // Tuesday
    }

    [Fact]
    public void A_skipped_occurrence_neither_fires_nor_warns_and_the_next_one_fires()
    {
        var (s, c) = New();
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None, warnBefore: true);
        var fired = 0; s.Fired += (_, _) => fired++;
        var warned = 0; s.Warning += (_, _) => warned++;
        var expired = 0; s.Expired += (_, _) => expired++;
        s.SkipNext(alarm);

        c.Now = At(1, 9).AddMinutes(56); s.Tick();   // inside today's warning lead
        c.Now = At(1, 10); s.Tick();                 // today's occurrence
        Assert.Equal(0, fired);
        Assert.Equal(0, warned);
        Assert.Equal(0, expired);

        c.Now = At(2, 10); s.Tick();                 // tomorrow's occurrence
        Assert.Equal(1, fired);
    }

    [Fact]
    public void The_skip_count_drops_once_the_skipped_occurrence_has_passed()
    {
        var (s, c) = New();
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None);
        s.SkipNext(alarm);

        c.Now = At(1, 10).AddMinutes(1);

        Assert.Equal(0, s.SkippedCount(alarm));
        Assert.Equal(At(2, 10), alarm.EndsAt);   // the alarm itself is untouched: the next one is due as normal
    }

    [Fact]
    public void UndoSkip_steps_back_one_occurrence()
    {
        var (s, _) = New();
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None);
        s.SkipNext(alarm);
        s.SkipNext(alarm);

        Assert.True(s.UndoSkip(alarm));

        Assert.Equal(At(2, 10), alarm.EndsAt);
        Assert.Equal(1, s.SkippedCount(alarm));
    }

    [Fact]
    public void UndoSkip_without_a_skip_does_nothing()
    {
        var (s, _) = New();
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None);

        Assert.False(s.UndoSkip(alarm));
        Assert.Equal(At(1, 10), alarm.EndsAt);
    }

    [Fact]
    public void UndoSkip_re_arms_the_heads_up_for_the_restored_occurrence()
    {
        var (s, c) = New();
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None, warnBefore: true);
        s.SkipNext(alarm);
        s.UndoSkip(alarm);
        var warned = 0; s.Warning += (_, _) => warned++;

        c.Now = At(1, 9).AddMinutes(56); s.Tick();

        Assert.Equal(1, warned);
    }

    [Fact]
    public void Skip_inside_the_warning_lead_does_not_warn_for_the_new_occurrence_early()
    {
        var (s, c) = New();
        c.Now = At(1, 9).AddMinutes(57);
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None, warnBefore: true);

        s.SkipNext(alarm);

        Assert.False(alarm.WarningSent);   // tomorrow's heads-up is still owed
    }

    [Fact]
    public void One_shot_and_disabled_alarms_cannot_be_skipped()
    {
        var (s, _) = New();
        var once = s.ArmClockAlarm(At(1, 10), null, SoundChoice.None);
        var off = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None, enabled: false);

        Assert.False(s.SkipNext(once));
        Assert.False(s.SkipNext(off));
        Assert.Equal(At(1, 10), once.EndsAt);
        Assert.Equal(0, s.SkippedCount(once));
    }

    [Fact]
    public void ReapplySkips_skips_the_same_number_on_a_re_armed_alarm()
    {
        var (s, _) = New();
        var alarm = s.ArmRecurringAlarm(11, 0, RecurrenceRules.AllDays, null, SoundChoice.None);

        s.ReapplySkips(alarm, 2);

        Assert.Equal(At(3, 11), alarm.EndsAt);
    }
}

public class SkipNextViewModelTests
{
    private static MainViewModel New(out FakeClock clock, out SchedulerService sched)
    {
        clock = new FakeClock();
        sched = new SchedulerService(clock);
        return new MainViewModel(sched, new FakeSoundService(), SoundChoice.None, hasCustomSound: () => false);
    }

    private static MainViewModel WithDailyTalk(out FakeClock clock, out SchedulerService sched)
    {
        var vm = New(out clock, out sched);
        vm.AlarmTimeInput = "10:00";
        vm.AlarmLabel = "Talk";
        vm.AlarmRepeat = RepeatOption.Daily;
        vm.AddAlarmCommand.Execute(null);
        return vm;
    }

    [Fact]
    public void SkipAlarm_skips_today_persists_and_announces_both_dates()
    {
        var vm = WithDailyTalk(out _, out _);
        var changed = 0; vm.AlarmsChanged += (_, _) => changed++;
        string? announced = null; vm.Announcement += (_, m) => announced = m;

        vm.SkipAlarmCommand.Execute(vm.Alarms[0]);

        var row = Assert.Single(vm.Alarms);
        Assert.True(row.IsSkipped);
        Assert.Equal("Skipped, next Fri 02.01", row.SkipText);
        Assert.Equal(1, changed);
        Assert.Equal("Skipped the alarm at 10:00 on Thu 01.01. Next: Fri 02.01 10:00", announced);
    }

    [Fact]
    public void SkipAlarm_twice_counts_both_skips_on_the_row()
    {
        var vm = WithDailyTalk(out _, out _);

        vm.SkipAlarmCommand.Execute(vm.Alarms[0]);
        vm.SkipAlarmCommand.Execute(vm.Alarms[0]);

        Assert.Equal("2 skipped, next Sat 03.01", vm.Alarms[0].SkipText);
        Assert.Contains("2 skipped, next sat 03.01", vm.Alarms[0].AccessibleName);
    }

    [Fact]
    public void UndoSkipAlarm_brings_back_the_skipped_day()
    {
        var vm = WithDailyTalk(out _, out _);
        vm.SkipAlarmCommand.Execute(vm.Alarms[0]);
        string? announced = null; vm.Announcement += (_, m) => announced = m;

        vm.UndoSkipAlarmCommand.Execute(vm.Alarms[0]);

        Assert.False(vm.Alarms[0].IsSkipped);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero), vm.Alarms[0].Item.EndsAt);
        Assert.Equal("Skip undone. Next: Thu 01.01 10:00", announced);
    }

    [Fact]
    public void The_skipped_cue_goes_by_itself_once_the_skipped_time_passes()
    {
        var vm = WithDailyTalk(out var clock, out _);
        vm.SkipAlarmCommand.Execute(vm.Alarms[0]);

        clock.Now = new DateTimeOffset(2026, 1, 1, 10, 1, 0, TimeSpan.Zero);
        vm.RefreshAll();

        Assert.False(vm.Alarms[0].IsSkipped);
    }

    [Fact]
    public void Editing_a_skipped_alarm_keeps_the_skip()
    {
        var vm = WithDailyTalk(out _, out _);
        vm.SkipAlarmCommand.Execute(vm.Alarms[0]);
        var id = vm.Alarms[0].Item.Id;

        vm.ApplyAlarmEdit(id, 10, 0, RecurrenceRules.AllDays, "Talk with notes", SoundChoice.None, false);

        var row = Assert.Single(vm.Alarms);
        Assert.Equal("Talk with notes", row.Item.Label);
        Assert.True(row.IsSkipped);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero), row.Item.EndsAt);
    }

    [Fact]
    public void A_one_shot_or_disabled_row_offers_no_skip_and_ignores_the_command()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AddAlarmCommand.Execute(null);          // one-shot
        var changed = 0; vm.AlarmsChanged += (_, _) => changed++;

        Assert.False(vm.Alarms[0].CanSkip);
        vm.SkipAlarmCommand.Execute(vm.Alarms[0]);
        Assert.Equal(0, changed);

        var daily = WithDailyTalk(out _, out _);
        daily.ToggleAlarmCommand.Execute(daily.Alarms[0]);                      // off
        Assert.False(daily.Alarms[0].CanSkip);
    }
}
