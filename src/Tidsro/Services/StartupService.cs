using System.Diagnostics;
using Microsoft.Win32;

namespace Tidsro.Services;

public sealed class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Tidsro";

    /// <summary>Passed on the Run-key command so a boot launch can stay in the tray (a manual launch shows the window).</summary>
    public const string StartupArg = "--startup";

    private readonly string _exePath;
    public StartupService(string exePath) => _exePath = exePath;

    public static string CurrentExePath =>
        Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName;

    /// <summary>Fully-quoted path plus the boot flag, so a space in the path can't mis-parse.</summary>
    public static string RunValueFor(string exePath) => "\"" + exePath + "\" " + StartupArg;

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.SetValue(ValueName, RunValueFor(_exePath));
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>If enabled, repoint a stale path after an app move/update.</summary>
    public void RefreshIfEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(ValueName) is string existing && existing != RunValueFor(_exePath))
            key.SetValue(ValueName, RunValueFor(_exePath));
    }
}
