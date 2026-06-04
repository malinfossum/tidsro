using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Tidsro.Services;

/// <summary>Registers a system-wide hotkey (default Ctrl+Alt+T) on a message-only window.</summary>
public sealed class HotkeyService : IDisposable
{
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mod, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_NOREPEAT = 0x4000;
    private const uint VK_T = 0x54;
    private readonly int _id = 0x5444; // "TD"
    private readonly HwndSource _source;

    public event EventHandler? Pressed;

    public HotkeyService()
    {
        var p = new HwndSourceParameters("TidsroHotkey") { ParentWindow = new IntPtr(-3) }; // HWND_MESSAGE
        _source = new HwndSource(p);
        _source.AddHook(WndProc);
    }

    public bool Register() =>
        RegisterHotKey(_source.Handle, _id, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_T);

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == _id)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterHotKey(_source.Handle, _id);
        _source.Dispose();
    }
}
