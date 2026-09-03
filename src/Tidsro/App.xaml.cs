using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    private LogService _log = null!;
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
    private readonly FailureAlertPolicy _alerts = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        InstallExceptionHandlers();   // build the log and the safety nets before anything else can throw

        try
        {
            if (!TryClaimSingleInstance())   // a second launch surfaces the first window, then exits
                return;

            LoadStateAndServices();
            WireSchedulerEvents();
            StartTickLoop();
            RegisterHotkey();
            _tray = TrayBuilder.Create(ShowMainWindow, FocusLatestAlert, () => _openPopups.Count > 0, OpenLogFolder, Quit);
            ShowWindowUnlessBootLaunch(e);
        }
        catch (Exception ex)
        {
            // A startup failure must explain itself, not vanish. There is no tray yet, so the
            // last resort is a single message — not knowing is the worst outcome (spec).
            _log.Log(ex, "OnStartup");
            MessageBox.Show("Tidsro couldn't start. See tidsro.log.", "Tidsro",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
        }
    }

    // Build the crash log and install the app-wide safety nets. Done first in OnStartup so even a
    // failure during the rest of startup is recorded. UI-thread errors are kept alive; background
    // crashes are logged best-effort (the runtime is already tearing down when they surface).
    private void InstallExceptionHandlers()
    {
        _log = new LogService(LogService.DefaultPath, new SystemClock());
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _log.Log(e.Exception, "DispatcherUnhandledException");   // always logged; the policy alone decides the dialog
        if (_alerts.TryClaimCrash())
            ShowFailureDialog("Tidsro", "Tidsro hit a problem but is still running. See Tray ▸ Open log folder.");
        e.Handled = true;   // a single glitch must never silently kill an alarm app
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            _log.Log(ex, "AppDomain.UnhandledException");   // best-effort: the process is terminating
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _log.Log(e.Exception, "TaskScheduler.UnobservedTaskException");
        e.SetObserved();
    }

    // Open the folder holding the crash log, selecting the file if it exists. Reachable from the tray
    // so the log is discoverable after a failure dialog. Best-effort — opening a folder must never crash.
    private void OpenLogFolder()
    {
        try
        {
            var dir = Path.GetDirectoryName(LogService.DefaultPath)!;
            Directory.CreateDirectory(dir);
            if (File.Exists(LogService.DefaultPath))
                Process.Start("explorer.exe", $"/select,\"{LogService.DefaultPath}\"");
            else
                Process.Start("explorer.exe", dir);
        }
        catch { /* opening Explorer is a convenience, never critical */ }
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
        _mainVm.ClosePopupsRequested += (_, _) =>
        {
            foreach (var popup in _openPopups.ToList())
                // IsLoaded: never re-close a window already closing. CloseWithoutRestoringFocus, not
                // Close: a normal close restores the foreground window captured when the card appeared
                // (often a different app), which would push the modal Settings dialog behind it.
                if (popup.IsLoaded) popup.CloseWithoutRestoringFocus();
        };

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
        _main ??= new MainWindow(_mainVm, () => new SettingsWindow(confirm =>
                new SettingsViewModel(_settings, new StartupService(StartupService.CurrentExePath),
                    SaveData, _mainVm.SetDefaultSound,
                    clearAllAlarms: _mainVm.ClearAllAlarms,
                    alarmCount: () => _scheduler.Alarms.Count + _scheduler.Running.Count,
                    hasAnythingToClear: () => _mainVm.HasAnythingToClear,
                    // Also returns the live view to the first tab; clearing the stored value alone
                    // would leave the reset invisible until the next launch.
                    resetWindowPlacement: () => { _main?.ResetPlacement(); _mainVm.SelectedTabIndex = 0; },
                    confirm: confirm,
                    dataPorts: BuildDataPorts())),
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
        _mainVm.CommitPendingDelete();   // an uncommitted delete commits on quit (spec §3.1)
        _main?.CaptureWindowState();   // the tray's Quit never runs OnClosing; null when the window was never opened
        SaveData(finalSave: true);       // flush the final armed set before the window that would own a failure dialog goes away
        _tray?.Dispose();
        Shutdown();
    }

    // The owner for every app-level dialog: the data dialogs and the failure alerts alike. While
    // Settings is up, dialogs centre on it so the modal chain stays intact; otherwise the main window
    // when it exists, or null (very early in startup, before any window has been built).
    private Window? DialogOwner =>
        Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault() ?? (Window?)_main;

    // Always on top so it cannot land underneath a Topmost alarm card (whose buttons would otherwise
    // go inert without looking disabled). Catches rather than lets an exception escape: from
    // OnDispatcherUnhandledException that would skip e.Handled = true and kill the process on a
    // survivable glitch; from Quit() it would abort after the timer and hotkey are already torn down,
    // leaving the app alive with its heartbeat stopped and no alarm ever firing again. The finally
    // still guarantees the policy's dialog-open flag clears either way.
    private void ShowFailureDialog(string title, string message)
    {
        try { ChoiceDialog.ShowMessage(DialogOwner, title, message, alwaysOnTop: true); }
        catch (Exception ex) { _log.Log(ex, "ShowFailureDialog"); }
        finally { _alerts.ReleaseDialog(); }
    }

    private DataPorts BuildDataPorts() => new(
        Dialogs: new FileDialogService(),
        Transfer: new DataTransferService(PersistenceService.DefaultPath),
        BuildData: BuildData,
        AskImportChoice: message => ChoiceDialog.AskImport(DialogOwner, message),
        ApplyImport: ApplyImport,
        ShowMessage: (title, message) => ChoiceDialog.ShowMessage(DialogOwner, title, message),
        Today: () => DateTime.Today);

    // Replacing the schedule raises AlarmsChanged, which persists through the existing SaveData path.
    private void ApplyImport(TidsroData data, bool includeSettings)
    {
        _mainVm.ReplaceAllAlarms(data.Alarms, data.RecurringAlarms);
        if (!includeSettings || data.Settings is not AppSettings imported) return;

        // Startup goes through the service, never the field alone — a checkbox that disagrees with the
        // HKCU Run key is the class of bug PR #16 fixed.
        var startup = new StartupService(StartupService.CurrentExePath);
        if (imported.LaunchAtStartup) startup.Enable(); else startup.Disable();

        _settings.LaunchAtStartup = imported.LaunchAtStartup;
        _settings.DefaultSound = imported.DefaultSound;
        _settings.SelectedTab = imported.SelectedTab;
        _settings.WindowLeft = imported.WindowLeft;
        _settings.WindowTop = imported.WindowTop;
        _settings.WindowWidth = imported.WindowWidth;
        _settings.WindowHeight = imported.WindowHeight;

        _mainVm.SetDefaultSound(imported.DefaultSound);
        _mainVm.SelectedTabIndex = imported.SelectedTab;
        _main?.ApplyPlacement(imported);   // the live window, or OnClosing overwrites the restore
        SaveData();
    }

    // The live state as a document. Export uses this too, so an export still captures good data when
    // saves have been failing — which is exactly when someone reaches for a backup.
    private TidsroData BuildData()
    {
        var armed = _scheduler.Alarms;
        return new TidsroData
        {
            Settings = _settings,
            Alarms = armed.Where(a => a.TriggerType == TriggerType.ClockTime).Select(ToRecord).ToList(),
            RecurringAlarms = armed.Where(a => a.TriggerType == TriggerType.Recurring).Select(ToRecurringRecord).ToList(),
        };
    }

    // Passed as a method group in ShowMainWindow (to SettingsViewModel and to MainWindow), so an
    // overload is needed here rather than an optional parameter — that would not convert to a
    // zero-argument delegate.
    private void SaveData() => SaveData(finalSave: false);

    private void SaveData(bool finalSave)
    {
        try
        {
            _persistence.Save(BuildData());
            _alerts.NoteSaveSucceeded();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Always logged (its own 5 s dedup still governs the log file); the policy alone decides
            // the dialog now, so the two are no longer coupled.
            _log.Log(ex, "SaveData");
            // A final save ignores whether a mid-session failure was already announced (see
            // TryClaimFinalSaveFailure) — otherwise the strictly more urgent quit-time warning would
            // be defeated by the ordering that happens in nearly every sustained outage.
            var claimed = finalSave ? _alerts.TryClaimFinalSaveFailure() : _alerts.TryClaimSaveFailure();
            if (claimed)
                ShowFailureDialog("Couldn't save", finalSave
                    ? "Tidsro couldn't save your latest changes. They will be lost when Tidsro closes. See Tray ▸ Open log folder for details."
                    : "Tidsro couldn't save your changes to disk. Your alarms are still here, but they may not survive closing the app. See Tray ▸ Open log folder for details.");
        }
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
        EndMinute = a.EndMinute,
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
                _scheduler.ArmRecurringAlarm(r.Hour, r.Minute, r.Days, r.Label, r.Sound, r.Id, next, r.WarnBefore, r.Enabled, r.EndMinute);
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
