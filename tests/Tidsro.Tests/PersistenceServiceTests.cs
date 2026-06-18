using System.IO;
using Tidsro.Models;
using Tidsro.Services;
using Xunit;

namespace Tidsro.Tests;

public class PersistenceServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;
    public PersistenceServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "TidsroTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "data.json");
    }
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private static AlarmRecord Alarm(Guid id, int hour) => new()
    {
        Id = id,
        FireAt = new DateTime(2026, 6, 17, hour, 0, 0, DateTimeKind.Local),
        Label = "Lunch",
        Sound = SoundChoice.Bell,
    };

    [Fact]
    public void Load_missing_file_returns_defaults()
    {
        var data = new PersistenceService(_path).Load();
        Assert.False(data.Settings!.LaunchAtStartup);
        Assert.Equal(SoundChoice.None, data.Settings!.DefaultSound);
        Assert.Empty(data.Alarms);
    }

    [Fact]
    public void Save_then_Load_round_trips_settings_and_alarms()
    {
        var svc = new PersistenceService(_path);
        var id = Guid.NewGuid();
        svc.Save(new TidsroData
        {
            Settings = new AppSettings { LaunchAtStartup = true, DefaultSound = SoundChoice.Bell },
            Alarms = { Alarm(id, 14) },
        });

        var data = svc.Load();
        Assert.True(data.Settings!.LaunchAtStartup);
        Assert.Equal(SoundChoice.Bell, data.Settings!.DefaultSound);
        var a = Assert.Single(data.Alarms);
        Assert.Equal(id, a.Id);
        Assert.Equal(14, a.FireAt.Hour);
        Assert.Equal(SoundChoice.Bell, a.Sound);
    }

    [Fact]
    public void Load_a_v1_0_bare_settings_file_preserves_settings_and_has_no_alarms()
    {
        // v1.0 wrote a bare AppSettings (no "Settings"/"Alarms" wrapper).
        File.WriteAllText(_path,
            "{\"SchemaVersion\":1,\"LaunchAtStartup\":true,\"DefaultSound\":3,\"WindowLeft\":120.0,\"WindowTop\":40.0}");
        var data = new PersistenceService(_path).Load();

        Assert.True(data.Settings!.LaunchAtStartup);
        Assert.Equal(SoundChoice.Bell, data.Settings!.DefaultSound);   // enum 3 == Bell
        Assert.Equal(120.0, data.Settings!.WindowLeft);
        Assert.Empty(data.Alarms);
    }

    [Fact]
    public void Save_is_atomic_and_leaves_no_temp_file()
    {
        var svc = new PersistenceService(_path);
        svc.Save(TidsroData.Defaults());
        Assert.True(File.Exists(_path));
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public void Load_corrupt_file_quarantines_and_returns_defaults()
    {
        File.WriteAllText(_path, "{ this is not valid json ");
        var data = new PersistenceService(_path).Load();
        Assert.Equal(SoundChoice.None, data.Settings!.DefaultSound);   // defaults
        Assert.True(File.Exists(_path + ".corrupt"));                  // quarantined, app still launches
    }

    [Fact]
    public void Load_drops_a_bad_alarm_but_keeps_the_good_one()
    {
        var goodId = Guid.NewGuid();
        var json =
            "{\"SchemaVersion\":2,\"Settings\":{\"DefaultSound\":0}," +
            "\"Alarms\":[" +
            "{\"Id\":\"" + goodId + "\",\"FireAt\":\"2026-06-17T14:00:00\",\"Label\":\"ok\",\"Sound\":3}," +
            "{\"Id\":\"" + Guid.NewGuid() + "\",\"FireAt\":\"2026-06-17T15:00:00\",\"Label\":\"bad\",\"Sound\":999}" +
            "]}";
        File.WriteAllText(_path, json);

        var data = new PersistenceService(_path).Load();
        var a = Assert.Single(data.Alarms);
        Assert.Equal("ok", a.Label);
    }

    [Fact]
    public void A_good_save_clears_a_stale_corrupt_file()
    {
        File.WriteAllText(_path, "{ broken ");
        var svc = new PersistenceService(_path);
        svc.Load();                                       // quarantines -> .corrupt exists
        Assert.True(File.Exists(_path + ".corrupt"));

        svc.Save(TidsroData.Defaults());                  // a good save no longer needs the recovery copy
        Assert.False(File.Exists(_path + ".corrupt"));    // labels in the quarantine file don't linger (spec §8)
    }

    [Fact]
    public void Save_then_Load_round_trips_window_position()
    {
        var svc = new PersistenceService(_path);
        svc.Save(new TidsroData { Settings = new AppSettings { WindowLeft = 123.5, WindowTop = 45.0 } });
        var data = svc.Load();
        Assert.Equal(123.5, data.Settings!.WindowLeft);
        Assert.Equal(45.0, data.Settings!.WindowTop);
    }

    [Fact]
    public void Save_then_Load_round_trips_a_recurring_alarm()
    {
        var svc = new PersistenceService(_path);
        var id = Guid.NewGuid();
        svc.Save(new TidsroData
        {
            Settings = new AppSettings(),
            RecurringAlarms =
            {
                new RecurringAlarmRecord
                {
                    Id = id, Hour = 7, Minute = 0,
                    Days = Weekdays.Mon | Weekdays.Wed | Weekdays.Fri,
                    Label = "Stand-up", Sound = SoundChoice.Bell,
                    NextFireAt = new DateTime(2026, 6, 19, 7, 0, 0, DateTimeKind.Local),
                },
            },
        });

        var data = svc.Load();
        var r = Assert.Single(data.RecurringAlarms);
        Assert.Equal(id, r.Id);
        Assert.Equal(7, r.Hour);
        Assert.Equal(Weekdays.Mon | Weekdays.Wed | Weekdays.Fri, r.Days);
        Assert.Equal(SoundChoice.Bell, r.Sound);
    }

    [Fact]
    public void Load_a_v2_file_with_no_recurring_list_yields_an_empty_recurring_list()
    {
        File.WriteAllText(_path,
            "{\"SchemaVersion\":2,\"Settings\":{\"DefaultSound\":0},\"Alarms\":[]}");
        var data = new PersistenceService(_path).Load();
        Assert.Empty(data.RecurringAlarms);   // missing key -> empty, settings preserved
        Assert.NotNull(data.Settings);
    }

    [Fact]
    public void Save_then_Load_round_trips_the_warn_before_flag()
    {
        var svc = new PersistenceService(_path);
        svc.Save(new TidsroData
        {
            Settings = new AppSettings(),
            Alarms = { new AlarmRecord { Id = Guid.NewGuid(), FireAt = new DateTime(2026, 6, 17, 14, 0, 0, DateTimeKind.Local), Label = "Lunch", Sound = SoundChoice.Bell, WarnBefore = true } },
            RecurringAlarms = { new RecurringAlarmRecord { Id = Guid.NewGuid(), Hour = 7, Minute = 0, Days = Weekdays.Mon, Label = "Stand-up", Sound = SoundChoice.Bell, NextFireAt = new DateTime(2026, 6, 19, 7, 0, 0, DateTimeKind.Local), WarnBefore = true } },
        });

        var data = svc.Load();
        Assert.True(Assert.Single(data.Alarms).WarnBefore);
        Assert.True(Assert.Single(data.RecurringAlarms).WarnBefore);
    }

    [Fact]
    public void Load_an_alarm_saved_without_warn_before_defaults_it_to_false()
    {
        File.WriteAllText(_path,
            "{\"SchemaVersion\":3,\"Settings\":{\"DefaultSound\":0},\"Alarms\":[" +
            "{\"Id\":\"" + Guid.NewGuid() + "\",\"FireAt\":\"2026-06-17T14:00:00\",\"Label\":\"ok\",\"Sound\":3}]}");
        var data = new PersistenceService(_path).Load();
        Assert.False(Assert.Single(data.Alarms).WarnBefore);   // missing key -> false (back-compat)
    }
}
