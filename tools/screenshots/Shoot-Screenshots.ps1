<#
.SYNOPSIS
    Re-shoots the README screenshots against a fictional fixture schedule.

.DESCRIPTION
    The README screenshots must never be taken against my own data - a week grid publishes what I do,
    on which days, at which hours, in a public repo. This builds a throwaway copy of the app that
    reads a fixture schedule from a scratch folder, drives it, and captures the shots. My installed
    Tidsro can stay running throughout: the throwaway copy has its own single-instance mutex and its
    own data path, so it never touches %AppData%\Tidsro or the launch-at-startup registry value.

    Shoots week.png, schedule.png and alarm-dialog.png. main-window.png and completion-card.png are
    still hand-shot - they need a live countdown and a fired card, which this doesn't drive yet.

.PARAMETER OutDir
    Where to write the PNGs. Defaults to docs/screenshots.

.PARAMETER ScratchRoot
    Working folder for the throwaway build and its fixture data. Defaults to a temp folder.

.PARAMETER KeepScratch
    Leave the throwaway build in place afterwards instead of deleting it.

.EXAMPLE
    ./tools/screenshots/Shoot-Screenshots.ps1
    ./tools/screenshots/Shoot-Screenshots.ps1 -OutDir ./scratch -KeepScratch
#>
[CmdletBinding()]
param(
    [string]$OutDir,
    [string]$ScratchRoot = (Join-Path $env:TEMP 'tidsro-shoot'),
    [switch]$KeepScratch
)

$ErrorActionPreference = 'Stop'

# UIAutomationClient only loads cleanly on Windows PowerShell, so re-launch there if we started in pwsh 7.
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    $argv = @('-NoProfile', '-ExecutionPolicy', 'RemoteSigned', '-File', $PSCommandPath)
    if ($OutDir)      { $argv += @('-OutDir', $OutDir) }
    if ($ScratchRoot) { $argv += @('-ScratchRoot', $ScratchRoot) }
    if ($KeepScratch) { $argv += '-KeepScratch' }
    & powershell.exe @argv
    exit $LASTEXITCODE
}

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if (-not $OutDir) { $OutDir = Join-Path $repo 'docs\screenshots' }
$shootSrc = Join-Path $ScratchRoot 'src\Tidsro'
$dataRoot = Join-Path $ScratchRoot 'appdata'
$exe      = Join-Path $shootSrc 'bin\Release\net10.0-windows\Tidsro.exe'

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

# ---------------------------------------------------------------------------- win32

Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class ShootWin {
  delegate bool Cb(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(Cb cb, IntPtr l);
  [DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, [Out] char[] s, int n);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

  static string Title(IntPtr h) {
    var buf = new char[512]; int n = GetWindowText(h, buf, buf.Length);
    return new string(buf, 0, n);
  }
  // A WPF modal opened from a UIA Invoke does NOT show up under AutomationElement.RootElement's
  // children, so find windows the Win32 way or you will stack invisible dialogs without noticing.
  public static List<IntPtr> Visible(int pid, string title) {
    var found = new List<IntPtr>();
    EnumWindows((h, l) => {
      int p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h) && Title(h) == title) found.Add(h);
      return true;
    }, IntPtr.Zero);
    return found;
  }
}
"@

# PW_RENDERFULLCONTENT renders the window from its own surface, so nothing that happens to be on top
# of it can end up in the frame. CopyFromScreen photographs the desktop and needs the window in the
# foreground, which a background process cannot reliably arrange.
function Save-WindowImage {
    param([IntPtr]$Handle, [string]$Out, [switch]$IncludeChrome)

    $wr = New-Object ShootWin+RECT; [void][ShootWin]::GetWindowRect($Handle, [ref]$wr)
    $full = New-Object System.Drawing.Bitmap (($wr.R - $wr.L), ($wr.B - $wr.T))
    $g = [System.Drawing.Graphics]::FromImage($full)
    $hdc = $g.GetHdc()
    $ok = [ShootWin]::PrintWindow($Handle, $hdc, 2)
    $g.ReleaseHdc($hdc); $g.Dispose()
    if (-not $ok) { $full.Dispose(); throw "PrintWindow failed for $Out" }

    if ($IncludeChrome) {
        # Trim the invisible resize border: the window rect is larger than what is actually drawn.
        $dw = New-Object ShootWin+RECT
        [void][ShootWin]::DwmGetWindowAttribute($Handle, 9, [ref]$dw, 16)   # DWMWA_EXTENDED_FRAME_BOUNDS
        $rect = New-Object System.Drawing.Rectangle (($dw.L - $wr.L), ($dw.T - $wr.T), ($dw.R - $dw.L), ($dw.B - $dw.T))
    } else {
        # The client area alone - most of the README shots carry no title bar.
        $cr = New-Object ShootWin+RECT; [void][ShootWin]::GetClientRect($Handle, [ref]$cr)
        $co = New-Object ShootWin+POINT; [void][ShootWin]::ClientToScreen($Handle, [ref]$co)
        $rect = New-Object System.Drawing.Rectangle (($co.X - $wr.L), ($co.Y - $wr.T), ($cr.R - $cr.L), ($cr.B - $cr.T))
    }

    # Not $out - PowerShell variables are case-insensitive, so that would clobber the $Out path.
    $cropped = $full.Clone($rect, $full.PixelFormat)
    $cropped.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host ("  {0}  {1}x{2}" -f (Split-Path $Out -Leaf), $cropped.Width, $cropped.Height)
    $cropped.Dispose(); $full.Dispose()
}

