#requires -Version 7
<#
.SYNOPSIS
    Installs a throwaway copy of the installer and asserts that a silent uninstall never asks.

.DESCRIPTION
    Builds installer/Tidsro.iss under its own AppId, name (TidsroRig) and data folder, then runs the
    uninstaller three ways: /SILENT, /VERYSILENT and clicked through. A silent run must finish on its
    own with the data folder kept and no "Also delete your alarms" prompt. The clicked-through run
    must still show that prompt, and answering No must keep the data.

    The payload is a stand-in exe, so nothing of Tidsro is built or started. Asserts afterwards that
    the rig left nothing behind and that the real install, its HKCU\...\Run value, its InstallDir
    value and %AppData%\Tidsro\data.json are untouched. An installed Tidsro can stay running.

.EXAMPLE
    ./tools/rigs/Verify-SilentUninstall.ps1
    ./tools/rigs/Verify-SilentUninstall.ps1 -Ref v2.6.1     # fails: the prompt waits under /SILENT
#>
[CmdletBinding()]
param(
    [string]$Ref = 'WORKTREE',
    [string]$Root = (Join-Path $env:TEMP 'tidsro-rig-uninstall')
)
$ErrorActionPreference = 'Stop'

# The message boxes are read with plain Win32. UI Automation, called from the process that started
# the uninstaller, only saw the title bar (see the README).
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class RigDialog { public IntPtr Handle; public string Title = ""; public string Text = ""; public List<int> ButtonIds = new List<int>(); }
public static class RigWin {
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern int GetDlgCtrlID(IntPtr h);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    static string TextOf(IntPtr h) { var sb = new StringBuilder(2048); GetWindowText(h, sb, sb.Capacity); return sb.ToString(); }
    static string ClassOf(IntPtr h) { var sb = new StringBuilder(256); GetClassName(h, sb, sb.Capacity); return sb.ToString(); }
    // Visible message boxes (class #32770) that belong to the given process.
    public static List<RigDialog> Find(uint pid) {
        var found = new List<RigDialog>();
        EnumWindows((h, l) => {
            uint owner; GetWindowThreadProcessId(h, out owner);
            if (owner != pid || !IsWindowVisible(h) || ClassOf(h) != "#32770") return true;
            var d = new RigDialog { Handle = h, Title = TextOf(h) };
            EnumChildWindows(h, (c, l2) => {
                var cls = ClassOf(c);
                if (cls == "Button") d.ButtonIds.Add(GetDlgCtrlID(c));
                else if (cls == "Static") d.Text += TextOf(c) + " ";
                return true;
            }, IntPtr.Zero);
            found.Add(d);
            return true;
        }, IntPtr.Zero);
        return found;
    }
    // Answers a message box the way its own button would: WM_COMMAND with the button's id.
    public static void Press(IntPtr dialog, int buttonId) { PostMessage(dialog, 0x0111, (IntPtr)buttonId, IntPtr.Zero); }
}
'@

$repo     = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$rigName  = 'TidsroRig'
$rigGuid  = '7E5C0B1E-5D0A-4C55-9B7E-52C1A0D1F0AA'
$realGuid = 'AD23F2BA-DA9E-4A84-A72A-B5D18F51AE18'
$appDir   = Join-Path $Root 'app'
$outDir   = Join-Path $Root 'out'
$payload  = Join-Path $Root 'payload'
$rigData  = Join-Path $env:APPDATA $rigName
$unKey    = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{$rigGuid}_is1"
$IdYes    = 6
$IdNo     = 7

function Get-RealState {
    $run  = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue).Tidsro
    $inst = (Get-ItemProperty 'HKCU:\Software\Tidsro' -ErrorAction SilentlyContinue).InstallDir
    $data = Join-Path $env:APPDATA 'Tidsro\data.json'
    $hash = (Test-Path $data) ? (Get-FileHash $data -Algorithm SHA256).Hash : 'absent'
    $key  = Test-Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{$realGuid}_is1"
    "$run|$inst|$hash|$key"
}

