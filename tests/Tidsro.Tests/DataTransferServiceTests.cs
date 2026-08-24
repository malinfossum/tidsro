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
        _dir = Path.Combine(Path.GetTempPath(), "TidsroTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _dataPath = Path.Combine(_dir, "data.json");
        _svc = new DataTransferService(_dataPath);
    }

    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

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
            Id = Guid.NewGuid(),
            Hour = 8,
            Minute = 30,
            Days = Weekdays.Mon,
            Label = "Class",
            Sound = SoundChoice.Bell,
            NextFireAt = new DateTime(2026, 9, 1, 8, 30, 0),
            Enabled = true,
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
        Assert.Empty(read!.RecurringAlarms);         // hour 99 is dropped
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
