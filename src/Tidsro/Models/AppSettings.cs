namespace Tidsro.Models;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public bool LaunchAtStartup { get; set; }
    public SoundChoice DefaultSound { get; set; } = SoundChoice.None;

    /// <summary>The name of the .wav or .mp3 behind <see cref="SoundChoice.Custom"/>, shown in Settings so the
    /// user can see which file they chose. A label and nothing else — the audio always lives at the one
    /// path <c>CustomSoundStore</c> owns, so this string never reaches the file system.</summary>
    public string? CustomSoundName { get; set; }

    /// <summary>Index of the tab the main window opens on. 0 = Quick timers, 1 = Schedule, 2 = Week.</summary>
    public int SelectedTab { get; set; }

    /// <summary>Tabs the shell has: 0 Quick timers, 1 Schedule, 2 Week.</summary>
    public const int TabCount = 3;

    // Last on-screen window position; null until the window has been shown and dismissed once.
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }

    public static AppSettings Defaults() => new();

    /// <summary>Harden untrusted input loaded from disk: unknown enum -> None, non-finite coords -> null.</summary>
    public AppSettings Sanitized() => new()
    {
        SchemaVersion = 1,
        LaunchAtStartup = LaunchAtStartup,
        DefaultSound = Enum.IsDefined(DefaultSound) ? DefaultSound : SoundChoice.None,
        CustomSoundName = NormalizeName(CustomSoundName),
        SelectedTab = SelectedTab >= 0 && SelectedTab < TabCount ? SelectedTab : 0,
        WindowLeft = WindowLeft is double l && double.IsFinite(l) ? l : null,
        WindowTop = WindowTop is double t && double.IsFinite(t) ? t : null,
        WindowWidth = WindowWidth is double w && double.IsFinite(w) && w >= 380 ? w : null,
        WindowHeight = WindowHeight is double h && double.IsFinite(h) && h >= 480 ? h : null,
    };

    private const int MaxName = 200;

    private static string? NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var trimmed = name.Trim();
        return TextRules.Cap(trimmed, MaxName);
    }
}
