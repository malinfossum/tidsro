using System.IO;
using System.Windows;

namespace Tidsro.Views;

// Shows the OFL text that is embedded in this binary. OFL 1.1 §2 requires the licence to accompany
// every redistributed copy of the font software, and publish.ps1 produces a standalone portable
// Tidsro.exe with no companion files - so the licence has to be reachable from inside the app, not
// only from the installer's OFL-IBMPlex.txt.
public partial class LicenceDialog : Window
{
    /// <summary>Where the licence lives inside Tidsro.g.resources. Pinned by FontResourceTests -
    /// a wrong path here would leave the portable exe unable to show a licence it still ships.</summary>
    internal const string EmbeddedLicencePath = "Assets/fonts/OFL.txt";

    private LicenceDialog()
    {
        InitializeComponent();
        LicenceText.Text = ReadEmbeddedLicence();
    }

    /// <summary>Show the licence modally over its owner. Settings is itself a modal dialog, so this
    /// one is owned by that window rather than by the main one.</summary>
    public static void Show(Window owner) => new LicenceDialog { Owner = owner }.ShowDialog();

    /// <summary>Shown in place of the licence when this build cannot produce it. Still names the two
    /// other places the same text lives, so a reader is never left with nothing.</summary>
    private const string FallbackText =
        "The licence text could not be read from this build.\r\n\r\n"
        + "The SIL Open Font License 1.1 is available at https://openfontlicense.org, "
        + "and ships beside the installed application as OFL-IBMPlex.txt.";

    private static string ReadEmbeddedLicence()
    {
        // Never throws: a licence this app cannot read is a packaging fault, and crashing out of
        // Settings is a worse answer than telling the reader where else the same text lives. The
        // resource is guarded by a test, so reaching the fallback means something is genuinely wrong.
        //
        // A null return is only half the story - GetResourceStream raises IOException for a part it
        // cannot resolve rather than returning null, and StreamReader can fail on a corrupt entry.
        // This runs straight off a Click handler with nothing above it to catch either, so the catch
        // is deliberately broad: every failure here has exactly one useful answer, and the method has
        // no side effects to leave half-done.
        try
        {
            var resource = Application.GetResourceStream(new Uri(EmbeddedLicencePath, UriKind.Relative));
            if (resource is null) return FallbackText;

            using var stream = resource.Stream;
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch (Exception)
        {
            return FallbackText;
        }
    }
}
