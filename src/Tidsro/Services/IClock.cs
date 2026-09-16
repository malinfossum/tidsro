namespace Tidsro.Services;
public interface IClock
{
    DateTimeOffset Now { get; }
    /// <summary>The zone whose wall clock alarm times are entered in.</summary>
    TimeZoneInfo Zone { get; }
}
