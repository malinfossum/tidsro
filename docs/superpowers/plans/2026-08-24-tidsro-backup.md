# Export and Import Data Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add **Export data…** and **Import data…** to the Settings "Data" section, so a user can write their whole schedule to a JSON file they own and restore it — choosing at import time whether to restore the alarms alone or the settings too.

**Architecture:** No new file format — export writes the same schema-4 `TidsroData` that `PersistenceService` already writes, so an export and `%AppData%\Tidsro\data.json` are the same artifact. File-level work (write, read, validate, snapshot) goes into a new `DataTransferService` with no WPF dependency; dialogs go behind `IFileDialogService` so view-model tests never open a real one. Applying an import reuses the `ClearAllAlarms` shape from the clear-data slice, with an arming pass on the end.

**Tech Stack:** C# / .NET 10 · WPF · CommunityToolkit.Mvvm · System.Text.Json · xUnit

**Spec:** `docs/superpowers/specs/2026-08-24-tidsro-backup-design.md`

## Global Constraints

- Target `net10.0-windows`; no new NuGet packages.
- Tests are xUnit in `tests/Tidsro.Tests`. The suite is green at **359 tests** before this plan starts; it must be green at every commit.
- **No `Co-Authored-By` trailer and no Claude attribution in any commit message.** Malin is the sole listed contributor.
- Services are the I/O boundary; view models never reference `System.Windows`.
- Best-effort file operations (snapshot, quarantine) **must never throw** — they wrap in `try`/`catch` and swallow.
- Failure surfaces are in-app dialogs, never tray balloons: balloons are invisible on this machine (`ToastEnabled = 0`).
- Import size ceiling: **8 MB** (`DataTransferService.MaxImportBytes`).
- Pre-import copy: `%AppData%\Tidsro\data-before-import.json`, single file, overwritten each import.
- Suggested export name: `tidsro-backup-yyyy-MM-dd.json`; dialog filter `Tidsro backup (*.json)|*.json|All files (*.*)|*.*`.
- Run tests with `dotnet test Tidsro.slnx`. **Stop a running Tidsro first** (`Get-Process Tidsro | Stop-Process -Force`) or the build fails with MSB3027.

---

### Task 1: Split the atomic write out of `PersistenceService`

Export must reuse the temp-then-replace write without inheriting quarantine semantics: `Save` ends with `ClearQuarantine()`, which deletes `<path>.corrupt`. Against a user-chosen destination that would silently delete a neighbouring file. A failed write must also stop leaving a stale `.tmp` behind, which matters more in the user's Documents folder than it did in AppData.

