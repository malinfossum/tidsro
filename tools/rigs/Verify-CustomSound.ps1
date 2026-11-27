<#
.SYNOPSIS
    Drives the custom-sound slot in a throwaway build: choose a .wav, see it, remove it, then the
    same with an .mp3.

.DESCRIPTION
    Opens Settings on a rig with an empty slot, asserts it says so, picks a .wav through the real
    Open dialog, and asserts the chosen name reaches both the window and data.json - then removes it
    and asserts the slot empties again. Then the same for an .mp3, which must arrive on disk as a
    wave file SoundPlayer loads. Also asserts a bad file of each kind is refused rather than accepted
    and silent, which is the failure that would leave an alarm quiet with nothing on screen to say why.

    The suite proves the view-models; only this proves the window. It found two defects a green
    607-test suite did not: Preview/Remove rendered identically enabled and disabled, because
    QuietAction has no dimmed state; and an AutomationProperties.Name on the file-name TextBlock
    replaced the accessible name its own text provides, so a screen reader announced the label and
    never the chosen file.

    Asserts afterwards that HKCU\...\Run is untouched. The rig has its own mutex, data path AND
    sounds folder, so an installed Tidsro can stay running throughout.

    Run it with pwsh 7, and its helpers run under pwsh too: Windows PowerShell 5.1 refuses to run
    scripts on my machine.

.PARAMETER ShotDir
    Save screenshots of the empty and chosen states here. Skipped when not given.

.EXAMPLE
    ./tools/rigs/Verify-CustomSound.ps1
    ./tools/rigs/Verify-CustomSound.ps1 -ShotDir $env:TEMP\shots -KeepRig
#>
[CmdletBinding()]
param(
    [string]$Ref = 'WORKTREE',
    [string]$Root = (Join-Path $env:TEMP 'tidsro-rig-sound'),
    [string]$ShotDir,
    [switch]$KeepRig
)
$ErrorActionPreference = 'Stop'
$here     = $PSScriptRoot
$repo     = (Resolve-Path (Join-Path $here '..\..')).Path
$dataFile = Join-Path $Root 'appdata\Tidsro\data.json'
$soundDir = Join-Path $Root 'appdata\Tidsro\sounds'
$installed = Join-Path $soundDir 'custom.wav'

$runBefore = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue).Tidsro

& "$here\Build-Rig.ps1" -Root $Root -Mutex 'Tidsro.SingleInstance.RigSound' -Ref $Ref

# An empty slot, and nothing else to distract the window.
if (Test-Path $soundDir) { Remove-Item $soundDir -Recurse -Force }
$fixture = [ordered]@{
    SchemaVersion = 5
    Settings = [ordered]@{
        SchemaVersion = 1; LaunchAtStartup = $false; DefaultSound = 0; SelectedTab = 1
        WindowLeft = 200.0; WindowTop = 80.0; WindowWidth = 1100.0; WindowHeight = 700.0
    }
    Alarms = @(); RecurringAlarms = @()
}
New-Item -ItemType Directory -Force -Path (Split-Path $dataFile) | Out-Null
$fixture | ConvertTo-Json -Depth 6 | Set-Content $dataFile -Encoding UTF8

# Two files to offer it: a real chime under a name that proves the label comes from the source, and
# a text file wearing a .wav extension, which stands in for an .mp3 renamed by hand.
$good = Join-Path $Root 'Kitchen Gong.wav'
$bad  = Join-Path $Root 'not-really-audio.wav'
Copy-Item (Join-Path $repo 'src\Tidsro\Assets\sounds\Bell-Jingle.wav') $good -Force
Set-Content $bad -Value 'ID3 this is not wave audio' -Encoding UTF8

# The .mp3 pass: the test tone the suite decodes, under a name with a space, and a text file
# wearing an .mp3 extension.
$goodMp3 = Join-Path $Root 'Morning Birds.mp3'
$badMp3  = Join-Path $Root 'not-really-audio.mp3'
Copy-Item (Join-Path $repo 'tests\Tidsro.Tests\Fixtures\test-tone.mp3') $goodMp3 -Force
Set-Content $badMp3 -Value 'ID3 this is not mp3 audio either' -Encoding UTF8

