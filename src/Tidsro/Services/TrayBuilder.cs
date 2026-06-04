using System.Windows.Controls;
using H.NotifyIcon;

namespace Tidsro.Services;

public static class TrayBuilder
{
    public static TaskbarIcon Create(Action onOpen, Action onFocusAlert, Action onQuit)
    {
        var menu = new ContextMenu();
        var open = new MenuItem { Header = "Open" };
        open.Click += (_, _) => onOpen();
        var focusAlert = new MenuItem { Header = "Focus latest alert" };   // keyboard fallback when the hotkey is unavailable (spec §5.3)
        focusAlert.Click += (_, _) => onFocusAlert();
        var quit = new MenuItem { Header = "Quit" };
        quit.Click += (_, _) => onQuit();
        menu.Items.Add(open);
        menu.Items.Add(focusAlert);
        menu.Items.Add(new Separator());
        menu.Items.Add(quit);

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
}