**Files:**
- Modify: `src/Tidsro/Services/PersistenceService.cs:47-55`
- Test: `tests/Tidsro.Tests/PersistenceServiceTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `public static void WriteTo(string path, TidsroData data)` on `PersistenceService`. Task 2 calls it.

- [ ] **Step 1: Write the failing tests**

Add to `tests/Tidsro.Tests/PersistenceServiceTests.cs`:

```csharp
[Fact]
public void WriteTo_does_not_delete_a_corrupt_file_beside_the_destination()
{
    var dir = Path.Combine(Path.GetTempPath(), "tidsro-t1-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    try
    {
        var target = Path.Combine(dir, "export.json");
        var neighbour = target + ".corrupt";
        File.WriteAllText(neighbour, "someone else's file");

        PersistenceService.WriteTo(target, TidsroData.Defaults());

        Assert.True(File.Exists(target));
        Assert.True(File.Exists(neighbour));   // export must not clear quarantine outside AppData
    }
    finally { Directory.Delete(dir, recursive: true); }
}

[Fact]
public void WriteTo_leaves_no_temp_file_when_the_write_fails()
{
    var dir = Path.Combine(Path.GetTempPath(), "tidsro-t1-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    try
    {
        // A directory where the file should go: the write throws, and the temp must not survive.
        var target = Path.Combine(dir, "export.json");
        Directory.CreateDirectory(target);

        Assert.ThrowsAny<Exception>(() => PersistenceService.WriteTo(target, TidsroData.Defaults()));
        Assert.False(File.Exists(target + ".tmp"));
    }
    finally { Directory.Delete(dir, recursive: true); }
}

[Fact]
public void Save_still_clears_the_quarantine_copy()
{
    var dir = Path.Combine(Path.GetTempPath(), "tidsro-t1-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    try
    {
        var path = Path.Combine(dir, "data.json");
        File.WriteAllText(path + ".corrupt", "quarantined");

        new PersistenceService(path).Save(TidsroData.Defaults());

        Assert.False(File.Exists(path + ".corrupt"));   // a good save retires the recovery copy
    }
    finally { Directory.Delete(dir, recursive: true); }
}
```

- [ ] **Step 2: Run them and watch them fail**

Run: `dotnet test Tidsro.slnx --filter "FullyQualifiedName~PersistenceServiceTests"`
Expected: FAIL — `PersistenceService` does not contain a definition for `WriteTo`.

- [ ] **Step 3: Implement the split**

Replace `Save` in `src/Tidsro/Services/PersistenceService.cs`:

```csharp
public void Save(TidsroData data)
{
    WriteTo(_path, data);
    ClearQuarantine();   // a good save means any stale .corrupt recovery copy is no longer needed
}

/// <summary>The atomic write on its own: create the directory, write a temp file, replace.
/// Export uses this rather than <see cref="Save"/> — Save also clears the quarantine copy, which is
/// right for our own data file and quietly destructive against a path the user chose.</summary>
public static void WriteTo(string path, TidsroData data)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    var tmp = path + ".tmp";
    try
    {
        File.WriteAllText(tmp, JsonSerializer.Serialize(data, Options));   // flushed on close
        if (File.Exists(path)) File.Replace(tmp, path, null);              // atomic, same volume
        else File.Move(tmp, path);
    }
    catch
    {
        // Never leave a half-written temp beside a file the user chose.
        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort */ }
        throw;
    }
}
```

- [ ] **Step 4: Run the tests and watch them pass**

Run: `dotnet test Tidsro.slnx --filter "FullyQualifiedName~PersistenceServiceTests"`
Expected: PASS, all of them.

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/Services/PersistenceService.cs tests/Tidsro.Tests/PersistenceServiceTests.cs
git commit -m "refactor(persistence): split the atomic write out of Save

Export needs temp-then-replace without ClearQuarantine, which would
delete a <path>.corrupt neighbour at a destination the user chose. A
failed write now also removes its own temp file."
```

---

### Task 2: `DataTransferService` — export, validated read, pre-import snapshot

All the file-level work for the slice, with no WPF dependency so it is fully unit-testable. The shape gate here is the load-bearing safety check of the whole feature: `JsonSerializer` succeeds on any JSON object, so without it `{"foo":1}` reads as a valid empty backup and a confirmed import destroys the live schedule.

**Files:**
- Create: `src/Tidsro/Services/DataTransferService.cs`
- Test: `tests/Tidsro.Tests/DataTransferServiceTests.cs`

**Interfaces:**
- Consumes: `PersistenceService.WriteTo(string, TidsroData)` from Task 1.
- Produces:
  - `DataTransferService(string dataPath)`
  - `const long MaxImportBytes = 8 * 1024 * 1024`
  - `string SnapshotPath { get; }`
  - `void Export(string path, TidsroData data)`
  - `TidsroData? Read(string path)` — `null` means rejected
  - `void SnapshotBeforeImport()`
  Tasks 6 and 7 call all of these.

- [ ] **Step 1: Write the failing tests**

Create `tests/Tidsro.Tests/DataTransferServiceTests.cs`:

```csharp
using System.IO;
using Tidsro.Models;
using Tidsro.Services;
using Xunit;

namespace Tidsro.Tests;

public class DataTransferServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _dataPath;
    private readonly DataTransferService _svc;

    public DataTransferServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "tidsro-dt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _dataPath = Path.Combine(_dir, "data.json");
        _svc = new DataTransferService(_dataPath);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name, string contents)
    {
        var p = Path.Combine(_dir, name);
        File.WriteAllText(p, contents);
        return p;
    }

    [Fact]
    public void Export_then_Read_round_trips_the_alarms()
    {
        var data = TidsroData.Defaults();
        data.RecurringAlarms.Add(new RecurringAlarmRecord
        {
            Id = Guid.NewGuid(), Hour = 8, Minute = 30, Days = Weekdays.Monday,
            Label = "Class", Sound = SoundChoice.Bell,
            NextFireAt = new DateTime(2026, 9, 1, 8, 30, 0), Enabled = true,
        });
        var path = Path.Combine(_dir, "export.json");

        _svc.Export(path, data);
        var read = _svc.Read(path);

        Assert.NotNull(read);
        var alarm = Assert.Single(read!.RecurringAlarms);
        Assert.Equal("Class", alarm.Label);
        Assert.Equal(8, alarm.Hour);
    }

    [Fact]
    public void Read_rejects_a_valid_json_document_that_is_not_a_Tidsro_file()
    {
        // The data-loss guard: this deserializes into an empty-but-valid TidsroData.
        var path = Write("package.json", """{"name":"something","version":"1.0.0"}""");

        Assert.Null(_svc.Read(path));
    }

    [Fact]
    public void Read_accepts_a_Tidsro_file_that_holds_no_alarms()
    {
        var path = Write("empty.json", """{"SchemaVersion":4,"Settings":{},"Alarms":[],"RecurringAlarms":[]}""");

        var read = _svc.Read(path);

        Assert.NotNull(read);
        Assert.Empty(read!.Alarms);   // an empty schedule is a legitimate thing to restore
    }

    [Fact]
    public void Read_accepts_a_document_carrying_only_one_recognised_key()
    {
        var path = Write("alarms-only.json", """{"Alarms":[]}""");

        Assert.NotNull(_svc.Read(path));
    }

    [Fact]
    public void Read_rejects_a_file_that_is_not_json_at_all()
    {
        Assert.Null(_svc.Read(Write("notes.txt", "just some text")));
    }

    [Fact]
    public void Read_rejects_a_missing_file()
    {
        Assert.Null(_svc.Read(Path.Combine(_dir, "nope.json")));
    }

    [Fact]
    public void Read_rejects_a_file_over_the_size_ceiling_without_reading_it()
    {
        var path = Path.Combine(_dir, "huge.json");
        using (var fs = File.Create(path)) fs.SetLength(DataTransferService.MaxImportBytes + 1);

        Assert.Null(_svc.Read(path));
    }

    [Fact]
    public void Read_sanitizes_what_it_returns()
    {
        var path = Write("dirty.json", """
            {"SchemaVersion":4,"Settings":{"SelectedTab":99},
             "RecurringAlarms":[{"Id":"11111111-1111-1111-1111-111111111111","Hour":99,"Minute":0,
                                 "Days":1,"Sound":0,"NextFireAt":"2026-09-01T08:30:00"}]}
            """);

        var read = _svc.Read(path);

        Assert.NotNull(read);
        Assert.Empty(read!.RecurringAlarms);        // hour 99 is dropped
        Assert.Equal(0, read.Settings!.SelectedTab); // out-of-range tab clamped
    }

    [Fact]
    public void SnapshotBeforeImport_copies_the_live_data_file()
    {
        File.WriteAllText(_dataPath, """{"SchemaVersion":4,"Alarms":[]}""");

        _svc.SnapshotBeforeImport();

        Assert.True(File.Exists(_svc.SnapshotPath));
        Assert.Equal(File.ReadAllText(_dataPath), File.ReadAllText(_svc.SnapshotPath));
    }

    [Fact]
    public void SnapshotBeforeImport_overwrites_the_previous_snapshot()
    {
        File.WriteAllText(_dataPath, "first");
        _svc.SnapshotBeforeImport();
        File.WriteAllText(_dataPath, "second");

        _svc.SnapshotBeforeImport();

        Assert.Equal("second", File.ReadAllText(_svc.SnapshotPath));
    }

    [Fact]
    public void SnapshotBeforeImport_is_a_no_op_when_there_is_no_data_file_yet()
    {
        _svc.SnapshotBeforeImport();   // must not throw on a first run

        Assert.False(File.Exists(_svc.SnapshotPath));
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

Run: `dotnet test Tidsro.slnx --filter "FullyQualifiedName~DataTransferServiceTests"`
Expected: FAIL — the type `DataTransferService` does not exist.

- [ ] **Step 3: Implement the service**

Create `src/Tidsro/Services/DataTransferService.cs`:

```csharp
using System.IO;
using System.Text.Json;
using Tidsro.Models;

namespace Tidsro.Services;

/// <summary>File-level export and import: write a chosen file, read one back with validation, and
/// keep a single copy of the state an import is about to replace. No WPF here — the dialogs live
/// behind <see cref="IFileDialogService"/> and the decisions live in the view model.</summary>
public sealed class DataTransferService
{
    /// <summary>A file the user picked by accident — a log, a video — must not be read into memory.
    /// 8 MB is thousands of alarms; OutOfMemoryException is outside the caught set below and would
    /// reach the global handler as a crash.</summary>
    public const long MaxImportBytes = 8 * 1024 * 1024;

    // A JSON object deserializes into TidsroData whatever it contains, so a document carrying none
    // of these keys is not a Tidsro file however well-formed it is.
    private static readonly string[] KnownKeys =
        { "SchemaVersion", "Settings", "Alarms", "RecurringAlarms" };

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        // No polymorphic/$type handling. Default, non-polymorphic contracts only.
    };

    private readonly string _dataPath;
    public DataTransferService(string dataPath) => _dataPath = dataPath;

    /// <summary>Where the pre-import copy goes: one file beside the live data, overwritten each time.</summary>
    public string SnapshotPath =>
        Path.Combine(Path.GetDirectoryName(_dataPath)!, "data-before-import.json");

    /// <summary>Write the current state to a file the user chose. Throws on failure — the caller
    /// reports it, because an export that fails silently leaves the user believing they have a
    /// backup they do not have.</summary>
    public void Export(string path, TidsroData data) => PersistenceService.WriteTo(path, data);

    /// <summary>Read and validate a file the user chose. Returns null for anything that is not a
    /// usable Tidsro document; the caller shows the error and changes nothing.</summary>
    public TidsroData? Read(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxImportBytes) return null;

            var json = File.ReadAllText(path);
            if (!LooksLikeTidsroDocument(json)) return null;

            var data = JsonSerializer.Deserialize<TidsroData>(json, Options);
            return data?.Sanitized();
        }
        catch (Exception ex) when (ex is JsonException or IOException
                                     or UnauthorizedAccessException or OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>Copy the state an import is about to replace. Best effort: an import must never be
    /// blocked by a snapshot that could not be written.</summary>
    public void SnapshotBeforeImport()
    {
        try { if (File.Exists(_dataPath)) File.Copy(_dataPath, SnapshotPath, overwrite: true); }
        catch { /* the snapshot must never throw */ }
    }

    private static bool LooksLikeTidsroDocument(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            foreach (var key in KnownKeys)
                if (doc.RootElement.TryGetProperty(key, out _)) return true;
            return false;
        }
        catch (JsonException) { return false; }
    }
}
```

- [ ] **Step 4: Run the tests and watch them pass**

Run: `dotnet test Tidsro.slnx --filter "FullyQualifiedName~DataTransferServiceTests"`
Expected: PASS, 11 tests.

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/Services/DataTransferService.cs tests/Tidsro.Tests/DataTransferServiceTests.cs
git commit -m "feat(services): DataTransferService for export, validated import and a pre-import copy

The shape gate is the point: any JSON object deserializes into an
empty-but-valid TidsroData, so without it a mistyped file reads as a
legitimate empty backup and a confirmed import wipes the schedule."
```

