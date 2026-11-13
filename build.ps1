<#
.SYNOPSIS
  Builds Path++, runs the tests, and produces the installer and a portable zip in .\artifacts.

.EXAMPLE
  .\build.ps1
  .\build.ps1 -SkipTests
  .\build.ps1 -MakeNsis "D:\Tools\NSIS\makensis.exe"
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [switch] $SkipTests,
    [string] $MakeNsis
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

function Invoke-Checked([string] $exe, [string[]] $arguments) {
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "$exe failed with exit code $LASTEXITCODE" }
}

# Version comes from Directory.Build.props so there's a single source of truth.
[xml] $props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
Write-Host "Path++ $version ($Configuration)" -ForegroundColor Cyan

# 1. Build + test
Invoke-Checked dotnet @('build', (Join-Path $root 'PathPlusPlus.sln'), '-c', $Configuration, '--nologo')
if (-not $SkipTests) {
    Invoke-Checked dotnet @('test', (Join-Path $root 'PathPlusPlus.sln'), '-c', $Configuration, '--no-build', '--nologo')
}

# 2. Stage the files that ship
$artifacts = Join-Path $root 'artifacts'
$stage = Join-Path $artifacts 'bin'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null

$appOut = Join-Path $root "src\PathPlusPlus.App\bin\$Configuration\net48"
$cliOut = Join-Path $root "src\PathPlusPlus.Cli\bin\$Configuration\net48"
Copy-Item (Join-Path $appOut 'PathPlusPlus.exe'), (Join-Path $appOut 'PathPlusPlus.exe.config'), (Join-Path $appOut 'PathPlusPlus.Core.dll') $stage
Copy-Item (Join-Path $cliOut 'pathpp.exe'), (Join-Path $cliOut 'pathpp.exe.config') $stage
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $stage 'LICENSE.txt')
Copy-Item (Join-Path $root 'installer\README.txt') (Join-Path $stage 'README.txt')

# 3. Installer
if (-not $MakeNsis) {
    $candidates = @(
        (Get-Command makensis.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
        "${env:ProgramFiles(x86)}\NSIS\makensis.exe",
        "$env:ProgramFiles\NSIS\makensis.exe"
    ) | Where-Object { $_ -and (Test-Path $_) }
    $MakeNsis = $candidates | Select-Object -First 1
}
if (-not $MakeNsis) { throw 'makensis.exe not found. Install NSIS 3 or pass -MakeNsis <path>.' }

$setup = Join-Path $artifacts "Path++-Setup-$version.exe"
Invoke-Checked $MakeNsis @('/V2', '/INPUTCHARSET', 'UTF8', "/DVERSION=$version", "/DSOURCE_DIR=$stage", "/DOUTPUT_FILE=$setup", (Join-Path $root 'installer\PathPlusPlus.nsi'))

# 4. Portable zip
$zip = Join-Path $artifacts "Path++-$version-portable.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip

# 5. Checksums
$sums = Get-ChildItem -Path $artifacts -File | Where-Object { $_.Extension -in '.exe', '.zip' } |
    ForEach-Object { '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name }
$sums | Set-Content (Join-Path $artifacts 'SHA256SUMS.txt') -Encoding ascii

Write-Host ''
Write-Host 'Artifacts:' -ForegroundColor Green
Get-ChildItem $artifacts -File | ForEach-Object { '  {0,-40} {1,10:N0} bytes' -f $_.Name, $_.Length }
