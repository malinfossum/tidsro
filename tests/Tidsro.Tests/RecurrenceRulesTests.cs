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
        var next = RecurrenceRules.NextOccurrence(At(1, 9, 0), TimeZoneInfo.Utc, 10, 0, RecurrenceRules.AllDays);
        Assert.Equal(At(1, 10, 0), next);   // Thu 10:00 still ahead
    }

    [Fact]
    public void NextOccurrence_rolls_forward_when_today_has_passed()
    {
        var weekdays = RecurrenceRules.DaysFor(RepeatOption.Weekdays, Weekdays.None);
        var next = RecurrenceRules.NextOccurrence(At(1, 11, 0), TimeZoneInfo.Utc, 10, 0, weekdays);
        Assert.Equal(At(2, 10, 0), next);   // Thu passed -> Fri 10:00
    }

    [Fact]
    public void NextOccurrence_skips_the_weekend_for_a_weekdays_set()
    {
        var weekdays = RecurrenceRules.DaysFor(RepeatOption.Weekdays, Weekdays.None);
        var next = RecurrenceRules.NextOccurrence(At(2, 11, 0), TimeZoneInfo.Utc, 9, 0, weekdays);
        Assert.Equal(At(5, 9, 0), next);    // Fri passed -> skip Sat/Sun -> Mon 09:00
    }

    [Fact]
    public void NextOccurrence_is_strictly_after_now()
    {
        var next = RecurrenceRules.NextOccurrence(At(1, 9, 0), TimeZoneInfo.Utc, 9, 0, RecurrenceRules.AllDays);
        Assert.Equal(At(2, 9, 0), next);    // 09:00 == now is ambiguous -> tomorrow
    }

    [Fact]
    public void MostRecentOccurrence_returns_today_when_time_has_passed()
    {
        var prev = RecurrenceRules.MostRecentOccurrence(At(1, 10, 5), TimeZoneInfo.Utc, 10, 0, RecurrenceRules.AllDays);
        Assert.Equal(At(1, 10, 0), prev);
    }

    [Fact]
    public void MostRecentOccurrence_skips_the_weekend_backwards()
    {
        var weekdays = RecurrenceRules.DaysFor(RepeatOption.Weekdays, Weekdays.None);
        var prev = RecurrenceRules.MostRecentOccurrence(At(5, 8, 0), TimeZoneInfo.Utc, 10, 0, weekdays);
        Assert.Equal(At(2, 10, 0), prev);   // Mon 08:00, 10:00 not yet -> back past weekend -> Fri 10:00
    }

    [Fact]
    public void MostRecentOccurrence_includes_an_exact_now_match()
    {
        var prev = RecurrenceRules.MostRecentOccurrence(At(1, 10, 0), TimeZoneInfo.Utc, 10, 0, RecurrenceRules.AllDays);
        Assert.Equal(At(1, 10, 0), prev);   // exactly now counts as "at or before"
    }

    [Theory]
    [InlineData(RepeatOption.Once, Weekdays.None)]
    [InlineData(RepeatOption.Daily, Weekdays.Mon | Weekdays.Tue | Weekdays.Wed | Weekdays.Thu | Weekdays.Fri | Weekdays.Sat | Weekdays.Sun)]
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

    // Europe/Oslo: CEST (+02:00) ends 2026-10-25 03:00, CET (+01:00) ends 2026-03-29 02:00.
    private static readonly TimeZoneInfo Oslo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");

    [Fact]
    public void NextOccurrence_uses_the_offset_in_force_on_the_day_it_lands()
    {
        // Sat 2026-10-24 22:00 CEST; a daily 07:00 lands on Sunday, after the switch to CET.
        var now = new DateTimeOffset(2026, 10, 24, 22, 0, 0, TimeSpan.FromHours(2));
        var next = RecurrenceRules.NextOccurrence(now, Oslo, 7, 0, RecurrenceRules.AllDays);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 7, 0, 0, TimeSpan.FromHours(1)), next);
    }

    [Fact]
    public void MostRecentOccurrence_uses_the_offset_in_force_on_the_day_it_landed()
    {
        // Mon 2026-03-30 08:00 CEST; the weekday 10:00 before it was Friday 27th, still on CET.
        var now = new DateTimeOffset(2026, 3, 30, 8, 0, 0, TimeSpan.FromHours(2));
        var weekdays = RecurrenceRules.DaysFor(RepeatOption.Weekdays, Weekdays.None);
        var prev = RecurrenceRules.MostRecentOccurrence(now, Oslo, 10, 0, weekdays);
        Assert.Equal(new DateTimeOffset(2026, 3, 27, 10, 0, 0, TimeSpan.FromHours(1)), prev);
    }
}
