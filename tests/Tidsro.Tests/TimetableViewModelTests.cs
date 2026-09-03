using Tidsro.Models;
using Tidsro.Services;
using Tidsro.ViewModels;
using Xunit;

namespace Tidsro.Tests;

public class TimetableViewModelTests
{
    private static (TimetableViewModel vm, SchedulerService scheduler, FakeClock clock) Build()
    {
        var clock = new FakeClock { Now = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) };
        var scheduler = new SchedulerService(clock);
        return (new TimetableViewModel(scheduler), scheduler, clock);
    }

    [Fact]
    public void Starts_empty_when_there_are_no_recurring_alarms()
    {
        var (vm, _, _) = Build();
        Assert.True(vm.Week.IsEmpty);
    }

    [Fact]
    public void Rebuild_picks_up_a_newly_armed_recurring_alarm()
    {
        var (vm, scheduler, _) = Build();
        scheduler.ArmRecurringAlarm(9, 0, Weekdays.Mon, "Class", SoundChoice.None);

        vm.Rebuild();

        Assert.False(vm.Week.IsEmpty);
        Assert.Single(vm.Week.Days.Single(d => d.Day == Weekdays.Mon).Entries);
    }

    [Fact]
    public void RefreshForTick_does_not_reproject_within_the_same_day()
    {
        var (vm, scheduler, clock) = Build();
        scheduler.ArmRecurringAlarm(9, 0, Weekdays.Mon, "Class", SoundChoice.None);
        vm.Rebuild();
        var first = vm.Week;

        for (var i = 0; i < 1000; i++)
        {
            clock.Now = clock.Now.AddSeconds(1);
            vm.RefreshForTick();
        }

        Assert.Same(first, vm.Week);   // same instance -> no work was done
    }

    [Fact]
    public void RefreshForTick_reprojects_once_when_the_date_changes()
    {
        var (vm, scheduler, clock) = Build();
        scheduler.ArmRecurringAlarm(9, 0, Weekdays.Mon, "Class", SoundChoice.None);
        vm.Rebuild();
        var first = vm.Week;

        clock.Now = clock.Now.AddDays(1);   // Thursday -> Friday
        vm.RefreshForTick();
        var second = vm.Week;
        vm.RefreshForTick();

        Assert.NotSame(first, second);
        Assert.Same(second, vm.Week);       // the second call is a no-op
    }

    [Fact]
    public void Today_moves_when_the_date_changes()
    {
        var (vm, scheduler, clock) = Build();
        scheduler.ArmRecurringAlarm(9, 0, Weekdays.Mon, "Class", SoundChoice.None);
        vm.Rebuild();
        Assert.True(vm.Week.Days.Single(d => d.Day == Weekdays.Thu).IsToday);

        clock.Now = clock.Now.AddDays(1);
        vm.RefreshForTick();

        Assert.True(vm.Week.Days.Single(d => d.Day == Weekdays.Fri).IsToday);
    }

    // The cached rollover date and TimetableLayout's IsToday must both read off the DateTimeOffset
    // value's own offset, never the machine's local zone. They previously disagreed near midnight for
    // any clock whose offset differs from the machine's — the .LocalDateTime conversion here saw a
    // different wall-clock date than TimetableLayout.Build's DayOfWeek (which never converts). Picking
    // an offset exactly 12 hours from the machine's own makes that disagreement reproduce regardless of
    // what timezone this test happens to run in.
    [Fact]
    public void RefreshForTick_uses_the_clocks_own_offset_not_the_machines_local_zone()
    {
        var localHours = (int)Math.Round(TimeZoneInfo.Local.GetUtcOffset(DateTimeOffset.UtcNow).TotalHours);
        var offsetHours = localHours >= 0 ? localHours - 12 : localHours + 12;
        var offset = TimeSpan.FromHours(offsetHours);

        // 2026-01-01 is a Thursday. Just before midnight IN THE CLOCK'S OWN OFFSET.
        var clock = new FakeClock { Now = new DateTimeOffset(2026, 1, 1, 23, 59, 0, offset) };
        var scheduler = new SchedulerService(clock);
        scheduler.ArmRecurringAlarm(9, 0, Weekdays.Mon, "Class", SoundChoice.None);
        var vm = new TimetableViewModel(scheduler);
        Assert.True(vm.Week.Days.Single(d => d.Day == Weekdays.Thu).IsToday);

        clock.Now = clock.Now.AddMinutes(2);   // 00:01 the next day, same offset -> its own date rolled
        vm.RefreshForTick();

        Assert.True(vm.Week.Days.Single(d => d.Day == Weekdays.Fri).IsToday);
    }

    [Fact]
    public void Raises_property_changed_so_the_view_redraws()
    {
        var (vm, scheduler, _) = Build();
        scheduler.ArmRecurringAlarm(9, 0, Weekdays.Mon, "Class", SoundChoice.None);
        var raised = 0;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.Week)) raised++; };

        vm.Rebuild();

        Assert.Equal(1, raised);
    }

    [Fact]
    public void NowMinuteOfDay_starts_on_the_clock()
    {
        var (vm, _, _) = Build();
        Assert.Equal(8 * 60, vm.NowMinuteOfDay);
    }

    [Fact]
    public void A_tick_inside_the_same_minute_raises_nothing()
    {
        var (vm, _, clock) = Build();
        var raised = 0;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.NowMinuteOfDay)) raised++; };

        // Four ticks a second, all inside 08:00 -- the projection cost this gate exists to avoid.
        for (var i = 0; i < 100; i++) { clock.Advance(TimeSpan.FromMilliseconds(250)); vm.RefreshForTick(); }
        Assert.Equal(0, raised);

        clock.Advance(TimeSpan.FromMinutes(1));
        vm.RefreshForTick();
        Assert.Equal(1, raised);
        Assert.Equal(8 * 60 + 1, vm.NowMinuteOfDay);
    }

    [Fact]
    public void Crossing_midnight_reprojects_before_the_minute_is_read()
    {
        var (vm, scheduler, clock) = Build();
        scheduler.ArmRecurringAlarm(9, 0, Weekdays.Mon, "Class", SoundChoice.None);
        vm.Rebuild();
        var before = vm.Week;

        clock.Advance(TimeSpan.FromHours(16));   // 2026-01-01 08:00 -> 2026-01-02 00:00
        vm.RefreshForTick();

        Assert.NotSame(before, vm.Week);         // the date moved, so the week was rebuilt
        Assert.Equal(0, vm.NowMinuteOfDay);
    }
}
