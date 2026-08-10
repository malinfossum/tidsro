using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Tidsro.Services;

public sealed class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Tidsro";

    /// <summary>Written by the installer (and removed by the uninstaller) so we can recognise the installed copy.</summary>
    private const string AppKey = @"Software\Tidsro";
    private const string InstallDirValue = "InstallDir";

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

    /// <summary>Where the installer recorded it put us — absent for a portable copy.</summary>
    public static string? RecordedInstallFolder()
    {
        using var key = Registry.CurrentUser.OpenSubKey(AppKey);
        return key?.GetValue(InstallDirValue) as string;
    }

    /// <summary>The exe a Run value points at, or null when it isn't a path we can verify.</summary>
    public static string? TargetOf(string? runValue)
    {
        if (string.IsNullOrWhiteSpace(runValue)) return null;

        var value = runValue.Trim();
        string candidate;
        if (value.StartsWith('"'))
        {
            var close = value.IndexOf('"', 1);
            if (close < 2) return null;
            candidate = value[1..close];
        }
        else
        {
            var flag = value.IndexOf(" " + StartupArg, StringComparison.OrdinalIgnoreCase);
            candidate = flag > 0 ? value[..flag] : value;
        }

        return Path.IsPathFullyQualified(candidate) ? candidate : null;
    }

    /// <summary>
    /// Whether this copy should rewrite the Run key. Two gates, so autostart survives a move without a
    /// stray copy ever hijacking it: only a copy entitled to claim startup may write, and it writes only
    /// when the registered target is gone. A live target means autostart works — leave it alone.
    /// </summary>
    public static bool ShouldRepair(string exePath, string? registeredValue, string? recordedInstallFolder,
        Func<string, bool> targetExists)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return false;
        if (registeredValue is null) return false;                 // autostart is switched off
        if (registeredValue == RunValueFor(exePath)) return false;  // already points at us

        if (!MayClaimStartup(exePath, recordedInstallFolder)) return false;

        var target = TargetOf(registeredValue);
        return target is null || !targetExists(target);
    }

    /// <summary>
    /// The installed copy, identified by the folder the installer recorded. Without that record (a portable
    /// copy, or an install predating it) anything but a build output qualifies — a dev run must never claim
    /// startup, or a later clean deletes the exe that boot depends on.
    /// </summary>
    private static bool MayClaimStartup(string exePath, string? recordedInstallFolder)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(exePath));
        if (folder is null) return false;

        if (!string.IsNullOrWhiteSpace(recordedInstallFolder))
            return string.Equals(
                Trimmed(folder), Trimmed(Path.GetFullPath(recordedInstallFolder)),
                StringComparison.OrdinalIgnoreCase);

        return !IsBuildOutput(folder);

        static string Trimmed(string p) => p.TrimEnd(Path.DirectorySeparatorChar);
    }

    private static bool IsBuildOutput(string folder)
    {
        var s = Path.DirectorySeparatorChar;
        var probe = folder.TrimEnd(s) + s;
        return probe.Contains($"{s}bin{s}Debug{s}", StringComparison.OrdinalIgnoreCase)
            || probe.Contains($"{s}bin{s}Release{s}", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>If enabled, repoint the Run key after an app move — but only once it actually points nowhere.</summary>
    public void RefreshIfEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        var existing = key?.GetValue(ValueName) as string;

        if (ShouldRepair(_exePath, existing, RecordedInstallFolder(), File.Exists))
            key!.SetValue(ValueName, RunValueFor(_exePath));
    }
}
