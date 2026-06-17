using Tidsro.Models;

namespace Tidsro.Services;

public sealed class SchedulerService
{
    private readonly IClock _clock;
    private readonly List<TimerItem> _running = new();
    private readonly List<TimerItem> _alarms = new();

    public SchedulerService(IClock clock) => _clock = clock;

    public IReadOnlyList<TimerItem> Running => _running;
    public event EventHandler<TimerItem>? Fired;

    /// <summary>Within this window after FireAt, a missed alarm still fires; beyond it, it expires quietly.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);

    public IReadOnlyList<TimerItem> Alarms => _alarms;
    public DateTimeOffset Now => _clock.Now;
    public event EventHandler<TimerItem>? Expired;

    /// <summary>Arm a one-shot clock-time alarm. Pass <paramref name="id"/> to restore a persisted alarm's identity.</summary>
    public TimerItem ArmClockAlarm(DateTimeOffset fireAt, string? label, SoundChoice sound, Guid? id = null)
    {
        var item = new TimerItem
        {
            Id = id ?? Guid.NewGuid(),
            TriggerType = TriggerType.ClockTime,
            Label = label,
            Sound = sound,
            EndsAt = fireAt,
            State = TimerState.Running,
        };
        _alarms.Add(item);
        return item;
    }

    public void RemoveAlarm(TimerItem item) => _alarms.Remove(item);

    public TimerItem StartCountdown(TimeSpan duration, string? label, SoundChoice sound)
    {
        var item = new TimerItem
        {
            TriggerType = TriggerType.Countdown,
            Label = label,
            Sound = sound,
            OriginalDuration = duration,
            Duration = duration,
            EndsAt = _clock.Now + duration,
            State = TimerState.Running,
        };
        _running.Add(item);
        return item;
    }

    public TimeSpan Remaining(TimerItem item)
    {
        if (item.State == TimerState.Paused) return item.PausedRemaining ?? TimeSpan.Zero;
        if (item.EndsAt is not { } end) return TimeSpan.Zero;
        var rem = end - _clock.Now;
        return rem > TimeSpan.Zero ? rem : TimeSpan.Zero;
    }

    public void Tick()
    {
        var now = _clock.Now;

        foreach (var item in _running.ToList())   // snapshot: handlers may mutate _running
        {
            if (item.State == TimerState.Running && item.EndsAt is { } end && now >= end)
            {
                item.State = TimerState.Fired;     // guard: fire at most once
                Fired?.Invoke(this, item);
            }
        }

        foreach (var alarm in _alarms.ToList())
        {
            if (alarm.State != TimerState.Running || alarm.EndsAt is not { } end || now < end) continue;

            _alarms.Remove(alarm);                 // one-shot leaves the armed set whether it fires or expires
            if (now - end <= Grace)
            {
                alarm.State = TimerState.Fired;    // removal + Fired-state == durable single-fire across a tick gap
                Fired?.Invoke(this, alarm);        // sound + corner card (App handler)
            }
            else
            {
                Expired?.Invoke(this, alarm);      // quiet missed-while-away note, no sound/card
            }
        }
    }

    public void Pause(TimerItem item)
    {
        if (item.State != TimerState.Running) return;
        item.PausedRemaining = Remaining(item);
        item.State = TimerState.Paused;
    }

    public void Resume(TimerItem item)
    {
        if (item.State != TimerState.Paused) return;
        item.EndsAt = _clock.Now + (item.PausedRemaining ?? TimeSpan.Zero);
        item.PausedRemaining = null;
        item.State = TimerState.Running;
    }

    // An item lives in exactly one list; remove from both so Cancel/Snooze are correct for alarms too.
    public void Cancel(TimerItem item) { _running.Remove(item); _alarms.Remove(item); }

    public TimerItem Snooze(TimerItem item, TimeSpan by)
    {
        Cancel(item);
        return StartCountdown(by, item.Label, item.Sound);
    }

    public TimerItem Restart(TimerItem item)
    {
        Cancel(item);
        return StartCountdown(item.OriginalDuration, item.Label, item.Sound);
    }

    public void Reset(TimerItem item)
    {
        item.Duration = item.OriginalDuration;
        if (item.State == TimerState.Paused)
        {
            item.PausedRemaining = item.OriginalDuration;   // back to the start, still paused
        }
        else
        {
            item.EndsAt = _clock.Now + item.OriginalDuration;
            item.State = TimerState.Running;
        }
    }
}
