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
        Assert.Equal(2, clean.SchemaVersion);
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
}
