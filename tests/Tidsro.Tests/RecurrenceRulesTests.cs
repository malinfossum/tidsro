using Tidsro.Models;
using Xunit;

namespace Tidsro.Tests;

public class RecurrenceRulesTests
{
    // 2026-01-01 is a Thursday. Jan 2 Fri, 3 Sat, 4 Sun, 5 Mon, 6 Tue, 7 Wed.
    private static DateTimeOffset At(int day, int hour, int minute) =>
        new(2026, 1, day, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void NextOccurrence_uses_today_when_the_weekday_matches_and_time_is_ahead()
    {
        var next = RecurrenceRules.NextOccurrence(At(1, 9, 0), 10, 0, RecurrenceRules.AllDays);
        Assert.Equal(At(1, 10, 0), next);   // Thu 10:00 still ahead
    }

    [Fact]
    public void NextOccurrence_rolls_forward_when_today_has_passed()
    {
        var weekdays = RecurrenceRules.DaysFor(RepeatOption.Weekdays, Weekdays.None);
        var next = RecurrenceRules.NextOccurrence(At(1, 11, 0), 10, 0, weekdays);
        Assert.Equal(At(2, 10, 0), next);   // Thu passed -> Fri 10:00
    }

    [Fact]
    public void NextOccurrence_skips_the_weekend_for_a_weekdays_set()
    {
        var weekdays = RecurrenceRules.DaysFor(RepeatOption.Weekdays, Weekdays.None);
        var next = RecurrenceRules.NextOccurrence(At(2, 11, 0), 9, 0, weekdays);
        Assert.Equal(At(5, 9, 0), next);    // Fri passed -> skip Sat/Sun -> Mon 09:00
    }

    [Fact]
    public void NextOccurrence_is_strictly_after_now()
    {
        var next = RecurrenceRules.NextOccurrence(At(1, 9, 0), 9, 0, RecurrenceRules.AllDays);
        Assert.Equal(At(2, 9, 0), next);    // 09:00 == now is ambiguous -> tomorrow
    }

    [Fact]
    public void MostRecentOccurrence_returns_today_when_time_has_passed()
    {
        var prev = RecurrenceRules.MostRecentOccurrence(At(1, 10, 5), 10, 0, RecurrenceRules.AllDays);
        Assert.Equal(At(1, 10, 0), prev);
    }

    [Fact]
    public void MostRecentOccurrence_skips_the_weekend_backwards()
    {
        var weekdays = RecurrenceRules.DaysFor(RepeatOption.Weekdays, Weekdays.None);
        var prev = RecurrenceRules.MostRecentOccurrence(At(5, 8, 0), 10, 0, weekdays);
        Assert.Equal(At(2, 10, 0), prev);   // Mon 08:00, 10:00 not yet -> back past weekend -> Fri 10:00
    }

    [Theory]
    [InlineData(RepeatOption.Once, Weekdays.None)]
    [InlineData(RepeatOption.Weekends, Weekdays.Sat | Weekdays.Sun)]
    public void DaysFor_resolves_presets(RepeatOption option, Weekdays expected)
    {
        Assert.Equal(expected, RecurrenceRules.DaysFor(option, Weekdays.None));
    }

    [Fact]
    public void DaysFor_weekdays_is_monday_to_friday()
    {
        var expected = Weekdays.Mon | Weekdays.Tue | Weekdays.Wed | Weekdays.Thu | Weekdays.Fri;
        Assert.Equal(expected, RecurrenceRules.DaysFor(RepeatOption.Weekdays, Weekdays.None));
    }

    [Fact]
    public void DaysFor_custom_passes_through()
    {
        var custom = Weekdays.Mon | Weekdays.Wed | Weekdays.Fri;
        Assert.Equal(custom, RecurrenceRules.DaysFor(RepeatOption.Custom, custom));
    }

    [Theory]
    [InlineData(Weekdays.None, "once")]
    [InlineData(Weekdays.Sat | Weekdays.Sun, "Weekends")]
    [InlineData(Weekdays.Mon | Weekdays.Wed | Weekdays.Fri, "Mon Wed Fri")]
    public void CadenceLabel_names_the_set(Weekdays days, string expected)
    {
        Assert.Equal(expected, RecurrenceRules.CadenceLabel(days));
    }

    [Fact]
    public void CadenceLabel_names_daily_and_weekdays()
    {
        Assert.Equal("Daily", RecurrenceRules.CadenceLabel(RecurrenceRules.AllDays));
        var weekdays = Weekdays.Mon | Weekdays.Tue | Weekdays.Wed | Weekdays.Thu | Weekdays.Fri;
        Assert.Equal("Weekdays", RecurrenceRules.CadenceLabel(weekdays));
    }

    [Theory]
    [InlineData(Weekdays.None, RepeatOption.Once)]
    [InlineData(Weekdays.Sat | Weekdays.Sun, RepeatOption.Weekends)]
    [InlineData(Weekdays.Mon | Weekdays.Wed, RepeatOption.Custom)]
    public void OptionFor_reverses_a_day_set(Weekdays days, RepeatOption expected)
    {
        Assert.Equal(expected, RecurrenceRules.OptionFor(days));
    }
}
