using System.IO;
using System.Media;
using Tidsro.Models;

namespace Tidsro.Services;

public sealed class SoundService : ISoundService
{
    private static string File(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "sounds", name);

    private static string? PathFor(SoundChoice c) => c switch
    {
        SoundChoice.SoftChime => File("soft-chime.wav"),
        SoundChoice.Marimba   => File("marimba.wav"),
        SoundChoice.Bell      => File("bell.wav"),
        _ => null,   // None = silent
    };

    /// <summary>Play the chosen sound once. Silent and never throws.</summary>
    public void Play(SoundChoice choice)
    {
        var path = PathFor(choice);
        if (path is null || !System.IO.File.Exists(path)) return;
        try { new SoundPlayer(path).Play(); }   // async, fire-and-forget, plays once
        catch { /* sound must never crash a timer */ }
    }
}
