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

    // internal for tests
    internal static string? FileFor(SoundChoice c) => c switch
    {
        SoundChoice.SoftChime           => "soft-chime.wav",
        SoundChoice.Marimba             => "marimba.wav",
        SoundChoice.Bell                => "bell.wav",
        SoundChoice.PianoJingle         => "Piano-Jingle.wav",
        SoundChoice.ElectricPianoJingle => "Electric-Piano-Jingle.wav",
        SoundChoice.BellJingle          => "Bell-Jingle.wav",
        _ => null,   // None = silent
    };

    /// <summary>Resolve the embedded resource name for a choice (null = silent or missing). internal for tests.</summary>
    internal static string? ResourceNameFor(SoundChoice choice)
    {
        var file = FileFor(choice);
        if (file is null) return null;

        // Match ".<file>" — the leading dot is the namespace/path separator, so a short
        // name like "Piano-Jingle.wav" can't also match "Electric-Piano-Jingle.wav".
        var suffix = "." + file;
        return Asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Play the chosen sound once. Silent and never throws.</summary>
    public void Play(SoundChoice choice)
    {
        var name = ResourceNameFor(choice);
        if (name is null) return;
        try
        {
            using var stream = Asm.GetManifestResourceStream(name);
            if (stream is null) return;

            _player?.Dispose();
            _player = new SoundPlayer(stream);
            _player.Load();   // copy the wav into the player now...
            _player.Play();   // ...then play async from that in-memory copy
        }
        catch { /* sound must never crash a timer */ }
    }
}
