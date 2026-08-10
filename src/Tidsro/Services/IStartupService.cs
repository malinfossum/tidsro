namespace Tidsro.Services;

/// <summary>The launch-at-startup toggle, behind an interface so view-model tests never touch the
/// real HKCU Run key. The path-repair logic stays on the concrete StartupService.</summary>
public interface IStartupService
{
    bool IsEnabled();
    void Enable();
    void Disable();
}