# ---------------------------------------------------------------------------- uia

$CT = [System.Windows.Automation.ControlType]
$AE = [System.Windows.Automation.AutomationElement]

function Get-AppRoot {
    param([System.Diagnostics.Process]$Process)
    for ($i = 0; $i -lt 60 -and $Process.MainWindowHandle -eq 0; $i++) {
        Start-Sleep -Milliseconds 250; $Process.Refresh()
    }
    if ($Process.MainWindowHandle -eq 0) { throw 'the app never opened a window' }
    Start-Sleep -Milliseconds 800   # let the first layout pass settle before anything is read or shot
    $AE::FromHandle($Process.MainWindowHandle)
}

# Matching on Name alone also matches the TextBlock inside a button, which carries no InvokePattern -
# always AND in a control type.
function Find-Element {
    param($Root, [string]$Name, $Type)
    $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.AndCondition(
            (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $Name)),
            (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $Type)))))
}

function Get-Element {
    param($Root, [string]$Name, $Type)
    $el = Find-Element $Root $Name $Type
    if (-not $el) { throw "no $Type named '$Name'" }
    $el
}

function Set-Text  { param($El, [string]$Value) $El.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Value) }
function Invoke-Element { param($El) $El.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Select-Element { param($El) $El.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }

# ---------------------------------------------------------------------------- fixture

# A fictional demo week. Nothing here describes anyone's real routine, and it is chosen to exercise
# every behaviour the README claims: a row whose entries disagree on the minute (so the cells print
# their own times), a single-day alarm, and an empty weekend.
$WeekPlan = @(
    @{ Hour = 7;  Minute = 30; Days = 'MonTueWedThuFri'; Label = 'Morning walk';    Sound = 1; Warn = $false }
    @{ Hour = 9;  Minute = 0;  Days = 'MonTueWedThuFri'; Label = 'Stand-up';        Sound = 1; Warn = $true  }
    # Blocks (schema 5). End is minutes from midnight; null/absent is an instant.
    @{ Hour = 10; Minute = 0;  Days = 'MonWedFri';       Label = 'Lecture';         Sound = 1; Warn = $true;  End = 690 }
    @{ Hour = 10; Minute = 15; Days = 'TueThu';          Label = 'Focus block';     Sound = 0; Warn = $false; End = 720 }
    # Overlaps the Focus block, so the grid draws the two side by side in their own lanes.
    @{ Hour = 11; Minute = 0;  Days = 'Tue';             Label = 'Lab';             Sound = 0; Warn = $false; End = 750 }
    @{ Hour = 12; Minute = 0;  Days = 'MonWedFri';       Label = 'Lunch';           Sound = 2; Warn = $false }
    @{ Hour = 12; Minute = 15; Days = 'TueThu';          Label = 'Lunch';           Sound = 2; Warn = $false }
    @{ Hour = 14; Minute = 0;  Days = 'MonTueWedThuFri'; Label = 'Stretch';         Sound = 0; Warn = $false }
    @{ Hour = 16; Minute = 0;  Days = 'Wed';             Label = 'Guitar practice'; Sound = 3; Warn = $true  }
    @{ Hour = 17; Minute = 30; Days = 'MonTueWedThuFri'; Label = 'Wrap up';         Sound = 1; Warn = $false }
)

# The Schedule shot wants three rows in ascending clock order, and the first one is what the
# Edit-alarm dialog opens on - hence the piano jingle and the warning toggle.
$SchedulePlan = @(
    @{ Hour = 8;  Minute = 0;  Days = 'MonTueWedThuFri'; Label = 'Morning walk';    Sound = 4; Warn = $true  }
    @{ Hour = 10; Minute = 30; Days = 'Thu';             Label = 'Guitar practice'; Sound = 1; Warn = $false }
    @{ Hour = 11; Minute = 30; Days = 'MonTueWedThuFri'; Label = 'Lunch';           Sound = 1; Warn = $false }
)

$DayFlags = @{ Mon = 1; Tue = 2; Wed = 4; Thu = 8; Fri = 16; Sat = 32; Sun = 64 }

function ConvertTo-DayFlags {
    param([string]$Days)
    $value = 0
    foreach ($name in $DayFlags.Keys) { if ($Days -match $name) { $value += $DayFlags[$name] } }
    if ($value -eq 0) { throw "no weekday recognised in '$Days'" }
    $value
}

# The next occurrence, kept far enough ahead that nothing fires mid-shoot.
function Get-NextFireAt {
    param([int]$Hour, [int]$Minute, [int]$Days)
    $now = Get-Date
    foreach ($offset in 0..14) {
        $day = $now.Date.AddDays($offset)
        $bit = $DayFlags[$day.DayOfWeek.ToString().Substring(0, 3)]
        if (-not ($Days -band $bit)) { continue }
        $at = $day.AddHours($Hour).AddMinutes($Minute)
        if ($at -gt $now.AddMinutes(20)) { return $at.ToString('yyyy-MM-ddTHH:mm:sszzz') }
    }
    throw 'no occurrence within a fortnight'
}

# Enums persist as INTEGERS. Writing a string enum name here makes Load throw, quarantine the file
# and open an empty app - which looks like the fixture was ignored.
function Write-Fixture {
    param($Plan, [int]$Tab, [int]$Width, [int]$Height)
    $alarms = foreach ($a in $Plan) {
        $days = ConvertTo-DayFlags $a.Days
        [ordered]@{
            Id         = [guid]::NewGuid().ToString()
            Hour       = $a.Hour
            Minute     = $a.Minute
            Days       = $days
            Label      = $a.Label
            Sound      = $a.Sound
            NextFireAt = Get-NextFireAt $a.Hour $a.Minute $days
            WarnBefore = $a.Warn
            Enabled    = $true
            EndMinute  = $(if ($a.ContainsKey('End')) { $a.End } else { $null })
        }
    }
    $data = [ordered]@{
        SchemaVersion = 5
        Settings      = [ordered]@{
            SchemaVersion   = 1
            LaunchAtStartup = $false     # never let a scratch build claim HKCU\...\Run
            DefaultSound    = 0
            SelectedTab     = $Tab
            WindowLeft      = 240.0
            WindowTop       = 90.0
            WindowWidth     = [double]$Width
            WindowHeight    = [double]$Height
        }
        Alarms          = @()
        RecurringAlarms = @($alarms)
    }
    $dir = Join-Path $dataRoot 'Tidsro'
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $data | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $dir 'data.json') -Encoding UTF8
}

