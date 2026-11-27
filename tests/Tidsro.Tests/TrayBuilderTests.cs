using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Tidsro.Services;

namespace Tidsro.Tests;

// "Focus latest alert" focuses the newest completion card, so with no card on screen it has nothing
// to do. It used to stay enabled anyway and silently did nothing when clicked.
public class TrayBuilderTests
{
    [Fact]
    public void FocusLatestAlertIsDisabledWhenNoCardIsOpen() => RunSta(() =>
    {
        var menu = BuildMenu(() => false);

        Open(menu);

        Assert.False(FocusLatestAlert(menu).IsEnabled);
    });

    [Fact]
    public void FocusLatestAlertIsEnabledWhenACardIsOpen() => RunSta(() =>
    {
        var menu = BuildMenu(() => true);

        Open(menu);

        Assert.True(FocusLatestAlert(menu).IsEnabled);
    });

    // The tray menu outlives every card, so a state read once at build time would be wrong within
    // seconds. Each open has to ask again.
    [Fact]
    public void FocusLatestAlertIsReevaluatedOnEveryOpen() => RunSta(() =>
    {
        var hasCard = false;
        var menu = BuildMenu(() => hasCard);

        Open(menu);
        Assert.False(FocusLatestAlert(menu).IsEnabled);

        hasCard = true;
        Open(menu);
        Assert.True(FocusLatestAlert(menu).IsEnabled);

        hasCard = false;
        Open(menu);
        Assert.False(FocusLatestAlert(menu).IsEnabled);
    });

    // ── While Quit runs (a failed final save keeps its dialog up) or startup has not finished ──

    [Fact]
    public void OpenFocusAndQuitDoNothingWhileTheAppIsNotRunning() => RunSta(() =>
    {
        var calls = new List<string>();
        var menu = TrayBuilder.BuildMenu(() => calls.Add("open"), () => calls.Add("focus"), () => true,
            () => calls.Add("log"), () => calls.Add("quit"), isRunning: () => false);

        Click(Item(menu, "Open"));
        Click(Item(menu, "Focus latest alert"));
        Click(Item(menu, "Quit"));

        Assert.Empty(calls);
    });

    // The failed-save message says "See Tray > Open log folder", so that one has to keep working.
    [Fact]
    public void OpenLogFolderStillWorksWhileQuitIsRunning() => RunSta(() =>
    {
        var opened = 0;
        var menu = TrayBuilder.BuildMenu(() => { }, () => { }, () => true, () => opened++, () => { },
            isRunning: () => false);

        Click(Item(menu, "Open log folder"));

        Assert.Equal(1, opened);
    });

    [Fact]
    public void TheMenuShowsWhatIsUnavailableWhileTheAppIsNotRunning() => RunSta(() =>
    {
        var running = false;
        var menu = TrayBuilder.BuildMenu(() => { }, () => { }, () => true, () => { }, () => { },
            isRunning: () => running);

        Open(menu);
        Assert.False(Item(menu, "Open").IsEnabled);
        Assert.False(Item(menu, "Focus latest alert").IsEnabled);
        Assert.False(Item(menu, "Quit").IsEnabled);
        Assert.True(Item(menu, "Open log folder").IsEnabled);

        running = true;
        Open(menu);
        Assert.True(Item(menu, "Open").IsEnabled);
        Assert.True(Item(menu, "Focus latest alert").IsEnabled);
        Assert.True(Item(menu, "Quit").IsEnabled);
    });

    [Fact]
    public void TheMenuActsAgainOnceTheAppIsRunning() => RunSta(() =>
    {
        var running = false;
        var quits = 0;
        var menu = TrayBuilder.BuildMenu(() => { }, () => { }, () => true, () => { }, () => quits++,
            isRunning: () => running);

        Click(Item(menu, "Quit"));
        running = true;
        Click(Item(menu, "Quit"));

        Assert.Equal(1, quits);
    });

    private static ContextMenu BuildMenu(Func<bool> hasAlert) =>
        TrayBuilder.BuildMenu(() => { }, () => { }, hasAlert, () => { }, () => { }, isRunning: () => true);

    private static MenuItem Item(ContextMenu menu, string header) =>
        menu.Items.OfType<MenuItem>().Single(item => (string)item.Header == header);

    // RaiseEvent ignores IsEnabled, so this proves the click handler itself refuses, not just the look.
    private static void Click(MenuItem item) =>
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));

    // Raise Opened directly rather than setting IsOpen: a real popup needs a placement target and a
    // message pump, and the handler under test is the only thing that matters here.
    private static void Open(ContextMenu menu) =>
        menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent, menu));

    private static MenuItem FocusLatestAlert(ContextMenu menu) =>
        menu.Items.OfType<MenuItem>().Single(item => (string)item.Header == "Focus latest alert");

    // WPF objects can only be created on an STA thread; xUnit runs tests on MTA threads.
    private static void RunSta(Action test)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                test();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
