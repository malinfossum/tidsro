using System.IO;
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

    // Embedded is not the same as playable: SoundPlayer needs PCM inside the RIFF container, and
    // Load() parses the format without needing a speaker. This is the check that a chime still
    // plays after any change to how sounds are handled.
    [Theory]
    [InlineData(SoundChoice.SoftChime)]
    [InlineData(SoundChoice.Marimba)]
    [InlineData(SoundChoice.Bell)]
    [InlineData(SoundChoice.PianoJingle)]
    [InlineData(SoundChoice.ElectricPianoJingle)]
    [InlineData(SoundChoice.BellJingle)]
    public void Each_chime_loads_in_SoundPlayer(SoundChoice choice)
    {
        using var stream = App.GetManifestResourceStream(SoundService.ResourceNameFor(choice)!)!;
        using var player = new System.Media.SoundPlayer(stream);

        player.Load();   // throws InvalidOperationException on a wave SoundPlayer cannot play
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

    // The custom sound is a file the user chose, not something shipped in the binary.
    [Fact]
    public void The_custom_choice_has_no_embedded_resource()
        => Assert.Null(SoundService.ResourceNameFor(SoundChoice.Custom));

    // A removed or never-chosen .wav must be silence, not a crash mid-alarm.
    [Fact]
    public void Playing_the_custom_choice_with_no_file_chosen_is_silent_and_does_not_throw()
    {
        var svc = new SoundService(() => null);
        svc.Play(SoundChoice.Custom);
    }

    [Fact]
    public void Playing_the_custom_choice_with_a_missing_file_does_not_throw()
    {
        var svc = new SoundService(() => Path.Combine(Path.GetTempPath(), "TidsroTests", "gone.wav"));
        svc.Play(SoundChoice.Custom);
    }
}
