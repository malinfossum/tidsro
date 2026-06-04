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
    public void Cancel_removes_the_item()
    {
        var (s, _) = New();
        var item = s.StartCountdown(TimeSpan.FromMinutes(1), null, SoundChoice.None);
        s.Cancel(item);
        Assert.Empty(s.Running);
    }
}
