using System.Text.Json;
using Tidsro.Models;
using Tidsro.Services;
using Tidsro.ViewModels;

namespace Tidsro.Tests;

public class StopwatchTests
{
    private static MainViewModel New(out FakeClock clock)
    {
        clock = new FakeClock();
        var sched = new SchedulerService(clock);
        return new MainViewModel(sched, new FakeSoundService(), SoundChoice.None, hasCustomSound: () => false);
    }

    [Fact]
    public void A_stopwatch_counts_up_pauses_and_resumes()
    {
        var clock = new FakeClock();
        var sw = new StopwatchItemViewModel(() => clock.Now, "Focus");
        Assert.False(sw.IsRunning);
        Assert.Equal("Start", sw.StartPauseLabel);

        sw.StartPause();
        clock.Advance(TimeSpan.FromSeconds(65.4));
        sw.Refresh();
        Assert.True(sw.IsRunning);
        Assert.Equal("01:05", sw.ElapsedText);
        Assert.Equal(".4", sw.FractionText);

        sw.StartPause();                           // pause
        clock.Advance(TimeSpan.FromMinutes(10));   // time passing while paused doesn't count
        sw.Refresh();
        Assert.Equal("01:05", sw.ElapsedText);
        Assert.Equal("Resume", sw.StartPauseLabel);

        sw.StartPause();                           // resume
        clock.Advance(TimeSpan.FromHours(1));
        sw.Refresh();
        Assert.Equal("1:01:05", sw.ElapsedText);
    }

    [Fact]
    public void Reset_returns_to_zero_and_a_running_stopwatch_keeps_running()
    {
        var clock = new FakeClock();
        var sw = new StopwatchItemViewModel(() => clock.Now);
        sw.StartPause();
        clock.Advance(TimeSpan.FromSeconds(30));
        sw.Reset();
        Assert.Equal("00:00", sw.ElapsedText);
        Assert.True(sw.IsRunning);
        clock.Advance(TimeSpan.FromSeconds(3));
        sw.Refresh();
        Assert.Equal("00:03", sw.ElapsedText);
    }

    [Fact]
    public void A_clock_set_backwards_never_makes_elapsed_negative()
    {
        var clock = new FakeClock();
        var sw = new StopwatchItemViewModel(() => clock.Now);
        sw.StartPause();
        clock.Advance(TimeSpan.FromMinutes(-5));
        Assert.Equal(TimeSpan.Zero, sw.Elapsed);
    }

    [Fact]
    public void A_running_stopwatch_round_trips_through_its_record_and_keeps_counting()
    {
        var clock = new FakeClock();
        var sw = new StopwatchItemViewModel(() => clock.Now, "Run");
        sw.StartPause();
        clock.Advance(TimeSpan.FromSeconds(10));

        var restored = StopwatchItemViewModel.FromRecord(sw.ToRecord(), () => clock.Now);
        clock.Advance(TimeSpan.FromSeconds(5));   // the app was closed for a while
        Assert.Equal(TimeSpan.FromSeconds(15), restored.Elapsed);
        Assert.Equal("Run", restored.Label);
        Assert.Equal(sw.Id, restored.Id);
    }

    [Fact]
    public void Several_stopwatches_run_independently()
    {
        var vm = New(out var clock);
        vm.StopwatchLabel = "first";
        vm.AddStopwatchCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(20));
        vm.StartStopwatchWithLabelCommand.Execute("Second");
        clock.Advance(TimeSpan.FromSeconds(5));
        vm.Stopwatches[0].StartPause();   // pause the first
        clock.Advance(TimeSpan.FromSeconds(5));
        vm.RefreshStopwatches();

