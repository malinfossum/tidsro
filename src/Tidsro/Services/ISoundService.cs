using Tidsro.Models;

namespace Tidsro.Services;

/// <summary>Plays a chosen sound once. Lets the view-model preview a sound without depending on audio in tests.</summary>
public interface ISoundService
{
    void Play(SoundChoice choice);
}
