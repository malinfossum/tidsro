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

    // ── A second launch asks the first copy to show its window ──

    [Fact]
    public void A_show_request_while_running_shows_the_window_now()
    {
        var life = new AppLifecycle();
        life.MarkStarted();

        Assert.True(life.RequestShow());
    }

    // Startup shows a message box if it fails, and that box pumps messages, so the request can be
    // handled before the window has been built. It has to wait for startup to finish.
    [Fact]
    public void A_show_request_during_startup_waits_until_startup_has_finished()
    {
        var life = new AppLifecycle();

        Assert.False(life.RequestShow());
        Assert.True(life.MarkStarted());     // the held request is due now
    }

    [Fact]
    public void A_show_request_during_a_failed_startup_is_dropped()
    {
        var life = new AppLifecycle();

        Assert.False(life.RequestShow());
        life.MarkExiting();

        Assert.False(life.MarkStarted());
        Assert.False(life.RequestShow());
    }

    [Fact]
    public void A_startup_with_no_show_request_has_nothing_to_show()
    {
        var life = new AppLifecycle();
        Assert.False(life.MarkStarted());
    }

    [Fact]
    public void A_held_show_request_is_due_only_once()
    {
        var life = new AppLifecycle();
        life.RequestShow();
        life.RequestShow();

        Assert.True(life.MarkStarted());
        Assert.False(life.MarkStarted());
    }

    [Fact]
    public void A_show_request_after_quit_has_begun_is_dropped()
    {
        var life = new AppLifecycle();
        life.MarkStarted();
        life.TryBeginQuit();

        Assert.False(life.RequestShow());
    }
}