---

### Task 3: `IFileDialogService` and its Win32 implementation

The same reason `IStartupService` exists: keep the real dialog out of view-model tests.

**Files:**
- Create: `src/Tidsro/Services/IFileDialogService.cs`
- Create: `src/Tidsro/Services/FileDialogService.cs`
- Create: `tests/Tidsro.Tests/FakeFileDialogService.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `IFileDialogService` with `string? AskSavePath(string suggestedFileName)` and `string? AskOpenPath()`; `FakeFileDialogService` with settable `SavePath`, `OpenPath`, and recorded `LastSuggestedName`. Task 6 uses the fake, Task 7 uses the real one.

- [ ] **Step 1: Write the interface**

Create `src/Tidsro/Services/IFileDialogService.cs`:

```csharp
namespace Tidsro.Services;

/// <summary>The Save/Open file dialogs behind an interface, so view-model tests never open a real
/// one. Both return null when the user cancels.</summary>
public interface IFileDialogService
{
    string? AskSavePath(string suggestedFileName);
    string? AskOpenPath();
}
```

- [ ] **Step 2: Write the implementation**

Create `src/Tidsro/Services/FileDialogService.cs`:

```csharp
using System.IO;
using Microsoft.Win32;

namespace Tidsro.Services;

public sealed class FileDialogService : IFileDialogService
{
    private const string Filter = "Tidsro backup (*.json)|*.json|All files (*.*)|*.*";

