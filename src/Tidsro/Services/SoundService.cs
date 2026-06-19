using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using Tidsro.Models;

namespace Tidsro.Services;

public sealed class SoundService : ISoundService
{
    private static readonly Assembly Asm = typeof(SoundService).Assembly;

    // Held so the player isn't collected while a memory-backed sound is still playing async.
    private SoundPlayer? _player;

    private static string? FileFor(SoundChoice c) => c switch
    {
        SoundChoice.SoftChime           => "soft-chime.wav",
        SoundChoice.Marimba             => "marimba.wav",
        SoundChoice.Bell                => "bell.wav",
        SoundChoice.PianoJingle         => "Piano-Jingle.wav",
        SoundChoice.ElectricPianoJingle => "Electric-Piano-Jingle.wav",
        SoundChoice.BellJingle          => "Bell-Jingle.wav",
        _ => null,   // None = silent
    };

    private static Stream? Open(string file)
    {
        var name = Asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(file, System.StringComparison.OrdinalIgnoreCase));
        return name is null ? null : Asm.GetManifestResourceStream(name);
    }

    /// <summary>Play the chosen sound once. Silent and never throws.</summary>
    public void Play(SoundChoice choice)
    {
        var file = FileFor(choice);
        if (file is null) return;
        try
        {
            using var stream = Open(file);
            if (stream is null) return;

            _player?.Dispose();
            _player = new SoundPlayer(stream);
            _player.Load();   // copy the wav into the player now...
            _player.Play();   // ...then play async from that in-memory copy
        }
        catch { /* sound must never crash a timer */ }
    }
}
