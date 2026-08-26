using CommunityToolkit.Mvvm.ComponentModel;
using Tidsro.Models;
using Tidsro.Services;

namespace Tidsro.ViewModels;

/// <summary>
/// Read-only projection of the scheduler's recurring alarms into a week. No commands: the Week tab
/// shows, it does not edit.
///
/// <para>Owned by <see cref="MainViewModel"/> for the application's lifetime — deliberately not created
/// per tab activation, which would leak a subscription per visit.</para>
/// </summary>
public sealed partial class TimetableViewModel : ObservableObject
{
    private readonly SchedulerService _scheduler;
    private DateOnly _builtFor;

    public TimetableViewModel(SchedulerService scheduler)
    {
        _scheduler = scheduler;
        Rebuild();
    }

    [ObservableProperty] private TimetableWeek _week = null!;

    /// <summary>Re-project unconditionally. Called when the alarm set changes.</summary>
    public void Rebuild()
    {
        var now = _scheduler.Now;
        _builtFor = DateOnly.FromDateTime(now.LocalDateTime);
        Week = TimetableLayout.Build(_scheduler.Alarms, now);
    }

    /// <summary>
    /// Re-project only if the calendar date moved, so "today" never sits on yesterday.
    /// The tick loop runs at 250 ms, so an unconditional rebuild here would be roughly 345,000 week
    /// projections a day. Comparing the date also survives the machine sleeping through midnight,
    /// because it measures a date rather than elapsed time.
    /// </summary>
    public void RefreshForTick()
    {
        if (DateOnly.FromDateTime(_scheduler.Now.LocalDateTime) != _builtFor) Rebuild();
    }
}
