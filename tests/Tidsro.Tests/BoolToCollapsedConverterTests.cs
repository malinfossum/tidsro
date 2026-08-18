using System.Globalization;
using System.Windows;
using Tidsro.Views;
using Xunit;

namespace Tidsro.Tests;

// Polarity matters and nothing else can catch it: this converter decides which running-timer rows
// the list renders, and inverted it would hide every row EXCEPT the one the hero is already showing.
// XAML bindings are not exercised by the suite, so the converter is pinned here directly.
public class BoolToCollapsedConverterTests
{
    private static object Convert(object? value) =>
        new BoolToCollapsedConverter().Convert(value, typeof(Visibility), null, CultureInfo.InvariantCulture);

    [Fact]
    public void The_row_the_hero_already_shows_is_collapsed() =>
        Assert.Equal(Visibility.Collapsed, Convert(true));

    [Fact]
    public void Every_other_row_stays_visible() =>
        Assert.Equal(Visibility.Visible, Convert(false));

    [Fact]
    public void An_unset_value_stays_visible_rather_than_hiding_the_row() =>
        Assert.Equal(Visibility.Visible, Convert(null));
}