# Started directly rather than through Seed-Fixture: that writes its own week, and this check
# wants the empty schedule above so nothing competes with the Settings window.
$exe = Join-Path $Root 'src\Tidsro\bin\Release\net10.0-windows\Tidsro.exe'
$rig = Start-Process -FilePath $exe -PassThru
for ($i = 0; $i -lt 60 -and $rig.MainWindowHandle -eq 0; $i++) { Start-Sleep -Milliseconds 250; $rig.Refresh() }
if ($rig.MainWindowHandle -eq 0) { throw 'no main window appeared' }
Start-Sleep -Milliseconds 1500

Add-Type -AssemblyName System.Drawing, UIAutomationClient, UIAutomationTypes
Add-Type @"
using System; using System.Runtime.InteropServices;
public class SoundRigShot {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
"@
$AE   = [System.Windows.Automation.AutomationElement]
$Tree = [System.Windows.Automation.TreeScope]
$CT   = [System.Windows.Automation.ControlType]

# NOT $root: the parameter [string]$Root is the same variable - names are case-insensitive - and
# would silently coerce this element to its string form, failing much later on a missing FindFirst.
$uiaRoot = $AE::RootElement
$byPid = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $rig.Id)

# Looks up by AutomationId, never by text, so a copy change cannot break the rig.
function Find-El {
    param([string]$Id, $Type)
    $cond = New-Object System.Windows.Automation.AndCondition(
        $byPid,
        (New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $Id)),
        (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $Type)))
    $el = $null
    for ($i = 0; $i -lt 20 -and -not $el; $i++) {
        # Descendants, not Children: a WPF modal is not a direct child of the desktop element.
        $el = $uiaRoot.FindFirst($Tree::Descendants, $cond)
        if (-not $el) { Start-Sleep -Milliseconds 300 }
    }
    $el
}

