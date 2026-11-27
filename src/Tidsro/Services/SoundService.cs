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

    // Which playback is current. Stop(id) only acts on the latest one, so closing an old card
    // cannot silence a newer alarm.
    private int _lastId;
    private int _currentId;

    // Where the user's own .wav lives right now, or null when none is installed. A live read, so
    // choosing or clearing one in Settings takes effect on the next chime without rewiring anything.
    private readonly Func<string?> _customPath;

    public SoundService(Func<string?> customPath) => _customPath = customPath;

    // internal for tests
    internal static string? FileFor(SoundChoice c) => c switch
    {
        SoundChoice.SoftChime => "soft-chime.wav",
        SoundChoice.Marimba => "marimba.wav",
        SoundChoice.Bell => "bell.wav",
        SoundChoice.PianoJingle => "Piano-Jingle.wav",
        SoundChoice.ElectricPianoJingle => "Electric-Piano-Jingle.wav",
        SoundChoice.BellJingle => "Bell-Jingle.wav",
        _ => null,   // None = silent, Custom = a file on disk rather than an embedded chime
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

    /// <summary>Play the chosen sound once. Never throws; returns 0 when nothing started.</summary>
    public int Play(SoundChoice choice)
    {
        var started = choice == SoundChoice.Custom ? PlayCustom() : PlayChime(choice);
        if (!started) return 0;
        _currentId = ++_lastId;
        return _currentId;
    }

    /// <summary>Stop a playback if it is still the current one. My sound can be a song several
    /// minutes long, so closing its card or the window that previewed it has to end it.</summary>
    public void Stop(int playId)
    {
        if (playId == 0 || playId != _currentId) return;
        _currentId = 0;
        try { _player?.Stop(); }
        catch { /* stopping must never crash a timer either */ }
    }

    private bool PlayChime(SoundChoice choice)
    {
        var name = ResourceNameFor(choice);
        if (name is null) return false;
        try
        {
            using var stream = Asm.GetManifestResourceStream(name);
            if (stream is null) return false;

            _player?.Dispose();
            _player = new SoundPlayer(stream);
            _player.Load();   // copy the wav into the player now...
            _player.Play();   // ...then play async from that in-memory copy
            return true;
        }
        catch { return false; /* sound must never crash a timer */ }
    }

    /// <summary>Play My sound, always a .wav on disk (an .mp3 was decoded when it was chosen). A
    /// sound that has been removed since it was chosen is silence, never an error mid-alarm.</summary>
    private bool PlayCustom()
    {
        try
        {
            var path = _customPath();
            if (path is null || !File.Exists(path)) return false;

            _player?.Dispose();
            _player = new SoundPlayer(path);
            _player.Load();
            _player.Play();
            return true;
        }
        catch { return false; /* sound must never crash a timer */ }
    }
}
