using System.Text.Json;
using Tidsro.Models;
using Xunit;

namespace Tidsro.Tests;

public class TidsroDataTests
{
    private static AlarmRecord Good(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        FireAt = new DateTime(2026, 6, 17, 14, 0, 0, DateTimeKind.Local),
        Label = "Lunch",
        Sound = SoundChoice.Bell,
    };

    [Fact]
    public void Sanitized_keeps_a_valid_alarm_and_defaults_null_settings()
    {
        var data = new TidsroData { Settings = null, Alarms = { Good() } };
        var clean = data.Sanitized();
        Assert.NotNull(clean.Settings);                 // null settings -> defaults
        Assert.Equal(4, clean.SchemaVersion);
        Assert.Single(clean.Alarms);
    }

    [Fact]
    public void Sanitized_drops_an_undefined_sound()
    {
        var bad = Good(); bad.Sound = (SoundChoice)999;
        var clean = new TidsroData { Settings = new(), Alarms = { bad } }.Sanitized();
        Assert.Empty(clean.Alarms);
    }

    [Fact]
    public void Sanitized_drops_a_default_or_extreme_FireAt()
    {
        var zero = Good(); zero.FireAt = default;
        var max = Good(); max.FireAt = DateTime.MaxValue;
        var clean = new TidsroData { Settings = new(), Alarms = { zero, max } }.Sanitized();
        Assert.Empty(clean.Alarms);                     // both rejected -> arming can never throw
    }

    [Fact]
    public void Sanitized_drops_duplicate_ids_keeping_the_first()
    {
        var id = Guid.NewGuid();
        var first = Good(id); first.Label = "first";
        var second = Good(id); second.Label = "second";
        var clean = new TidsroData { Settings = new(), Alarms = { first, second } }.Sanitized();
        Assert.Equal("first", Assert.Single(clean.Alarms).Label);
    }

    [Fact]
    public void Sanitized_trims_and_caps_labels()
    {
        var spaced = Good(); spaced.Label = "  Lunch  ";
        var huge = Good(); huge.Label = new string('x', 500);
        var clean = new TidsroData { Settings = new(), Alarms = { spaced, huge } }.Sanitized();
        Assert.Equal("Lunch", clean.Alarms[0].Label);
        Assert.Equal(200, clean.Alarms[1].Label!.Length);
    }

    private static RecurringAlarmRecord GoodRec(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Hour = 7,
        Minute = 0,
        Days = Weekdays.Mon | Weekdays.Wed | Weekdays.Fri,
        Label = "Stand-up",
        Sound = SoundChoice.Bell,
        NextFireAt = new DateTime(2026, 6, 19, 7, 0, 0, DateTimeKind.Local),
    };

    [Fact]
    public void Sanitized_keeps_a_valid_recurring_alarm()
    {
        var clean = new TidsroData { Settings = new(), RecurringAlarms = { GoodRec() } }.Sanitized();
        var r = Assert.Single(clean.RecurringAlarms);
        Assert.Equal(Weekdays.Mon | Weekdays.Wed | Weekdays.Fri, r.Days);
    }

    [Fact]
    public void Sanitized_drops_a_recurring_alarm_with_no_days()
    {
        var bad = GoodRec(); bad.Days = Weekdays.None;
        var clean = new TidsroData { Settings = new(), RecurringAlarms = { bad } }.Sanitized();
        Assert.Empty(clean.RecurringAlarms);
    }

    [Fact]
    public void Sanitized_strips_unknown_day_bits()
    {
        var bad = GoodRec(); bad.Days = (Weekdays)128 | Weekdays.Mon;   // 128 is not a real day
        var clean = new TidsroData { Settings = new(), RecurringAlarms = { bad } }.Sanitized();
        Assert.Equal(Weekdays.Mon, Assert.Single(clean.RecurringAlarms).Days);
    }

    [Theory]
    [InlineData(24, 0)]
    [InlineData(-1, 0)]
    [InlineData(7, 60)]
    [InlineData(7, -1)]
    public void Sanitized_drops_an_out_of_range_time(int hour, int minute)
    {
        var bad = GoodRec(); bad.Hour = hour; bad.Minute = minute;
        var clean = new TidsroData { Settings = new(), RecurringAlarms = { bad } }.Sanitized();
        Assert.Empty(clean.RecurringAlarms);
    }

