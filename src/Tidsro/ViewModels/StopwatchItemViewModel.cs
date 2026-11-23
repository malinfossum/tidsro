using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidsro.Models;

namespace Tidsro.ViewModels;

/// <summary>One stopwatch: counts up from zero, start/pause/reset, with an editable label.
/// Elapsed time is derived from the clock on every read (banked time + the current run), never
/// accumulated tick by tick, so a missed or late tick can never make it drift.</summary>
public partial class StopwatchItemViewModel : ObservableObject
{
    private static readonly string PauseGlyph = ((char)0xE769).ToString();
    private static readonly string PlayGlyph = ((char)0xE768).ToString();

    private readonly Func<DateTimeOffset> _now;
    private TimeSpan _banked;
    private DateTimeOffset? _startedAt;

    public Guid Id { get; }

    /// <summary>Raised when something worth persisting changed (label, start, pause, reset).</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when a run ends (pause, or reset while running) with its start and end, so the
    /// owner can write it to the time log.</summary>
    public event EventHandler<(DateTimeOffset Start, DateTimeOffset End)>? RunEnded;

    /// <summary>When the current run began, or null while stopped.</summary>
    public DateTimeOffset? RunningSince => _startedAt;

    [ObservableProperty] private string _label = "";
    [ObservableProperty] private string _elapsedText = "00:00";
    [ObservableProperty] private string _fractionText = ".0";
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _startPauseGlyph = PlayGlyph;
    [ObservableProperty] private string _startPauseLabel = "Start";

    public StopwatchItemViewModel(Func<DateTimeOffset> now, string? label = null, Guid? id = null,
        TimeSpan banked = default, DateTimeOffset? startedAt = null)
    {
        _now = now;
        Id = id ?? Guid.NewGuid();
        _label = label ?? "";
        _banked = banked < TimeSpan.Zero ? TimeSpan.Zero : banked;
        _startedAt = startedAt;
        Refresh();
    }

    public static StopwatchItemViewModel FromRecord(StopwatchRecord r, Func<DateTimeOffset> now) =>
        new(now, r.Label, r.Id, TimeSpan.FromMilliseconds(r.ElapsedMs), r.StartedAt);

    public StopwatchRecord ToRecord() => new()
    {
        Id = Id,
        Label = string.IsNullOrWhiteSpace(Label) ? null : Label.Trim(),
        ElapsedMs = (long)_banked.TotalMilliseconds,
        StartedAt = _startedAt,
    };

    public TimeSpan Elapsed
    {
        get
        {
            if (_startedAt is not { } s) return _banked;
            var run = _now() - s;
            return _banked + (run < TimeSpan.Zero ? TimeSpan.Zero : run);   // a clock set backwards never subtracts
        }
    }

    public bool HasLabel => !string.IsNullOrWhiteSpace(Label);

    public string AccessibleName => HasLabel ? $"Stopwatch {Label}" : "Stopwatch";

    partial void OnLabelChanged(string value)
    {
        OnPropertyChanged(nameof(HasLabel));
        OnPropertyChanged(nameof(AccessibleName));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Refresh()
    {
        var e = Elapsed;
        ElapsedText = Format(e);
        FractionText = "." + (e.Milliseconds / 100).ToString();
        IsRunning = _startedAt is not null;
        StartPauseGlyph = IsRunning ? PauseGlyph : PlayGlyph;
        StartPauseLabel = IsRunning ? "Pause" : (e > TimeSpan.Zero ? "Resume" : "Start");
    }

    public static string Format(TimeSpan e)
    {
        var whole = TimeSpan.FromSeconds(Math.Floor(e.TotalSeconds));
        return whole.TotalHours >= 1
            ? $"{(int)whole.TotalHours}:{whole.Minutes:00}:{whole.Seconds:00}"
            : $"{whole.Minutes:00}:{whole.Seconds:00}";
    }

    [RelayCommand]
    public void StartPause()
    {
        if (_startedAt is { } s)
        {
            var now = _now();
            var run = now - s;
            _banked += run < TimeSpan.Zero ? TimeSpan.Zero : run;
            _startedAt = null;
            RunEnded?.Invoke(this, (s, now));
        }
        else
        {
            _startedAt = _now();
        }
        Refresh();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void Reset()
    {
        _banked = TimeSpan.Zero;
        // A running stopwatch restarts from zero and keeps going; a stopped one returns to 00:00.
        if (_startedAt is { } s)
        {
            var now = _now();
            RunEnded?.Invoke(this, (s, now));
            _startedAt = now;
        }
        Refresh();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
