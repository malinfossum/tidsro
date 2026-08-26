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

    private static ContextMenu BuildMenu(Func<bool> hasAlert) =>
        TrayBuilder.BuildMenu(() => { }, () => { }, hasAlert, () => { }, () => { });

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
