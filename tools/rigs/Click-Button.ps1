<#
.SYNOPSIS
    Clicks named buttons in a running rig through UI Automation.

.DESCRIPTION
    Runs on Windows PowerShell only - UIAutomationClient does not load cleanly in pwsh 7, so call
    this with powershell.exe, not from pwsh directly.

    Names are ACCESSIBLE names, not the visible label: the Settings dialog's save button answers to
    'Save settings', not 'Save'. Ask for a name that does not exist and this throws rather than
    clicking something else.

.EXAMPLE
    powershell.exe -NoProfile -File ./Click-Button.ps1 -ProcessId 1234 -Buttons @('Settings','Save settings')
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

foreach ($name in $Buttons) {
    $byName = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)
    # Name alone matches the TextBlock inside the button, which has no InvokePattern - AND a type.
    $byType = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    $both = New-Object System.Windows.Automation.AndCondition(
        $byPid, (New-Object System.Windows.Automation.AndCondition($byName, $byType)))

    $el = $null
    for ($i = 0; $i -lt 20 -and -not $el; $i++) {
        # From the root, not from the window: a WPF modal is a separate element under the desktop.
        $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $both)
        if (-not $el) { Start-Sleep -Milliseconds 250 }
    }
    if (-not $el) { throw "button '$name' not found" }
    $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Write-Host "invoked '$name'"
    Start-Sleep -Milliseconds 900
}
