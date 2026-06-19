using System.Linq;
using System.Reflection;
using Tidsro.Models;
using Tidsro.Services;

namespace Tidsro.Tests;

// Guards the single-file build: each chime must be embedded in the app assembly,
// not shipped as a loose file next to the exe (which a single-file exe wouldn't carry).
public class SoundResourceTests
{
    private static readonly Assembly App = typeof(SoundService).Assembly;

    private static readonly SoundChoice[] SoundedChoices =
    [
        SoundChoice.SoftChime, SoundChoice.Marimba, SoundChoice.Bell,
        SoundChoice.PianoJingle, SoundChoice.ElectricPianoJingle, SoundChoice.BellJingle,
    ];

    [Theory]
    [InlineData("soft-chime.wav")]
    [InlineData("marimba.wav")]
    [InlineData("bell.wav")]
    [InlineData("Piano-Jingle.wav")]
    [InlineData("Electric-Piano-Jingle.wav")]
    [InlineData("Bell-Jingle.wav")]
    public void Each_chime_is_embedded_in_the_app_assembly(string file)
    {
        var name = App.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("." + file, System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(name);

        using var stream = App.GetManifestResourceStream(name!);
        Assert.NotNull(stream);
        Assert.True(stream!.Length > 0);
    }

    // Regression: PianoJingle and ElectricPianoJingle both resolved to Electric-Piano-Jingle.wav,
    // because the lookup matched EndsWith("Piano-Jingle.wav") — a suffix of the electric file's name.
    [Fact]
    public void Every_sounded_choice_resolves_to_a_distinct_resource()
    {
        var resolved = SoundedChoices.Select(SoundService.ResourceNameFor).ToList();

        Assert.All(resolved, name => Assert.NotNull(name));            // every choice found its file
        Assert.Equal(resolved.Count, resolved.Distinct().Count());    // and no two share one
    }

    [Theory]
    [InlineData(SoundChoice.PianoJingle, "Piano-Jingle.wav")]
    [InlineData(SoundChoice.ElectricPianoJingle, "Electric-Piano-Jingle.wav")]
    public void Sounded_choice_resolves_to_its_own_file(SoundChoice choice, string expectedFile)
    {
        var name = SoundService.ResourceNameFor(choice);
        Assert.NotNull(name);
        Assert.EndsWith("." + expectedFile, name!);
    }
}
