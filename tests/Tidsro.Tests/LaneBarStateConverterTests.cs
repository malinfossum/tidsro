using System.Globalization;
using System.Windows;
using Tidsro.Models;
using Tidsro.Views;
using Xunit;

namespace Tidsro.Tests;

public class LaneBarStateConverterTests
{
    // 2026-01-05 is a Monday.
    private static DateTimeOffset At(int hour, int minute) => new(2026, 1, 5, hour, minute, 0, TimeSpan.Zero);

    private static TimerItem Block(int hour, int minute, int endMinute, string label) => new()
    {
        Label = label,
        TriggerType = TriggerType.Recurring,
        RecurringDays = Weekdays.Mon,
        EndsAt = At(hour, minute),
        EndMinute = endMinute,
    };

    /// <summary>The 08:00 band of a Monday holding 07:35-08:20 and 08:20-09:35: one lane, two blocks.</summary>
    private static TimetableLane BackToBackBand()
    {
        var week = TimetableLayout.Build(
            new[] { Block(7, 35, 500, "Lecture"), Block(8, 20, 575, "Seminar") }, At(8, 0));
        return week.Rows.Single(r => r.Slot.Hour == 8 && r.Slot.Minute == 0).Cells[0].Lanes[0];
    }

    private static object Convert(TimetableLane lane, bool isToday, int now) =>
        new LaneBarStateConverter().Convert(
            new object[] { lane.Bars, isToday, now }, typeof(string), null, CultureInfo.InvariantCulture);

    [Fact]
    public void The_band_lights_for_the_block_that_has_just_begun()
    {
        // 08:20: the first block has finished and the second has started, both in this band.
        Assert.Equal("Now", Convert(BackToBackBand(), isToday: true, 500));
    }

    [Fact]
    public void The_band_lights_for_the_block_that_is_about_to_finish()
    {
        Assert.Equal("Now", Convert(BackToBackBand(), isToday: true, 499));
    }

    [Fact]
    public void A_band_whose_blocks_have_all_finished_draws_a_plain_bar()
    {
        Assert.Equal("Block", Convert(BackToBackBand(), isToday: true, 575));
    }

    [Fact]
    public void Another_day_never_lights()
    {
        Assert.Equal("Block", Convert(BackToBackBand(), isToday: false, 500));
    }

    [Fact]
    public void A_band_with_no_block_in_it_draws_nothing()
    {
        var week = TimetableLayout.Build(
            new[] { new TimerItem { Label = "Stretch", TriggerType = TriggerType.Recurring,
                                    RecurringDays = Weekdays.Mon, EndsAt = At(9, 0) } },
            At(8, 0));
        var band = week.Rows.Single(r => r.Slot.Hour == 9 && r.Slot.Minute == 0).Cells[0].Lanes[0];

        Assert.Equal("None", Convert(band, isToday: true, 540));
    }

    [Fact]
    public void Anything_the_bindings_have_not_resolved_draws_nothing()
    {
        Assert.Equal("None", new LaneBarStateConverter().Convert(
            new object[] { DependencyProperty.UnsetValue, true, 500 },
            typeof(string), null, CultureInfo.InvariantCulture));
    }
}
