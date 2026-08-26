using System.Globalization;
using Tidsro.Views;
using Xunit;

namespace Tidsro.Tests;

public class SlotLabelConverterTests
{
    private static object Convert(string label, bool isWholeHour, bool wholeHoursOnly) =>
        new SlotLabelConverter().Convert(
            new object[] { label, isWholeHour, wholeHoursOnly }, typeof(string), null, CultureInfo.InvariantCulture);

    [Fact]
    public void A_whole_hour_label_always_shows()
        => Assert.Equal("09:00", Convert("09:00", isWholeHour: true, wholeHoursOnly: true));

    [Fact]
    public void A_half_hour_label_shows_when_the_week_is_short()
        => Assert.Equal("09:30", Convert("09:30", isWholeHour: false, wholeHoursOnly: false));

    [Fact]
    public void A_half_hour_label_is_blanked_on_a_long_week()
        => Assert.Equal("", Convert("09:30", isWholeHour: false, wholeHoursOnly: true));

    [Fact]
    public void A_whole_hour_label_shows_even_on_a_long_week()
        => Assert.Equal("09:00", Convert("09:00", isWholeHour: true, wholeHoursOnly: true));

    [Fact]
    public void Missing_or_malformed_values_blank_rather_than_throw()
        => Assert.Equal("", new SlotLabelConverter().Convert(
            new object[] { "09:00" }, typeof(string), null, CultureInfo.InvariantCulture));

    [Fact]
    public void ConvertBack_is_not_supported()
        => Assert.Throws<NotSupportedException>(() =>
            new SlotLabelConverter().ConvertBack("09:00", new[] { typeof(string) }, null, CultureInfo.InvariantCulture));
}
