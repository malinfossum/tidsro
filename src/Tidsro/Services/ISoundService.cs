using Tidsro.Models;

namespace Tidsro.Services;

/// <summary>Plays a chosen sound once. Lets the view-model preview a sound without depending on audio in tests.</summary>
public interface ISoundService
{
    /// <summary>Start <paramref name="choice"/> and return an id for this one playback, or 0 when
    /// nothing was started (silent, or no file).</summary>
    public int Play(SoundChoice choice);

    /// <summary>Stop the playback <paramref name="playId"/> started, if it is still the one playing.
    /// A later sound is left alone, so closing an old card cannot cut off a newer alarm. Harmless
    /// when it has already finished.</summary>
    public void Stop(int playId);
}
