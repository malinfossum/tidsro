using System.Collections;
using System.Linq;
using System.Reflection;
using System.Resources;
using Tidsro.Services;

namespace Tidsro.Tests;

// Guards the premise of the walnut redesign: FontSans used to name "Inter", which is not installed
// and was never embedded, so every user silently got Segoe UI for months. A missing font file is a
// silent visual fallback, never an error - so it has to be a test.
public class FontResourceTests
{
    private static readonly Assembly App = typeof(SoundService).Assembly;

    // WPF Resource items live in Tidsro.g.resources, and WPF lower-cases every key.
    private static List<string> ResourceKeys()
    {
        using var stream = App.GetManifestResourceStream("Tidsro.g.resources");
        Assert.NotNull(stream);
        using var reader = new ResourceReader(stream!);
        return reader.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToList();
    }

    [Theory]
    [InlineData("assets/fonts/ibmplexsans-regular.ttf")]
    [InlineData("assets/fonts/ibmplexsans-semibold.ttf")]
    [InlineData("assets/fonts/ibmplexmono-regular.ttf")]
    [InlineData("assets/fonts/ibmplexmono-medium.ttf")]
    public void Each_font_is_embedded_as_a_wpf_resource(string key)
    {
        Assert.Contains(key, ResourceKeys());
    }
}