    [Fact]
    public void Sanitized_drops_a_recurring_alarm_with_undefined_sound_or_bad_next_fire()
    {
        var badSound = GoodRec(); badSound.Sound = (SoundChoice)999;
        var badNext = GoodRec(); badNext.NextFireAt = default;
        var clean = new TidsroData { Settings = new(), RecurringAlarms = { badSound, badNext } }.Sanitized();
        Assert.Empty(clean.RecurringAlarms);
    }

    [Fact]
    public void Sanitized_drops_duplicate_recurring_ids_keeping_the_first()
    {
        var id = Guid.NewGuid();
        var first = GoodRec(id); first.Label = "first";
        var second = GoodRec(id); second.Label = "second";
        var clean = new TidsroData { Settings = new(), RecurringAlarms = { first, second } }.Sanitized();
        Assert.Equal("first", Assert.Single(clean.RecurringAlarms).Label);
    }

    [Fact]
    public void Sanitized_preserves_the_warn_before_flag_on_a_clock_alarm()
    {
        var a = Good(); a.WarnBefore = true;
        var clean = new TidsroData { Settings = new(), Alarms = { a } }.Sanitized();
        Assert.True(Assert.Single(clean.Alarms).WarnBefore);
    }

    [Fact]
    public void Sanitized_preserves_the_warn_before_flag_on_a_recurring_alarm()
    {
        var r = GoodRec(); r.WarnBefore = true;
        var clean = new TidsroData { Settings = new(), RecurringAlarms = { r } }.Sanitized();
        Assert.True(Assert.Single(clean.RecurringAlarms).WarnBefore);
    }

    [Fact]
    public void A_record_is_enabled_by_default()
    {
        Assert.True(Good().Enabled);
        Assert.True(GoodRec().Enabled);
    }

    [Fact]
    public void Sanitized_preserves_the_enabled_flag_on_a_clock_alarm()
    {
        var a = Good(); a.Enabled = false;
        var clean = new TidsroData { Settings = new(), Alarms = { a } }.Sanitized();
        Assert.False(Assert.Single(clean.Alarms).Enabled);
    }

    [Fact]
    public void Sanitized_preserves_the_enabled_flag_on_a_recurring_alarm()
    {
        var r = GoodRec(); r.Enabled = false;
        var clean = new TidsroData { Settings = new(), RecurringAlarms = { r } }.Sanitized();
        Assert.False(Assert.Single(clean.RecurringAlarms).Enabled);
    }

    [Fact]
    public void Sanitized_keeps_a_valid_selected_tab()
    {
        var data = new TidsroData { Settings = new AppSettings { SelectedTab = 1 } };
        Assert.Equal(1, data.Sanitized().Settings!.SelectedTab);
    }

    [Fact]
    public void Sanitized_resets_a_selected_tab_outside_the_range()
    {
        var high = new TidsroData { Settings = new AppSettings { SelectedTab = 7 } };
        var low = new TidsroData { Settings = new AppSettings { SelectedTab = -1 } };
        Assert.Equal(0, high.Sanitized().Settings!.SelectedTab);
        Assert.Equal(0, low.Sanitized().Settings!.SelectedTab);
    }

    [Fact]
    public void A_file_written_before_the_tab_shell_loads_on_quick_timers()
    {
        // A v4 file has no SelectedTab key at all — the absent-key case must land on 0, not throw.
        var json = """
        {"SchemaVersion":4,"Settings":{"LaunchAtStartup":false,"DefaultSound":0},
         "Alarms":[],"RecurringAlarms":[]}
        """;
        var data = JsonSerializer.Deserialize<TidsroData>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal(0, data.Sanitized().Settings!.SelectedTab);
    }

    [Fact]
    public void The_week_tab_index_survives_sanitising()
    {
        var settings = new AppSettings { SelectedTab = 2 }.Sanitized();
        Assert.Equal(2, settings.SelectedTab);
    }

    [Fact]
    public void A_tab_index_past_the_last_tab_falls_back_to_the_first()
    {
        var settings = new AppSettings { SelectedTab = 3 }.Sanitized();
        Assert.Equal(0, settings.SelectedTab);
    }
}
