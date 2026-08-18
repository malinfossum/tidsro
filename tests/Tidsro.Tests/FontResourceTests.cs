using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Media;
using Tidsro.Services;
using Tidsro.Views;

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
    private const string ApplicationPrefix = "pack://application:,,,/";
    private const string AssemblyComponent = "Tidsro;component";

    // WPF Resource items live in Tidsro.g.resources, and WPF lower-cases every key.
    private static List<string> ResourceKeys()
    {
        using var stream = App.GetManifestResourceStream("Tidsro.g.resources");
        Assert.NotNull(stream);
        using var reader = new ResourceReader(stream!);
        return reader.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToList();
    }

    private static string ResourceText(string key)
    {
        using var stream = App.GetManifestResourceStream("Tidsro.g.resources");
        Assert.NotNull(stream);
        using var reader = new ResourceReader(stream!);
        var value = reader.Cast<DictionaryEntry>().Single(e => (string)e.Key == key).Value;
        using var content = Assert.IsAssignableFrom<Stream>(value);
        return new StreamReader(content, detectEncodingFromByteOrderMarks: true).ReadToEnd();
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

    // OFL 1.1 §2 requires the licence to accompany every redistributed copy of the font software.
    // The installer ships OFL-IBMPlex.txt beside the app, but publish.ps1 also produces a standalone
    // portable Tidsro.exe with no companion files - so for that channel the licence only travels if
    // it is inside the binary. The path comes from LicenceDialog, so moving the file without moving
    // the reader (or vice versa) fails here rather than at a user's Settings ▸ View font licence.
    [Fact]
    public void The_font_licence_is_embedded_where_the_dialog_reads_it()
    {
        Assert.Contains(LicenceDialog.EmbeddedLicencePath.ToLowerInvariant(), ResourceKeys());
    }

    [Fact]
    public void The_embedded_licence_is_the_OFL_text_itself_not_a_pointer_to_it()
    {
        var text = ResourceText(LicenceDialog.EmbeddedLicencePath.ToLowerInvariant());

        Assert.Contains("SIL OPEN FONT LICENSE Version 1.1", text, StringComparison.Ordinal);
        Assert.Contains("PERMISSION & CONDITIONS", text, StringComparison.Ordinal);
        Assert.Contains("TERMINATION", text, StringComparison.Ordinal);
    }

    // FontFamily.Source is a literal echo of the string a FontFamily was built from - it proves
    // nothing about whether WPF actually resolved that string to a font. And the mechanism that
    // would normally do the resolving for an application-relative pack URI
    // ("pack://application:,,,/<path>#<name>") is Application.ResourceAssembly, which is write-once
    // and - verified empirically - is already claimed by the test host (testhost.exe, not
    // Tidsro.dll) before any code in this class runs, so there's no way to point it at Tidsro.dll
    // from here.
    //
    // Instead, take the token's own text (its location and family name, exactly as XAML parsed it)
    // and resolve it through the fully assembly-qualified pack form ("Tidsro;component/...") that
    // Application.ResourceAssembly would have produced - the same mechanism
    // The_embedded_font_directory_exposes_both_IBM_Plex_families already uses successfully below.
    // A wrong location (e.g. the trailing slash before '#' missing) makes Fonts.GetFontFamilies
    // return no families at all; a wrong family name makes it return families that don't match.
    // Either way ResolveTokenTypefaces comes back empty - only a token whose location AND family
    // name are both correct resolves to real typefaces.
    //
    // The match against the returned family must be exact, not a substring check: Assets/fonts/
    // holds two families that share a prefix ("IBM Plex Sans" and "IBM Plex Mono"), so a truncated
    // token name like "IBM Plex" would still Contains-match the real "IBM Plex Sans" family and
    // resolve to genuinely-correct typefaces, passing a test that should have caught it. WPF reports
    // each resolved family's Source relative to the base URI it was looked up from - "./#<name>" -
    // so that's what the exact family name is compared against.
    private static List<Typeface> ResolveTokenTypefaces(string tokenText)
    {
        var hashIndex = tokenText.IndexOf('#');
        Assert.True(hashIndex > 0, $"token has no '#' family separator: {tokenText}");

        var location = tokenText[..hashIndex];
        var primaryFamilyName = tokenText[(hashIndex + 1)..].Split(',')[0].Trim();

        Assert.StartsWith(ApplicationPrefix, location);
        var relativePath = location[ApplicationPrefix.Length..];
        var qualifiedLocation = new Uri($"{ApplicationPrefix}{AssemblyComponent}/{relativePath}");

        var expectedSource = $"./#{primaryFamilyName}";
        var family = Fonts.GetFontFamilies(qualifiedLocation)
            .FirstOrDefault(f => f.Source == expectedSource);

        return family?.GetTypefaces().ToList() ?? [];
    }

    private static List<Typeface> EmbeddedTypefaces(string familyNameContains) =>
        Fonts.GetFontFamilies(new Uri(FontBaseUri))
            .Single(f => f.Source.Contains(familyNameContains, StringComparison.Ordinal))
            .GetTypefaces()
            .ToList();

    private static string Signature(Typeface typeface) =>
        $"{typeface.Weight}/{typeface.Style}/{typeface.Stretch}";

    [Fact]
    public void FontSans_token_resolves_to_the_embedded_IBM_Plex_Sans_family()
    {
        var dict = new ResourceDictionary { Source = new Uri(TokensUri) };
        var fontSans = Assert.IsType<FontFamily>(dict["FontSans"]);

        var tokenTypefaces = ResolveTokenTypefaces(fontSans.Source).Select(Signature).OrderBy(s => s);
        var embeddedTypefaces = EmbeddedTypefaces("IBM Plex Sans").Select(Signature).OrderBy(s => s);

        Assert.Equal(embeddedTypefaces, tokenTypefaces);
    }

    [Fact]
    public void FontMono_token_resolves_to_the_embedded_IBM_Plex_Mono_family()
    {
        var dict = new ResourceDictionary { Source = new Uri(TokensUri) };
        var fontMono = Assert.IsType<FontFamily>(dict["FontMono"]);

        var tokenTypefaces = ResolveTokenTypefaces(fontMono.Source).Select(Signature).OrderBy(s => s);
        var embeddedTypefaces = EmbeddedTypefaces("IBM Plex Mono").Select(Signature).OrderBy(s => s);

        Assert.Equal(embeddedTypefaces, tokenTypefaces);
    }

    // The weight hierarchy (spec §6) asks for SemiBold on the selected tab, section headings and
    // primary buttons, and Medium for Plex Mono. Asking for a weight a family does not have is not
    // an error: WPF synthesises a fake bold from the Regular face, which looks plausible and ships
    // 359 KB of unused .ttf alongside it - the same silent-degradation shape as the "Inter" bug.
    // TryGetGlyphTypeface resolves the request all the way down to the physical face, so comparing
    // that face's own weight is what proves a real embedded face renders rather than a synthesised one.
    [Theory]
    [InlineData("IBM Plex Sans", 600)]   // SemiBold — IBMPlexSans-SemiBold.ttf
    [InlineData("IBM Plex Mono", 500)]   // Medium   — IBMPlexMono-Medium.ttf
    public void The_requested_weight_resolves_to_a_real_embedded_face(string familyName, int openTypeWeight)
    {
        var family = Fonts.GetFontFamilies(new Uri(FontBaseUri))
            .Single(f => f.Source.EndsWith($"#{familyName}", StringComparison.Ordinal));
        var requested = FontWeight.FromOpenTypeWeight(openTypeWeight);

        Assert.Contains(family.GetTypefaces(), t => t.Weight == requested);

        var typeface = new Typeface(family, FontStyles.Normal, requested, FontStretches.Normal);
        Assert.True(typeface.TryGetGlyphTypeface(out var glyphTypeface));
        Assert.Equal(requested, glyphTypeface.Weight);
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
