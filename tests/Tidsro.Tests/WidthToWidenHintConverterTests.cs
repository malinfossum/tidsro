using System.Globalization;
using System.Windows;
using Tidsro.Models;
using Tidsro.Views;
using Xunit;

namespace Tidsro.Tests;

public class WidthToWidenHintConverterTests
{
    private const double FiveColumns = TimetableLayout.MinimumGridWidth;

    private static object Convert(double panel, double window, double workArea, int lanes = 1, int columns = 5) =>
        new WidthToWidenHintConverter { WorkAreaWidth = workArea }.Convert(
            new object[] { panel, window, lanes, (object)columns },
            typeof(Visibility), null, CultureInfo.InvariantCulture);

    [Fact]
    public void Nothing_is_said_while_the_grid_is_drawing()
        => Assert.Equal(Visibility.Collapsed, Convert(FiveColumns, FiveColumns + 60, 1920));

    [Fact]
    public void The_offer_appears_when_widening_would_reveal_the_grid()
        => Assert.Equal(Visibility.Visible, Convert(500, 560, 1920));

    [Fact]
    public void The_offer_stays_away_on_a_screen_too_narrow_to_keep_it()
        => Assert.Equal(Visibility.Collapsed, Convert(500, 560, FiveColumns + 59));

    [Fact]
    public void An_empty_week_is_offered_nothing()
        => Assert.Equal(Visibility.Collapsed, Convert(500, 560, 1920, columns: 0));

    [Fact]
    public void Anything_the_bindings_have_not_resolved_says_nothing()
        => Assert.Equal(Visibility.Collapsed, new WidthToWidenHintConverter { WorkAreaWidth = 1920 }.Convert(
            new object[] { DependencyProperty.UnsetValue, 560, 1, 5 },
            typeof(Visibility), null, CultureInfo.InvariantCulture));
}
