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
    [ObservableProperty] private bool _isNext;   // soonest-finishing active timer — the parent sets this
    // True for the one row the hero card is already showing, so the list below can leave it out.
    // Derived view state, set by MainViewModel whenever Running changes — never persisted.
    [ObservableProperty] private bool _isInHero;
    [ObservableProperty] private string _pauseResumeGlyph = PauseGlyph;
    [ObservableProperty] private string _pauseResumeLabel = "Pause";
    [ObservableProperty] private string _finishText = "";
    [ObservableProperty] private bool _showFinish;

    public TimerItemViewModel(TimerItem item, SchedulerService scheduler)
    {
        Item = item; _scheduler = scheduler;
        Refresh();
    }

    public string? Label => Item.Label;
    public bool HasSound => Item.Sound != SoundChoice.None;
    public string SoundTag => HasSound ? "sound" : "silent";

    /// <summary>The hero card's caption above the countdown. A paused timer stays in Running, so the
    /// hero would otherwise show a frozen clock at 42px under the word RUNNING. The row below already
    /// mutes its own countdown when paused; at hero size the state needs saying as well as showing.</summary>
    public string StateCaption => IsPaused ? "PAUSED" : "RUNNING";

    /// <summary>The caption's accessible name, kept coherent with the caption it labels. The running
    /// value is byte-identical to the name that shipped ("Running timer"); only the paused state adds
    /// a string, and it reads correctly alone, without the 42px numerals beside it for context.</summary>
    public string StateAccessibleName => IsPaused ? "Paused timer" : "Running timer";

    // Both captions are computed, so they read live-correct whether or not a notification ever fires
    // - only watching PropertyChanged can prove the UI is actually told. See the tests.
    partial void OnIsPausedChanged(bool value)
    {
        OnPropertyChanged(nameof(StateCaption));
        OnPropertyChanged(nameof(StateAccessibleName));
    }

    public void Refresh()
    {
        var r = _scheduler.Remaining(Item);
        var ts = TimeSpan.FromSeconds(Math.Ceiling(r.TotalSeconds));
        RemainingText = ts.Hours > 0 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"mm\:ss");
        IsPaused = Item.State == TimerState.Paused;
        PauseResumeGlyph = IsPaused ? PlayGlyph : PauseGlyph;
        PauseResumeLabel = IsPaused ? "Resume" : "Pause";

        if (Item.State == TimerState.Running && Item.EndsAt is { } end)
        {
            FinishText = "done " + end.ToString("HH\\:mm");
            ShowFinish = true;
        }
        else
        {
            ShowFinish = false;
        }
    }

    [RelayCommand] private void PauseResume()
    {
        if (Item.State == TimerState.Running) _scheduler.Pause(Item);
        else if (Item.State == TimerState.Paused) _scheduler.Resume(Item);
        Refresh();
    }

    [RelayCommand] private void Reset() { _scheduler.Reset(Item); Refresh(); }
}
