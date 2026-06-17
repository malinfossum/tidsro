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
}
