namespace Tidsro.Models;

/// <summary>Persisted shape of one clock-time alarm. Plain data; the runtime form reuses <see cref="TimerItem"/>.</summary>
public sealed class AlarmRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime FireAt { get; set; }   // local; absolute date + time
    public string? Label { get; set; }
    public SoundChoice Sound { get; set; } = SoundChoice.None;
    public bool WarnBefore { get; set; }
}
