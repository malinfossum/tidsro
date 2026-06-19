using System.Linq;
using System.Reflection;
using Tidsro.Services;

namespace Tidsro.Tests;

// Guards the single-file build: each chime must be embedded in the app assembly,
// not shipped as a loose file next to the exe (which a single-file exe wouldn't carry).
public class SoundResourceTests
{
    private static readonly Assembly App = typeof(SoundService).Assembly;

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
            .FirstOrDefault(n => n.EndsWith(file, System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(name);

        using var stream = App.GetManifestResourceStream(name!);
        Assert.NotNull(stream);
        Assert.True(stream!.Length > 0);
    }
}
