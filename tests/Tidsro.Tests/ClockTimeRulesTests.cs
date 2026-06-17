using Tidsro.Models;
using Xunit;

namespace Tidsro.Tests;

public class ClockTimeRulesTests
{
    [Theory]
    [InlineData("09:00", 9, 0)]
    [InlineData("9:00", 9, 0)]      // single-digit hour ok
    [InlineData("00:00", 0, 0)]
    [InlineData("23:59", 23, 59)]
    [InlineData(" 7:5 ", 7, 5)]     // trimmed; minutes "5" == 05
    public void TryParse_accepts_valid_times(string input, int hour, int minute)
    {
        Assert.True(ClockTimeRules.TryParse(input, out var h, out var m, out var err));
        Assert.Null(err);
        Assert.Equal(hour, h);
        Assert.Equal(minute, m);
    }

    [Theory]
    [InlineData("")]         // empty
    [InlineData("   ")]      // blank
    [InlineData("24:00")]    // hour out of range
    [InlineData("12:60")]    // minute out of range
    [InlineData("-1:00")]    // negative
    [InlineData("9")]        // missing minutes
    [InlineData("9:00:00")]  // too many parts
    [InlineData("abc")]      // junk
    [InlineData("9:ab")]     // junk minutes
    public void TryParse_rejects_invalid_times(string input)
    {
        Assert.False(ClockTimeRules.TryParse(input, out _, out _, out var err));
        Assert.NotNull(err);
    }

    [Fact]
    public void ComputeFireAt_uses_today_when_the_time_is_still_ahead()
    {
        var now = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var fire = ClockTimeRules.ComputeFireAt(now, 10, 30);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 10, 30, 0, TimeSpan.Zero), fire);
    }

    [Fact]
    public void ComputeFireAt_rolls_to_tomorrow_when_the_time_has_passed()
    {
        var now = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var fire = ClockTimeRules.ComputeFireAt(now, 8, 0);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 8, 0, 0, TimeSpan.Zero), fire);
    }

    [Fact]
    public void ComputeFireAt_rolls_to_tomorrow_when_the_time_equals_now()
    {
        var now = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var fire = ClockTimeRules.ComputeFireAt(now, 9, 0);     // "now" is ambiguous → tomorrow
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero), fire);
    }
}