    private static string Documents =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    public string? AskSavePath(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            DefaultExt = ".json",
            Filter = Filter,
            InitialDirectory = Documents,
            OverwritePrompt = true,   // the Windows dialog asks; we do not double up
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? AskOpenPath()
    {
        var dialog = new OpenFileDialog
        {
            DefaultExt = ".json",
            Filter = Filter,
            InitialDirectory = Documents,
            CheckFileExists = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
```

- [ ] **Step 3: Write the fake**

Create `tests/Tidsro.Tests/FakeFileDialogService.cs`:

```csharp
using Tidsro.Services;

namespace Tidsro.Tests;

/// <summary>Canned answers for the two dialogs, plus what the view model suggested as a file name.</summary>
public sealed class FakeFileDialogService : IFileDialogService
{
    public string? SavePath { get; set; }
    public string? OpenPath { get; set; }
    public string? LastSuggestedName { get; private set; }

    public string? AskSavePath(string suggestedFileName)
    {
        LastSuggestedName = suggestedFileName;
        return SavePath;
    }

    public string? AskOpenPath() => OpenPath;
}
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build Tidsro.slnx`
Expected: build succeeded, no warnings introduced.

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/Services/IFileDialogService.cs src/Tidsro/Services/FileDialogService.cs tests/Tidsro.Tests/FakeFileDialogService.cs
git commit -m "feat(services): IFileDialogService so view-model tests never open a real dialog"
```

---

### Task 4: `ChoiceDialog` — the three-way import choice and the message box

`ConfirmDialog` is a two-button yes/no. Import needs three buttons, and both export and import need a single-OK message surface. One dialog covers both.

**Files:**
- Create: `src/Tidsro/Views/ChoiceDialog.xaml`
- Create: `src/Tidsro/Views/ChoiceDialog.xaml.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `enum ImportChoice { Cancel, AlarmsOnly, Everything }` in `Tidsro.Views`
  - `static ImportChoice ChoiceDialog.AskImport(Window owner, string message)`
  - `static void ChoiceDialog.ShowMessage(Window owner, string title, string message)`
  Tasks 6 and 7 use both.

- [ ] **Step 1: Write the XAML**

Create `src/Tidsro/Views/ChoiceDialog.xaml`. Cancel is `IsCancel` **and** `IsDefault` **and** focused — the same safety posture as `ConfirmDialog`, so Enter and Esc both mean cancel. Tab order runs left to right through the two restore buttons before reaching Cancel.

```xml
<Window x:Class="Tidsro.Views.ChoiceDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Width="380" SizeToContent="Height" WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize" ShowInTaskbar="False"
        FocusManager.FocusedElement="{Binding ElementName=CancelButton}"
        Background="{StaticResource PageBg}" Foreground="{StaticResource Text}"
        FontFamily="{StaticResource FontSans}">
  <StackPanel Margin="24">
    <TextBlock x:Name="MessageText" TextWrapping="Wrap"/>
    <StackPanel x:Name="ChoiceButtons" Margin="0,24,0,0">
      <Button x:Name="AlarmsOnlyButton" Content="Restore alarms only"
              Style="{StaticResource GoldAction}" TabIndex="0"
              HorizontalAlignment="Left" MinWidth="180" Margin="0,0,0,8"
              Click="AlarmsOnly_Click"
              KeyboardNavigation.AcceptsReturn="True"
              AutomationProperties.Name="Restore alarms only"/>
      <Button x:Name="EverythingButton" Content="Restore everything"
              Style="{StaticResource QuietAction}" TabIndex="1"
              HorizontalAlignment="Left" MinWidth="180"
              Click="Everything_Click"
              KeyboardNavigation.AcceptsReturn="True"
              AutomationProperties.Name="Restore everything, alarms and settings"/>
    </StackPanel>
    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,24,0,0">
      <Button x:Name="CancelButton" Content="Cancel" Style="{StaticResource QuietAction}"
              IsCancel="True" IsDefault="True" MinWidth="84" TabIndex="2"
              AutomationProperties.Name="Cancel"/>
    </StackPanel>
  </StackPanel>
</Window>
```

- [ ] **Step 2: Write the code-behind**

Create `src/Tidsro/Views/ChoiceDialog.xaml.cs`:

```csharp
using System.Windows;

namespace Tidsro.Views;

/// <summary>What an import should restore. Cancel is the default in every ambiguous case —
/// Esc, the title-bar X, and Enter all land here.</summary>
public enum ImportChoice { Cancel, AlarmsOnly, Everything }

// The three-way sibling of ConfirmDialog, doubling as the app's single-OK message box. Closing with
// the title-bar X leaves DialogResult null, which reads as Cancel.
public partial class ChoiceDialog : Window
{
    private ImportChoice _choice = ImportChoice.Cancel;

    private ChoiceDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;              // announced by screen readers when the modal opens
        MessageText.Text = message;
    }

    /// <summary>Ask what to restore. Returns Cancel unless the user picked a restore explicitly.</summary>
    public static ImportChoice AskImport(Window owner, string message)
    {
        var dialog = new ChoiceDialog("Import data", message) { Owner = owner };
        dialog.ShowDialog();
        return dialog._choice;
    }

    /// <summary>A single-OK message. Used for both export results and import failures — never a tray
    /// balloon, which is invisible on machines with notifications disabled.</summary>
    public static void ShowMessage(Window owner, string title, string message)
    {
        var dialog = new ChoiceDialog(title, message) { Owner = owner };
        dialog.ChoiceButtons.Visibility = Visibility.Collapsed;
        dialog.CancelButton.Content = "OK";
        dialog.CancelButton.SetValue(AutomationProperties.NameProperty, "OK");
        dialog.ShowDialog();
    }

    private void AlarmsOnly_Click(object sender, RoutedEventArgs e)
    {
        _choice = ImportChoice.AlarmsOnly;
        DialogResult = true;
    }

    private void Everything_Click(object sender, RoutedEventArgs e)
    {
        _choice = ImportChoice.Everything;
        DialogResult = true;
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build Tidsro.slnx`
Expected: build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/Tidsro/Views/ChoiceDialog.xaml src/Tidsro/Views/ChoiceDialog.xaml.cs
git commit -m "feat(views): ChoiceDialog for the three-way import choice and in-app messages

Cancel is IsCancel, IsDefault and focused, so Enter, Esc and the title-bar
X all mean cancel."
```

---

### Task 5: `MainViewModel.ReplaceAllAlarms`

Applying an import is `ClearAllAlarms` with an arming pass on the end, so it inherits the three invariants that slice established: walk the scheduler and not the derived view collections, disarm before emptying, and close open popups first.

**Files:**
- Modify: `src/Tidsro/ViewModels/MainViewModel.cs` (beside `ClearAllAlarms`, around line 359)
- Test: `tests/Tidsro.Tests/MainViewModelTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `public void ReplaceAllAlarms(IEnumerable<AlarmRecord> alarms, IEnumerable<RecurringAlarmRecord> recurring)` on `MainViewModel`. Task 7 calls it.

- [ ] **Step 1: Write the failing tests**

Add to `tests/Tidsro.Tests/MainViewModelTests.cs`. Match the construction style already used in that file for the scheduler and view model.

```csharp
[Fact]
public void ReplaceAllAlarms_clears_the_scheduler_before_arming_the_imported_set()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.Zero));
    var scheduler = new SchedulerService(clock);
    var vm = new MainViewModel(scheduler, new FakeSoundService(), SoundChoice.None);
    scheduler.ArmClockAlarm(clock.Now.AddHours(1), "old", SoundChoice.None);
    vm.RefreshAll();

    vm.ReplaceAllAlarms(
        new[] { new AlarmRecord { Id = Guid.NewGuid(), FireAt = new DateTime(2026, 8, 24, 18, 0, 0), Label = "new", Sound = SoundChoice.None, Enabled = true } },
        Array.Empty<RecurringAlarmRecord>());

    var armed = Assert.Single(scheduler.Alarms);
    Assert.Equal("new", armed.Label);   // the old alarm is gone from the scheduler, not just the view
}

[Fact]
public void ReplaceAllAlarms_closes_open_popups_before_arming()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.Zero));
    var scheduler = new SchedulerService(clock);
    var vm = new MainViewModel(scheduler, new FakeSoundService(), SoundChoice.None);
    var closedWhileEmpty = false;
    vm.ClosePopupsRequested += (_, _) => closedWhileEmpty = scheduler.Alarms.Count > 0;

    vm.ReplaceAllAlarms(
        new[] { new AlarmRecord { Id = Guid.NewGuid(), FireAt = new DateTime(2026, 8, 24, 18, 0, 0), Sound = SoundChoice.None, Enabled = true } },
        Array.Empty<RecurringAlarmRecord>());

    Assert.False(closedWhileEmpty);   // popups close before the imported set is armed
}

[Fact]
public void ReplaceAllAlarms_raises_AlarmsChanged_once_so_the_import_is_persisted()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.Zero));
    var scheduler = new SchedulerService(clock);
    var vm = new MainViewModel(scheduler, new FakeSoundService(), SoundChoice.None);
    var changes = 0;
    vm.AlarmsChanged += (_, _) => changes++;

    vm.ReplaceAllAlarms(
        new[] { new AlarmRecord { Id = Guid.NewGuid(), FireAt = new DateTime(2026, 8, 24, 18, 0, 0), Sound = SoundChoice.None, Enabled = true } },
        Array.Empty<RecurringAlarmRecord>());

    Assert.Equal(1, changes);
}

