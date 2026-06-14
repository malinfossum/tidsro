using System.Globalization;
using Tidsro.Models;
using Tidsro.Views;

namespace Tidsro.Tests;

public class SoundChoiceToLabelConverterTests
{
    [Theory]
    [InlineData(SoundChoice.None, "Silent")]
    [InlineData(SoundChoice.SoftChime, "Soft chime")]
    [InlineData(SoundChoice.Marimba, "Marimba")]
    [InlineData(SoundChoice.Bell, "Bell")]
    public void Converts_each_choice_to_a_friendly_label(SoundChoice choice, string expected)
    {
        var converter = new SoundChoiceToLabelConverter();
        var result = converter.Convert(choice, typeof(string), null, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }
}
