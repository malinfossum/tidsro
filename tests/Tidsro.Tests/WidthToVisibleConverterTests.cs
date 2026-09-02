using System.Globalization;
using System.Windows;
using Tidsro.Views;
using Xunit;

namespace Tidsro.Tests;

public class WidthToVisibleConverterTests
{
    private static object Convert(object? value, string? parameter) =>
        new WidthToVisibleConverter().Convert(value, typeof(Visibility), parameter, CultureInfo.InvariantCulture);

    [Fact]
    public void Wide_shows_at_the_threshold()
        => Assert.Equal(Visibility.Visible, Convert(760d, "Wide"));

    [Fact]
    public void Wide_hides_below_the_threshold()
        => Assert.Equal(Visibility.Collapsed, Convert(759d, "Wide"));

    [Fact]
    public void Narrow_shows_below_the_threshold()
        => Assert.Equal(Visibility.Visible, Convert(759d, "Narrow"));

    [Fact]
    public void Narrow_hides_at_the_threshold()
        => Assert.Equal(Visibility.Collapsed, Convert(760d, "Narrow"));

    [Fact]
    public void Exactly_one_side_is_visible_at_any_width()
    {
        foreach (var width in new[] { 0d, 380d, 759d, 760d, 1200d })
        {
            var wide = Convert(width, "Wide");
            var narrow = Convert(width, "Narrow");
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
    public void An_unknown_parameter_collapses()
        => Assert.Equal(Visibility.Collapsed, Convert(900d, "Sideways"));

    [Fact]
    public void ConvertBack_is_not_supported()
        => Assert.Throws<NotSupportedException>(() =>
            new WidthToVisibleConverter().ConvertBack(Visibility.Visible, typeof(double), "Wide", CultureInfo.InvariantCulture));
}
