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
}