# Quoted defensively: pwsh -File splits an unquoted argument on spaces, though no id has one.
function Click-Async([string]$Id) {
    Start-Process pwsh -WindowStyle Hidden -ArgumentList @(
        '-NoProfile', '-File', "$here\Click-Button.ps1",
        '-ProcessId', $rig.Id, '-Buttons', "`"$Id`"")
}

# Opens the file dialog from Settings and types a path into it.
function Choose-File([string]$Path) {
    Click-Async 'ChooseCustomSoundButton'
    Start-Sleep -Seconds 4
    & pwsh -NoProfile -File "$here\Front-Dialog.ps1" -ProcessId $rig.Id
    & pwsh -NoProfile -File "$here\Type-IntoDialog.ps1" -ProcessId $rig.Id -Path $Path
    Start-Sleep -Seconds 3
}

# What SoundPlayer makes of the installed copy: the same Load() every alarm sound goes through.
function Test-Playable([string]$Path) {
    try { $p = [System.Media.SoundPlayer]::new($Path); $p.Load(); $p.Dispose(); $true } catch { $false }
}

# The slot's state, read off the window: the file name, or the note that there is none. The value
# TextBlock carries no AutomationProperties.Name, so its accessible name IS its text.
function Get-Slot($Win) {
    $t = $Win.FindFirst($Tree::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, 'CustomSoundLabel')))
    if ($t) { $t.Current.Name } else { '<not found>' }
}

function Save-Shot([IntPtr]$Handle, [string]$Name) {
    if (-not $ShotDir) { return }
    New-Item -ItemType Directory -Force -Path $ShotDir | Out-Null
    # It can open behind other windows, and CopyFromScreen shoots whatever is on top.
    [void][SoundRigShot]::SetForegroundWindow($Handle); Start-Sleep -Milliseconds 800
    $r = New-Object SoundRigShot+RECT
    [void][SoundRigShot]::DwmGetWindowAttribute($Handle, 9, [ref]$r, 16)
    $bmp = New-Object System.Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size); $g.Dispose()
    $bmp.Save((Join-Path $ShotDir $Name), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    Write-Host "  shot $Name"
}

$failures = [System.Collections.Generic.List[string]]::new()
function Expect([bool]$Ok, [string]$What) {
    if ($Ok) { Write-Host "  ok   $What" } else { Write-Host "  FAIL $What"; $failures.Add($What) }
}

# The refusal card, found by id and then checked by title: the dialog is shared, and the title is
# what is under test. Dismissed so the next step starts from Settings again.
function Expect-Refusal([string]$What) {
    $refusal = Find-El 'ChoiceDialog' $CT::Window
    Expect ($null -ne $refusal -and $refusal.Current.Name -eq "That file won't do") "the refusal of $What reaches the screen"
    if ($refusal) {
        Click-Async 'ChoiceDialogDismissButton'
        Start-Sleep -Seconds 2
    }
}

try {
    # Settings is modal, so ShowDialog blocks the click - fire it and drive from here.
    Click-Async 'OpenSettingsButton'
    Start-Sleep -Seconds 5
    $settings = Find-El 'SettingsWindow' $CT::Window
    if (-not $settings) { throw 'the Settings window never appeared' }
    $hSettings = [IntPtr]$settings.Current.NativeWindowHandle

    Write-Host '--- empty slot ---'
    Expect ((Get-Slot $settings) -eq 'No sound chosen') 'the window says no sound is chosen'
    Expect (-not (Test-Path $installed)) 'nothing is installed on disk'
    Save-Shot $hSettings 'custom-sound-empty.png'

    Write-Host '--- a file it cannot play ---'
    Choose-File $bad
    Expect-Refusal 'a text file named .wav'
    Expect (-not (Test-Path $installed)) 'the refused file was not installed'

    Write-Host '--- choosing a real .wav ---'
    Choose-File $good
    Expect ((Get-Slot $settings) -eq 'Kitchen Gong.wav') 'the window names the chosen file'
    Expect (Test-Path $installed) 'the .wav was copied in as custom.wav'
    Expect ((Test-Path $installed) -and ((Get-FileHash $installed).Hash -eq (Get-FileHash $good).Hash)) 'the copy is byte for byte the original'
    Expect (Test-Path $good) 'the original file is still where it was'
    $saved = (Get-Content $dataFile -Raw | ConvertFrom-Json).Settings.CustomSoundName
    Expect ($saved -eq 'Kitchen Gong.wav') "the name reached data.json (got '$saved')"
    Save-Shot $hSettings 'custom-sound-chosen.png'

    Write-Host '--- removing it ---'
    Click-Async 'RemoveCustomSoundButton'
    Start-Sleep -Seconds 3
    Expect ((Get-Slot $settings) -eq 'No sound chosen') 'the window says no sound is chosen again'
    Expect (-not (Test-Path $installed)) 'the copy is gone from disk'
    $saved = (Get-Content $dataFile -Raw | ConvertFrom-Json).Settings.CustomSoundName
    Expect ([string]::IsNullOrEmpty($saved)) "the name was cleared from data.json (got '$saved')"

    Write-Host '--- an .mp3 it cannot play ---'
    Choose-File $badMp3
    Expect-Refusal 'a text file named .mp3'
    Expect (-not (Test-Path $installed)) 'the refused .mp3 was not installed'

    Write-Host '--- choosing a real .mp3 ---'
    Choose-File $goodMp3
    Expect ((Get-Slot $settings) -eq 'Morning Birds.mp3') 'the window names the chosen .mp3'
    Expect (Test-Path $installed) 'the .mp3 arrived as custom.wav'
    $head = if (Test-Path $installed) { [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($installed), 0, 4) } else { '' }
    Expect ($head -eq 'RIFF') "the installed copy is wave audio (starts '$head')"
    Expect ((Test-Path $installed) -and (Test-Playable $installed)) 'SoundPlayer loads the installed copy'
    Expect (Test-Path $goodMp3) 'the original .mp3 is still where it was'
    $saved = (Get-Content $dataFile -Raw | ConvertFrom-Json).Settings.CustomSoundName
    Expect ($saved -eq 'Morning Birds.mp3') "the .mp3 name reached data.json (got '$saved')"
    Save-Shot $hSettings 'custom-sound-mp3.png'

    Write-Host '--- removing the .mp3 ---'
    Click-Async 'RemoveCustomSoundButton'
    Start-Sleep -Seconds 3
    Expect ((Get-Slot $settings) -eq 'No sound chosen') 'the window says no sound is chosen after the .mp3'
    Expect (-not (Test-Path $installed)) 'the decoded copy is gone from disk'
}
finally {
    if (-not $KeepRig -and $rig -and -not $rig.HasExited) {
        Stop-Process -Id $rig.Id -Force -ErrorAction SilentlyContinue
    }
}

$runAfter = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue).Tidsro
if ($runAfter -ne $runBefore) { throw "HKCU\...\Run changed: '$runBefore' -> '$runAfter'" }
Write-Host "HKCU\...\Run unchanged: $runAfter"

if ($failures.Count) { throw "$($failures.Count) check(s) failed: $($failures -join '; ')" }
Write-Host 'PASS - the custom-sound slot fills, refuses, reports and empties, for a .wav and an .mp3'
