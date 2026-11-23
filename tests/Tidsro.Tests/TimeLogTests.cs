using System.IO;
using Tidsro.Models;
using Tidsro.Services;
using Tidsro.ViewModels;

namespace Tidsro.Tests;

public class TimeLogTests
{
    private static MainViewModel New(out FakeClock clock, out SchedulerService sched)
    {
        clock = new FakeClock();
        sched = new SchedulerService(clock);
        return new MainViewModel(sched, new FakeSoundService(), SoundChoice.None, hasCustomSound: () => false);
    }

    [Fact]
    public void Pausing_a_stopwatch_logs_the_run()
    {
        var vm = New(out var clock, out _);
        var saves = 0;
        vm.TimeLogChanged += (_, _) => saves++;
        vm.StartStopwatchWithLabelCommand.Execute("Deep work");
        var start = clock.Now;
        clock.Advance(TimeSpan.FromMinutes(25));
        vm.Stopwatches[0].StartPause();

        var e = Assert.Single(vm.TimeLogRecords());
        Assert.Equal(TimeLogEntry.StopwatchKind, e.Kind);
        Assert.Equal("Deep work", e.Label);
        Assert.Equal(start, e.StartedAt);
        Assert.Equal(TimeSpan.FromMinutes(25), e.Duration);
        Assert.Equal(1, saves);
        Assert.True(vm.HasTimeLog);
        Assert.Equal("0:25:00", Assert.Single(vm.RecentLog).DurationText);
    }

    [Fact]
    public void Reset_while_running_logs_the_run_and_keeps_counting()
    {
        var vm = New(out var clock, out _);
        vm.StartStopwatchWithLabelCommand.Execute("A");
        clock.Advance(TimeSpan.FromMinutes(3));
        vm.Stopwatches[0].Reset();
        Assert.Single(vm.TimeLogRecords());
        Assert.True(vm.Stopwatches[0].IsRunning);
    }

    [Fact]
    public void Removing_a_running_stopwatch_logs_only_once_the_removal_commits()
    {
        var vm = New(out var clock, out _);
        vm.StartStopwatchWithLabelCommand.Execute("A");
        clock.Advance(TimeSpan.FromMinutes(2));
        vm.RemoveStopwatchCommand.Execute(vm.Stopwatches[0]);
        Assert.Empty(vm.TimeLogRecords());            // still undoable

        vm.UndoDeleteCommand.Execute(null);
        Assert.Empty(vm.TimeLogRecords());
        Assert.True(vm.Stopwatches[0].IsRunning);

        vm.RemoveStopwatchCommand.Execute(vm.Stopwatches[0]);
        clock.Advance(TimeSpan.FromSeconds(10));
        vm.CommitPendingDelete();
        Assert.Equal(TimeSpan.FromSeconds(130), Assert.Single(vm.TimeLogRecords()).Duration);
    }

    [Fact]
    public void A_sub_second_run_is_not_logged()
    {
        var vm = New(out var clock, out _);
        vm.StartStopwatchWithLabelCommand.Execute("A");
        clock.Advance(TimeSpan.FromMilliseconds(300));
        vm.Stopwatches[0].StartPause();
        Assert.Empty(vm.TimeLogRecords());
    }

    [Fact]
    public void A_finished_countdown_is_logged_and_an_alarm_is_not()
    {
        var vm = New(out _, out var sched);
        vm.Label = "Pasta";
        vm.StartPresetCommand.Execute(5);
        vm.LogTimerCompleted(vm.Running[0].Item);
        var alarm = sched.ArmClockAlarm(sched.Now.AddHours(1), "Alarm", SoundChoice.None);
        vm.LogTimerCompleted(alarm);

        var e = Assert.Single(vm.TimeLogRecords());
        Assert.Equal(TimeLogEntry.TimerKind, e.Kind);
        Assert.Equal("Pasta", e.Label);
        Assert.Equal(TimeSpan.FromMinutes(5), e.Duration);
    }

    [Fact]
    public void Export_writes_a_csv_through_the_dialog()
    {
        var vm = New(out var clock, out _);
        var dialogs = new FakeFileDialogService();
        var path = Path.Combine(Path.GetTempPath(), $"tidsro-log-{Guid.NewGuid():N}.csv");
        dialogs.CsvPath = path;
        vm.FileDialogs = dialogs;
        vm.StartStopwatchWithLabelCommand.Execute("Write, \"report\"");
        clock.Advance(TimeSpan.FromSeconds(90));
        vm.Stopwatches[0].StartPause();

        try
        {
            vm.ExportTimeLogCommand.Execute(null);
            Assert.StartsWith("tidsro-time-log-", dialogs.LastSuggestedName);
            var lines = File.ReadAllLines(path);
            Assert.Equal(TimeLogCsv.Header, lines[0].TrimStart('﻿'));
            Assert.Contains("\"Write, \"\"report\"\"\"", lines[1]);
            Assert.EndsWith(",0:01:30,90.0", lines[1]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Csv_neutralises_formula_like_labels()
    {
        var csv = TimeLogCsv.Build(new[]
        {
            new TimeLogEntry { Label = "=HYPERLINK(1)", StartedAt = DateTimeOffset.Now, EndedAt = DateTimeOffset.Now, DurationMs = 1000 },
        });
        Assert.Contains(",'=HYPERLINK(1),", csv);
    }

    [Fact]
    public void Clear_asks_first_and_respects_no()
    {
        var vm = New(out var clock, out _);
        vm.StartStopwatchWithLabelCommand.Execute("A");
        clock.Advance(TimeSpan.FromMinutes(1));
        vm.Stopwatches[0].StartPause();

        vm.Confirm = (_, _) => false;
        vm.ClearTimeLogCommand.Execute(null);
        Assert.Single(vm.TimeLogRecords());

        vm.Confirm = (_, _) => true;
        vm.ClearTimeLogCommand.Execute(null);
        Assert.Empty(vm.TimeLogRecords());
        Assert.False(vm.HasTimeLog);
    }

    [Fact]
    public void Sanitise_drops_bad_entries_and_caps_the_log()
    {
        var t = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var entries = Enumerable.Range(0, TidsroData.MaxTimeLog + 10)
            .Select(i => new TimeLogEntry { StartedAt = t.AddMinutes(i), EndedAt = t.AddMinutes(i + 1), DurationMs = 60000 })
            .ToList();
        entries.Add(new TimeLogEntry { StartedAt = t, EndedAt = t.AddMinutes(-1) });   // ends before it starts
        entries.Add(null!);
        var clean = new TidsroData { Settings = new AppSettings(), TimeLog = entries }.Sanitized();
        Assert.Equal(TidsroData.MaxTimeLog, clean.TimeLog.Count);
        Assert.Equal(t.AddMinutes(10), clean.TimeLog[0].StartedAt);   // the oldest were dropped
    }
}
