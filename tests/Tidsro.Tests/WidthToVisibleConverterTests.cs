using System.Globalization;
using System.Windows;
using Tidsro.Models;
using Tidsro.Views;
using Xunit;

namespace Tidsro.Tests;

public class WidthToVisibleConverterTests
{
    private const double Base = TimetableLayout.MinimumGridWidth;

    private static object Convert(object width, string? parameter, int lanes = 1, int columns = 5) =>
        new WidthToVisibleConverter().Convert(
            new[] { width, lanes, (object)columns }, typeof(Visibility), parameter, CultureInfo.InvariantCulture);

    [Fact]
    public void Wide_shows_at_the_threshold()
        => Assert.Equal(Visibility.Visible, Convert(Base, "Wide"));

    [Fact]
    public void Wide_hides_below_the_threshold()
        => Assert.Equal(Visibility.Collapsed, Convert(Base - 1, "Wide"));

    [Fact]
    public void Narrow_shows_below_the_threshold()
        => Assert.Equal(Visibility.Visible, Convert(Base - 1, "Narrow"));

    [Fact]
    public void Narrow_hides_at_the_threshold()
        => Assert.Equal(Visibility.Collapsed, Convert(Base, "Narrow"));

    [Fact]
    public void A_second_lane_raises_the_width_the_grid_asks_for()
    {
        // A width that draws a one-lane week draws two lanes at ~70px each, which is a bar with no
        // label. The agenda lists the same alarms and reads at any width, so it takes over.
        Assert.Equal(Visibility.Collapsed, Convert(Base, "Wide", lanes: 2));
        Assert.Equal(Visibility.Visible, Convert(Base, "Narrow", lanes: 2));
    }

    [Fact]
    public void The_grid_returns_once_two_lanes_fit()
    {
        var needed = TimetableLayout.RequiredGridWidth(2, 5);

        Assert.Equal(Visibility.Visible, Convert(needed, "Wide", lanes: 2));
        Assert.Equal(Visibility.Collapsed, Convert(needed - 1, "Wide", lanes: 2));
    }

    [Fact]
    public void Exactly_one_side_is_visible_at_any_width()
    {
        foreach (var width in new[] { 0d, 380d, Base - 1, Base, 1200d, 2400d })
            foreach (var lanes in new[] { 1, 2, 3 })
            {
                var wide = Convert(width, "Wide", lanes);
                var narrow = Convert(width, "Narrow", lanes);
                Assert.NotEqual(wide, narrow);
            }
    }

    [Fact]
    public void NaN_width_falls_back_to_narrow()
    {
        Assert.Equal(Visibility.Visible, Convert(double.NaN, "Narrow"));
        Assert.Equal(Visibility.Collapsed, Convert(double.NaN, "Wide"));
    }

    [Fact]
    public void A_binding_that_has_not_produced_values_yet_falls_back_to_narrow()
    {
        // MultiBinding hands over DependencyProperty.UnsetValue until every source has resolved.
        var values = new object[] { DependencyProperty.UnsetValue, DependencyProperty.UnsetValue, DependencyProperty.UnsetValue };
        var converter = new WidthToVisibleConverter();

        Assert.Equal(
            Visibility.Visible,
            converter.Convert(values, typeof(Visibility), "Narrow", CultureInfo.InvariantCulture));
        Assert.Equal(
            Visibility.Collapsed,
            converter.Convert(values, typeof(Visibility), "Wide", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void An_unknown_parameter_collapses()
        => Assert.Equal(Visibility.Collapsed, Convert(2400d, "Sideways"));

    [Fact]
    public void ConvertBack_is_not_supported()
        => Assert.Throws<NotSupportedException>(() =>
            new WidthToVisibleConverter().ConvertBack(
                Visibility.Visible, new[] { typeof(double) }, "Wide", CultureInfo.InvariantCulture));
}
