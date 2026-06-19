using Tidsro.Models;
using Tidsro.Services;
using Xunit;

namespace Tidsro.Tests;

public class SchedulerServiceTests
{
    private static (SchedulerService s, FakeClock c) New()
    {
        var c = new FakeClock();
        return (new SchedulerService(c), c);
    }

    [Fact]
    public void StartCountdown_adds_a_running_item()
    {
        var (s, c) = New();
        var item = s.StartCountdown(TimeSpan.FromMinutes(25), "focus", SoundChoice.None);
        Assert.Single(s.Running);
        Assert.Equal(TimerState.Running, item.State);
        Assert.Equal(c.Now + TimeSpan.FromMinutes(25), item.EndsAt);
    }

    [Fact]
    public void Tick_before_end_does_not_fire_and_remaining_counts_down()
    {
        var (s, c) = New();
        var item = s.StartCountdown(TimeSpan.FromMinutes(1), null, SoundChoice.None);
        var fired = 0; s.Fired += (_, _) => fired++;
        c.Advance(TimeSpan.FromSeconds(40)); s.Tick();
        Assert.Equal(0, fired);
        Assert.Equal(TimeSpan.FromSeconds(20), s.Remaining(item));
    }

    [Fact]
    public void Tick_at_or_after_end_fires_exactly_once()
    {
        var (s, c) = New();
        var item = s.StartCountdown(TimeSpan.FromMinutes(1), null, SoundChoice.None);
        var fired = 0; s.Fired += (_, _) => fired++;
        c.Advance(TimeSpan.FromSeconds(61));
        s.Tick(); s.Tick();              // tick twice past zero
        Assert.Equal(1, fired);          // single-fire guard
        Assert.Equal(TimerState.Fired, item.State);
        Assert.Equal(TimeSpan.Zero, s.Remaining(item));
    }

    [Fact]
    public void Pause_then_Resume_preserves_remaining()
    {
        var (s, c) = New();
        var item = s.StartCountdown(TimeSpan.FromMinutes(10), null, SoundChoice.None);
        c.Advance(TimeSpan.FromMinutes(4)); s.Pause(item);
        Assert.Equal(TimeSpan.FromMinutes(6), s.Remaining(item));
        c.Advance(TimeSpan.FromMinutes(3)); s.Resume(item);   // time passes while paused
        Assert.Equal(TimeSpan.FromMinutes(6), s.Remaining(item));
    }

    [Fact]
    public void Snooze_rearms_fresh_five_minutes_and_Restart_uses_original()
    {
        var (s, c) = New();
        var item = s.StartCountdown(TimeSpan.FromMinutes(25), "pom", SoundChoice.Bell);
        c.Advance(TimeSpan.FromMinutes(26)); s.Tick();        // fires
        var snoozed = s.Snooze(item, TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.FromMinutes(5), s.Remaining(snoozed));
        Assert.Equal("pom", snoozed.Label);
        var restarted = s.Restart(snoozed);
        Assert.Equal(TimeSpan.FromMinutes(5), restarted.OriginalDuration); // restart re-runs the snooze's 5m
    }

    [Fact]
    public void Snooze_on_an_alarm_returns_it_to_the_schedule_not_quick_timers()
    {
        var (s, c) = New();
        var alarm = s.ArmClockAlarm(c.Now + TimeSpan.FromMinutes(1), "Lunch", SoundChoice.Bell);
        c.Advance(TimeSpan.FromMinutes(2)); s.Tick();        // fires + leaves the armed set (one-shot)

        var snoozed = s.Snooze(alarm, TimeSpan.FromMinutes(5));

        Assert.Equal(TriggerType.ClockTime, snoozed.TriggerType);   // re-armed as an alarm, not a countdown
        Assert.Contains(snoozed, s.Alarms);                          // shows in the Schedule
        Assert.Empty(s.Running);                                     // not parked in Quick timers
        Assert.Equal("Lunch", snoozed.Label);
        Assert.Equal(SoundChoice.Bell, snoozed.Sound);
        Assert.Equal(c.Now + TimeSpan.FromMinutes(5), snoozed.EndsAt);
    }

    [Fact]
    public void Cancel_removes_the_item()
    {
        var (s, _) = New();
        var item = s.StartCountdown(TimeSpan.FromMinutes(1), null, SoundChoice.None);
        s.Cancel(item);
        Assert.Empty(s.Running);
    }

