<#
.SYNOPSIS
    Types a path into the file dialog in front, then presses Enter.

.DESCRIPTION
    Guarded: it refuses unless the foreground window belongs to the expected process, so a stray
    keystroke can never land in another app. Front the dialog first with Front-Dialog.ps1.

    An Open dialog takes a typed path in its name box. A SAVE dialog does not reliably - typing
    there can land in the file list as type-ahead and the pre-filled name is what gets saved. Pass
    no path to accept that name, and read the result off disk rather than assuming it.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][int]$ProcessId, [string]$Path = '')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public class FG {
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
}
"@
$owner = 0
[void][FG]::GetWindowThreadProcessId([FG]::GetForegroundWindow(), [ref]$owner)
if ($owner -ne $ProcessId) { throw "foreground window belongs to pid $owner, not $ProcessId - refusing to type" }

if ($Path) { [System.Windows.Forms.SendKeys]::SendWait($Path) }
Start-Sleep -Milliseconds 400
[System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
Write-Host "typed into pid ${ProcessId}: $Path"
