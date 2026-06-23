using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using H.NotifyIcon;
using Tidsro.Models;
using Tidsro.Services;
using Tidsro.ViewModels;
using Tidsro.Views;

namespace Tidsro;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "Tidsro.SingleInstance.v1";
    private const string ShowWindowEventName = "Tidsro.ShowWindow.v1";

    private TaskbarIcon? _tray;
    private SchedulerService _scheduler = null!;
    private SoundService _sound = null!;
    private PersistenceService _persistence = null!;
    private TidsroData _data = null!;
    private MainViewModel _mainVm = null!;
    private AppSettings _settings = null!;
    private HotkeyService _hotkey = null!;
    private DispatcherTimer _timer = null!;
    private MainWindow? _main;
    private readonly List<CompletionPopup> _openPopups = new();
    private readonly Dictionary<CompletionPopup, DateTimeOffset> _warningFireTimes = new();
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!TryClaimSingleInstance())   // a second launch surfaces the first window, then exits
            return;

        LoadStateAndServices();
        WireSchedulerEvents();
        StartTickLoop();
        RegisterHotkey();
        _tray = TrayBuilder.Create(ShowMainWindow, FocusLatestAlert, Quit);
        ShowWindowUnlessBootLaunch(e);
    }

    // Claim the single-instance mutex. Returns false for a second launch — after signalling the first
    // instance to surface its window — so OnStartup bails out. The first instance registers the wait that
    // brings its window forward when a later launch signals it.
    private bool TryClaimSingleInstance()
    {
        _instanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirst);
        if (!isFirst)
        {
            try { EventWaitHandle.OpenExisting(ShowWindowEventName).Set(); }
            catch { /* the first instance may be mid-exit; nothing useful to do */ }
            Shutdown();
            return false;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
        ThreadPool.RegisterWaitForSingleObject(_showEvent,
            (_, _) => Dispatcher.Invoke(ShowMainWindow), null, Timeout.Infinite, executeOnlyOnce: false);
        return true;
    }

    // Load persisted data, build the services and main view-model, arm the saved alarms, and self-heal a
    // stale launch-at-startup Run-key path.
    private void LoadStateAndServices()
    {
        _persistence = new PersistenceService(PersistenceService.DefaultPath);
        _data = _persistence.Load();
        _settings = _data.Settings ?? AppSettings.Defaults();
        _scheduler = new SchedulerService(new SystemClock());
        _sound = new SoundService();
        _mainVm = new MainViewModel(_scheduler, _sound, _settings.DefaultSound);
        ArmLoadedAlarms(_data.Alarms);
        ArmLoadedRecurring(_data.RecurringAlarms);
        _mainVm.AlarmsChanged += (_, _) => SaveData();

        new StartupService(StartupService.CurrentExePath).RefreshIfEnabled();
    }

    // Connect the scheduler's events to the UI: fired cards, pre-alarm warnings, and missed alarms.
    private void WireSchedulerEvents()
    {
        _scheduler.Fired += OnTimerFired;
        _scheduler.Warning += OnAlarmWarning;
        _scheduler.Expired += (_, item) => { _mainVm.AddMissed(item); SaveData(); };
    }

    // The 250 ms heartbeat: advance the scheduler, refresh the UI, and retire fired warning cards.
    private void StartTickLoop()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => { _scheduler.Tick(); _mainVm.RefreshAll(); CloseFiredWarnings(); };
        _timer.Start();
    }

    // Register the global show/focus hotkey. Best-effort — the tray "Focus latest alert" item is the
    // keyboard fallback when the chord is already taken.
    private void RegisterHotkey()
    {
        _hotkey = new HotkeyService();
        _hotkey.Pressed += (_, _) => FocusLatestAlert();
        _hotkey.Register();
    }

    // Surface the window on a normal launch so it's discoverable; stay in the tray when auto-started at boot.
    // On a boot launch the window isn't built yet, so a missed-while-away alarm's UIA announcement is
    // best-effort — the visible MissedNote still persists and is shown (and read) once the window opens.
    private void ShowWindowUnlessBootLaunch(StartupEventArgs e)
    {
        if (!e.Args.Contains(StartupService.StartupArg))
            ShowMainWindow();
    }

    // Keyboard route to the newest completion card — shared by the global hotkey and the tray
    // "Focus latest alert" item (the fallback when the hotkey can't register; spec §5.3)
    private void FocusLatestAlert() => _openPopups.LastOrDefault()?.FocusForKeyboard();

    private void OnTimerFired(object? sender, TimerItem item)
    {
        _sound.Play(item.Sound);

        ShowCard(new PopupViewModel(item,
            onSnooze: i => { var r = _scheduler.Snooze(i, TimeSpan.FromMinutes(5)); _mainVm.RefreshAll(); SaveData(); return r; },
            onRestart: i => { var r = _scheduler.Restart(i); _mainVm.RefreshAll(); return r; },
            onDismiss: i => _scheduler.Cancel(i)));

        if (item.TriggerType == TriggerType.ClockTime) SaveData();   // a one-shot left the armed set, or a recurring fire advanced its next occurrence — mirror to disk
    }

    private void OnAlarmWarning(object? sender, TimerItem item)
    {
        // Mirror the alarm's sound choice: a soft chime only when the alarm itself is sounded; silent otherwise.
        if (item.Sound != SoundChoice.None) _sound.Play(SoundChoice.SoftChime);

        var head = string.IsNullOrWhiteSpace(item.Label) ? "Alarm" : item.Label!.Trim();
        var popup = ShowCard(new PopupViewModel(item, head));   // heads-up (close-only) variant
        _warningFireTimes[popup] = item.EndsAt ?? _scheduler.Now;   // capture this occurrence's fire time
    }

    // Show a completion card bottom-right without stealing focus, track it in the stack, and keep the
    // stack tidy as cards open and close. Returns the popup so a caller can track extra state (a warning's
    // fire time). Removing from _warningFireTimes on close is a no-op for ordinary fired cards.
    private CompletionPopup ShowCard(PopupViewModel vm)
    {
        var popup = new CompletionPopup(vm);
        popup.Closed += (_, _) => { _openPopups.Remove(popup); _warningFireTimes.Remove(popup); RestackPopups(); };
        // first placement uses an estimated height; reposition the stack once the card has actually measured
        popup.ContentRendered += (_, _) => RestackPopups();
        _openPopups.Add(popup);
        PositionPopup(popup, _openPopups.Count - 1);
        popup.Show();   // ShowActivated=false -> appears without stealing focus
        return popup;
    }

    // The heads-up gives way to the completion card: close any warning whose alarm has reached its fire time.
    // Decoupled from Fired, so it works for one-shots and recurring alike (the captured fire time is the
    // occurrence's, not the live alarm's already-advanced EndsAt).
    private void CloseFiredWarnings()
    {
        var now = _scheduler.Now;
        foreach (var (popup, fireAt) in _warningFireTimes.ToList())
            if (now >= fireAt && popup.IsLoaded) popup.Close();   // IsLoaded: never re-close a window already closing
    }

    private void PositionPopup(CompletionPopup popup, int indexFromBottom)
    {
        var anchor = _main ?? (Application.Current.MainWindow as MainWindow);
        var work = anchor is not null ? ScreenHelper.WorkAreaForWindow(anchor) : SystemParameters.WorkArea;
        popup.UpdateLayout();
        var size = new Size(popup.Width, popup.ActualHeight > 0 ? popup.ActualHeight : 140);
        var p = ScreenHelper.ClampBottomRight(work, size, 16);
        popup.Left = p.X;
        popup.Top = p.Y - indexFromBottom * (size.Height + 8);   // stack upward
    }

    private void RestackPopups()
    {
        for (var i = 0; i < _openPopups.Count; i++) PositionPopup(_openPopups[i], i);
    }

    private void ShowMainWindow()
    {
        Func<AlarmItemViewModel, EditAlarmWindow> editFactory = row => new EditAlarmWindow(
            new EditAlarmViewModel(row.Item.Id, row.Item.EndsAt?.ToString("HH\\:mm") ?? "",
                row.Item.Label ?? "", row.Item.Sound, row.Item.RecurringDays ?? Weekdays.None, row.Item.WarnBefore,
                _mainVm.SoundOptions, _mainVm.ApplyAlarmEdit, _sound));
        _main ??= new MainWindow(_mainVm, () => new SettingsWindow(
                new SettingsViewModel(_settings, new StartupService(StartupService.CurrentExePath),
                    SaveData, _mainVm.SetDefaultSound)),
            editFactory, _settings, SaveData);
        Application.Current.MainWindow = _main;
        _main.Show();
        _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    private void Quit()
    {
        _timer.Stop();
        _hotkey.Dispose();
        _tray?.Dispose();
        _mainVm.CommitPendingDelete();   // an uncommitted delete commits on quit (spec §3.1)
        SaveData();                      // flush the final armed set
        Shutdown();
    }

    private void SaveData()
    {
        var armed = _scheduler.Alarms;
        var data = new TidsroData
        {
            Settings = _settings,
            Alarms = armed.Where(a => a.TriggerType == TriggerType.ClockTime).Select(ToRecord).ToList(),
            RecurringAlarms = armed.Where(a => a.TriggerType == TriggerType.Recurring).Select(ToRecurringRecord).ToList(),
        };
        try { _persistence.Save(data); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* non-critical */ }
    }

    private static AlarmRecord ToRecord(TimerItem a) => new()
    {
        Id = a.Id,
        FireAt = a.EndsAt?.LocalDateTime ?? default,
        Label = a.Label,
        Sound = a.Sound,
        WarnBefore = a.WarnBefore,
        Enabled = a.IsEnabled,
    };

    private static RecurringAlarmRecord ToRecurringRecord(TimerItem a) => new()
    {
        Id = a.Id,
        Hour = a.EndsAt?.Hour ?? 0,
        Minute = a.EndsAt?.Minute ?? 0,
        Days = a.RecurringDays ?? Weekdays.None,
        Label = a.Label,
        Sound = a.Sound,
        WarnBefore = a.WarnBefore,
        Enabled = a.IsEnabled,
        NextFireAt = a.EndsAt?.LocalDateTime ?? default,   // the next occurrence — the durable dedup marker
    };

    private void ArmLoadedAlarms(IEnumerable<AlarmRecord> records)
    {
        foreach (var r in records)
        {
            try
            {
                _scheduler.ArmClockAlarm(LocalToOffset(r.FireAt), r.Label, r.Sound, r.Id, r.WarnBefore, r.Enabled);
            }
            catch { /* a residual bad record must never stop launch (spec §4) */ }
        }
    }

    private void ArmLoadedRecurring(IEnumerable<RecurringAlarmRecord> records)
    {
        foreach (var r in records)
        {
            try
            {
                // Restore the persisted next occurrence so a quick relaunch doesn't re-fire within grace;
                // the first tick reconciles any occurrence missed while the app was closed.
                var next = LocalToOffset(r.NextFireAt);
                _scheduler.ArmRecurringAlarm(r.Hour, r.Minute, r.Days, r.Label, r.Sound, r.Id, next, r.WarnBefore, r.Enabled);
            }
            catch { /* a residual bad record must never stop launch (spec §4) */ }
        }
    }

    // A persisted alarm time is a wall-clock local time; tag it Local before lifting to DateTimeOffset so
    // the scheduler compares against the right instant.
    private static DateTimeOffset LocalToOffset(DateTime local) =>
        new(DateTime.SpecifyKind(local, DateTimeKind.Local));

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _showEvent?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
