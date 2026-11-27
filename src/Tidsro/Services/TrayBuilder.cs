using System.Reflection;
using System.Windows.Controls;
using H.NotifyIcon;

namespace Tidsro.Services;

public static class TrayBuilder
{
    public static TaskbarIcon Create(Action onOpen, Action onFocusAlert, Func<bool> hasAlert, Action onOpenLog, Action onQuit,
        Func<bool> isRunning)
    {
        var menu = BuildMenu(onOpen, onFocusAlert, hasAlert, onOpenLog, onQuit, isRunning);

        var icon = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/Assets/icons/tidsro.ico"));
        icon.Freeze();   // immutable -> releases the underlying stream; safe for a lifetime-held tray icon

        var tray = new TaskbarIcon
        {
            ToolTipText = "Tidsro",
            ContextMenu = menu,
            IconSource = icon
        };
        tray.TrayLeftMouseUp += (_, _) => { if (isRunning()) onOpen(); };
        tray.ForceCreate();
        return tray;
    }

    // Separate from Create so the menu's wiring can be tested without a tray icon, which needs a
    // live window to attach to. isRunning is false before startup has finished and once Quit has begun
    // (a failed final save keeps a dialog up while Quit waits): Open, Focus latest alert and Quit then do
    // nothing and show as unavailable. Open log folder always works, since the failure message points to it.
    public static ContextMenu BuildMenu(Action onOpen, Action onFocusAlert, Func<bool> hasAlert, Action onOpenLog, Action onQuit,
        Func<bool> isRunning)
    {
        var menu = new ContextMenu();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var about = new MenuItem
        {
            Header = $"Tidsro {version?.Major}.{version?.Minor}.{version?.Build}",
            IsEnabled = false   // informational header showing the installed version
        };
        var open = new MenuItem { Header = "Open" };
        open.Click += (_, _) => { if (isRunning()) onOpen(); };
        var focusAlert = new MenuItem { Header = "Focus latest alert" };   // keyboard fallback when the hotkey is unavailable (spec §5.3)
        focusAlert.Click += (_, _) => { if (isRunning()) onFocusAlert(); };
        var openLog = new MenuItem { Header = "Open log folder" };
        openLog.Click += (_, _) => onOpenLog();
        var quit = new MenuItem { Header = "Quit" };
        quit.Click += (_, _) => { if (isRunning()) onQuit(); };

        // With no card on screen "Focus latest alert" has nothing to focus, so it would be a silent
        // no-op. Re-checked on every open because cards come and go while the app sits in the tray.
        menu.Opened += (_, _) =>
        {
            var running = isRunning();
            open.IsEnabled = running;
            focusAlert.IsEnabled = running && hasAlert();
            quit.IsEnabled = running;
        };

        menu.Items.Add(about);
        menu.Items.Add(new Separator());
        menu.Items.Add(open);
        menu.Items.Add(focusAlert);
        menu.Items.Add(openLog);
        menu.Items.Add(new Separator());
        menu.Items.Add(quit);

        return menu;
    }
}