# ---------------------------------------------------------------------------- throwaway build

function Build-ShootCopy {
    Write-Host 'building a throwaway copy of the app...'
    if (Test-Path $ScratchRoot) { Remove-Item $ScratchRoot -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $shootSrc | Out-Null
    robocopy (Join-Path $repo 'src\Tidsro') $shootSrc /E /XD bin obj /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
    $global:LASTEXITCODE = 0

    # Two patches, in the copy only. Both are load-bearing: without the mutex rename the copy refuses
    # to start beside an installed Tidsro, and without the path redirect it would read and WRITE my
    # real schedule. Each replacement is asserted - a silent miss would point the copy at %AppData%.
    Edit-File (Join-Path $shootSrc 'App.xaml.cs') `
        '"Tidsro.SingleInstance.v1"' '"Tidsro.SingleInstance.Shoot"'
    foreach ($file in 'Services\PersistenceService.cs', 'Services\LogService.cs') {
        $name = if ($file -match 'Log') { 'tidsro.log' } else { 'data.json' }
        Edit-File (Join-Path $shootSrc $file) `
            "Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), `"Tidsro`", `"$name`");" `
            "@`"$dataRoot`", `"Tidsro`", `"$name`");"
    }

    dotnet build (Join-Path $shootSrc 'Tidsro.csproj') -c Release --nologo -v quiet | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'the throwaway build failed' }
    if (-not (Test-Path $exe)) { throw "no exe at $exe" }
}

