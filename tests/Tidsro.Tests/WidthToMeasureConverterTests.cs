using System;
using System.Globalization;
using Tidsro.Views;
using Xunit;

namespace Tidsro.Tests;

public class WidthToMeasureConverterTests
{
    private static double Convert(object? value) =>
        (double)new WidthToMeasureConverter().Convert(value, typeof(double), null, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(0)]      // before first layout, ActualWidth is 0
    [InlineData(376)]    // the default 440 window, minus padding
    [InlineData(760)]    // 66% lands exactly on the floor here
    public void A_narrow_window_gets_the_floor(double available) =>
        Assert.Equal(WidthToMeasureConverter.Floor, Convert(available));

    [Fact]
    public void A_mid_window_gets_its_share()
    {
        // 66% of 900 = 594: between the floor and the ceiling, so the share passes through.
        Assert.Equal(900 * WidthToMeasureConverter.Share, Convert(900.0), precision: 9);
    }

    [Theory]
    [InlineData(1200)]
    [InlineData(3440)]   // ultrawide
    public void A_wide_window_stops_at_the_ceiling(double available) =>
        Assert.Equal(WidthToMeasureConverter.Ceiling, Convert(available));

    [Fact]
    public void The_share_grows_monotonically_between_the_clamps()
    {
        // The point of a proportional measure: wider window, wider column - never a jump back.
        var previous = 0.0;
        for (var width = 300.0; width <= 1300.0; width += 50)
        {
            var measure = Convert(width);
            Assert.True(measure >= previous, $"measure shrank at {width}");
            previous = measure;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(double.NaN)]
    public void Anything_unusable_falls_back_to_the_floor_rather_than_throws(object? value) =>
        Assert.Equal(WidthToMeasureConverter.Floor, Convert(value));

    [Fact]
    public void ConvertBack_is_not_supported() =>
        Assert.Throws<NotSupportedException>(() =>
            new WidthToMeasureConverter().ConvertBack(502.0, typeof(double), null, CultureInfo.InvariantCulture));
}
