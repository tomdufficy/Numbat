<#
.SYNOPSIS
    Builds Numbat and packages it as a multi-targeted Yak package (net48 + net8.0).

.DESCRIPTION
    This script is used by CI and for local testing. The version always comes from
    <Version> in Numbat.csproj, which is the single source of truth. The package layout
    follows McNeel's guidance for multi-targeted Rhino 8 plug-ins:

        manifest.yml
        numbat-icon-512.png
        net48/   Numbat.rhp ...
        net8.0/  Numbat.rhp ...

    Rhino loads whichever build matches the runtime it is running under.

.PARAMETER Yak
    Path to yak.exe. If omitted, the script uses $env:YAK_EXE, then the Rhino 8 install,
    then PATH.

.PARAMETER OutDir
    Output folder for the .yak file. The default is ./dist.

.PARAMETER Platform
    The Yak platform tag. The default is 'win', because Numbat is officially Windows-only.
    The package is then named numbat-<version>-rh8_21-win.yak and is not offered on Mac.

.OUTPUTS
    The full path of the built .yak file.
#>
[CmdletBinding()]
param(
    [string]$Yak,
    [string]$OutDir = (Join-Path $PSScriptRoot '..' 'dist'),
    [ValidateSet('win', 'mac', 'any')]
    [string]$Platform = 'win'
)

$ErrorActionPreference = 'Stop'

$repoRoot  = Resolve-Path (Join-Path $PSScriptRoot '..')
$project   = Join-Path $repoRoot 'src/Numbat/Numbat/Numbat.csproj'
$manifest  = Join-Path $repoRoot 'src/Numbat/Numbat/manifest.yml'
$icon      = Join-Path $repoRoot 'assets/brand/numbat-icon-512.png'
$binDir    = Join-Path $repoRoot 'src/Numbat/Numbat/bin/Release'
$frameworks = 'net48', 'net8.0'

# --- Version (single source of truth: the csproj) ----------------------------
$version = ([xml](Get-Content $project -Raw)).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> found in $project" }
$version = $version.Trim()
Write-Host "Numbat version: $version"

# --- Locate yak.exe ----------------------------------------------------------
if (-not $Yak) { $Yak = $env:YAK_EXE }
if (-not $Yak) {
    $rhinoYak = Join-Path $env:ProgramFiles 'Rhino 8/System/Yak.exe'
    if (Test-Path $rhinoYak) { $Yak = $rhinoYak }
}
if (-not $Yak) { $Yak = (Get-Command yak -ErrorAction SilentlyContinue).Source }
if (-not $Yak -or -not (Test-Path $Yak)) {
    throw 'yak.exe not found. Pass -Yak, set YAK_EXE, or install Rhino 8.'
}

# --- Build -------------------------------------------------------------------
dotnet build $project -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }

# --- Stage the package folder ------------------------------------------------
$stage = Join-Path $OutDir 'package'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null

Copy-Item $manifest (Join-Path $stage 'manifest.yml')
Copy-Item $icon     (Join-Path $stage 'numbat-icon-512.png')

foreach ($fw in $frameworks) {
    $src = Join-Path $binDir $fw
    if (-not (Test-Path (Join-Path $src 'Numbat.rhp'))) { throw "Missing build output: $src" }
    $dst = Join-Path $stage $fw
    New-Item -ItemType Directory -Force $dst | Out-Null
    Copy-Item (Join-Path $src '*') $dst -Recurse
}

# --- yak build ---------------------------------------------------------------
Push-Location $stage
try {
    Get-ChildItem *.yak -ErrorAction SilentlyContinue | Remove-Item
    & $Yak build --platform $Platform --version $version
    if ($LASTEXITCODE -ne 0) { throw 'yak build failed' }
    $built = Get-ChildItem *.yak | Select-Object -First 1
    if (-not $built) { throw 'yak build produced no .yak file' }
    $target = Join-Path (Resolve-Path $OutDir) $built.Name
    Move-Item $built.FullName $target -Force
}
finally {
    Pop-Location
}

Write-Host "Built: $target"
if ($env:GITHUB_OUTPUT) {
    "version=$version"  | Out-File $env:GITHUB_OUTPUT -Append -Encoding utf8
    "yak_path=$target"  | Out-File $env:GITHUB_OUTPUT -Append -Encoding utf8
}
$target
