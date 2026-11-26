using System.IO;
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

    private static DateOnly Day(int day) => new(2026, 1, day);

    [Fact]
    public void ReapplySkips_passes_over_the_skipped_dates_on_a_re_armed_alarm()
    {
        var (s, _) = New();
        var alarm = s.ArmRecurringAlarm(11, 0, RecurrenceRules.AllDays, null, SoundChoice.None);

        var lost = s.ReapplySkips(alarm, [Day(1), Day(2)]);

        Assert.Equal(At(3, 11), alarm.EndsAt);
        Assert.Empty(lost);
    }

    [Fact]
    public void ReapplySkips_does_not_skip_a_day_that_was_not_skipped()
    {
        // 09:00 Thursday, today's 10:00 skipped. Moving the alarm to 08:00 makes Friday the next
        // occurrence. A count of one would skip Friday; the dates leave it alone.
        var (s, _) = New();
        var alarm = s.ArmRecurringAlarm(8, 0, RecurrenceRules.AllDays, null, SoundChoice.None);

        var lost = s.ReapplySkips(alarm, [Day(1)]);

        Assert.Equal(At(2, 8), alarm.EndsAt);
        Assert.Empty(lost);   // Thursday 08:00 has passed, so it does not ring on the skipped day either
    }

    [Fact]
    public void ReapplySkips_reports_a_skipped_day_the_new_schedule_rings_on_after_all()
    {
        // A Friday alarm with Fri 02.01 skipped, edited to Thu + Fri: Thursday now comes first, and a
        // skip only passes over the next occurrences in order, so Friday rings after all.
        var (s, _) = New();
        var alarm = s.ArmRecurringAlarm(10, 0, Weekdays.Thu | Weekdays.Fri, null, SoundChoice.None);

        var lost = s.ReapplySkips(alarm, [Day(2)]);

        Assert.Equal(At(1, 10), alarm.EndsAt);
        Assert.Equal([Day(2)], lost);
    }

    [Fact]
    public void ReapplySkips_on_a_one_shot_reports_the_day_it_rings_on()
    {
        var (s, _) = New();
        var once = s.ArmClockAlarm(At(1, 10), null, SoundChoice.None);

        Assert.Equal([Day(1)], s.ReapplySkips(once, [Day(1)]));
        Assert.Empty(s.ReapplySkips(once, [Day(2)]));
    }

    [Fact]
    public void An_alarm_that_is_off_keeps_its_skipped_dates()
    {
        var (s, _) = New();
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None);
        s.SkipNext(alarm);

        s.SetEnabled(alarm, false);

        Assert.Equal(0, s.SkippedCount(alarm));   // the row shows no skip while the alarm is off
        Assert.Equal([Day(1)], s.SkippedDates(alarm));

        s.SetEnabled(alarm, true);
        Assert.Equal(1, s.SkippedCount(alarm));   // and it is still there when it comes back on
    }

    [Fact]
    public void SkipNext_stops_at_the_cap()
    {
        var (s, _) = New();
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None);

        for (var i = 0; i < SchedulerService.MaxSkips; i++) Assert.True(s.SkipNext(alarm));
        var capped = alarm.EndsAt;

        Assert.False(s.SkipNext(alarm));
        Assert.Equal(capped, alarm.EndsAt);
        Assert.Equal(SchedulerService.MaxSkips, s.SkippedCount(alarm));
    }

    [Fact]
    public void A_skip_survives_a_save_and_a_load()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tidsro-skip-{Guid.NewGuid():N}.json");
        try
        {
            var (s, _) = New();
            var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, "Talk", SoundChoice.None);
            s.SkipNext(alarm);
            var end = alarm.EndsAt!.Value;
            var svc = new PersistenceService(path);
            // The fake clock runs in UTC, so its wall time is the local time the app would save.
            svc.Save(new TidsroData
            {
                Settings = AppSettings.Defaults(),
                RecurringAlarms =
                {
                    new RecurringAlarmRecord
                    {
                        Id = alarm.Id, Hour = end.Hour, Minute = end.Minute, Days = RecurrenceRules.AllDays,
                        Label = "Talk", Sound = SoundChoice.None, Enabled = true, NextFireAt = end.DateTime,
                    },
                },
            });

            var r = Assert.Single(svc.Load().RecurringAlarms);
            var (fresh, _) = New();
            var restored = fresh.ArmRecurringAlarm(r.Hour, r.Minute, r.Days, r.Label, r.Sound, r.Id,
                new DateTimeOffset(r.NextFireAt, TimeSpan.Zero), r.WarnBefore, r.Enabled, r.EndMinute);

            Assert.Equal(At(2, 10), restored.EndsAt);
            Assert.Equal(1, fresh.SkippedCount(restored));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_saved_next_time_beyond_the_cap_falls_back_to_the_natural_one()
    {
        var (s, _) = New();

        var stale = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None,
            nextFireAt: At(1, 10).AddDays(SchedulerService.MaxSkips + 1));

        Assert.Equal(At(1, 10), stale.EndsAt);
        Assert.Equal(0, s.SkippedCount(stale));
    }

    [Fact]
    public void A_stale_next_time_is_not_carried_through_an_edit()
    {
        // A clock that stepped back leaves the next time far more than MaxSkips occurrences out.
        var (s, c) = New();
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None);
        c.Now = At(1, 9).AddDays(-(SchedulerService.MaxSkips + 10));

        Assert.Empty(s.SkippedDates(alarm));
    }

    [Fact]
    public void FirstSkipped_is_the_skipped_occurrence_until_it_passes()
    {
        var (s, c) = New();
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None);
        Assert.Null(s.FirstSkipped(alarm));

        s.SkipNext(alarm);
        Assert.Equal(At(1, 10), s.FirstSkipped(alarm));

        c.Now = At(1, 10).AddMinutes(1);
        Assert.Null(s.FirstSkipped(alarm));
    }

    [Fact]
    public void WillRingAt_is_false_once_the_occurrence_is_skipped_or_off()
    {
        var (s, _) = New();
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None);

        Assert.True(s.WillRingAt(alarm.Id, At(1, 10)));
        s.SkipNext(alarm);
        Assert.False(s.WillRingAt(alarm.Id, At(1, 10)));

        s.UndoSkip(alarm);
        s.SetEnabled(alarm, false);
        Assert.False(s.WillRingAt(alarm.Id, At(1, 10)));
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

    private static DateTimeOffset Jan(int day, int hour) => new(2026, 1, day, hour, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Moving_a_skipped_alarm_earlier_skips_the_same_day_not_the_next_one()
    {
        var vm = WithDailyTalk(out _, out _);
        vm.SkipAlarmCommand.Execute(vm.Alarms[0]);   // skips Thu 01.01

        vm.ApplyAlarmEdit(vm.Alarms[0].Item.Id, 8, 0, RecurrenceRules.AllDays, "Talk", SoundChoice.None, false);

        var row = Assert.Single(vm.Alarms);
        Assert.Equal(Jan(2, 8), row.Item.EndsAt);   // Friday still rings
        Assert.False(row.IsSkipped);
    }

    [Fact]
    public void Changing_the_days_of_a_skipped_alarm_skips_the_same_day()
    {
        var vm = WithDailyTalk(out _, out _);
        vm.SkipAlarmCommand.Execute(vm.Alarms[0]);   // skips Thu 01.01

        vm.ApplyAlarmEdit(vm.Alarms[0].Item.Id, 10, 0, Weekdays.Thu | Weekdays.Sat, "Talk", SoundChoice.None, false);

        var row = Assert.Single(vm.Alarms);
        Assert.Equal(Jan(3, 10), row.Item.EndsAt);
        Assert.Equal("Skipped, next Sat 03.01", row.SkipText);
    }

    [Fact]
    public void An_edit_that_rings_on_a_skipped_day_says_so()
    {
        var vm = WithDailyTalk(out _, out _);
        vm.SkipAlarmCommand.Execute(vm.Alarms[0]);   // skips Thu 01.01
        string? announced = null; vm.Announcement += (_, m) => announced = m;

        vm.ApplyAlarmEdit(vm.Alarms[0].Item.Id, 10, 0, Weekdays.None, "Talk", SoundChoice.None, false);   // now a one-shot

        Assert.Equal(Jan(1, 10), vm.Alarms[0].Item.EndsAt);
        Assert.Equal("Alarm updated for 10:00. The skip no longer applies to Thu 01.01", announced);
    }

    [Fact]
    public void Editing_an_alarm_that_is_off_keeps_it_off_and_keeps_its_skip()
    {
        var vm = WithDailyTalk(out _, out var sched);
        vm.SkipAlarmCommand.Execute(vm.Alarms[0]);
        vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);   // off

        vm.ApplyAlarmEdit(vm.Alarms[0].Item.Id, 10, 0, RecurrenceRules.AllDays, "Talk later", SoundChoice.None, false);

        var item = Assert.Single(vm.Alarms).Item;
        Assert.False(item.IsEnabled);
        Assert.Equal(Jan(2, 10), item.EndsAt);

        vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);   // back on: the skip is still there
        Assert.True(vm.Alarms[0].IsSkipped);
        Assert.Equal(1, sched.SkippedCount(vm.Alarms[0].Item));
    }

    [Fact]
    public void Editing_a_one_shot_alarm_that_is_off_keeps_it_off()
    {
        var vm = New(out _, out _);
        vm.AlarmTimeInput = "10:00"; vm.AddAlarmCommand.Execute(null);
        vm.ToggleAlarmCommand.Execute(vm.Alarms[0]);

        vm.ApplyAlarmEdit(vm.Alarms[0].Item.Id, 11, 0, Weekdays.None, "Later", SoundChoice.None, false);

        Assert.False(Assert.Single(vm.Alarms).Item.IsEnabled);
    }

    [Fact]
    public void PreviewLostSkips_names_the_day_an_edit_brings_back_and_changes_nothing()
    {
        // A Friday alarm with Fri 02.01 skipped. Adding Thursday puts Thu 01.01 first, so Friday rings.
        var vm = New(out _, out var sched);
        vm.AlarmTimeInput = "10:00"; vm.AlarmRepeat = RepeatOption.Custom;
        foreach (var t in vm.AlarmDayToggles) t.IsSelected = t.Flag == Weekdays.Fri;
        vm.AddAlarmCommand.Execute(null);
        vm.SkipAlarmCommand.Execute(vm.Alarms[0]);
        var item = vm.Alarms[0].Item;

        var lost = vm.PreviewLostSkips(item.Id, 10, 0, Weekdays.Thu | Weekdays.Fri);

        Assert.Equal([new DateOnly(2026, 1, 2)], lost);
        Assert.Empty(vm.PreviewLostSkips(item.Id, 10, 0, Weekdays.Fri));
        Assert.Equal(Jan(9, 10), item.EndsAt);           // the live alarm is untouched
        Assert.Same(item, Assert.Single(sched.Alarms));
    }

    [Fact]
    public void Skip_at_the_cap_is_announced_not_ignored()
    {
        var vm = WithDailyTalk(out _, out _);
        for (var i = 0; i < SchedulerService.MaxSkips; i++) vm.SkipAlarmCommand.Execute(vm.Alarms[0]);
        var changed = 0; vm.AlarmsChanged += (_, _) => changed++;
        string? announced = null; vm.Announcement += (_, m) => announced = m;

        vm.SkipAlarmCommand.Execute(vm.Alarms[0]);

        Assert.Equal(0, changed);
        Assert.Equal($"Cannot skip more than {SchedulerService.MaxSkips} occurrences ahead", announced);
    }
}

