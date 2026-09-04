<#
.SYNOPSIS
    Drives a real import in a throwaway build and reports whether end times survive it.

.DESCRIPTION
    Seeds a fictional week with two blocks, copies its data file aside as a backup, clears every
    alarm through Settings, imports the backup, and prints the end times on the other side. Ends
    that come back as (none) are the v2.5.1 bug: MainViewModel dropped EndMinute when re-arming.

    Asserts afterwards that HKCU\...\Run is untouched. The rig has its own mutex and data path, so
    an installed Tidsro can stay running throughout.

.EXAMPLE
    ./tools/rigs/Verify-Import.ps1
    ./tools/rigs/Verify-Import.ps1 -Ref v2.5.0     # the release the bug shipped in
#>
[CmdletBinding()]
param(
    [string]$Ref = 'WORKTREE',
    [string]$Root = (Join-Path $env:TEMP 'tidsro-rig-import'),
    [switch]$KeepRig
)
$ErrorActionPreference = 'Stop'
$here   = $PSScriptRoot
$data   = Join-Path $Root 'appdata\Tidsro\data.json'
$backup = Join-Path $Root 'backup.json'

function Click([int]$ProcId, [string]$Name) {
    & powershell.exe -NoProfile -ExecutionPolicy RemoteSigned `
        -Command "& '$here\Click-Button.ps1' -ProcessId $ProcId -Buttons @('$Name')"
    if ($LASTEXITCODE -ne 0) { throw "clicking '$Name' failed" }
}
function Show-Ends {
    (Get-Content $data -Raw | ConvertFrom-Json).RecurringAlarms | ForEach-Object {
        '  {0:D2}:{1:D2} {2,-14} end={3}' -f $_.Hour, $_.Minute, $_.Label,
            $(if ($null -eq $_.EndMinute) { '(none)' } else { '{0:D2}:{1:D2}' -f [int]($_.EndMinute / 60), ($_.EndMinute % 60) })
    }
}

$runBefore = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue).Tidsro

& "$here\Build-Rig.ps1" -Root $Root -Mutex 'Tidsro.SingleInstance.RigImport' -Ref $Ref
$rig = & "$here\Seed-Fixture.ps1" -Root $Root -Tab 1 -Width 700 -Height 820 | Select-Object -Last 1
Copy-Item $data $backup -Force          # schema 5, the same shape Export writes
Write-Host '--- seeded, copied to a backup file ---'
Show-Ends | ForEach-Object { Write-Host $_ }

Click $rig.Id 'Settings'
Click $rig.Id 'Clear all alarms'
Click $rig.Id 'Confirm'
Start-Sleep -Seconds 1
$left = (Get-Content $data -Raw | ConvertFrom-Json).RecurringAlarms.Count
if ($left -ne 0) { throw "clear left $left recurring alarms behind" }
Write-Host '--- cleared ---'

# The click blocks while the dialog is up, so fire it off and drive the dialog from here.
Start-Process powershell.exe -ArgumentList '-NoProfile', '-ExecutionPolicy', 'RemoteSigned', `
    '-Command', "& '$here\Click-Button.ps1' -ProcessId $($rig.Id) -Buttons @('Import data from a file')"
Start-Sleep -Seconds 4
& powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File "$here\Front-Dialog.ps1" -ProcessId $rig.Id
& powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File "$here\Type-IntoDialog.ps1" -ProcessId $rig.Id -Path $backup
Start-Sleep -Seconds 3
Click $rig.Id 'Restore alarms only'
Start-Sleep -Seconds 2

Write-Host '--- after import ---'
$ends = Show-Ends
$ends | ForEach-Object { Write-Host $_ }
$lost = @($ends | Where-Object { $_ -match 'Lecture|Lab' } | Where-Object { $_ -match 'end=\(none\)' })

if (-not $KeepRig) { Stop-Process -Id $rig.Id -Force -ErrorAction SilentlyContinue }

$runAfter = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue).Tidsro
if ($runAfter -ne $runBefore) { throw "HKCU\...\Run changed: '$runBefore' -> '$runAfter'" }
Write-Host "HKCU\...\Run unchanged: $runAfter"

if ($lost.Count) { throw "$($lost.Count) block(s) lost an end time through the import" }
Write-Host 'PASS - both blocks kept their end times'
