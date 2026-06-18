namespace Tidsro.Models;

/// <summary>Persisted shape of one recurring alarm. Plain data; the runtime form reuses <see cref="TimerItem"/>.</summary>
public sealed class RecurringAlarmRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Hour { get; set; }            // 0–23
    public int Minute { get; set; }          // 0–59
    public Weekdays Days { get; set; }
    public string? Label { get; set; }
    public SoundChoice Sound { get; set; } = SoundChoice.None;
    public DateTime NextFireAt { get; set; } // local; the next occurrence — the durable dedup marker
}
