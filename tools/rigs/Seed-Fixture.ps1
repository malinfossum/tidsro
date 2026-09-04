<#
.SYNOPSIS
    Writes a fictional week into a rig's data file and starts the rig on it.

.DESCRIPTION
    The fixture is invented, never my own timetable, and LaunchAtStartup is false so no build can
    claim HKCU\...\Run. Enums persist as INTEGERS - a string enum name makes Load throw, quarantine
    the file and open an empty app, which reads as though the fixture were ignored.

    Returns the started process, so a caller can drive it:  $rig = ./Seed-Fixture.ps1 -Root ...

.PARAMETER Tab
    0 Quick timers, 1 Schedule, 2 Week.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Root,
    [ValidateRange(0, 2)][int]$Tab = 1,
    [int]$Width = 1100,
    [int]$Height = 700,
    [string]$ShotPath
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$dataDir = Join-Path $Root 'appdata\Tidsro'
$exe     = Join-Path $Root 'src\Tidsro\bin\Release\net10.0-windows\Tidsro.exe'
if (-not (Test-Path $exe)) { throw "no rig at $exe - run Build-Rig.ps1 first" }

$DayFlags = [ordered]@{ Mon = 1; Tue = 2; Wed = 4; Thu = 8; Fri = 16; Sat = 32; Sun = 64 }
function Get-NextFireAt {
    param([int]$Hour, [int]$Minute, [int]$Days)
    $now = Get-Date
    foreach ($offset in 0..14) {
        $day = $now.Date.AddDays($offset)
        if (-not ($Days -band $DayFlags[$day.DayOfWeek.ToString().Substring(0, 3)])) { continue }
        $at = $day.AddHours($Hour).AddMinutes($Minute)
        if ($at -gt $now.AddMinutes(20)) { return $at.ToString('yyyy-MM-ddTHH:mm:sszzz') }   # nothing fires mid-run
    }
    throw 'no occurrence within a fortnight'
}

# Two blocks and two instants: enough to tell a block from a point, and to catch a lost end time.
$plan = @(
    @{ Hour = 7;  Minute = 30; Days = 31; Label = 'Morning walk';  Sound = 0; Warn = $false; End = $null }
    @{ Hour = 9;  Minute = 0;  Days = 21; Label = 'Lecture';       Sound = 1; Warn = $true;  End = 660  }
    @{ Hour = 13; Minute = 0;  Days = 2;  Label = 'Lab';           Sound = 0; Warn = $false; End = 870  }
    @{ Hour = 16; Minute = 15; Days = 8;  Label = 'Reading group'; Sound = 0; Warn = $false; End = $null }
)
$data = [ordered]@{
    SchemaVersion = 5
    Settings      = [ordered]@{
        SchemaVersion   = 1
        LaunchAtStartup = $false          # never let a scratch build claim HKCU\...\Run
        DefaultSound    = 0
        SelectedTab     = $Tab
        WindowLeft      = 200.0
        WindowTop       = 80.0
        WindowWidth     = [double]$Width
        WindowHeight    = [double]$Height
    }
    Alarms          = @(
        [ordered]@{
            Id = [guid]::NewGuid().ToString()
            FireAt = (Get-Date).Date.AddDays(1).AddHours(20).ToString('yyyy-MM-ddTHH:mm:sszzz')
            Label = 'Call Mum'; Sound = 0; WarnBefore = $false; Enabled = $true
        }
    )
    RecurringAlarms = @(foreach ($a in $plan) {
        [ordered]@{
            Id         = [guid]::NewGuid().ToString()
            Hour       = $a.Hour
            Minute     = $a.Minute
            Days       = $a.Days
            Label      = $a.Label
            Sound      = $a.Sound
            NextFireAt = Get-NextFireAt $a.Hour $a.Minute $a.Days
            WarnBefore = $a.Warn
            Enabled    = $true
            EndMinute  = $a.End
        }
    })
}
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
Get-ChildItem $dataDir -Filter '*.corrupt' -ErrorAction SilentlyContinue | Remove-Item -Force
$data | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $dataDir 'data.json') -Encoding UTF8
Write-Host 'seeded schema 5: 4 recurring (2 with end times) + 1 one-shot'

$p = Start-Process -FilePath $exe -PassThru
for ($i = 0; $i -lt 60 -and $p.MainWindowHandle -eq 0; $i++) { Start-Sleep -Milliseconds 250; $p.Refresh() }
if ($p.MainWindowHandle -eq 0) { throw 'no main window appeared' }
Start-Sleep -Milliseconds 1500

if ($ShotPath) {
    Add-Type @"
using System; using System.Runtime.InteropServices;
public class RigShot {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
"@
    [void][RigShot]::SetForegroundWindow($p.MainWindowHandle)
    Start-Sleep -Milliseconds 600
    $r = New-Object RigShot+RECT
    # DWMWA_EXTENDED_FRAME_BOUNDS (9) excludes the drop shadow GetWindowRect includes.
    if ([RigShot]::DwmGetWindowAttribute($p.MainWindowHandle, 9, [ref]$r, 16) -ne 0) {
        [void][RigShot]::GetWindowRect($p.MainWindowHandle, [ref]$r)
    }
    $bmp = New-Object System.Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size)
    $g.Dispose()
    $bmp.Save($ShotPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "shot $ShotPath"
}

$p
