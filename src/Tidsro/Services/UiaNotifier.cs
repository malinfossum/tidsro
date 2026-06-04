using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;

namespace Tidsro.Services;

public static class UiaNotifier
{
    /// <summary>Announce via a UIA Notification event — no focus change (spec §5.3, §9).</summary>
    public static void Announce(UIElement element, string message)
    {
        var peer = UIElementAutomationPeer.FromElement(element)
                   ?? UIElementAutomationPeer.CreatePeerForElement(element);
        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.ActionCompleted,
            AutomationNotificationProcessing.MostRecent,
            message,
            "TidsroTimerComplete");
    }
}
