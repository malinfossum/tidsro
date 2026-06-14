using Tidsro.Models;
using Tidsro.Services;
namespace Tidsro.Tests;
public sealed class FakeSoundService : ISoundService
{
    public SoundChoice? LastPlayed { get; private set; }
    public void Play(SoundChoice choice) => LastPlayed = choice;
}
