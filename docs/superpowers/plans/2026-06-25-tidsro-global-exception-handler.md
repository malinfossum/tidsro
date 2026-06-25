# Global Exception Handler & Crash Log — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An unhandled exception in Tidsro is caught, logged to a file, and — for UI-thread errors — survived, so the alarm app never silently vanishes and every caught error is discoverable.

**Architecture:** A new testable `LogService` (mirrors `PersistenceService`: a path in the constructor, an `IClock` for deterministic timestamps, all I/O self-contained, never throws) holds the formatting, throttle, and rollover logic. `App.xaml.cs` stays a thin wiring layer that installs three runtime hooks (`DispatcherUnhandledException` → survive; `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` → log), guards `OnStartup`, and shows a calm tray balloon. `TrayBuilder` gains an "Open log folder" item.

**Tech Stack:** .NET 10 / WPF, xUnit, H.NotifyIcon.Wpf 2.4.1, `IClock`/`FakeClock` (existing).

**Spec:** `docs/superpowers/specs/2026-06-25-tidsro-global-exception-handler-design.md`

---

## Conventions & gotchas (read once)

- **Run all tests:** `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj`
- **Run one test:** `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~LogServiceTests.<MethodName>"`
- **Build the app:** `dotnet build src/Tidsro/Tidsro.csproj`
- **Build lock gotcha:** a running `Tidsro.exe` locks its output and breaks the build. Stop it first:
  `Get-Process Tidsro -ErrorAction SilentlyContinue | Stop-Process -Force`
- **Zero warnings** is the project standard — match delegate signatures exactly (incl. nullability of `sender`) so no `CS86xx` warnings appear.
- Tasks 1–5 touch only `LogService.cs` + `LogServiceTests.cs`; the app keeps building and all existing tests keep passing at every commit. Task 6 wires it in.
- Commits: conventional style, **no `Co-Authored-By` / no Claude attribution** (Malin is sole author).

## File structure

| File | Responsibility | Tasks |
|---|---|---|
| `src/Tidsro/Services/LogService.cs` (**new**) | Format, append, throttle, and roll the crash log. Never throws. | 1–5 |
| `tests/Tidsro.Tests/LogServiceTests.cs` (**new**) | Drives `LogService` (temp file + `FakeClock`). | 1–5 |
| `src/Tidsro/App.xaml.cs` (**modify**) | Install handlers, guard `OnStartup`, balloon, `OpenLogFolder`. | 6 |
| `src/Tidsro/Services/TrayBuilder.cs` (**modify**) | "Open log folder" menu item. | 6 |

---

### Task 1: `LogService` skeleton + entry formatting

**Files:**
- Create: `src/Tidsro/Services/LogService.cs`
- Test: `tests/Tidsro.Tests/LogServiceTests.cs`

- [ ] **Step 1: Write the failing test** — create `tests/Tidsro.Tests/LogServiceTests.cs`:

```csharp
using System.IO;
using System.Linq;
using Tidsro.Services;
using Xunit;

namespace Tidsro.Tests;

public class LogServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;
    private readonly FakeClock _clock = new();

    public LogServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "TidsroTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "tidsro.log");
    }

    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private static int CountEntries(string text) =>
        text.Split(Environment.NewLine).Count(line => line.StartsWith("====="));

    [Fact]
    public void Format_includes_timestamp_version_source_type_and_message()
    {
        var now = new DateTimeOffset(2026, 6, 25, 14, 32, 1, TimeSpan.FromHours(2));
        var text = LogService.Format(now, new InvalidOperationException("boom"),
            "DispatcherUnhandledException", new Version(1, 4, 0));

        Assert.Contains("2026-06-25 14:32:01", text);
        Assert.Contains("v1.4.0", text);
        Assert.Contains("DispatcherUnhandledException", text);
        Assert.Contains("System.InvalidOperationException", text);
        Assert.Contains("boom", text);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~LogServiceTests.Format_includes_timestamp_version_source_type_and_message"`
