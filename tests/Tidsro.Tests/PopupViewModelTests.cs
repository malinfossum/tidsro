using Tidsro.Models;
using Tidsro.ViewModels;
using Xunit;

namespace Tidsro.Tests;

public class PopupViewModelTests
{
    private static TimerItem Item(string? label = "focus") =>
        new() { Label = label, OriginalDuration = TimeSpan.FromMinutes(25) };

    [Fact]
    public void Title_falls_back_when_label_is_blank()
    {
        Assert.Equal("Timer complete", new PopupViewModel(Item(" "), _ => Item(), _ => Item(), _ => { }).Title);
        Assert.Equal("focus", new PopupViewModel(Item("focus"), _ => Item(), _ => Item(), _ => { }).Title);
    }

    [Fact]
    public void Plus5_is_debounced_against_double_trigger()
    {
        var snoozes = 0;
        var vm = new PopupViewModel(Item(), _ => { snoozes++; return Item(); }, _ => Item(), _ => { });
        vm.Plus5Command.Execute(null);
        vm.Plus5Command.Execute(null);     // fast double-click
        Assert.Equal(1, snoozes);          // performed once
    }

    [Fact]
    public void Dismiss_raises_CloseRequested_and_calls_callback_once()
    {
        var dismissed = 0; var closed = 0;
        var vm = new PopupViewModel(Item(), _ => Item(), _ => Item(), _ => dismissed++);
        vm.CloseRequested += (_, _) => closed++;
        vm.DismissCommand.Execute(null);
        vm.DismissCommand.Execute(null);
        Assert.Equal(1, dismissed);
        Assert.Equal(1, closed);
    }
}
