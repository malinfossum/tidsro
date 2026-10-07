namespace Tidsro.Services;

public interface IClock
{
    public DateTimeOffset Now { get; }
    /// <summary>The zone whose wall clock alarm times are entered in.</summary>
    public TimeZoneInfo Zone { get; }
}
