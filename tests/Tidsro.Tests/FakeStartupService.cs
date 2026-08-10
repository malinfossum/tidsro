using Tidsro.Services;

namespace Tidsro.Tests;

// Records the calls instead of touching the registry.
public sealed class FakeStartupService : IStartupService
{
    public int EnableCalls { get; private set; }
    public int DisableCalls { get; private set; }
    public bool Enabled { get; set; }

    public bool IsEnabled() => Enabled;
    public void Enable() { EnableCalls++; Enabled = true; }
    public void Disable() { DisableCalls++; Enabled = false; }
}
