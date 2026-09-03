using Tidsro.Models;
using Tidsro.ViewModels;
using Xunit;

namespace Tidsro.Tests;

public class EditAlarmViewModelTests
{
    private static readonly SoundChoice[] Options =
        { SoundChoice.None, SoundChoice.SoftChime, SoundChoice.Marimba, SoundChoice.Bell };

    private static EditAlarmViewModel New(string timeInput, out List<(Guid id, int h, int m, Weekdays days, string? label, SoundChoice sound, bool warnBefore, int? endMinute)> applied,
        out FakeSoundService sound, Guid? id = null, Weekdays days = Weekdays.None, bool warnBefore = false,
        string endInput = "")
    {
        var captured = new List<(Guid, int, int, Weekdays, string?, SoundChoice, bool, int?)>();
        applied = captured;
        sound = new FakeSoundService();
        return new EditAlarmViewModel(id ?? Guid.NewGuid(), timeInput, endInput, "Tea", SoundChoice.Bell, days, warnBefore,
            Options, (i, h, m, d, l, s, w, e) => captured.Add((i, h, m, d, l, s, w, e)), sound);
    }

    [Fact]
    public void Save_with_a_valid_time_applies_once_and_requests_close_with_saved_true()
    {
        var id = Guid.NewGuid();
        var vm = New("11:15", out var applied, out _, id);
        bool? closedSaved = null;
        vm.CloseRequested += (_, saved) => closedSaved = saved;

        vm.SaveCommand.Execute(null);

        var call = Assert.Single(applied);
        Assert.Equal(id, call.id);
        Assert.Equal(11, call.h);
        Assert.Equal(15, call.m);
        Assert.Equal(SoundChoice.Bell, call.sound);
        Assert.Null(vm.Error);
        Assert.Equal(true, closedSaved);   // dialog closes as saved
    }

    [Fact]
    public void Save_with_an_invalid_time_sets_the_error_and_does_not_apply_or_close()
    {
        var vm = New("99:99", out var applied, out _);
        var closeRaised = false;
        vm.CloseRequested += (_, _) => closeRaised = true;

        vm.SaveCommand.Execute(null);

        Assert.Empty(applied);          // edit not applied
        Assert.NotNull(vm.Error);       // message shown, dialog stays open
        Assert.False(closeRaised);
    }

    [Fact]
    public void Cancel_requests_close_with_saved_false_and_does_not_apply()
    {
        var vm = New("11:15", out var applied, out _);
        bool? closedSaved = null;
        vm.CloseRequested += (_, saved) => closedSaved = saved;

        vm.CancelCommand.Execute(null);

        Assert.Empty(applied);
        Assert.Equal(false, closedSaved);
    }

    [Fact]
    public void Preview_is_gated_on_the_selected_sound()
    {
        var vm = New("11:15", out _, out var sound);
        vm.SelectedSound = SoundChoice.None;
        Assert.False(vm.PreviewSoundCommand.CanExecute(null));

        vm.SelectedSound = SoundChoice.Marimba;
        Assert.True(vm.PreviewSoundCommand.CanExecute(null));
        vm.PreviewSoundCommand.Execute(null);
        Assert.Equal(SoundChoice.Marimba, sound.LastPlayed);
    }

    [Fact]
    public void Constructed_from_a_recurring_alarm_preselects_the_repeat_option()
    {
        var weekdays = Weekdays.Mon | Weekdays.Tue | Weekdays.Wed | Weekdays.Thu | Weekdays.Fri;
        var vm = New("07:00", out var applied, out _, days: weekdays);
        Assert.Equal(RepeatOption.Weekdays, vm.Repeat);
        Assert.False(vm.ShowCustomDays);

        vm.SaveCommand.Execute(null);

        Assert.Equal(weekdays, Assert.Single(applied).days);
    }

    [Fact]
    public void Save_passes_none_for_a_one_shot_edit()
    {
        var vm = New("11:15", out var applied, out _);   // days defaults to None -> Once
        vm.SaveCommand.Execute(null);
        Assert.Equal(Weekdays.None, Assert.Single(applied).days);
    }

    [Fact]
    public void Constructed_from_a_custom_set_shows_custom_panel_and_round_trips_the_days()
    {
        var custom = Weekdays.Mon | Weekdays.Wed | Weekdays.Fri;
        var vm = New("09:00", out var applied, out _, days: custom);
        Assert.Equal(RepeatOption.Custom, vm.Repeat);
        Assert.True(vm.ShowCustomDays);
        vm.SaveCommand.Execute(null);
        Assert.Equal(custom, Assert.Single(applied).days);   // pre-selected toggles round-trip through ResolveDays
    }

    [Fact]
    public void Save_passes_the_warn_before_flag_through()
    {
        var vm = New("11:15", out var applied, out _, warnBefore: true);
        vm.SaveCommand.Execute(null);
        Assert.True(Assert.Single(applied).warnBefore);
    }

    [Fact]
    public void Save_passes_false_when_the_warning_is_off()
    {
        var vm = New("11:15", out var applied, out _);   // warnBefore defaults false
        vm.SaveCommand.Execute(null);
        Assert.False(Assert.Single(applied).warnBefore);
    }

    // ---- End times (timetable blocks) ------------------------------------------------------

    [Fact]
    public void An_empty_end_saves_as_an_instant()
    {
        var vm = New("09:00", out var applied, out _, days: Weekdays.Mon);

        vm.SaveCommand.Execute(null);

        Assert.Null(Assert.Single(applied).endMinute);
        Assert.Null(vm.Error);
    }

    [Fact]
    public void A_good_end_is_saved_as_minutes_from_midnight()
    {
        var vm = New("09:00", out var applied, out _, days: Weekdays.Mon, endInput: "10:30");

        vm.SaveCommand.Execute(null);

        Assert.Equal(630, Assert.Single(applied).endMinute);
    }

    [Fact]
    public void An_unparseable_end_keeps_the_dialog_open()
    {
        var vm = New("09:00", out var applied, out _, days: Weekdays.Mon, endInput: "half nine");
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        vm.SaveCommand.Execute(null);

        Assert.NotNull(vm.Error);
        Assert.Empty(applied);
        Assert.False(closed);
    }

    [Theory]
    [InlineData("09:00")]   // equal to the start
    [InlineData("08:30")]   // before it
    public void An_end_at_or_before_the_start_keeps_the_dialog_open(string end)
    {
        // The one place a bad end is reported rather than repaired: here there is a person to tell.
        var vm = New("09:00", out var applied, out _, days: Weekdays.Mon, endInput: end);

        vm.SaveCommand.Execute(null);

        Assert.Equal("The end must be after the start.", vm.Error);
        Assert.Empty(applied);
    }

    [Fact]
    public void A_one_shot_has_no_end_to_give()
    {
        // End times are a timetable feature, and the Week tab draws recurring alarms only.
        var vm = New("09:00", out var applied, out _, days: Weekdays.None, endInput: "10:30");

        Assert.False(vm.ShowEndInput);
        vm.SaveCommand.Execute(null);

        Assert.Null(Assert.Single(applied).endMinute);
        Assert.Null(vm.Error);
    }

    [Fact]
    public void The_end_field_appears_only_once_the_alarm_repeats()
    {
        var vm = New("09:00", out _, out _, days: Weekdays.None);
        Assert.False(vm.ShowEndInput);

        vm.Repeat = RepeatOption.Daily;

        Assert.True(vm.ShowEndInput);
    }
}
