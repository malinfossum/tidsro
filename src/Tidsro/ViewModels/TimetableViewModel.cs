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
        Week = TimetableLayout.Build(_scheduler.Alarms, now);
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
