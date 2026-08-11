using System.Globalization;
using System.Windows;
using Tidsro.Views;
using Xunit;

namespace Tidsro.Tests;

public class IndexToVisibleConverterTests
{
    private static object Convert(object? value, object? parameter) =>
        new IndexToVisibleConverter().Convert(value, typeof(Visibility), parameter, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    public void The_selected_panel_is_visible(int selected, string ownIndex) =>
        Assert.Equal(Visibility.Visible, Convert(selected, ownIndex));

    [Theory]
    [InlineData(0, "1")]
    [InlineData(1, "0")]
    public void Every_other_panel_is_collapsed(int selected, string ownIndex) =>
        Assert.Equal(Visibility.Collapsed, Convert(selected, ownIndex));

    [Fact]
    public void A_missing_or_unparseable_parameter_collapses_rather_than_throws() =>
        Assert.Equal(Visibility.Collapsed, Convert(0, null));
}
