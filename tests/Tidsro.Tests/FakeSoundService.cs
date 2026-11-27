using Tidsro.Models;
using Tidsro.Services;
namespace Tidsro.Tests;

public sealed class FakeSoundService : ISoundService
{
    private int _next;

    public SoundChoice? LastPlayed { get; private set; }
    public int LastPlayId { get; private set; }
    public List<int> Stopped { get; } = new();

    public int Play(SoundChoice choice)
    {
        LastPlayed = choice;
        LastPlayId = choice == SoundChoice.None ? 0 : ++_next;
        return LastPlayId;
    }

    public void Stop(int playId) => Stopped.Add(playId);

    public int StopCustomCalls { get; private set; }
    public void StopCustom() => StopCustomCalls++;
}