function Swap([string]$Text, [string]$Old, [string]$New) {
    if (-not $Text.Contains($Old)) { throw "Rig patch target not found in Tidsro.iss: $Old" }
    $Text.Replace($Old, $New)
}

# Inno Setup 6.7 names its second-stage uninstaller _unins.tmp; older versions used _iu*.tmp.
function Get-RigProcess {
    Get-CimInstance Win32_Process | Where-Object {
        $_.CommandLine -and $_.CommandLine.Contains($Root) -and
        ($_.Name -like 'unins*.exe' -or $_.Name -like '_unins*.tmp' -or $_.Name -like '_iu*.tmp' -or $_.Name -like "$rigName-Setup*")
    }
}

function Get-RigDialog {
    foreach ($proc in @(Get-RigProcess)) { [RigWin]::Find([uint32]$proc.ProcessId) }
}

function Stop-RigProcess {
    foreach ($proc in @(Get-RigProcess)) {
        if ($proc.Name -eq 'Tidsro.exe') { throw 'Refusing to stop a process named Tidsro.exe.' }
        Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
    }
}

$before = Get-RealState

# --- Build the rig installer -------------------------------------------------------------------
if (@(Get-RigProcess).Count) { throw "A rig uninstaller from an earlier run is still open under $Root. Close it first." }
Remove-Item $Root -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $outDir, $payload -Force | Out-Null
Copy-Item "$env:SystemRoot\System32\whoami.exe" (Join-Path $payload 'Tidsro.exe')

$iss = if ($Ref -eq 'WORKTREE') { Get-Content -LiteralPath (Join-Path $repo 'installer\Tidsro.iss') -Raw -Encoding utf8 }
       else { (& git -C $repo show "${Ref}:installer/Tidsro.iss") -join "`n" }
if (-not $iss) { throw "Could not read installer/Tidsro.iss at $Ref." }

# Every line that names the real install is repointed. The [UninstallRun] line matters most: it
# deletes the HKCU Run value by literal name, so unpatched it would remove the real autostart entry.
$iss = Swap $iss "AppId={{$realGuid}" "AppId={{$rigGuid}"
$iss = Swap $iss '#define AppName "Tidsro"' "#define AppName `"$rigName`""
$iss = Swap $iss '/v Tidsro /f' "/v $rigName /f"
$iss = Swap $iss 'Source: "..\dist\{#AppExeName}"' "Source: `"$payload\{#AppExeName}`""
$iss = Swap $iss 'Source: "..\src\' "Source: `"$repo\src\"
$iss = Swap $iss 'SetupIconFile=..\src\' "SetupIconFile=$repo\src\"
$iss = Swap $iss 'OutputDir=..\dist' "OutputDir=$outDir"
$iss = Swap $iss 'OutputBaseFilename=Tidsro-Setup' "OutputBaseFilename=$rigName-Setup"
$rigIss = Join-Path $Root 'rig.iss'
Set-Content -LiteralPath $rigIss -Value $iss -Encoding utf8BOM

$iscc = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source,
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup (ISCC.exe) not found.' }
$build = & $iscc '/Q' '/DAppVersion=0.0.1' $rigIss 2>&1
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE): $build" }
$setup = Join-Path $outDir "$rigName-Setup.exe"