[Fact]
public void ReplaceAllAlarms_skips_a_record_that_cannot_be_armed_and_keeps_the_rest()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.Zero));
    var scheduler = new SchedulerService(clock);
    var vm = new MainViewModel(scheduler, new FakeSoundService(), SoundChoice.None);

    vm.ReplaceAllAlarms(Array.Empty<AlarmRecord>(), new[]
    {
        new RecurringAlarmRecord { Id = Guid.NewGuid(), Hour = 99, Minute = 0, Days = Weekdays.Monday,
                                   Sound = SoundChoice.None, NextFireAt = new DateTime(2026, 9, 1, 8, 0, 0) },
        new RecurringAlarmRecord { Id = Guid.NewGuid(), Hour = 8, Minute = 0, Days = Weekdays.Monday,
                                   Label = "good", Sound = SoundChoice.None, NextFireAt = new DateTime(2026, 9, 1, 8, 0, 0) },
    });

    var armed = Assert.Single(scheduler.Alarms);
    Assert.Equal("good", armed.Label);
}

[Fact]
public void ReplaceAllAlarms_clears_the_missed_note()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.Zero));
    var scheduler = new SchedulerService(clock);
    var vm = new MainViewModel(scheduler, new FakeSoundService(), SoundChoice.None);
    var missed = scheduler.ArmClockAlarm(clock.Now.AddMinutes(1), "missed", SoundChoice.None);
    vm.AddMissed(missed);

    vm.ReplaceAllAlarms(Array.Empty<AlarmRecord>(), Array.Empty<RecurringAlarmRecord>());

    Assert.Null(vm.MissedNote);
}
```

- [ ] **Step 2: Run them and watch them fail**

Run: `dotnet test Tidsro.slnx --filter "FullyQualifiedName~MainViewModelTests"`
Expected: FAIL — `MainViewModel` does not contain a definition for `ReplaceAllAlarms`.

- [ ] **Step 3: Implement it**

Add to `src/Tidsro/ViewModels/MainViewModel.cs`, directly below `ClearAllAlarms`:

```csharp
/// <summary>Replace the whole schedule with an imported one. This is ClearAllAlarms with an arming
/// pass on the end, and it keeps that method's three invariants: walk the scheduler rather than the
/// derived view collections (SaveData persists from the scheduler, so anything the agenda has not
/// caught up with would survive and be written straight back), disarm before emptying so nothing can
/// fire from the tick in between, and close open cards first — an open card's Snooze would re-arm
/// into the set we are about to discard.</summary>
public void ReplaceAllAlarms(IEnumerable<AlarmRecord> alarms, IEnumerable<RecurringAlarmRecord> recurring)
{
    CommitPendingDelete();
    ClosePopupsRequested?.Invoke(this, EventArgs.Empty);

    foreach (var item in _scheduler.Running.ToList()) _scheduler.Cancel(item);
    foreach (var item in _scheduler.Alarms.ToList()) _scheduler.Cancel(item);

    Running.Clear();
    Alarms.Clear();
    MissedNote = null;

    var armed = 0;
    foreach (var r in alarms)
    {
        // A residual bad record must never abort the import — the same posture as launch (spec §4).
        try
        {
            _scheduler.ArmClockAlarm(LocalToOffset(r.FireAt), r.Label, r.Sound, r.Id, r.WarnBefore, r.Enabled);
            armed++;
        }
        catch { /* skip it and keep going */ }
    }

    foreach (var r in recurring)
    {
        try
        {
            _scheduler.ArmRecurringAlarm(r.Hour, r.Minute, r.Days, r.Label, r.Sound, r.Id,
                LocalToOffset(r.NextFireAt), r.WarnBefore, r.Enabled);
            armed++;
        }
        catch { /* skip it and keep going */ }
    }

    RebuildAgenda();
    OnPropertyChanged(nameof(IsDayEmpty));
    AlarmsChanged?.Invoke(this, EventArgs.Empty);
    Announce(armed == 1 ? "Imported 1 alarm" : $"Imported {armed} alarms");
}

// A persisted alarm time is a wall-clock local time; tag it Local before lifting to DateTimeOffset
// so the scheduler compares against the right instant. Mirrors App's loader.
private static DateTimeOffset LocalToOffset(DateTime local) =>
    new(DateTime.SpecifyKind(local, DateTimeKind.Local));
```

- [ ] **Step 4: Run the tests and watch them pass**

Run: `dotnet test Tidsro.slnx --filter "FullyQualifiedName~MainViewModelTests"`
Expected: PASS, including the pre-existing tests in that class.

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/ViewModels/MainViewModel.cs tests/Tidsro.Tests/MainViewModelTests.cs
git commit -m "feat(main-vm): ReplaceAllAlarms for an imported schedule

Keeps the ClearAllAlarms invariants: walk the scheduler not the view,
disarm before emptying, close open cards first."
```

---

### Task 6: The two commands on `SettingsViewModel`

**Files:**
- Modify: `src/Tidsro/ViewModels/SettingsViewModel.cs`
- Test: `tests/Tidsro.Tests/SettingsViewModelTests.cs`

**Interfaces:**
- Consumes: `IFileDialogService` (Task 3), `DataTransferService` (Task 2), `ImportChoice` (Task 4), `TidsroData`.
- Produces:
  - `sealed record DataPorts(IFileDialogService Dialogs, DataTransferService Transfer, Func<TidsroData> BuildData, Func<string, ImportChoice> AskImportChoice, Action<TidsroData, bool> ApplyImport, Action<string, string> ShowMessage, Func<DateTime> Today)` in `Tidsro.ViewModels`
  - `ExportDataCommand` and `ImportDataCommand`
  Task 7 constructs the `DataPorts`.

**Why a record rather than seven more positional parameters:** the constructor already takes nine. `DataPorts` is added as one **optional trailing parameter** so the twelve existing tests in this file keep compiling unchanged; the two commands no-op without it, and `App` always supplies it.

- [ ] **Step 1: Write the failing tests**

Add to `tests/Tidsro.Tests/SettingsViewModelTests.cs`. The helper keeps the seven-field record out of every test body.