// The heads-up card rules, without a window: a string stands in for the card.
public class HeadsUpTrackerTests
{
    private static readonly Guid Id = Guid.NewGuid();
    private static readonly DateTimeOffset Ten = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Before = Ten.AddMinutes(-4);

    [Fact]
    public void A_card_closes_when_its_alarm_fires()
    {
        var t = new HeadsUpTracker<string>();
        t.Opened("card", Id, Ten);

        var c = t.Reconcile(Ten, (_, _) => true);

        Assert.Equal(["card"], c.Fired);
        Assert.Empty(c.Gone);
    }

    [Fact]
    public void A_card_closes_early_when_its_occurrence_will_not_ring_and_comes_back_with_it()
    {
        var t = new HeadsUpTracker<string>();
        t.Opened("card", Id, Ten);
        var ringing = false;

        var gone = t.Reconcile(Before, (_, _) => ringing);
        t.Closed("card");
        ringing = true;                                  // an undo, or switched back on
        var back = t.Reconcile(Before.AddSeconds(1), (_, _) => ringing);

        Assert.Equal(["card"], gone.Gone);
        Assert.Equal([Id], back.Back);
    }

    [Fact]
    public void A_parked_card_is_forgotten_once_its_time_has_passed()
    {
        var t = new HeadsUpTracker<string>();
        t.Opened("card", Id, Ten);
        t.Reconcile(Before, (_, _) => false);
        t.Closed("card");

        Assert.Empty(t.Reconcile(Ten, (_, _) => false).Back);
        Assert.Empty(t.Reconcile(Before, (_, _) => true).Back);   // even if the clock steps back
    }

    [Fact]
    public void A_card_the_user_closed_stays_closed()
    {
        var t = new HeadsUpTracker<string>();
        t.Opened("card", Id, Ten);
        t.Closed("card");                                // the user dismissed it

        var c = t.Reconcile(Before, (_, _) => true);

        Assert.Empty(c.Back);
        Assert.False(t.IsShown(Id, Ten));
    }

    [Fact]
    public void IsShown_is_true_only_for_the_same_occurrence()
    {
        var t = new HeadsUpTracker<string>();
        t.Opened("card", Id, Ten);

        Assert.True(t.IsShown(Id, Ten));
        Assert.False(t.IsShown(Id, Ten.AddDays(1)));
    }
}
