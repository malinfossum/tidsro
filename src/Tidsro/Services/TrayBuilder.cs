using System.Reflection;
using System.Windows.Controls;
using H.NotifyIcon;

namespace Tidsro.Services;

public static class TrayBuilder
{
    public static TaskbarIcon Create(Action onOpen, Action onFocusAlert, Func<bool> hasAlert, Action onOpenLog, Action onQuit)
    {
        var menu = BuildMenu(onOpen, onFocusAlert, hasAlert, onOpenLog, onQuit);

        var icon = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/Assets/icons/tidsro.ico"));
        icon.Freeze();   // immutable -> releases the underlying stream; safe for a lifetime-held tray icon

        var tray = new TaskbarIcon
        {
            ToolTipText = "Tidsro",
            ContextMenu = menu,
            IconSource = icon
        };
        tray.TrayLeftMouseUp += (_, _) => onOpen();
        tray.ForceCreate();
        return tray;
    }

    // Separate from Create so the menu's wiring can be tested without a tray icon, which needs a
    // live window to attach to.
    public static ContextMenu BuildMenu(Action onOpen, Action onFocusAlert, Func<bool> hasAlert, Action onOpenLog, Action onQuit)
    {
        var menu = new ContextMenu();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var about = new MenuItem
        {
            Header = $"Tidsro {version?.Major}.{version?.Minor}.{version?.Build}",
            IsEnabled = false   // informational header showing the installed version
        };
        var open = new MenuItem { Header = "Open" };
        open.Click += (_, _) => onOpen();
        var focusAlert = new MenuItem { Header = "Focus latest alert" };   // keyboard fallback when the hotkey is unavailable (spec §5.3)
        focusAlert.Click += (_, _) => onFocusAlert();
        // With no card on screen the item has nothing to focus, so it would be a silent no-op.
        // Re-checked on every open because cards come and go while the app sits in the tray.
        menu.Opened += (_, _) => focusAlert.IsEnabled = hasAlert();
        var openLog = new MenuItem { Header = "Open log folder" };
        openLog.Click += (_, _) => onOpenLog();
        var quit = new MenuItem { Header = "Quit" };
        quit.Click += (_, _) => onQuit();

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