```csharp
private static (SettingsViewModel Vm, FakeFileDialogService Dialogs, List<(string Title, string Message)> Messages,
                List<(TidsroData Data, bool IncludeSettings)> Applied, DataTransferService Transfer, string Dir)
    MakeImportVm(AppSettings shared, IStartupService startup, ImportChoice choice, TidsroData? built = null)
{
    var dir = Path.Combine(Path.GetTempPath(), "tidsro-svm-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    var dialogs = new FakeFileDialogService();
    var messages = new List<(string, string)>();
    var applied = new List<(TidsroData, bool)>();
    var transfer = new DataTransferService(Path.Combine(dir, "data.json"));
    var ports = new DataPorts(
        Dialogs: dialogs,
        Transfer: transfer,
        BuildData: () => built ?? TidsroData.Defaults(),
        AskImportChoice: _ => choice,
        ApplyImport: (d, includeSettings) => applied.Add((d, includeSettings)),
        ShowMessage: (t, m) => messages.Add((t, m)),
        Today: () => new DateTime(2026, 8, 24));

    var vm = new SettingsViewModel(shared, startup, save: () => { }, _ => { },
        clearAllAlarms: () => { }, alarmCount: () => 0, hasAnythingToClear: () => true,
        resetWindowPlacement: () => { }, confirm: (_, _) => true, dataPorts: ports);

    return (vm, dialogs, messages, applied, transfer, dir);
}

[Fact]
public void Export_with_a_cancelled_dialog_writes_nothing()
{
    var (vm, dialogs, messages, _, _, dir) = MakeImportVm(new AppSettings(), new FakeStartupService(), ImportChoice.Cancel);
    try
    {
        dialogs.SavePath = null;   // user cancelled

        vm.ExportDataCommand.Execute(null);

        Assert.Empty(messages);    // no success message, no error
        Assert.Empty(Directory.GetFiles(dir, "*.json"));
    }
    finally { Directory.Delete(dir, recursive: true); }
}

[Fact]
public void Export_suggests_a_dated_file_name_and_reports_success()
{
    var (vm, dialogs, messages, _, _, dir) = MakeImportVm(new AppSettings(), new FakeStartupService(), ImportChoice.Cancel);
    try
    {
        dialogs.SavePath = Path.Combine(dir, "backup.json");

        vm.ExportDataCommand.Execute(null);

        Assert.Equal("tidsro-backup-2026-08-24.json", dialogs.LastSuggestedName);
        Assert.True(File.Exists(dialogs.SavePath));
        var (title, message) = Assert.Single(messages);
        Assert.Equal("Exported", title);
        Assert.Contains("backup.json", message);
    }
    finally { Directory.Delete(dir, recursive: true); }
}

[Fact]
public void Export_reports_a_failure_instead_of_failing_silently()
{
    var (vm, dialogs, messages, _, _, dir) = MakeImportVm(new AppSettings(), new FakeStartupService(), ImportChoice.Cancel);
    try
    {
        var target = Path.Combine(dir, "taken.json");
        Directory.CreateDirectory(target);   // a directory in the file's place: the write throws
        dialogs.SavePath = target;

        vm.ExportDataCommand.Execute(null);

        var (title, _) = Assert.Single(messages);
        Assert.Equal("Export failed", title);
    }
    finally { Directory.Delete(dir, recursive: true); }
}

[Fact]
public void Import_with_a_cancelled_open_dialog_changes_nothing()
{
    var (vm, dialogs, messages, applied, _, dir) = MakeImportVm(new AppSettings(), new FakeStartupService(), ImportChoice.AlarmsOnly);
    try
    {
        dialogs.OpenPath = null;

        vm.ImportDataCommand.Execute(null);

        Assert.Empty(applied);
        Assert.Empty(messages);
    }
    finally { Directory.Delete(dir, recursive: true); }
}

[Fact]
public void Import_of_a_valid_json_file_that_is_not_a_Tidsro_document_is_refused()
{
    var (vm, dialogs, messages, applied, transfer, dir) = MakeImportVm(new AppSettings(), new FakeStartupService(), ImportChoice.Everything);
    try
    {
        var path = Path.Combine(dir, "package.json");
        File.WriteAllText(path, """{"name":"something"}""");
        dialogs.OpenPath = path;

        vm.ImportDataCommand.Execute(null);

        Assert.Empty(applied);                              // the data-loss guard
        Assert.False(File.Exists(transfer.SnapshotPath));   // and no snapshot was taken
        var (title, _) = Assert.Single(messages);
        Assert.Equal("Import failed", title);
    }
    finally { Directory.Delete(dir, recursive: true); }
}

[Fact]
public void Import_cancelled_at_the_choice_dialog_takes_no_snapshot_and_changes_nothing()
{
    var (vm, dialogs, messages, applied, transfer, dir) = MakeImportVm(new AppSettings(), new FakeStartupService(), ImportChoice.Cancel);
    try
    {
        var path = Path.Combine(dir, "good.json");
        File.WriteAllText(path, """{"SchemaVersion":4,"Settings":{},"Alarms":[],"RecurringAlarms":[]}""");
        dialogs.OpenPath = path;

        vm.ImportDataCommand.Execute(null);

        Assert.Empty(applied);
        Assert.Empty(messages);
        Assert.False(File.Exists(transfer.SnapshotPath));   // step 4 comes after step 3
    }
    finally { Directory.Delete(dir, recursive: true); }
}

[Fact]
public void Import_alarms_only_applies_without_settings_and_snapshots_first()
{
    var (vm, dialogs, _, applied, transfer, dir) = MakeImportVm(new AppSettings(), new FakeStartupService(), ImportChoice.AlarmsOnly);
    try
    {
        File.WriteAllText(Path.Combine(dir, "data.json"), """{"SchemaVersion":4,"Alarms":[]}""");
        var path = Path.Combine(dir, "good.json");
        File.WriteAllText(path, """{"SchemaVersion":4,"Settings":{},"Alarms":[],"RecurringAlarms":[]}""");
        dialogs.OpenPath = path;

        vm.ImportDataCommand.Execute(null);

        var (_, includeSettings) = Assert.Single(applied);
        Assert.False(includeSettings);
        Assert.True(File.Exists(transfer.SnapshotPath));
    }
    finally { Directory.Delete(dir, recursive: true); }
}

[Fact]
public void Import_everything_applies_with_settings()
{
    var (vm, dialogs, _, applied, _, dir) = MakeImportVm(new AppSettings(), new FakeStartupService(), ImportChoice.Everything);
    try
    {
        var path = Path.Combine(dir, "good.json");
        File.WriteAllText(path, """{"SchemaVersion":4,"Settings":{},"Alarms":[],"RecurringAlarms":[]}""");
        dialogs.OpenPath = path;

        vm.ImportDataCommand.Execute(null);

        var (_, includeSettings) = Assert.Single(applied);
        Assert.True(includeSettings);
    }
    finally { Directory.Delete(dir, recursive: true); }
}

[Fact]
public void Import_names_the_counts_and_the_recovery_file_in_the_question()
{
    var dir = Path.Combine(Path.GetTempPath(), "tidsro-svm-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    try
    {
        var dialogs = new FakeFileDialogService();
        string? asked = null;
        var ports = new DataPorts(dialogs, new DataTransferService(Path.Combine(dir, "data.json")),
            () => TidsroData.Defaults(), m => { asked = m; return ImportChoice.Cancel; },
            (_, _) => { }, (_, _) => { }, () => new DateTime(2026, 8, 24));
        var vm = new SettingsViewModel(new AppSettings(), new FakeStartupService(), () => { }, _ => { },
            () => { }, () => 0, () => true, () => { }, (_, _) => true, ports);

        var path = Path.Combine(dir, "good.json");
        File.WriteAllText(path, """
            {"SchemaVersion":4,"Settings":{},"Alarms":[],
             "RecurringAlarms":[{"Id":"11111111-1111-1111-1111-111111111111","Hour":8,"Minute":0,
                                 "Days":1,"Sound":0,"NextFireAt":"2026-09-01T08:00:00"}]}
            """);
        dialogs.OpenPath = path;

        vm.ImportDataCommand.Execute(null);

        Assert.Contains("1 recurring alarm", asked);
        Assert.Contains("data-before-import.json", asked);
    }
    finally { Directory.Delete(dir, recursive: true); }
}

[Fact]
public void The_data_commands_no_op_when_no_ports_were_supplied()
{
    var vm = new SettingsViewModel(new AppSettings(), new FakeStartupService(), () => { }, _ => { },
        () => { }, () => 0, () => true, () => { }, (_, _) => true);

    vm.ExportDataCommand.Execute(null);   // must not throw
    vm.ImportDataCommand.Execute(null);
}
```

