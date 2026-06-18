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
        Assert.Equal(3, clean.SchemaVersion);
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
}