Expected: **build error** — `LogService` does not exist yet. (That is the red.)

- [ ] **Step 3: Write the minimal implementation** — create `src/Tidsro/Services/LogService.cs`:

```csharp
using System.Globalization;
using System.IO;
using System.Reflection;

namespace Tidsro.Services;

/// <summary>
/// Appends unhandled-exception records to a log file so a crash is never silent. Mirrors
/// PersistenceService: a path in, all I/O self-contained, tested against a temp file. Never throws —
/// a logger that crashes while logging a crash is useless.
/// </summary>
public sealed class LogService
{
    private const long MaxBytes = 512 * 1024;                              // roll the log past ~512 KB
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromSeconds(5);

    private readonly string _path;
    private readonly IClock _clock;
    private string? _lastSignature;
    private DateTimeOffset _lastWritten;

    public LogService(string path, IClock clock)
    {
        _path = path;
        _clock = clock;
    }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tidsro", "tidsro.log");

    /// <summary>The exact text of one log entry. Pure, so it can be asserted directly in tests.</summary>
    public static string Format(DateTimeOffset now, Exception ex, string source, Version? version)
    {
        var stamp = now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
        var ver = version is null ? "?" : $"v{version.Major}.{version.Minor}.{version.Build}";
        return $"===== {stamp} · {ver} · {source} ====={Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";
    }
}
```