    [Fact]
    public void Reset_rearms_the_same_item_to_its_original_duration()
    {
        var (s, c) = New();
        var item = s.StartCountdown(TimeSpan.FromMinutes(25), "focus", SoundChoice.None);
        c.Advance(TimeSpan.FromMinutes(10));                 // 15 left
        s.Reset(item);
        Assert.Same(item, Assert.Single(s.Running));         // same item re-armed, not a new one
        Assert.Equal(TimerState.Running, item.State);
        Assert.Equal(TimeSpan.FromMinutes(25), s.Remaining(item));
        Assert.Equal(TimeSpan.FromMinutes(25), item.OriginalDuration);
    }

    [Fact]
    public void Reset_on_a_paused_item_stays_paused_at_full_duration()
    {
        var (s, c) = New();
        var item = s.StartCountdown(TimeSpan.FromMinutes(25), null, SoundChoice.None);
        c.Advance(TimeSpan.FromMinutes(10)); s.Pause(item);
        s.Reset(item);
        Assert.Equal(TimerState.Paused, item.State);         // stays stopped at the start
        Assert.Equal(TimeSpan.FromMinutes(25), s.Remaining(item));
    }

    [Fact]
    public void ArmClockAlarm_adds_to_alarms_not_running()
    {
        var (s, c) = New();
        var fire = c.Now + TimeSpan.FromHours(2);
        var alarm = s.ArmClockAlarm(fire, "lunch", SoundChoice.Bell);
        Assert.Empty(s.Running);
        Assert.Same(alarm, Assert.Single(s.Alarms));
        Assert.Equal(TriggerType.ClockTime, alarm.TriggerType);
        Assert.Equal(fire, alarm.EndsAt);
        Assert.Equal(TimerState.Running, alarm.State);
    }

    [Fact]
    public void ArmClockAlarm_preserves_a_supplied_id()
    {
        var (s, c) = New();
        var id = Guid.NewGuid();
        var alarm = s.ArmClockAlarm(c.Now + TimeSpan.FromHours(1), null, SoundChoice.None, id);
        Assert.Equal(id, alarm.Id);
    }

    [Fact]
    public void RemoveAlarm_disarms_the_alarm()
    {
        var (s, c) = New();
        var alarm = s.ArmClockAlarm(c.Now + TimeSpan.FromHours(1), null, SoundChoice.None);
        s.RemoveAlarm(alarm);
        Assert.Empty(s.Alarms);
    }

    [Fact]
    public void Cancel_removes_an_item_from_either_list()
    {
        var (s, c) = New();
        var alarm = s.ArmClockAlarm(c.Now + TimeSpan.FromHours(1), null, SoundChoice.None);
        s.Cancel(alarm);                                   // alarm lives in _alarms, not _running
        Assert.Empty(s.Alarms);
    }

    [Fact]
    public void Now_reflects_the_clock()
    {
        var (s, c) = New();
        Assert.Equal(c.Now, s.Now);
        c.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(c.Now, s.Now);
    }

    [Fact]
    public void Tick_fires_an_alarm_at_or_after_its_time_within_grace()
    {
        var (s, c) = New();
        var alarm = s.ArmClockAlarm(c.Now + TimeSpan.FromMinutes(10), "lunch", SoundChoice.Bell);
        TimerItem? fired = null; s.Fired += (_, i) => fired = i;

        c.Advance(TimeSpan.FromMinutes(10));               // exactly at FireAt
        s.Tick();

        Assert.Same(alarm, fired);
        Assert.Equal(TimerState.Fired, alarm.State);
        Assert.Empty(s.Alarms);                            // one-shot removed after firing
    }

    [Fact]
    public void Tick_fires_within_the_five_minute_grace_after_a_gap()
    {
        var (s, c) = New();
        var alarm = s.ArmClockAlarm(c.Now + TimeSpan.FromMinutes(1), null, SoundChoice.None);
        var fired = 0; s.Fired += (_, _) => fired++;

        c.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromMinutes(5));   // 5 min late == on the boundary
        s.Tick();

