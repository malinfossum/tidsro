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

    /// <summary>How long before an alarm a WarnBefore heads-up fires.</summary>
    public static readonly TimeSpan WarningLead = TimeSpan.FromMinutes(5);

    public IReadOnlyList<TimerItem> Alarms => _alarms;
    public DateTimeOffset Now => _clock.Now;
    public TimeZoneInfo Zone => _clock.Zone;
    public event EventHandler<TimerItem>? Expired;

    /// <summary>Raised once, WarningLead before an alarm with WarnBefore on, so the App can show a heads-up.</summary>
    public event EventHandler<TimerItem>? Warning;

    /// <summary>Arm a one-shot clock-time alarm. Pass <paramref name="id"/> to restore a persisted alarm's identity.</summary>
    public TimerItem ArmClockAlarm(DateTimeOffset fireAt, string? label, SoundChoice sound, Guid? id = null, bool warnBefore = false, bool enabled = true)
    {
        var item = new TimerItem
        {
            Id = id ?? Guid.NewGuid(),
            TriggerType = TriggerType.ClockTime,
            Label = label,
            Sound = sound,
            EndsAt = fireAt,
            State = TimerState.Running,
            WarnBefore = warnBefore,
            WarningSent = warnBefore && _clock.Now >= fireAt - WarningLead,   // armed inside the window -> no insta-warn
            IsEnabled = enabled,
        };
        _alarms.Add(item);
        return item;
    }

    public void RemoveAlarm(TimerItem item) => _alarms.Remove(item);

    /// <summary>True while the alarm with this id is on and still due at exactly this time. A skip, an
    /// edit to another time, switching it off or deleting it makes this false, which is how the App
    /// knows to close a heads-up for an occurrence that will no longer ring.</summary>
    public bool WillRingAt(Guid id, DateTimeOffset fireAt) =>
        _alarms.Any(a => a.Id == id && a.IsEnabled && a.EndsAt == fireAt);

    /// <summary>Turn an alarm on or off. Re-enabling a recurring alarm whose next occurrence has
    /// already passed (e.g. switched off over the summer) rolls it forward to the next future one,
    /// so it never fires the instant it comes back, and never emits a stale missed note.</summary>
    public void SetEnabled(TimerItem alarm, bool enabled)
    {
        alarm.IsEnabled = enabled;
        if (enabled
            && alarm.TriggerType == TriggerType.Recurring
            && alarm.RecurringDays is { } days
            && alarm.EndsAt is { } end
            && end <= _clock.Now)
        {
            var next = RecurrenceRules.NextOccurrence(_clock.Now, _clock.Zone, end.Hour, end.Minute, days);
            alarm.EndsAt = next;
            alarm.WarningSent = alarm.WarnBefore && _clock.Now >= next - WarningLead;
        }
    }

    // Skip-next keeps no skip list. A skip moves the alarm's own next occurrence one step forward,
    // so the skipped day is simply never reached (ADR 0002: the alarm's state is the record), and
    // the saved NextFireAt carries it through a relaunch and an export with no new field.

    /// <summary>The most occurrences one alarm can pass over. It bounds every skip walk, and a saved
    /// next time further out than this is treated as stale rather than as a skip.</summary>
    public const int MaxSkips = 100;

    /// <summary>Skip the next occurrence of a recurring alarm that is on. Each call skips one more,
    /// up to <see cref="MaxSkips"/>. Returns false, changing nothing, for a one-shot alarm, an alarm
    /// that is off, or one already at the cap.</summary>
    public bool SkipNext(TimerItem alarm) =>
        alarm.IsEnabled && SkippedOccurrences(alarm, MaxSkips).Count < MaxSkips && Advance(alarm);

    /// <summary>Take back the latest skip: the alarm steps back one occurrence. Returns false when
    /// nothing is skipped, so it can never move an alarm to a time that has already passed.</summary>
    public bool UndoSkip(TimerItem alarm)
    {
        if (SkippedCount(alarm) == 0 || alarm.EndsAt is not { } end || alarm.RecurringDays is not { } days)
            return false;
        SetNext(alarm, RecurrenceRules.MostRecentOccurrence(end.AddSeconds(-1), _clock.Zone, end.Hour, end.Minute, days));
        return true;
    }

    /// <summary>How many occurrences lie between now and the alarm's next one, i.e. how many it
    /// will pass over. Zero for anything that is not a recurring alarm that is on. It falls by itself
    /// as a skipped occurrence's time goes by.</summary>
    public int SkippedCount(TimerItem alarm) => alarm.IsEnabled ? SkippedOccurrences(alarm, MaxSkips).Count : 0;

    /// <summary>The local dates the alarm passes over before it rings again. Unlike
    /// <see cref="SkippedCount"/> this counts an alarm that is off too: its skip waits in its next
    /// time and still applies when it is switched back on. Empty when the next time is further out
    /// than <see cref="MaxSkips"/> occurrences: that is a stale time (a clock that stepped back), not
    /// a skip, and an edit drops it the way a load does.</summary>
    public IReadOnlyList<DateOnly> SkippedDates(TimerItem alarm)
    {
        var skipped = SkippedOccurrences(alarm, MaxSkips + 1);
        return skipped.Count > MaxSkips ? [] : skipped.Select(LocalDate).ToList();
    }

    /// <summary>The first occurrence the alarm passes over, or null. Cheap enough for every tick: it
    /// changes exactly when a skipped occurrence's time goes by, which is when the row must change.</summary>
    public DateTimeOffset? FirstSkipped(TimerItem alarm) =>
        SkippedOccurrences(alarm, 1) is [var first] ? first : null;

    /// <summary>Carry skips across an edit, which re-arms the alarm from scratch. The skips travel as
    /// dates, not a count, so a new time or new days still pass over the same days: the alarm skips
    /// each leading occurrence that falls on a skipped date. Returns the skipped dates it now rings
    /// on after all, because the new schedule puts an unskipped occurrence first (or is a one-shot).</summary>
    public IReadOnlyList<DateOnly> ReapplySkips(TimerItem alarm, IReadOnlyCollection<DateOnly> dates)
    {
        if (dates.Count == 0) return [];
        var skip = dates.ToHashSet();
        for (var i = 0; i < MaxSkips && alarm.EndsAt is { } next && skip.Contains(LocalDate(next)); i++)
            if (!Advance(alarm)) break;

        // Walk what is left of the schedule up to the last skipped date and collect the skipped
        // dates it still rings on. The date ends the walk; the count only guards the loop, sized for
        // the longest span a skip can cover (MaxSkips weekly occurrences, walked daily).
        var last = skip.Max();
        var ringsOn = new List<DateOnly>();
        var occurrence = alarm.EndsAt;
        for (var i = 0; i <= 7 * (MaxSkips + 1) && occurrence is { } o && LocalDate(o) <= last; i++)
        {
            if (skip.Contains(LocalDate(o))) ringsOn.Add(LocalDate(o));
            occurrence = alarm.RecurringDays is { } days
                ? RecurrenceRules.NextOccurrence(o, _clock.Zone, o.Hour, o.Minute, days)
                : null;
        }
        return ringsOn;
    }

    /// <summary>The skipped dates an edit to this time and these days would ring on after all, worked
    /// out on a detached copy so nothing changes. The Edit dialog asks before such a Save.</summary>
    public IReadOnlyList<DateOnly> PreviewLostSkips(TimerItem existing, int hour, int minute, Weekdays days)
    {
        var dates = SkippedDates(existing);
        if (dates.Count == 0) return [];
        var draft = days == Weekdays.None
            ? new TimerItem
            {
                TriggerType = TriggerType.ClockTime,
                EndsAt = ClockTimeRules.ComputeFireAt(_clock.Now, _clock.Zone, hour, minute),
            }
            : new TimerItem
            {
                TriggerType = TriggerType.Recurring,
                RecurringDays = days,
                EndsAt = RecurrenceRules.NextOccurrence(_clock.Now, _clock.Zone, hour, minute, days),
            };
        return ReapplySkips(draft, dates);
    }

    // The occurrences between now and the alarm's next time, at most `limit` of them. Ignores IsEnabled
    // on purpose: SkippedCount and SkipNext add that rule, SkippedDates must not.
    private List<DateTimeOffset> SkippedOccurrences(TimerItem alarm, int limit)
    {
        var skipped = new List<DateTimeOffset>();
        if (alarm.TriggerType != TriggerType.Recurring || alarm.RecurringDays is not { } days
            || alarm.EndsAt is not { } end)
            return skipped;
        var candidate = RecurrenceRules.NextOccurrence(_clock.Now, _clock.Zone, end.Hour, end.Minute, days);
        while (candidate < end && skipped.Count < limit)
        {
            skipped.Add(candidate);
            candidate = RecurrenceRules.NextOccurrence(candidate, _clock.Zone, end.Hour, end.Minute, days);
        }
        return skipped;
    }

    // Move a recurring alarm one occurrence on, whether it is on or off.
    private bool Advance(TimerItem alarm)
    {
        if (alarm.TriggerType != TriggerType.Recurring || alarm.RecurringDays is not { } days
            || alarm.EndsAt is not { } end)
            return false;
        SetNext(alarm, RecurrenceRules.NextOccurrence(end, _clock.Zone, end.Hour, end.Minute, days));
        return true;
    }

    private DateOnly LocalDate(DateTimeOffset when) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(when, _clock.Zone).DateTime);

    private void SetNext(TimerItem alarm, DateTimeOffset next)
    {
        alarm.EndsAt = next;
        alarm.WarningSent = alarm.WarnBefore && _clock.Now >= next - WarningLead;
    }

    /// <summary>Arm a recurring alarm. Pass <paramref name="nextFireAt"/> to restore a persisted alarm's next occurrence.</summary>
    // endMinute is carried, never read: it is a timetable block's end, and this scheduler has exactly one
    // fire point per alarm. Nothing below branches on it, and nothing in Tick does either.
    public TimerItem ArmRecurringAlarm(int hour, int minute, Weekdays days, string? label, SoundChoice sound,
        Guid? id = null, DateTimeOffset? nextFireAt = null, bool warnBefore = false, bool enabled = true,
        int? endMinute = null)
    {
        var natural = RecurrenceRules.NextOccurrence(_clock.Now, _clock.Zone, hour, minute, days);
        var ends = nextFireAt ?? natural;
        var item = new TimerItem
        {
            Id = id ?? Guid.NewGuid(),
            TriggerType = TriggerType.Recurring,
            Label = label,
            Sound = sound,
            RecurringDays = days,
            EndsAt = ends,
            State = TimerState.Running,
            WarnBefore = warnBefore,
            WarningSent = warnBefore && _clock.Now >= ends - WarningLead,
            IsEnabled = enabled,
            EndMinute = endMinute,
        };
        // A saved next time more than MaxSkips occurrences out is no skip anyone made one press at a
        // time (a hand-edited file, or a clock that once ran far ahead). Left alone, the alarm would
        // stay silent for months and its row would claim a hundred skips, so it falls back to the
        // natural next occurrence.
        if (SkippedOccurrences(item, MaxSkips + 1).Count > MaxSkips) SetNext(item, natural);
        _alarms.Add(item);
        return item;
    }

    // A transient, list-less copy of one occurrence. The completion card's Snooze/Dismiss act on THIS,
    // so the live recurring alarm (still in _alarms, already advanced) is never canceled. Typed ClockTime
    // so the card shows Snooze +5 / Dismiss (no Restart).
    private static TimerItem OccurrenceSnapshot(TimerItem alarm, DateTimeOffset occurrence) => new()
    {
        TriggerType = TriggerType.ClockTime,
        Label = alarm.Label,
        Sound = alarm.Sound,
        EndsAt = occurrence,
        State = TimerState.Fired,
    };

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

    /// <summary>Put a canceled countdown back as it was: the same item, so its original duration and its
    /// paused state come back with it. A running one gets <paramref name="remaining"/> again from now,
    /// so the time spent in the undo window does not count against it.</summary>
    public void RestoreCountdown(TimerItem item, TimeSpan remaining)
    {
        if (item.State == TimerState.Fired) return;   // it already rang; back in the list it would never fire or clear
        if (item.State == TimerState.Running) item.EndsAt = _clock.Now + remaining;
        if (!_running.Contains(item)) _running.Add(item);
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
            if (alarm.State != TimerState.Running || !alarm.IsEnabled || alarm.EndsAt is not { } end) continue;

            // Heads-up: raise Warning once when we cross into the last WarningLead before the alarm.
            if (alarm.WarnBefore && !alarm.WarningSent && now >= end - WarningLead && now < end)
            {
                alarm.WarningSent = true;
                Warning?.Invoke(this, alarm);      // App: soft chime (if sounded) + a heads-up card
            }

            if (now < end) continue;               // not yet fire time

            if (alarm.TriggerType == TriggerType.Recurring && alarm.RecurringDays is { } days)
            {
                var prev = RecurrenceRules.MostRecentOccurrence(now, _clock.Zone, end.Hour, end.Minute, days);
                alarm.EndsAt = RecurrenceRules.NextOccurrence(now, _clock.Zone, end.Hour, end.Minute, days);  // advance first: in-session dedup
                alarm.WarningSent = false;         // re-arm the heads-up for the next occurrence
                if (now - prev <= Grace)
                    Fired?.Invoke(this, OccurrenceSnapshot(alarm, prev));    // transient copy -> card can't mutate the live alarm
                else
                    Expired?.Invoke(this, OccurrenceSnapshot(alarm, prev));  // quiet missed-while-away note
                continue;
            }

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
        // An alarm snoozes back into the Schedule as a clock-time alarm; a countdown stays a Quick timer.
        return item.TriggerType == TriggerType.Countdown
            ? StartCountdown(by, item.Label, item.Sound)
            : ArmClockAlarm(_clock.Now + by, item.Label, item.Sound);
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