function Edit-File {
    param([string]$Path, [string]$Old, [string]$New)
    $text = Get-Content $Path -Raw
    if ($text -notlike "*$Old*") { throw "pattern not found in $(Split-Path $Path -Leaf): $Old" }
    $text.Replace($Old, $New) | Set-Content $Path -Encoding UTF8 -NoNewline
}

function Start-Shoot {
    $p = Start-Process -FilePath $exe -PassThru
    $p, (Get-AppRoot -Process $p)
}

function Stop-Shoot {
    param([System.Diagnostics.Process]$Process)
    # Safe to kill: this copy owns its own data file, so nothing of mine is lost by skipping OnExit.
    Stop-Process -Id $Process.Id -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}

# ---------------------------------------------------------------------------- the shots

function Get-WeekShot {
    # Wide enough for the fixture's two-lane Tuesday: the grid asks for a readable 90px per lane
    # (TimetableLayout.RequiredGridWidth) and draws the agenda below that, which at the old 900
    # is what this shot would now capture. 1100 clears the 1044 flip point with room to spare, so
    # a font change that widens the gutter cannot silently turn the README shot into the agenda.
    Write-Fixture -Plan $WeekPlan -Tab 2 -Width 1100 -Height 575
    $proc, $root = Start-Shoot
    try {
        Save-WindowImage -Handle $proc.MainWindowHandle -Out (Join-Path $OutDir 'week.png') -IncludeChrome
    } finally { Stop-Shoot $proc }
}

function Get-ScheduleAndDialogShots {
    Write-Fixture -Plan $SchedulePlan -Tab 0 -Width 629 -Height 890
    $proc, $root = Start-Shoot
    try {
        # A running countdown so the cross-tab strip has something to show.
        Set-Text (Get-Element $root 'Custom duration' $CT::Edit) '5:00'
        Set-Text (Get-Element $root 'Label'           $CT::Edit) 'Laundry done'
        Invoke-Element (Get-Element $root 'Start timer' $CT::Button)
        Start-Sleep -Milliseconds 400
        Select-Element (Get-Element $root 'Schedule' $CT::TabItem)
        Start-Sleep -Milliseconds 600
        Save-WindowImage -Handle $proc.MainWindowHandle -Out (Join-Path $OutDir 'schedule.png')

        Invoke-Element (Get-Element $root 'Edit alarm at 08:00' $CT::Button)
        $dialog = $null
        foreach ($i in 1..25) {
            Start-Sleep -Milliseconds 200
            $found = [ShootWin]::Visible($proc.Id, 'Edit alarm')
            if ($found.Count -ge 1) { $dialog = $found[0]; break }
        }
        if (-not $dialog) { throw 'the Edit alarm dialog never opened' }
        Start-Sleep -Milliseconds 400
        Save-WindowImage -Handle $dialog -Out (Join-Path $OutDir 'alarm-dialog.png')
        [void][ShootWin]::SendMessage($dialog, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)   # WM_CLOSE
    } finally { Stop-Shoot $proc }
}

# ---------------------------------------------------------------------------- run

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$liveData = Join-Path $env:APPDATA 'Tidsro\data.json'
$before = if (Test-Path $liveData) { (Get-FileHash $liveData).Hash } else { $null }
$runBefore = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name Tidsro -ErrorAction SilentlyContinue).Tidsro

Build-ShootCopy
Write-Host 'shooting...'
Get-WeekShot
Get-ScheduleAndDialogShots

# The rig is meant to be provably harmless to a running install - say so rather than assume it.
$after = if (Test-Path $liveData) { (Get-FileHash $liveData).Hash } else { $null }
$runAfter = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name Tidsro -ErrorAction SilentlyContinue).Tidsro
if ($before -ne $after)     { throw 'my real data.json changed - the path patch did not hold' }
if ($runBefore -ne $runAfter) { throw 'the launch-at-startup registry value changed' }
Write-Host 'my own data.json and Run value are untouched.'

if (-not $KeepScratch) { Remove-Item $ScratchRoot -Recurse -Force -ErrorAction SilentlyContinue }
Write-Host "done - shots written to $OutDir"
