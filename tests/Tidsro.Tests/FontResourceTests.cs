using System.Collections;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Media;
using Tidsro.Services;

namespace Tidsro.Tests;

// Guards the premise of the walnut redesign: FontSans used to name "Inter", which is not installed
// and was never embedded, so every user silently got Segoe UI for months. A missing font file is a
// silent visual fallback, never an error - so it has to be a test.
public class FontResourceTests
{
    private static readonly Assembly App = typeof(SoundService).Assembly;

    // pack:// URI resolution needs the scheme registered, which normally happens as a side effect
    // of a running WPF Application. There is none in a test host, so touch it explicitly once.
    static FontResourceTests()
    {
        if (!UriParser.IsKnownScheme("pack"))
        {
            _ = new Application();
        }
    }

    private const string FontBaseUri = "pack://application:,,,/Tidsro;component/Assets/fonts/";
    private const string TokensUri = "pack://application:,,,/Tidsro;component/Resources/tokens.xaml";

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

    // Embedding proves the files are in the assembly; it says nothing about whether tokens.xaml
    // actually points at them. Load the real ResourceDictionary and read what FontSans/FontMono
    // resolve to, so a typo'd pack URI, a wrong family name, or a reverted token to "Inter" fails
    // this test instead of silently falling back to Segoe UI / Consolas.
    [Fact]
    public void FontSans_token_resolves_to_the_embedded_IBM_Plex_Sans_family()
    {
        var dict = new ResourceDictionary { Source = new Uri(TokensUri) };

        var fontSans = Assert.IsType<FontFamily>(dict["FontSans"]);

        Assert.Contains("IBM Plex Sans", fontSans.Source);
    }

    [Fact]
    public void FontMono_token_resolves_to_the_embedded_IBM_Plex_Mono_family()
    {
        var dict = new ResourceDictionary { Source = new Uri(TokensUri) };

        var fontMono = Assert.IsType<FontFamily>(dict["FontMono"]);

        Assert.Contains("IBM Plex Mono", fontMono.Source);
    }

    // Belt-and-suspenders on top of the two tests above: ask WPF what families it can actually see
    // at the pack base URI the tokens point into. This is the check from the design spec (§10) and
    // catches a mismatch between what tokens.xaml *says* and what the font files *are* (e.g. wrong
    // internal family name) that the Source-string check above cannot see.
    [Fact]
    public void The_embedded_font_directory_exposes_both_IBM_Plex_families()
    {
        var families = Fonts.GetFontFamilies(new Uri(FontBaseUri)).Select(f => f.Source).ToList();

        Assert.Contains(families, s => s.Contains("IBM Plex Sans"));
        Assert.Contains(families, s => s.Contains("IBM Plex Mono"));
    }
}