        Assert.Equal(1, fired);                            // boundary is inclusive
        Assert.Empty(s.Alarms);
    }

    [Fact]
    public void Tick_expires_an_alarm_past_the_grace_without_firing()
    {
        var (s, c) = New();
        var alarm = s.ArmClockAlarm(c.Now + TimeSpan.FromMinutes(1), "missed", SoundChoice.Bell);
        var fired = 0; s.Fired += (_, _) => fired++;
        TimerItem? expired = null; s.Expired += (_, i) => expired = i;

        c.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromMinutes(6));   // 6 min late, past grace
        s.Tick();

        Assert.Equal(0, fired);                            // no sound, no card
        Assert.Same(alarm, expired);                       // reported for the missed-while-away note
        Assert.Empty(s.Alarms);
    }

    [Fact]
    public void Tick_fires_an_alarm_at_most_once_even_across_repeated_ticks()
    {
        var (s, c) = New();
        s.ArmClockAlarm(c.Now + TimeSpan.FromMinutes(1), null, SoundChoice.None);
        var fired = 0; s.Fired += (_, _) => fired++;

        c.Advance(TimeSpan.FromMinutes(2));
        s.Tick(); s.Tick();                                // a sleep-induced double tick

        Assert.Equal(1, fired);                            // removed after the first fire -> durable dedup
    }

    [Fact]
    public void Tick_keeps_alarms_and_countdowns_independent()
    {
        var (s, c) = New();
        var countdown = s.StartCountdown(TimeSpan.FromMinutes(1), "cd", SoundChoice.None);
        var alarm = s.ArmClockAlarm(c.Now + TimeSpan.FromMinutes(10), "al", SoundChoice.None);

        c.Advance(TimeSpan.FromMinutes(2));                // countdown due, alarm not
        s.Tick();

        Assert.Equal(TimerState.Fired, countdown.State);
        Assert.Contains(countdown, s.Running);             // a fired countdown stays until dismissed
        Assert.Single(s.Alarms);                           // alarm untouched
        Assert.Equal(TimerState.Running, alarm.State);
    }

    [Fact]
    public void ArmRecurringAlarm_adds_a_recurring_alarm_with_its_next_occurrence()
    {
        var (s, c) = New();   // Thu 2026-01-01 09:00
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, "Stand-up", SoundChoice.Bell);
        Assert.Same(alarm, Assert.Single(s.Alarms));
        Assert.Equal(TriggerType.Recurring, alarm.TriggerType);
        Assert.Equal(RecurrenceRules.AllDays, alarm.RecurringDays);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero), alarm.EndsAt);
    }

    [Fact]
    public void ArmRecurringAlarm_preserves_a_supplied_id()
    {
        var (s, c) = New();
        var id = Guid.NewGuid();
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None, id);
        Assert.Equal(id, alarm.Id);
    }

    [Fact]
    public void Tick_fires_a_recurring_alarm_and_advances_to_the_next_occurrence()
    {
        var (s, c) = New();
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None);
        var fired = 0; s.Fired += (_, _) => fired++;

        c.Advance(TimeSpan.FromMinutes(61));   // 10:01 Thu
        s.Tick();

        Assert.Equal(1, fired);
        Assert.Same(alarm, Assert.Single(s.Alarms));            // stays armed (not removed)
        Assert.Equal(TimerState.Running, alarm.State);          // never permanently Fired
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero), alarm.EndsAt);   // advanced to Fri
    }

    [Fact]
    public void Tick_does_not_refire_a_recurring_alarm_after_it_advances()
    {
        var (s, c) = New();
        s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None);
        var fired = 0; s.Fired += (_, _) => fired++;

        c.Advance(TimeSpan.FromMinutes(61));
        s.Tick(); s.Tick();                 // a sleep-induced double tick

        Assert.Equal(1, fired);             // advancing EndsAt is the dedup
    }

    [Fact]
    public void Tick_fires_a_recurring_alarm_within_grace()
    {
        var (s, c) = New();
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.None);
        TimerItem? fired = null; s.Fired += (_, i) => fired = i;

        c.Advance(TimeSpan.FromMinutes(63));   // 10:03, 3 min late -> within grace
        s.Tick();

        Assert.NotNull(fired);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero), fired!.EndsAt);  // the occurrence
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero), alarm.EndsAt);   // live alarm advanced to Fri
    }

    [Fact]
    public void Tick_expires_a_recurring_alarm_past_grace_without_firing_and_advances()
    {
        var (s, c) = New();
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, "Stand-up", SoundChoice.Bell);
        var fired = 0; s.Fired += (_, _) => fired++;
        TimerItem? expired = null; s.Expired += (_, i) => expired = i;

        c.Advance(TimeSpan.FromMinutes(66));   // 10:06, 6 min late -> past grace
        s.Tick();

        Assert.Equal(0, fired);
        Assert.NotNull(expired);
        Assert.Same(alarm, Assert.Single(s.Alarms));            // still armed
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero), alarm.EndsAt);   // advanced to Fri
    }

    [Fact]
    public void A_fired_recurring_occurrence_is_a_transient_copy_so_dismiss_cannot_delete_the_alarm()
    {
        var (s, c) = New();
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, "Stand-up", SoundChoice.Bell);
        TimerItem? fired = null; s.Fired += (_, i) => fired = i;

        c.Advance(TimeSpan.FromMinutes(61));
        s.Tick();

        Assert.NotSame(alarm, fired);                              // the card gets a copy...
        Assert.Equal(TriggerType.ClockTime, fired!.TriggerType);   // ...typed so the card shows Snooze/Dismiss
        s.Cancel(fired);                                           // Dismiss cancels the snapshot
        Assert.Same(alarm, Assert.Single(s.Alarms));              // the live recurring alarm survives
    }

    [Fact]
    public void Tick_collapses_many_missed_occurrences_into_one_expiry_and_advances_to_the_future()
    {
        var (s, c) = New();   // Thu 2026-01-01 09:00
        // Restore a next occurrence a week in the past, as if the app was closed for a week.
        var past = new DateTimeOffset(2025, 12, 25, 10, 0, 0, TimeSpan.Zero);
        var alarm = s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, "Stand-up", SoundChoice.None, nextFireAt: past);
        var fired = 0; s.Fired += (_, _) => fired++;
        var expired = 0; s.Expired += (_, _) => expired++;

        s.Tick();   // most recent 10:00 was yesterday (well past grace)

        Assert.Equal(0, fired);
        Assert.Equal(1, expired);                                  // one quiet note, not one per missed day
        Assert.Same(alarm, Assert.Single(s.Alarms));
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero), alarm.EndsAt);   // advanced to today 10:00
    }

    // ── Pre-alarm warning (heads-up 5 minutes before) ──

    [Fact]
    public void Tick_raises_Warning_once_when_crossing_into_the_last_five_minutes()
    {
        var (s, c) = New();
        s.ArmClockAlarm(c.Now + TimeSpan.FromMinutes(10), "lunch", SoundChoice.Bell, warnBefore: true); // fires +10, warns +5
        var warned = 0; s.Warning += (_, _) => warned++;

        c.Advance(TimeSpan.FromMinutes(6));   // 6 min in -> inside [+5, +10)
        s.Tick(); s.Tick();                    // two ticks in the window

        Assert.Equal(1, warned);               // once per occurrence
    }

    [Fact]
    public void Tick_does_not_raise_Warning_when_warn_before_is_off()
    {
        var (s, c) = New();
        s.ArmClockAlarm(c.Now + TimeSpan.FromMinutes(10), null, SoundChoice.Bell); // warnBefore defaults false
        var warned = 0; s.Warning += (_, _) => warned++;

        c.Advance(TimeSpan.FromMinutes(6));
        s.Tick();

        Assert.Equal(0, warned);
    }

    [Fact]
    public void Tick_does_not_raise_Warning_at_or_after_the_fire_time()
    {
        var (s, c) = New();
        s.ArmClockAlarm(c.Now + TimeSpan.FromMinutes(10), null, SoundChoice.None, warnBefore: true);
        var warned = 0; s.Warning += (_, _) => warned++;

        c.Advance(TimeSpan.FromMinutes(10));   // exactly the fire time
        s.Tick();                               // fires; not a warning

        Assert.Equal(0, warned);
    }

    [Fact]
    public void An_alarm_armed_inside_the_last_five_minutes_does_not_warn()
    {
        var (s, c) = New();
        s.ArmClockAlarm(c.Now + TimeSpan.FromMinutes(3), null, SoundChoice.Bell, warnBefore: true); // already inside the window
        var warned = 0; s.Warning += (_, _) => warned++;

        c.Advance(TimeSpan.FromMinutes(1));    // 1 min in, still before fire
        s.Tick();

        Assert.Equal(0, warned);               // suppressed: WarningSent initialised true at arm
    }

    [Fact]
    public void Warning_carries_the_live_alarm_so_the_app_can_mirror_its_sound()
    {
        var (s, c) = New();
        var alarm = s.ArmClockAlarm(c.Now + TimeSpan.FromMinutes(10), "lunch", SoundChoice.Bell, warnBefore: true);
        TimerItem? warned = null; s.Warning += (_, i) => warned = i;

        c.Advance(TimeSpan.FromMinutes(6));
        s.Tick();

        Assert.Same(alarm, warned);
        Assert.Equal(SoundChoice.Bell, warned!.Sound);
    }

    [Fact]
    public void A_recurring_alarm_warns_before_each_occurrence()
    {
        var (s, c) = New();   // Thu 2026-01-01 09:00
        s.ArmRecurringAlarm(10, 0, RecurrenceRules.AllDays, null, SoundChoice.Bell, warnBefore: true); // today 10:00
        var warned = 0; s.Warning += (_, _) => warned++;

        c.Advance(TimeSpan.FromMinutes(56));   // 09:56 -> inside [09:55, 10:00)
        s.Tick();
        Assert.Equal(1, warned);

        c.Advance(TimeSpan.FromMinutes(5));    // 10:01 -> fires, advances to Fri 10:00, resets the heads-up
        s.Tick();

        c.Advance(TimeSpan.FromHours(23) + TimeSpan.FromMinutes(55));   // Fri 09:56
        s.Tick();
        Assert.Equal(2, warned);               // warned again for the next occurrence
    }
}
