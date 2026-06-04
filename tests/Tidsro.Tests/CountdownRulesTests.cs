using Tidsro.Models;
using Xunit;

namespace Tidsro.Tests;

public class CountdownRulesTests
{
    [Theory]
    [InlineData("25", 0, 25, 0)]        // bare minutes
    [InlineData("5:00", 0, 5, 0)]       // mm:ss
    [InlineData("1:30:00", 1, 30, 0)]   // h:mm:ss
    public void TryParse_accepts_valid_formats(string input, int h, int m, int s)
    {
        Assert.True(CountdownRules.TryParse(input, out var d, out var err));
        Assert.Null(err);
        Assert.Equal(new TimeSpan(h, m, s), d);
    }

    [Theory]
    [InlineData("0")]          // zero
    [InlineData("00:00:00")]   // zero
    [InlineData("25:00:00")]   // > 24h
    [InlineData("abc")]        // garbage
    [InlineData("")]           // empty
    [InlineData("5:99")]       // seconds out of range
    public void TryParse_rejects_invalid(string input)
    {
        Assert.False(CountdownRules.TryParse(input, out _, out var err));
        Assert.NotNull(err);
    }

    [Fact]
    public void TryValidate_rejects_zero_and_over_max()
    {
        Assert.False(CountdownRules.TryValidate(TimeSpan.Zero, out _));
        Assert.False(CountdownRules.TryValidate(TimeSpan.FromHours(25), out _));
        Assert.True(CountdownRules.TryValidate(TimeSpan.FromMinutes(25), out var err));
        Assert.Null(err);
    }
}
