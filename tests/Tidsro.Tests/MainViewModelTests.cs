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
}
