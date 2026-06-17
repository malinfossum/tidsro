using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidsro.Models;
using Tidsro.Services;

namespace Tidsro.ViewModels;

public partial class TimerItemViewModel : ObservableObject
{
    // Segoe Fluent Icons code points, rendered via the pause button's FontFamily in the View.
    private static readonly string PauseGlyph = ((char)0xE769).ToString();
    private static readonly string PlayGlyph = ((char)0xE768).ToString();

    private readonly SchedulerService _scheduler;
    public TimerItem Item { get; }

    [ObservableProperty] private string _remainingText = "00:00";
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private string _pauseResumeGlyph = PauseGlyph;
    [ObservableProperty] private string _pauseResumeLabel = "Pause";

    public TimerItemViewModel(TimerItem item, SchedulerService scheduler)
    {
        Item = item; _scheduler = scheduler;
        Refresh();
    }

    public string? Label => Item.Label;
    public bool HasSound => Item.Sound != SoundChoice.None;
    public string SoundTag => HasSound ? "sound" : "silent";

    public void Refresh()
    {
        var r = _scheduler.Remaining(Item);
        var ts = TimeSpan.FromSeconds(Math.Ceiling(r.TotalSeconds));
        RemainingText = ts.Hours > 0 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"mm\:ss");
        IsPaused = Item.State == TimerState.Paused;
        PauseResumeGlyph = IsPaused ? PlayGlyph : PauseGlyph;
        PauseResumeLabel = IsPaused ? "Resume" : "Pause";
    }

    [RelayCommand] private void PauseResume()
    {
        if (Item.State == TimerState.Running) _scheduler.Pause(Item);
        else if (Item.State == TimerState.Paused) _scheduler.Resume(Item);
        Refresh();
    }

    [RelayCommand] private void Reset() { _scheduler.Reset(Item); Refresh(); }
}
