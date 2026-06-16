namespace Tidsro.Models;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public bool LaunchAtStartup { get; set; }
    public SoundChoice DefaultSound { get; set; } = SoundChoice.None;

    // Last on-screen window position; null until the window has been shown and dismissed once.
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }

    public static AppSettings Defaults() => new();

    /// <summary>Harden untrusted input loaded from disk: unknown enum -> None, non-finite coords -> null.</summary>
    public AppSettings Sanitized() => new()
    {
        SchemaVersion = 1,
        LaunchAtStartup = LaunchAtStartup,
        DefaultSound = Enum.IsDefined(DefaultSound) ? DefaultSound : SoundChoice.None,
        WindowLeft = WindowLeft is double l && double.IsFinite(l) ? l : null,
        WindowTop = WindowTop is double t && double.IsFinite(t) ? t : null,
    };
}
