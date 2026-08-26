using System.Globalization;
using System.Windows;
using Tidsro.Views;
using Xunit;

namespace Tidsro.Tests;

public class CountToVisibleConverterTests
{
    private static object Convert(object? value) =>
        new CountToVisibleConverter().Convert(value, typeof(Visibility), null, CultureInfo.InvariantCulture);

    [Fact]
    public void A_populated_collection_is_visible() => Assert.Equal(Visibility.Visible, Convert(3));

    [Fact]
    public void An_empty_collection_collapses() => Assert.Equal(Visibility.Collapsed, Convert(0));

    [Fact]
    public void A_non_integer_collapses() => Assert.Equal(Visibility.Collapsed, Convert("three"));
}
