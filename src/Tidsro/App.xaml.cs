using System.Collections.Generic;
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
    private MainViewModel _mainVm = null!;
    private AppSettings _settings = null!;
    private HotkeyService _hotkey = null!;
    private DispatcherTimer _timer = null!;
    private MainWindow? _main;
    private readonly List<CompletionPopup> _openPopups = new();
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single instance: a second launch signals the first to surface its window, then exits.
        _instanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirst);
        if (!isFirst)
        {
            try { EventWaitHandle.OpenExisting(ShowWindowEventName).Set(); }
            catch { /* the first instance may be mid-exit; nothing useful to do */ }
            Shutdown();
            return;
        }
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
        ThreadPool.RegisterWaitForSingleObject(_showEvent,
            (_, _) => Dispatcher.Invoke(ShowMainWindow), null, Timeout.Infinite, executeOnlyOnce: false);

        _persistence = new PersistenceService(PersistenceService.DefaultPath);
        _settings = _persistence.Load();
        _scheduler = new SchedulerService(new SystemClock());
        _sound = new SoundService();
        _mainVm = new MainViewModel(_scheduler, _sound, _settings.DefaultSound);

        var startup = new StartupService(StartupService.CurrentExePath);
        startup.RefreshIfEnabled();          // self-heal a stale Run-key path

        _scheduler.Fired += OnTimerFired;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => { _scheduler.Tick(); _mainVm.RefreshAll(); };
        _timer.Start();

        _hotkey = new HotkeyService();
        _hotkey.Pressed += (_, _) => FocusLatestAlert();
        _hotkey.Register();   // best-effort; the tray "Focus latest alert" item is the keyboard fallback if the chord is taken

        _tray = TrayBuilder.Create(ShowMainWindow, FocusLatestAlert, Quit);

        // Surface the window on a normal launch so it's discoverable; stay in the tray when auto-started at boot.
        if (!e.Args.Contains(StartupService.StartupArg))
            ShowMainWindow();
    }

    // Keyboard route to the newest completion card — shared by the global hotkey and the tray
    // "Focus latest alert" item (the fallback when the hotkey can't register; spec §5.3)
    private void FocusLatestAlert() => _openPopups.LastOrDefault()?.FocusForKeyboard();

    private void OnTimerFired(object? sender, TimerItem item)
    {
        _sound.Play(item.Sound);

        var vm = new PopupViewModel(item,
            onSnooze: i => _scheduler.Snooze(i, TimeSpan.FromMinutes(5)),
            onRestart: i => _scheduler.Restart(i),
            onDismiss: i => _scheduler.Cancel(i));

        var popup = new CompletionPopup(vm);
        popup.Closed += (_, _) => { _openPopups.Remove(popup); RestackPopups(); };
        // first placement uses an estimated height; reposition the stack once the card has actually measured
        popup.ContentRendered += (_, _) => RestackPopups();
        _openPopups.Add(popup);
        PositionPopup(popup, _openPopups.Count - 1);
        popup.Show();   // ShowActivated=false -> appears without stealing focus
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
        _main ??= new MainWindow(_mainVm, () => new SettingsWindow(
                new SettingsViewModel(_settings, new StartupService(StartupService.CurrentExePath),
                    _persistence, _mainVm.SetDefaultSound)),
            _settings, () => _persistence.Save(_settings));
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
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _showEvent?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
