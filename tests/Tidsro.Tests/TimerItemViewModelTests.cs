using Tidsro.Models;
using Tidsro.Services;
using Tidsro.ViewModels;
using Xunit;

namespace Tidsro.Tests;

public class TimerItemViewModelTests
{
    // Segoe Fluent Icons code points the pause button is expected to show.
    private static readonly string PauseGlyph = ((char)0xE769).ToString();
    private static readonly string PlayGlyph = ((char)0xE768).ToString();

    private static (SchedulerService s, FakeClock c) New()
    {
        var c = new FakeClock();
        return (new SchedulerService(c), c);
    }

    [Fact]
    public void PauseResume_flips_glyph_and_accessible_name()
    {
        var (s, _) = New();
        var item = s.StartCountdown(TimeSpan.FromMinutes(25), "focus", SoundChoice.None);
        var vm = new TimerItemViewModel(item, s);

        Assert.Equal(PauseGlyph, vm.PauseResumeGlyph);
        Assert.Equal("Pause", vm.PauseResumeLabel);

        vm.PauseResumeCommand.Execute(null);                 // -> paused
        Assert.Equal(PlayGlyph, vm.PauseResumeGlyph);
        Assert.Equal("Resume", vm.PauseResumeLabel);

        vm.PauseResumeCommand.Execute(null);                 // -> running again
        Assert.Equal(PauseGlyph, vm.PauseResumeGlyph);
        Assert.Equal("Pause", vm.PauseResumeLabel);
    }

    [Fact]
    public void Pausing_raises_change_notification_for_glyph_and_label()
    {
        var (s, _) = New();
        var item = s.StartCountdown(TimeSpan.FromMinutes(25), "focus", SoundChoice.None);
        var vm = new TimerItemViewModel(item, s);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.PauseResumeCommand.Execute(null);   // pause

        Assert.Contains(nameof(vm.PauseResumeGlyph), changed);
        Assert.Contains(nameof(vm.PauseResumeLabel), changed);
    }

    [Fact]
    public void Reset_while_paused_returns_to_full_and_stays_paused()
    {
        var (s, c) = New();
        var item = s.StartCountdown(TimeSpan.FromMinutes(25), "focus", SoundChoice.None);
        var vm = new TimerItemViewModel(item, s);
        c.Advance(TimeSpan.FromMinutes(10)); vm.PauseResumeCommand.Execute(null);  // paused, 15 left

        vm.ResetCommand.Execute(null);

        Assert.True(vm.IsPaused);                 // stays stopped at the start
        Assert.Equal("25:00", vm.RemainingText);
    }

    [Fact]
    public void Reset_while_running_returns_to_full_and_keeps_running()
    {
        var (s, c) = New();
        var item = s.StartCountdown(TimeSpan.FromMinutes(25), "focus", SoundChoice.None);
        var vm = new TimerItemViewModel(item, s);
        c.Advance(TimeSpan.FromMinutes(10));      // running, 15 left

        vm.ResetCommand.Execute(null);

        Assert.False(vm.IsPaused);                // still running, from full
        Assert.Equal("25:00", vm.RemainingText);
    }

    [Fact]
    public void RemainingText_rounds_up_to_ceiling_second()
    {
        // With 90.4 s remaining the display should show 01:31 (ceiling), not 01:30 (floor/truncate).
        var (s, c) = New();
        var item = s.StartCountdown(TimeSpan.FromSeconds(120), "test", SoundChoice.None);
        var vm = new TimerItemViewModel(item, s);

        // Advance 29.6 s so that 90.4 s remain.
        c.Advance(TimeSpan.FromMilliseconds(29_600));
        vm.Refresh();

        Assert.Equal("01:31", vm.RemainingText);
    }
}
