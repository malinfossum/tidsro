using Tidsro.Services;
namespace Tidsro.Tests;

public sealed class FakeClock : IClock
{
    public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
    public TimeZoneInfo Zone => TimeZoneInfo.Utc;   // matches the zero offsets above
    public void Advance(TimeSpan by) => Now += by;
}