        Assert.Equal(2, vm.Stopwatches.Count);
        Assert.Equal("First", vm.Stopwatches[0].Label);   // capitalised like timer labels
        Assert.Equal("00:25", vm.Stopwatches[0].ElapsedText);
        Assert.Equal("00:10", vm.Stopwatches[1].ElapsedText);
        Assert.Equal("", vm.StopwatchLabel);              // the add field is consumed
        Assert.True(vm.AnyStopwatchRunning);
    }

    [Fact]
    public void Removing_a_stopwatch_can_be_undone_and_stays_on_disk_until_committed()
    {
        var vm = New(out _);
        vm.StartStopwatchWithLabelCommand.Execute("A");
        vm.StartStopwatchWithLabelCommand.Execute("B");
        var saves = 0;
        vm.StopwatchesChanged += (_, _) => saves++;

        vm.RemoveStopwatchCommand.Execute(vm.Stopwatches[0]);
        Assert.Single(vm.Stopwatches);
        Assert.True(vm.HasPendingDelete);
        Assert.Equal(2, vm.StopwatchRecords().Count);   // not deleted on disk yet
        Assert.Equal(0, saves);

        vm.UndoDeleteCommand.Execute(null);
        Assert.Equal(new[] { "A", "B" }, vm.Stopwatches.Select(s => s.Label));
        Assert.False(vm.HasPendingDelete);

        vm.RemoveStopwatchCommand.Execute(vm.Stopwatches[1]);
        vm.CommitPendingDelete();
        Assert.Single(vm.StopwatchRecords());
        Assert.Equal(1, saves);
    }

    [Fact]
    public void Editing_a_label_raises_a_change_to_persist()
    {
        var vm = New(out _);
        vm.StartStopwatchWithLabelCommand.Execute("x");
        var saves = 0;
        vm.StopwatchesChanged += (_, _) => saves++;
        vm.Stopwatches[0].Label = "Renamed";
        Assert.Equal(1, saves);
        Assert.Equal("Renamed", vm.StopwatchRecords()[0].Label);
    }

    [Fact]
    public void Start_all_and_pause_all()
    {
        var vm = New(out _);
        vm.StartStopwatchWithLabelCommand.Execute("a");
        vm.StartStopwatchWithLabelCommand.Execute("b");
        vm.PauseAllStopwatchesCommand.Execute(null);
        Assert.All(vm.Stopwatches, s => Assert.False(s.IsRunning));
        Assert.False(vm.AnyStopwatchRunning);
        vm.StartAllStopwatchesCommand.Execute(null);
        Assert.All(vm.Stopwatches, s => Assert.True(s.IsRunning));
    }

    [Fact]
    public void Stopwatches_persist_and_old_files_without_them_still_load()
    {
        var json = "{\"SchemaVersion\":5,\"Settings\":{},\"Alarms\":[],\"RecurringAlarms\":[]}";
        var data = JsonSerializer.Deserialize<TidsroData>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.Sanitized();
        Assert.Empty(data.Stopwatches);
        Assert.Empty(data.Settings!.SavedLabels);

        var id = Guid.NewGuid();
        var dirty = new TidsroData
        {
            Settings = new AppSettings(),
            Stopwatches =
            {
                new StopwatchRecord { Id = id, Label = "  Lap  ", ElapsedMs = -5 },
                new StopwatchRecord { Id = id, Label = "dupe" },
                null!,
            },
        }.Sanitized();
        var only = Assert.Single(dirty.Stopwatches);
        Assert.Equal("Lap", only.Label);
        Assert.Equal(0, only.ElapsedMs);
    }
}

public class SavedLabelTests
{
    private static MainViewModel New()
    {
        var sched = new SchedulerService(new FakeClock());
        return new MainViewModel(sched, new FakeSoundService(), SoundChoice.None, hasCustomSound: () => false);
    }

    [Fact]
    public void Saving_a_label_adds_it_once_and_raises_a_change()
    {
        var vm = New();
        var changes = 0;
        vm.SavedLabelsChanged += (_, _) => changes++;

        vm.SaveLabelCommand.Execute("  laundry ");
        vm.SaveLabelCommand.Execute("LAUNDRY");     // case-insensitive duplicate
        vm.SaveLabelCommand.Execute("   ");         // blank

        Assert.Equal(new[] { "Laundry" }, vm.SavedLabels);
        Assert.True(vm.HasSavedLabels);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void One_click_applies_a_saved_label_to_a_timer_an_alarm_or_a_new_stopwatch()
    {
        var vm = New();
        vm.SaveLabelCommand.Execute("Tea");

        vm.UseLabelForTimerCommand.Execute("Tea");
        Assert.Equal("Tea", vm.Label);
        vm.StartPresetCommand.Execute(5);
        Assert.Equal("Tea", vm.Running.Single().Label);

        vm.UseLabelForAlarmCommand.Execute("Tea");
        Assert.Equal("Tea", vm.AlarmLabel);

        vm.StartStopwatchWithLabelCommand.Execute("Tea");
        var sw = Assert.Single(vm.Stopwatches);
        Assert.Equal("Tea", sw.Label);
        Assert.True(sw.IsRunning);
    }

    [Fact]
    public void Removing_a_saved_label_forgets_it()
    {
        var vm = New();
        vm.LoadSavedLabels(new[] { "A", "B" });
        var changes = 0;
        vm.SavedLabelsChanged += (_, _) => changes++;
        vm.RemoveSavedLabelCommand.Execute("A");
        Assert.Equal(new[] { "B" }, vm.SavedLabels);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Saved_labels_are_capped()
    {
        var vm = New();
        vm.LoadSavedLabels(Enumerable.Range(0, 100).Select(i => $"L{i}"));
        Assert.Equal(AppSettings.MaxSavedLabels, vm.SavedLabels.Count);
        vm.SaveLabelCommand.Execute("One more");
        Assert.DoesNotContain("One more", vm.SavedLabels);
    }

    [Fact]
    public void Settings_sanitise_saved_labels()
    {
        var s = new AppSettings { SavedLabels = { " a ", "A", "", "b", new string('x', 500) } }.Sanitized();
        Assert.Equal(3, s.SavedLabels.Count);
        Assert.Equal("a", s.SavedLabels[0]);
        Assert.Equal(AppSettings.MaxSavedLabelLength, s.SavedLabels[2].Length);
    }

    [Fact]
    public void A_running_timer_label_can_be_edited_in_place()
    {
        var vm = New();
        vm.StartPresetCommand.Execute(5);
        var row = vm.Running.Single();
        row.Label = "Pasta";
        Assert.Equal("Pasta", row.Item.Label);
        row.Label = "   ";
        Assert.Null(row.Item.Label);
    }
}
