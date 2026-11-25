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
    [InlineData(SoundChoice.PianoJingle, "Piano jingle")]
    [InlineData(SoundChoice.ElectricPianoJingle, "Electric piano jingle")]
    [InlineData(SoundChoice.BellJingle, "Bell jingle")]
    // Flat wording on purpose: the picker rows sit in a three-column grid where a file name long
    // enough to be recognizable would push the preview button off the end. Settings names the file.
    [InlineData(SoundChoice.Custom, "My sound")]
    public void Converts_each_choice_to_a_friendly_label(SoundChoice choice, string expected)
    {
        var converter = new SoundChoiceToLabelConverter();
        var result = converter.Convert(choice, typeof(string), null, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }
}
