using Tidsro.Services;
using Xunit;

namespace Tidsro.Tests;

public class AppLifecycleTests
{
    [Fact]
    public void The_app_is_running_only_between_startup_and_quit()
    {
        var life = new AppLifecycle();
        Assert.False(life.IsRunning);

        life.MarkStarted();
        Assert.True(life.IsRunning);

        Assert.True(life.TryBeginQuit());
        Assert.False(life.IsRunning);
    }

    // A failed final save shows a dialog that pumps messages, so the tray can ask to quit again while
    // the first Quit is still waiting on that dialog.
    [Fact]
    public void Quit_starts_only_once()
    {
        var life = new AppLifecycle();
        life.MarkStarted();

        Assert.True(life.TryBeginQuit());
        Assert.False(life.TryBeginQuit());
    }

    [Fact]
    public void Quit_cannot_start_before_startup_has_finished()
    {
        var life = new AppLifecycle();
        Assert.False(life.TryBeginQuit());
    }

    [Fact]
    public void A_failed_startup_never_becomes_running()
    {
        var life = new AppLifecycle();
        life.MarkExiting();

        life.MarkStarted();

        Assert.False(life.IsRunning);
        Assert.True(life.IsExiting);
    }
}