Add `using System.IO;` and `using Tidsro.Views;` to the top of the test file.

- [ ] **Step 2: Run them and watch them fail**

Run: `dotnet test Tidsro.slnx --filter "FullyQualifiedName~SettingsViewModelTests"`
Expected: FAIL — `DataPorts` does not exist.

- [ ] **Step 3: Implement the record and the commands**

Add to `src/Tidsro/ViewModels/SettingsViewModel.cs`, above the class, with `using System.IO;`, `using Tidsro.Services;` and `using Tidsro.Views;`:

```csharp
/// <summary>Everything the two data-transfer commands need, bundled so the constructor does not grow
/// a seventh, eighth and ninth positional callback. App supplies it; the tests supply fakes.</summary>
/// <param name="BuildData">The live state to export — not a copy of data.json, so an export still
/// captures good data when saves have been failing.</param>
/// <param name="ApplyImport">(document, includeSettings) — replaces the schedule, and the settings too
/// when the user chose a full restore.</param>
public sealed record DataPorts(
    IFileDialogService Dialogs,
    DataTransferService Transfer,
    Func<TidsroData> BuildData,
    Func<string, ImportChoice> AskImportChoice,
    Action<TidsroData, bool> ApplyImport,
    Action<string, string> ShowMessage,
    Func<DateTime> Today);
```

Add the field and constructor parameter:

```csharp
    private readonly DataPorts? _data;   // null only in the older view-model tests; App always supplies it
```

```csharp
    public SettingsViewModel(AppSettings settings, IStartupService startup,
        Action save, Action<SoundChoice> onDefaultSoundChanged,
        Action clearAllAlarms, Func<int> alarmCount, Func<bool> hasAnythingToClear,
        Action resetWindowPlacement, Func<string, string, bool> confirm,
        DataPorts? dataPorts = null)
    {
        ...
        _data = dataPorts;
```

Add the two commands beside `ClearAlarms` and `ResetSettings`:

```csharp
    [RelayCommand]
    private void ExportData()
    {
        if (_data is null) return;

        var path = _data.Dialogs.AskSavePath($"tidsro-backup-{_data.Today():yyyy-MM-dd}.json");
        if (path is null) return;                    // cancelled — nothing happens

        var data = _data.BuildData();
        try
        {
            _data.Transfer.Export(path, data);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Never silent: a failed export leaves the user believing they have a backup they do not.
            _data.ShowMessage("Export failed", "Tidsro couldn't write that file. "
                                             + "Try another folder, and check the drive is still connected.");
            return;
        }

        var count = data.Alarms.Count + data.RecurringAlarms.Count;
        _data.ShowMessage("Exported", $"Exported {Plural(count)} to {Path.GetFileName(path)}.");
    }

    [RelayCommand]
    private void ImportData()
    {
        if (_data is null) return;

        var path = _data.Dialogs.AskOpenPath();
        if (path is null) return;                    // cancelled

        var imported = _data.Transfer.Read(path);    // size, shape and sanitise gates
        if (imported is null)
        {
            _data.ShowMessage("Import failed", "That doesn't look like a Tidsro backup. "
                                             + "Pick a file Tidsro exported, or your data.json.");
            return;
        }

        var choice = _data.AskImportChoice(
            $"This file holds {Plural(imported.Alarms.Count)} and "
          + $"{imported.RecurringAlarms.Count} recurring {(imported.RecurringAlarms.Count == 1 ? "alarm" : "alarms")}. "
          + "Your current data is copied to data-before-import.json first.");
        if (choice == ImportChoice.Cancel) return;   // no snapshot: nothing is being replaced

        _data.Transfer.SnapshotBeforeImport();
        _data.ApplyImport(imported, choice == ImportChoice.Everything);

        if (choice == ImportChoice.Everything)
        {
            // Refresh the draft, or a following Save writes the pre-import values straight back.
            LaunchAtStartup = _settings.LaunchAtStartup;
            DefaultSound = _settings.DefaultSound;
        }
    }

    private static string Plural(int count) => count == 1 ? "1 alarm" : $"{count} alarms";
```

- [ ] **Step 4: Run the tests and watch them pass**

Run: `dotnet test Tidsro.slnx --filter "FullyQualifiedName~SettingsViewModelTests"`
Expected: PASS, new tests and the twelve pre-existing ones.

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/ViewModels/SettingsViewModel.cs tests/Tidsro.Tests/SettingsViewModelTests.cs
git commit -m "feat(settings-vm): export and import commands

