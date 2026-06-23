using Tidsro.Models;
using Tidsro.ViewModels;
using Xunit;

namespace Tidsro.Tests;

public class AlarmItemViewModelTests
{
    private static TimerItem Alarm(string? label, SoundChoice sound, int hour, int minute) => new()
    {
        TriggerType = TriggerType.ClockTime,
        Label = label,
        Sound = sound,
        EndsAt = new DateTimeOffset(2026, 6, 17, hour, minute, 0, TimeSpan.Zero),
        State = TimerState.Running,
    };

    [Fact]
    public void Formats_time_label_and_sound()
    {
        var vm = new AlarmItemViewModel(Alarm("Meeting prep", SoundChoice.Bell, 14, 0), isTomorrow: false, isNext: false);
        Assert.Equal("14:00", vm.TimeText);
        Assert.Equal("Meeting prep", vm.DisplayLabel);
        Assert.True(vm.HasSound);
        Assert.Equal("chime", vm.SoundText);
    }

    [Fact]
    public void Shows_no_label_and_silent_when_unset()
    {
        var vm = new AlarmItemViewModel(Alarm(null, SoundChoice.None, 9, 5), isTomorrow: false, isNext: false);
        Assert.Equal("09:05", vm.TimeText);
        Assert.Equal("No label", vm.DisplayLabel);
        Assert.False(vm.HasSound);
        Assert.Equal("silent", vm.SoundText);
    }

    [Fact]
    public void Accessible_name_includes_tomorrow_and_next_cues_as_text()
    {
        var vm = new AlarmItemViewModel(Alarm("Lunch", SoundChoice.Bell, 14, 0), isTomorrow: true, isNext: true);
        Assert.Contains("14:00", vm.AccessibleName);
        Assert.Contains("tomorrow", vm.AccessibleName);
        Assert.Contains("Lunch", vm.AccessibleName);
        Assert.Contains("chime", vm.AccessibleName);
        Assert.Contains("next", vm.AccessibleName);
        Assert.Equal("Edit alarm at 14:00", vm.EditLabel);
        Assert.Equal("Delete alarm at 14:00", vm.DeleteLabel);
    }

    private static TimerItem Recurring(string? label, Weekdays days, int hour, int minute) => new()
    {
        TriggerType = TriggerType.Recurring,
        Label = label,
        RecurringDays = days,
        EndsAt = new DateTimeOffset(2026, 6, 17, hour, minute, 0, TimeSpan.Zero),
        State = TimerState.Running,
    };

    [Fact]
    public void Recurring_alarm_shows_its_cadence_tag_and_speaks_it()
    {
        var weekdays = Weekdays.Mon | Weekdays.Tue | Weekdays.Wed | Weekdays.Thu | Weekdays.Fri;
        var vm = new AlarmItemViewModel(Recurring("Stand-up", weekdays, 7, 0), isTomorrow: true, isNext: false);
        Assert.Equal("Weekdays", vm.CadenceText);          // cadence wins over the tomorrow cue
        Assert.Contains("weekdays", vm.AccessibleName);     // folded into the spoken name, lower-cased
    }

    [Fact]
    public void One_shot_alarm_shows_tomorrow_or_nothing_as_its_cadence()
    {
        var today = new AlarmItemViewModel(Alarm("Lunch", SoundChoice.Bell, 14, 0), isTomorrow: false, isNext: false);
        Assert.Equal("", today.CadenceText);
        var tomorrow = new AlarmItemViewModel(Alarm("Lunch", SoundChoice.Bell, 14, 0), isTomorrow: true, isNext: false);
        Assert.Equal("tomorrow", tomorrow.CadenceText);
    }

    [Fact]
    public void Warning_enabled_alarm_exposes_a_text_cue_and_speaks_it()
    {
        var item = Alarm("Stand-up", SoundChoice.Bell, 7, 0);
        item.WarnBefore = true;
        var vm = new AlarmItemViewModel(item, isTomorrow: false, isNext: false);
        Assert.True(vm.WarnBefore);
        Assert.Equal("5-min warning", vm.WarnText);
        Assert.Contains("warns 5 minutes before", vm.AccessibleName);
    }

    [Fact]
    public void Alarm_without_warning_has_an_empty_cue_and_no_warning_phrase()
    {
        var vm = new AlarmItemViewModel(Alarm("Lunch", SoundChoice.Bell, 14, 0), isTomorrow: false, isNext: false);
        Assert.False(vm.WarnBefore);
        Assert.Equal("", vm.WarnText);
        Assert.DoesNotContain("warns", vm.AccessibleName);
    }

    [Fact]
    public void Disabled_alarm_exposes_its_off_state_in_the_accessible_name()
    {
        var item = Alarm("Lunch", SoundChoice.Bell, 14, 0);
        item.IsEnabled = false;
        var vm = new AlarmItemViewModel(item, isTomorrow: false, isNext: false);
        Assert.False(vm.IsEnabled);
        Assert.Contains("off", vm.AccessibleName);
    }

    [Fact]
    public void Enabled_alarm_has_no_off_cue_and_exposes_a_toggle_label()
    {
        var vm = new AlarmItemViewModel(Alarm("Lunch", SoundChoice.Bell, 14, 0), isTomorrow: false, isNext: false);
        Assert.True(vm.IsEnabled);
        Assert.DoesNotContain("off", vm.AccessibleName);
        Assert.Equal("Alarm at 14:00", vm.ToggleLabel);
    }
}