(The `_path`, `_clock`, `_lastSignature`, `_lastWritten` fields are unused until Task 2/3 — that is expected; they keep the skeleton stable so later tasks only add a method.)

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~LogServiceTests.Format_includes_timestamp_version_source_type_and_message"`
Expected: **PASS**. (Unused-field warnings are acceptable here; they're consumed in Task 2–3. If the build treats them as errors, proceed to Task 2 which uses them.)

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/Services/LogService.cs tests/Tidsro.Tests/LogServiceTests.cs
git commit -m "feat: add LogService crash-entry formatting"
```

---

### Task 2: Append an entry to the log file

**Files:**
- Modify: `src/Tidsro/Services/LogService.cs`
- Test: `tests/Tidsro.Tests/LogServiceTests.cs`

- [ ] **Step 1: Write the failing tests** — add to `LogServiceTests`:

```csharp
    [Fact]
    public void Log_appends_an_entry_to_the_file()
    {
        new LogService(_path, _clock).Log(new InvalidOperationException("boom"), "Test");

        var text = File.ReadAllText(_path);
        Assert.Contains("System.InvalidOperationException", text);
        Assert.Contains("boom", text);
    }

    [Fact]
    public void Log_two_distinct_errors_writes_two_entries()
    {
        var svc = new LogService(_path, _clock);
        svc.Log(new InvalidOperationException("first"), "Test");
        svc.Log(new InvalidOperationException("second"), "Test");

        Assert.Equal(2, CountEntries(File.ReadAllText(_path)));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~LogServiceTests.Log_appends_an_entry_to_the_file|FullyQualifiedName~LogServiceTests.Log_two_distinct_errors_writes_two_entries"`
Expected: **build error** — `Log` is not defined yet.

- [ ] **Step 3: Write the minimal implementation** — add to `LogService` (note: **no** try/catch yet — that is driven by Task 4):

```csharp
    /// <summary>
    /// Records one unhandled exception. Returns true when this is a fresh error worth surfacing to the
    /// user (a balloon), false when it is a consecutive duplicate suppressed within the dedupe window.
    /// The return reflects the throttle decision only — it stays true even if the file write fails,
    /// because the user should still be told.
    /// </summary>
    public bool Log(Exception ex, string source)
    {
        var now = _clock.Now;
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        Write(Format(now, ex, source, version));
        return true;
    }

    private void Write(string entry)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.AppendAllText(_path, entry);
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~LogServiceTests.Log_"`
Expected: **PASS** (both append tests).

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/Services/LogService.cs tests/Tidsro.Tests/LogServiceTests.cs
git commit -m "feat: append crash entries to the log file"
```

---

### Task 3: Throttle consecutive identical errors

**Files:**
- Modify: `src/Tidsro/Services/LogService.cs`
- Test: `tests/Tidsro.Tests/LogServiceTests.cs`

- [ ] **Step 1: Write the failing tests** — add to `LogServiceTests`:

```csharp
    [Fact]
    public void Log_suppresses_an_identical_error_within_the_window()
    {
        var svc = new LogService(_path, _clock);

        Assert.True(svc.Log(new InvalidOperationException("boom"), "Test"));
        Assert.False(svc.Log(new InvalidOperationException("boom"), "Test"));   // same signature, same instant
        Assert.Equal(1, CountEntries(File.ReadAllText(_path)));
    }

    [Fact]
    public void Log_writes_again_after_the_window_elapses()
    {
        var svc = new LogService(_path, _clock);

        Assert.True(svc.Log(new InvalidOperationException("boom"), "Test"));
        _clock.Advance(TimeSpan.FromSeconds(6));
        Assert.True(svc.Log(new InvalidOperationException("boom"), "Test"));
        Assert.Equal(2, CountEntries(File.ReadAllText(_path)));
    }

    [Fact]
    public void Log_writes_a_different_signature_within_the_window()
    {
        var svc = new LogService(_path, _clock);

        Assert.True(svc.Log(new InvalidOperationException("boom"), "Test"));
        Assert.True(svc.Log(new InvalidOperationException("boom"), "Other"));   // different source -> different signature
        Assert.Equal(2, CountEntries(File.ReadAllText(_path)));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~LogServiceTests.Log_suppresses|FullyQualifiedName~LogServiceTests.Log_writes"`
Expected: **FAIL** — `Log_suppresses...` writes 2 entries and returns `true` (no throttle yet).

- [ ] **Step 3: Write the minimal implementation** — replace the body of `Log` with the throttled version:

```csharp
    public bool Log(Exception ex, string source)
    {
        var now = _clock.Now;
        var signature = $"{source}|{ex.GetType().FullName}|{ex.Message}";
        if (signature == _lastSignature && now - _lastWritten < DedupeWindow)
            return false;                                              // collapse a run of identical errors

        _lastSignature = signature;
        _lastWritten = now;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        Write(Format(now, ex, source, version));
        return true;
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~LogServiceTests"`
Expected: **PASS** (all LogService tests so far).

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/Services/LogService.cs tests/Tidsro.Tests/LogServiceTests.cs
git commit -m "feat: throttle duplicate crash log entries"
```

---

### Task 4: Never throw while logging

**Files:**
- Modify: `src/Tidsro/Services/LogService.cs`
- Test: `tests/Tidsro.Tests/LogServiceTests.cs`

- [ ] **Step 1: Write the failing test** — add to `LogServiceTests`:

```csharp
    [Fact]
    public void Log_does_not_throw_on_an_unwritable_path()
    {
        var fileInTheWay = Path.Combine(_dir, "blocker");
        File.WriteAllText(fileInTheWay, "x");                       // a file where a directory is needed
        var badPath = Path.Combine(fileInTheWay, "nested", "tidsro.log");

        var thrown = Record.Exception(() => new LogService(badPath, _clock)
            .Log(new InvalidOperationException("boom"), "Test"));
        Assert.Null(thrown);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~LogServiceTests.Log_does_not_throw_on_an_unwritable_path"`
Expected: **FAIL** — `Directory.CreateDirectory` throws `IOException` (a file sits where a directory is needed); `Record.Exception` returns non-null.

- [ ] **Step 3: Write the minimal implementation** — wrap `Write` in a guard:

```csharp
    private void Write(string entry)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.AppendAllText(_path, entry);
        }
        catch { /* logging must never throw — a logger that crashes while logging a crash is useless */ }
    }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~LogServiceTests"`
Expected: **PASS** (all LogService tests).

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/Services/LogService.cs tests/Tidsro.Tests/LogServiceTests.cs
git commit -m "feat: keep LogService from ever throwing while logging"
```

---

### Task 5: Roll the log over past the size cap

**Files:**
- Modify: `src/Tidsro/Services/LogService.cs`
- Test: `tests/Tidsro.Tests/LogServiceTests.cs`

- [ ] **Step 1: Write the failing test** — add to `LogServiceTests`:

```csharp
    [Fact]
    public void Log_rolls_the_file_over_to_old_past_the_cap()
    {
        File.WriteAllText(_path, new string('x', 600 * 1024));      // > 512 KB
        new LogService(_path, _clock).Log(new InvalidOperationException("boom"), "Test");

        Assert.True(File.Exists(_path + ".old"));
        Assert.True(new FileInfo(_path).Length < 512 * 1024);      // live file is fresh and small
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~LogServiceTests.Log_rolls_the_file_over_to_old_past_the_cap"`
Expected: **FAIL** — no `.old` archive is created (the entry is appended to the already-large file).

- [ ] **Step 3: Write the minimal implementation** — add `RollIfTooLarge` and call it inside `Write` before the append:

```csharp
    private void Write(string entry)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            RollIfTooLarge();
            File.AppendAllText(_path, entry);
        }
        catch { /* logging must never throw — a logger that crashes while logging a crash is useless */ }
    }

    private void RollIfTooLarge()
    {
        try
        {
            var info = new FileInfo(_path);
            if (info.Exists && info.Length > MaxBytes)
                File.Move(_path, _path + ".old", overwrite: true);    // keep one previous generation
        }
        catch { /* a failed roll must not stop logging */ }
    }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~LogServiceTests"`
Expected: **PASS** (all LogService tests).

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/Services/LogService.cs tests/Tidsro.Tests/LogServiceTests.cs
git commit -m "feat: roll the crash log over past 512 KB"
```

---

### Task 6: Wire the handlers, balloon, and tray item into the app

No unit test — `App` and `TrayBuilder` build WPF/H.NotifyIcon objects that can't be instantiated in xUnit. **Verification = the build succeeds and the full existing suite still passes**; behavior is verified by the manual acceptance in Task 7. `TrayBuilder` and the `App` call site change together so the build never breaks.

**Files:**
- Modify: `src/Tidsro/Services/TrayBuilder.cs`
- Modify: `src/Tidsro/App.xaml.cs`

- [ ] **Step 1: Add the "Open log folder" item to `TrayBuilder`** — change the signature and insert the item.

Change the method signature:

```csharp
    public static TaskbarIcon Create(Action onOpen, Action onFocusAlert, Action onOpenLog, Action onQuit)
```

Add the menu item next to the others (after the `focusAlert` item is built):

```csharp
        var openLog = new MenuItem { Header = "Open log folder" };
        openLog.Click += (_, _) => onOpenLog();
```

And insert it into the menu between `focusAlert` and the separator before `quit`:

```csharp
        menu.Items.Add(about);
        menu.Items.Add(new Separator());
        menu.Items.Add(open);
        menu.Items.Add(focusAlert);
        menu.Items.Add(openLog);
        menu.Items.Add(new Separator());
        menu.Items.Add(quit);
```

- [ ] **Step 2: Add the two usings to `App.xaml.cs`** (top of file, with the other `using` lines):

```csharp
using System.Diagnostics;
using System.Threading.Tasks;
```

- [ ] **Step 3: Add the `_log` field** to `App` (next to the other service fields, e.g. after `_persistence`):

```csharp
    private LogService _log = null!;
```

- [ ] **Step 4: Install the handlers and guard `OnStartup`** — replace the whole `OnStartup` method with:

```csharp
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
            _tray = TrayBuilder.Create(ShowMainWindow, FocusLatestAlert, OpenLogFolder, Quit);
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
```

- [ ] **Step 5: Add the handler methods** — insert after `OnStartup` (before `TryClaimSingleInstance`):

```csharp
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
        if (_log.Log(e.Exception, "DispatcherUnhandledException"))
            _tray?.ShowNotification("Tidsro", "Tidsro hit a problem but is still running.");
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
    // so the log is discoverable after a balloon. Best-effort — opening a folder must never crash.
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
```

- [ ] **Step 6: Stop any running app, then build**

Run:
```
Get-Process Tidsro -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build src/Tidsro/Tidsro.csproj
```
Expected: **Build succeeded, 0 warnings.** (If a `CS8622` nullability warning appears on a handler, confirm its `sender` parameter matches the table: `object` for Dispatcher/AppDomain, `object?` for the `EventHandler<UnobservedTaskExceptionEventArgs>`.)

- [ ] **Step 7: Run the full suite to confirm nothing regressed**

Run: `dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj`
Expected: **PASS** — all existing tests plus the new `LogService` tests.

- [ ] **Step 8: Commit**

```bash
git add src/Tidsro/App.xaml.cs src/Tidsro/Services/TrayBuilder.cs
git commit -m "feat: install global exception handlers, crash log, and balloon"
```

---

### Task 7: Manual acceptance & wrap (gates the PR)

Not a code task — this is the human/GUI verification the unit tests can't cover. Use a temporary, clearly-reverted throw to drive it.

- [ ] **Single caught crash** — temporarily add `throw new InvalidOperationException("acceptance");` as the first line inside the tick handler in `StartTickLoop` (`_timer.Tick += ...`). Run the built `Tidsro.exe`. Expect: the app **stays alive**, a calm tray balloon appears, and `%AppData%\Tidsro\tidsro.log` gets a timestamped entry. **Revert the throw.**
- [ ] **Runaway (every tick)** — with the throw still in (before reverting), confirm the balloon + log entry appear **once per ~5 s**, not four times a second, and the app keeps running. Then revert.
- [ ] **Open log folder** — tray → **"Open log folder"** opens Explorer at `%AppData%\Tidsro` with `tidsro.log` selected.
- [ ] **Rollover** — append junk to `tidsro.log` until it passes 512 KB (or copy a >512 KB file over it), trigger one more caught error, and confirm it rolls to `tidsro.log.old` with a fresh live file.
- [ ] **Startup failure** — temporarily `throw` at the top of `LoadStateAndServices`, launch, confirm the one-line *"Tidsro couldn't start. See tidsro.log."* message appears, an entry is written, and the app exits cleanly. **Revert.**
- [ ] **Screen reader** — with Narrator on, confirm the crash balloon is announced.
- [ ] **Final build + suite green after all reverts:**
  `Get-Process Tidsro -ErrorAction SilentlyContinue | Stop-Process -Force; dotnet build src/Tidsro/Tidsro.csproj; dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj`
- [ ] **Final holistic review**, then open a PR for Malin to merge (squash or merge-commit per her call). No `CHANGELOG`/README/version bump in this slice unless Malin wants to fold it into a release.

---

## Self-review (completed during planning)

- **Spec coverage:** `LogService` + `DefaultPath` (Task 1–5); three hooks (Task 6 §5); `OnStartup` guard + `MessageBox` fallback (Task 6 §4); throttle (Task 3); tray balloon (Task 6 §5); "Open log folder" (Task 6 §1, §5); size cap (Task 5); never-throws (Task 4); honest-scope background-log (Task 6, `OnDomainUnhandledException`). All spec sections map to a task.
- **Placeholder scan:** none — every code step shows complete code; every run step shows the exact command and expected result.
- **Type consistency:** `Log(Exception, string) → bool`, `Format(DateTimeOffset, Exception, string, Version?) → string`, `Write(string)`, `RollIfTooLarge()`, `InstallExceptionHandlers()`, `OpenLogFolder()`, `TrayBuilder.Create(Action, Action, Action, Action)` — names and signatures are identical across every task that references them.