Import order matters: validate, ask, snapshot, apply. Cancelling at the
question takes no snapshot, because nothing is being replaced."
```

---

### Task 7: Wire it up in `App` and restore placement to the live window

**Files:**
- Modify: `src/Tidsro/App.xaml.cs` (`SaveData` around line 293, `ShowMainWindow` around line 258)
- Modify: `src/Tidsro/Views/MainWindow.xaml.cs` (beside `ResetPlacement`, line 74)

**Interfaces:**
- Consumes: `DataPorts` (Task 6), `DataTransferService` (Task 2), `FileDialogService` (Task 3), `ChoiceDialog` (Task 4), `MainViewModel.ReplaceAllAlarms` (Task 5).
- Produces: `public void ApplyPlacement(AppSettings settings)` on `MainWindow`.

- [ ] **Step 1: Extract `BuildData` from `SaveData`**

In `src/Tidsro/App.xaml.cs`, replace the body of `SaveData` so both it and export use one builder:

```csharp
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

    private void SaveData()
    {
        try { _persistence.Save(BuildData()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (_log.Log(ex, "SaveData"))
                _tray?.ShowNotification("Tidsro", "Tidsro couldn't save your changes. See Tray ▸ Open log folder.");
        }
    }
```

- [ ] **Step 2: Add `ApplyPlacement` to `MainWindow`**

In `src/Tidsro/Views/MainWindow.xaml.cs`, below `ResetPlacement`:

```csharp
    /// <summary>Move an already-visible window to imported coordinates. Writing the settings alone is
    /// not enough: OnClosing writes the current placement back on every close, so the restore would
    /// silently revert. Off-screen coordinates fall back to centring — the same guard the launch path
    /// applies, for the same reason (an unplugged monitor or a lower resolution).</summary>
    public void ApplyPlacement(AppSettings settings)
    {
        if (settings.WindowWidth is double w) Width = w;
        if (settings.WindowHeight is double h) Height = h;
        if (settings.WindowLeft is double left && settings.WindowTop is double top && IsOnScreen(left, top))
        {
            Left = left;
            Top = top;
        }
        else
        {
            ResetPlacement();
        }
    }
```

- [ ] **Step 3: Build the `DataPorts` in `ShowMainWindow`**

In `src/Tidsro/App.xaml.cs`, replace the `SettingsWindow` factory:

```csharp
        _main ??= new MainWindow(_mainVm, () => new SettingsWindow(confirm =>
                new SettingsViewModel(_settings, new StartupService(StartupService.CurrentExePath),
                    SaveData, _mainVm.SetDefaultSound,
                    clearAllAlarms: _mainVm.ClearAllAlarms,
                    alarmCount: () => _scheduler.Alarms.Count + _scheduler.Running.Count,
                    hasAnythingToClear: () => _mainVm.HasAnythingToClear,
                    resetWindowPlacement: () => { _main?.ResetPlacement(); _mainVm.SelectedTabIndex = 0; },
                    confirm: confirm,
                    dataPorts: BuildDataPorts())),
            editFactory, _settings, SaveData);
```

and add the builder plus the apply step:

```csharp
    // The owner for the data dialogs is the Settings window when it is up, so they centre on it and
    // the modal chain stays intact.
    private Window DataDialogOwner =>
        Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault() ?? (Window)_main!;

    private DataPorts BuildDataPorts() => new(
        Dialogs: new FileDialogService(),
        Transfer: new DataTransferService(PersistenceService.DefaultPath),
        BuildData: BuildData,
        AskImportChoice: message => ChoiceDialog.AskImport(DataDialogOwner, message),
        ApplyImport: ApplyImport,
        ShowMessage: (title, message) => ChoiceDialog.ShowMessage(DataDialogOwner, title, message),
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
        _main?.ApplyPlacement(imported);   // live window, or OnClosing overwrites the restore
        SaveData();
    }
```

- [ ] **Step 4: Build and run the whole suite**

Run: `dotnet build Tidsro.slnx` then `dotnet test Tidsro.slnx`
Expected: build succeeded; all tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Tidsro/App.xaml.cs src/Tidsro/Views/MainWindow.xaml.cs
git commit -m "feat(app): wire export and import, restoring placement to the live window

A full restore applies launch-at-startup through StartupService and moves
the visible window: writing _settings alone would be undone by OnClosing."
```

---

### Task 8: The two buttons in the Settings "Data" section

**Files:**
- Modify: `src/Tidsro/Views/SettingsWindow.xaml:20-32`

- [ ] **Step 1: Add the buttons**

The two recoverable actions read before the two destructive ones. Insert directly after the "These take effect immediately" caption:

```xml
    <Button Content="Export data…" Style="{StaticResource QuietAction}"
            HorizontalAlignment="Left" MinWidth="150" Margin="0,0,0,8"
            Command="{Binding ExportDataCommand}"
            AutomationProperties.Name="Export data to a file"/>
    <Button Content="Import data…" Style="{StaticResource QuietAction}"
            HorizontalAlignment="Left" MinWidth="150" Margin="0,0,0,8"
            Command="{Binding ImportDataCommand}"
            AutomationProperties.Name="Import data from a file"/>
```

- [ ] **Step 2: Build and launch the app to see them**

Run: `Get-Process Tidsro -ErrorAction SilentlyContinue | Stop-Process -Force; dotnet build Tidsro.slnx`
Then: `Start-Process src\Tidsro\bin\Debug\net10.0-windows\Tidsro.exe`
Expected: Settings ▸ Data shows four buttons in the order Export, Import, Clear all alarms, Reset all settings.

- [ ] **Step 3: Commit**

```bash
git add src/Tidsro/Views/SettingsWindow.xaml
git commit -m "feat(settings): Export and Import buttons in the Data section"
```

---

### Task 9: README and CHANGELOG

**Files:**
- Modify: `README.md:79-81` (Roadmap) and the feature list
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Update the README**

Remove the Roadmap entry — cloud sync is not coming, and backup now exists:

```markdown
## Roadmap

- Weekly timetable view
```

Add to the feature list:

```markdown
- **Backup and restore** — export your alarms and settings to a JSON file, and import one back.
  Import asks whether to restore the alarms alone or the settings too, and copies your current data
  to `%AppData%\Tidsro\data-before-import.json` first, so a mistaken import can be undone by
  importing that file.
```

And a note under it:

```markdown
An export is an ordinary, unencrypted JSON file — your alarm labels are readable by anything on the
machine. Note that Documents is redirected into OneDrive on many Windows installs, so saving there
uploads a copy; pick a local folder if you would rather it stayed on the machine.
```

- [ ] **Step 2: Update the CHANGELOG**

Add above the newest released section:

```markdown
## [Unreleased]

### Added

- Export data… and Import data… in Settings ▸ Data. An export is the complete file — alarms and
  settings — and an import asks whether to restore the alarms alone or everything.
- A copy of the pre-import state at `%AppData%\Tidsro\data-before-import.json`, so a mistaken import
  can be undone.
```

- [ ] **Step 3: Commit**

```bash
git add README.md CHANGELOG.md
git commit -m "docs: backup and restore, and drop cloud sync from the roadmap"
```

---

## Manual pass (Malin only — after Task 9)

Back up `%AppData%\Tidsro\data.json` and the `HKCU\...\Run\Tidsro` value first, and close Tidsro
**gracefully** rather than force-killing it — a force-kill discards unsaved in-memory edits.

- [ ] Export to Documents; confirm the success dialog names the file, and open the file.
- [ ] Import it back with **Restore alarms only**; settings untouched.
- [ ] Import with **Restore everything**; check launch-at-startup, default sound, tab and window
      position all follow, then close and reopen the window and confirm the placement stuck.
- [ ] Import a deliberately corrupted file, and a valid non-Tidsro JSON file — both refused, nothing
      changes.
- [ ] Import while an alarm fires, to see the popup-over-modal behaviour.
- [ ] Keyboard-only pass over the import dialog: Tab order, Esc, focus return to the Import button.
- [ ] UIA name check on all four Data buttons.
