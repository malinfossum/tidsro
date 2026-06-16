#requires -Version 7.0
<#
  publish.ps1 - build Tidsro as a distributable Windows app.

  Outputs to .\dist\:
    Tidsro.exe        self-contained single-file portable build (no .NET needed)
    Tidsro-Setup.exe  per-user installer (Start Menu shortcut + uninstaller)

  Usage:  ./publish.ps1
#>

$ErrorActionPreference = 'Stop'

$root   = $PSScriptRoot
$proj   = Join-Path $root 'src\Tidsro\Tidsro.csproj'
$iss    = Join-Path $root 'installer\Tidsro.iss'
$dist   = Join-Path $root 'dist'
$appTmp = Join-Path $dist 'app'
$rid    = 'win-x64'

# Version: single source of truth is the .csproj <Version>.
$verLine = Select-String -Path $proj -Pattern '<Version>\s*(.+?)\s*</Version>' | Select-Object -First 1
if (-not $verLine) { throw "No <Version> found in $proj" }
$version = $verLine.Matches[0].Groups[1].Value
Write-Host "Building Tidsro $version ($rid)" -ForegroundColor Cyan

# A running Tidsro.exe locks the build output - stop it first.
$running = Get-Process Tidsro -ErrorAction SilentlyContinue
if ($running) {
    Write-Host 'Stopping running Tidsro...' -ForegroundColor DarkGray
    $running | ForEach-Object { try { $_.Kill(); [void]$_.WaitForExit(5000) } catch { } }
}

# Clean dist.
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Path $dist | Out-Null

# Publish: self-contained, single file, compressed, no symbols.
dotnet publish $proj `
    -c Release -r $rid --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -p:DebugSymbols=false `
    -v minimal -o $appTmp
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

# Portable exe -> dist root; drop the temp publish folder.
Copy-Item (Join-Path $appTmp 'Tidsro.exe') (Join-Path $dist 'Tidsro.exe') -Force
Remove-Item $appTmp -Recurse -Force

# Build the installer if Inno Setup is available.
$iscc = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source,
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $iscc) {
    Write-Warning 'Inno Setup (ISCC.exe) not found - portable exe built, installer skipped.'
    Write-Warning 'Install it with:  winget install --id JRSoftware.InnoSetup -e'
}
else {
    & $iscc "/DAppVersion=$version" $iss
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed ($LASTEXITCODE)" }
}

# Summary.
Write-Host "`nArtifacts in $dist :" -ForegroundColor Green
Get-ChildItem $dist -Filter *.exe | ForEach-Object {
    '  {0,-18} {1,7:N1} MB' -f $_.Name, ($_.Length / 1MB)
}
