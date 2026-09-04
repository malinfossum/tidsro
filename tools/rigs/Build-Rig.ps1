<#
.SYNOPSIS
    Builds a throwaway copy of Tidsro that cannot touch my real schedule.

.DESCRIPTION
    Copies src/Tidsro to a scratch folder and patches two things in the copy only: the
    single-instance mutex name, so it starts beside an installed Tidsro, and the data path, so it
    reads and writes its own data.json instead of %AppData%\Tidsro. Each replacement is asserted -
    a silent miss would point the copy at my real file.

    Nothing here writes to HKCU\...\Run either, as long as the fixture keeps LaunchAtStartup false.

.PARAMETER Root
    Scratch folder for the copy and its data. Wiped on each run.

.PARAMETER Mutex
    Mutex name for the copy. Give each rig its own, or two rigs will refuse to run together.

.PARAMETER Ref
    Git ref to build from. 'WORKTREE' (the default) builds the working tree, including
    uncommitted changes; anything else is resolved with git archive, e.g. 'v2.3.0'.

.EXAMPLE
    ./tools/rigs/Build-Rig.ps1 -Root $env:TEMP\tidsro-rig-now -Mutex Tidsro.SingleInstance.RigNow
    ./tools/rigs/Build-Rig.ps1 -Root $env:TEMP\tidsro-rig-old -Mutex Tidsro.Rig.Old -Ref v2.3.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Root,
    [Parameter(Mandatory)][string]$Mutex,
    [string]$Ref = 'WORKTREE'
)

$ErrorActionPreference = 'Stop'
$repo     = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$src      = Join-Path $Root 'src\Tidsro'
$dataRoot = Join-Path $Root 'appdata'
$exe      = Join-Path $src 'bin\Release\net10.0-windows\Tidsro.exe'

function Edit-File {
    param([string]$Path, [string]$Old, [string]$New)
    $text = Get-Content $Path -Raw
    if ($text -notlike "*$Old*") { throw "pattern not found in $(Split-Path $Path -Leaf): $Old" }
    $text.Replace($Old, $New) | Set-Content $Path -Encoding UTF8 -NoNewline
}

if (Test-Path $Root) { Remove-Item $Root -Recurse -Force }
New-Item -ItemType Directory -Force -Path $src | Out-Null

if ($Ref -eq 'WORKTREE') {
    robocopy (Join-Path $repo 'src\Tidsro') $src /E /XD bin obj /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
    $global:LASTEXITCODE = 0
} else {
    $tar = Join-Path $Root 'src.tar'
    Push-Location $repo
    try {
        git archive $Ref src/Tidsro -o $tar
        if ($LASTEXITCODE -ne 0) { throw "git archive $Ref failed" }
    } finally { Pop-Location }
    tar -xf $tar -C $Root
    Remove-Item $tar
}
if (-not (Test-Path (Join-Path $src 'Tidsro.csproj'))) { throw "no csproj under $src" }

Edit-File (Join-Path $src 'App.xaml.cs') '"Tidsro.SingleInstance.v1"' "`"$Mutex`""
foreach ($file in 'Services\PersistenceService.cs', 'Services\LogService.cs') {
    $name = if ($file -match 'Log') { 'tidsro.log' } else { 'data.json' }
    Edit-File (Join-Path $src $file) `
        "Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), `"Tidsro`", `"$name`");" `
        "@`"$dataRoot`", `"Tidsro`", `"$name`");"
}

dotnet build (Join-Path $src 'Tidsro.csproj') -c Release --nologo -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { throw "the throwaway build failed ($Ref)" }
if (-not (Test-Path $exe)) { throw "no exe at $exe" }
Write-Host "built $Ref -> $exe"
