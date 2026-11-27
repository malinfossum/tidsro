using CommunityToolkit.Mvvm.ComponentModel;
using Tidsro.Models;
using Tidsro.Services;

namespace Tidsro.ViewModels;

/// <summary>
/// Read-only projection of the scheduler's recurring alarms into a week. No commands: the Week tab
/// shows, it does not edit.
///
/// <para>Owned by <see cref="MainViewModel"/> for the application's lifetime and rebuilt by it. This
/// type subscribes to nothing — the alarm set lives on the scheduler and MainViewModel decides when a
/// change is worth re-projecting — so there is nothing here to unsubscribe and no teardown path.</para>
/// </summary>
public sealed partial class TimetableViewModel : ObservableObject
{
    private readonly SchedulerService _scheduler;
    private DateOnly _builtFor;

    // Today's skipped occurrences, as alarm id and start. A skip moves the alarm's next time on, so
    // once a skipped start has passed, the alarm looks exactly like one that rang (ADR 0002 keeps no
    // ledger). The Week tab must still not light that block, so the skip is remembered here for the
    // rest of the day. A skip whose start is still ahead is re-read every rebuild, so Undo skip
    // drops it. Limit: a skip made before Tidsro starts in the middle of the block is not known.
    private readonly Dictionary<Guid, DateTimeOffset> _skippedToday = new();

    public TimetableViewModel(SchedulerService scheduler)
    {
        _scheduler = scheduler;
        Rebuild();
        NowMinuteOfDay = MinuteOfDay(_scheduler.Now);
    }

    [ObservableProperty] private TimetableWeek _week = null!;

    /// <summary>Minutes since midnight, as the highlight reads it. Changes once a minute, never on
    /// the 250 ms tick: each change re-runs the current-block converter for every entry on screen, so
    /// assigning it per tick would cost 345,000 passes a day instead of 1,440 — the same cost
    /// <see cref="RefreshForTick"/>'s date gate exists to avoid, one layer down.</summary>
    [ObservableProperty] private int _nowMinuteOfDay;

    /// <summary>Re-project unconditionally. Called when the alarm set changes.</summary>
    public void Rebuild()
    {
        var now = _scheduler.Now;
        _builtFor = DateOnly.FromDateTime(now.Date);
        Week = TimetableLayout.Build(_scheduler.Alarms, now, SkippedToday(now));
    }

    private HashSet<(Guid Id, int Minute)> SkippedToday(DateTimeOffset now)
    {
        // Each start stays in its own offset, the zone's offset at that time, because that is where
        // the entry's minute comes from (EndsAt). Converting to now's offset would shift it by an hour
        // when a rebuild runs before a daylight-saving switch on the same day.
        var today = DateOnly.FromDateTime(now.Date);

        // Keep only skips whose start has passed today: those can no longer be undone.
        foreach (var (id, start) in _skippedToday.ToList())
            if (start > now || DateOnly.FromDateTime(start.DateTime) != today) _skippedToday.Remove(id);

        foreach (var alarm in _scheduler.Alarms)
            if (_scheduler.FirstSkipped(alarm) is { } first && DateOnly.FromDateTime(first.DateTime) == today)
                _skippedToday[alarm.Id] = first;

        return _skippedToday.Select(p => (p.Key, MinuteOfDay(p.Value))).ToHashSet();
    }

    /// <summary>
    /// Re-project only if the calendar date moved, so "today" never sits on yesterday.
    /// The tick loop runs at 250 ms, so an unconditional rebuild here would be roughly 345,000 week
    /// projections a day. Comparing the date also survives the machine sleeping through midnight,
    /// because it measures a date rather than elapsed time.
    ///
    /// <para>Uses <see cref="DateTimeOffset.Date"/> — the date component in the value's own offset —
    /// rather than converting to the machine's local zone. <see cref="TimetableLayout.Build"/> decides
    /// "today" off the same value's <c>DayOfWeek</c>, which also reads in the value's own offset; a
    /// <see cref="DateTimeOffset.LocalDateTime"/> conversion here would agree with that only by
    /// coincidence (it happens to match under <c>DateTimeOffset.Now</c>, whose offset is the local
    /// zone), and could disagree near midnight for any clock whose offset differs from the machine's.</para>
    /// </summary>
    public void RefreshForTick()
    {
        var now = _scheduler.Now;

        // The date check runs FIRST. At 00:00 the date and the minute change on the same tick, and
        // reading the minute against a week still built for yesterday would light a block in
        // yesterday's column for one tick.
        if (DateOnly.FromDateTime(now.Date) != _builtFor) Rebuild();

        var minute = MinuteOfDay(now);
        if (minute != NowMinuteOfDay) NowMinuteOfDay = minute;
    }

    private static int MinuteOfDay(DateTimeOffset now) => now.Hour * 60 + now.Minute;
}
