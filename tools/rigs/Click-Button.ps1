<#
.SYNOPSIS
    Clicks buttons in a running rig through UI Automation, found by their AutomationId.

.DESCRIPTION
    Runs in pwsh 7, which loads UIAutomationClient fine.

    -Buttons takes AutomationIds (AutomationProperties.AutomationId in the XAML), never a label or
    an accessible name, so a copy change cannot break a lookup. Ask for an id that does not exist
    and this throws rather than clicking something else.

.EXAMPLE
    pwsh -NoProfile -Command "& ./Click-Button.ps1 -ProcessId 1234 -Buttons OpenSettingsButton, ViewFontLicenseButton"
#>
[CmdletBinding()]
param([Parameter(Mandatory)][int]$ProcessId, [Parameter(Mandatory)][string[]]$Buttons)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$root = [System.Windows.Automation.AutomationElement]::RootElement
$byPid = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId)

$win = $null
for ($i = 0; $i -lt 40 -and -not $win; $i++) {
    $win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $byPid)
    if (-not $win) { Start-Sleep -Milliseconds 250 }
}
if (-not $win) { throw "no window for pid $ProcessId" }

foreach ($id in $Buttons) {
    $byId = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    # The type guards against an id that ends up on something without an InvokePattern.
    $byType = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    $both = New-Object System.Windows.Automation.AndCondition(
        $byPid, (New-Object System.Windows.Automation.AndCondition($byId, $byType)))

    $el = $null
    for ($i = 0; $i -lt 20 -and -not $el; $i++) {
        # From the root, not from the window: a WPF modal is a separate element under the desktop.
        $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $both)
        if (-not $el) { Start-Sleep -Milliseconds 250 }
    }
    if (-not $el) { throw "button '$id' not found" }
    $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Write-Host "invoked '$id'"
    Start-Sleep -Milliseconds 900
}