# --- One install and uninstall per mode --------------------------------------------------------
$results = foreach ($mode in '/SILENT', '/VERYSILENT', 'interactive') {
    $install = Start-Process $setup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=`"$appDir`"" -PassThru -Wait
    $unins = Join-Path $appDir 'unins000.exe'
    if ($install.ExitCode -ne 0 -or -not (Test-Path $unins)) { throw "Rig install failed for $mode (exit $($install.ExitCode))." }
    New-Item -ItemType Directory -Path $rigData -Force | Out-Null
    Set-Content (Join-Path $rigData 'data.json') '{"fixture":true}'

    $clicked    = $mode -eq 'interactive'
    $promptSeen = $false
    $other      = @()
    if ($clicked) { Start-Process $unins | Out-Null } else { Start-Process $unins -ArgumentList $mode | Out-Null }
    $deadline = (Get-Date).AddSeconds($clicked ? 60 : 20)
    do {
        Start-Sleep -Milliseconds 400
        foreach ($d in @(Get-RigDialog)) {
            $text = ($d.Text -replace '\s+', ' ').Trim()
            if ($text -match 'Also delete your alarms') {
                $promptSeen = $true
                if ($clicked) { [RigWin]::Press($d.Handle, $IdNo) }                                    # keep the data
            }
            elseif ($clicked -and $d.ButtonIds -contains $IdYes) { [RigWin]::Press($d.Handle, $IdYes) }   # "Are you sure": uninstall
            elseif ($clicked -and $d.ButtonIds.Count -eq 1) { [RigWin]::Press($d.Handle, $d.ButtonIds[0]) }   # the closing OK
            else { $other += "$($d.Title): $text" }
        }
        # A silent run that shows the prompt is already the answer; no need to wait out the clock.
        if ($promptSeen -and -not $clicked) { break }
    } while (@(Get-RigProcess).Count -gt 0 -and (Get-Date) -lt $deadline)

    $waited = @(Get-RigProcess).Count -gt 0
    if ($waited) { Stop-RigProcess; Start-Sleep -Seconds 1 }

    [pscustomobject]@{
        Mode       = $mode
        PromptSeen = $promptSeen
        Waited     = $waited
        DataKept   = Test-Path (Join-Path $rigData 'data.json')
        AppRemoved = -not (Test-Path (Join-Path $appDir 'Tidsro.exe'))
        Other      = ($other | Select-Object -Unique) -join ' / '
    }
    Remove-Item $rigData -Recurse -Force -ErrorAction SilentlyContinue
}
$results | Format-Table Mode, PromptSeen, Waited, DataKept, AppRemoved -AutoSize | Out-String -Width 200 | Write-Host

# --- Clean up, then judge ----------------------------------------------------------------------
# A run that was stopped while it waited leaves the rig half removed. Take the rest out here so a
# failing run does not leave the rig installed.
if (Test-Path (Join-Path $appDir 'unins000.exe')) {
    Start-Process (Join-Path $appDir 'unins000.exe') -ArgumentList '/VERYSILENT' | Out-Null
    $deadline = (Get-Date).AddSeconds(20)
    do { Start-Sleep -Milliseconds 400 } while (@(Get-RigProcess).Count -gt 0 -and (Get-Date) -lt $deadline)
    Stop-RigProcess
}
Remove-Item $rigData -Recurse -Force -ErrorAction SilentlyContinue

$leftovers = @()
if (Test-Path $unKey) { $leftovers += 'uninstall key' }
if (Test-Path "HKCU:\Software\$rigName") { $leftovers += 'HKCU\Software key' }
if (Test-Path (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\$rigName.lnk")) { $leftovers += 'Start Menu shortcut' }

$after = Get-RealState
if ($after -ne $before) { throw 'The real install changed during the run (Run value, InstallDir, uninstall key or data.json).' }
Write-Host 'Real install, HKCU\...\Run, InstallDir and data.json unchanged'

$failures = @()
foreach ($r in $results) {
    if ($r.Other) { $failures += "$($r.Mode): unexpected dialog: $($r.Other)" }
    if (-not $r.DataKept) { $failures += "$($r.Mode): the data folder was deleted" }
    if ($r.Mode -eq 'interactive') {
        if (-not $r.PromptSeen) { $failures += 'interactive: the delete-data prompt never appeared' }
        if ($r.Waited) { $failures += 'interactive: the uninstall did not finish after the prompt was answered' }
    }
    else {
        if ($r.PromptSeen) { $failures += "$($r.Mode): the delete-data prompt appeared" }
        if ($r.Waited) { $failures += "$($r.Mode): the uninstall did not finish on its own" }
    }
    if (-not $r.AppRemoved) { $failures += "$($r.Mode): the app was not removed" }
}
if ($leftovers.Count) { $failures += "rig leftovers: $($leftovers -join ', ')" }

if ($failures.Count) { throw ("FAIL`n  " + ($failures -join "`n  ")) }
Write-Host 'PASS - a silent uninstall never asks and keeps the data; a clicked-through one still asks'
