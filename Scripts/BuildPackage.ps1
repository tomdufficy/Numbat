$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$PackageDir = Join-Path $Root "Package"
$Version = ([xml](Get-Content (Join-Path $Root "Numbat.csproj"))).Project.PropertyGroup.Version

dotnet build (Join-Path $Root "Numbat.csproj") -c Release

if (Test-Path $PackageDir) {
    Remove-Item $PackageDir -Recurse -Force
}

New-Item -ItemType Directory -Path $PackageDir | Out-Null
New-Item -ItemType Directory -Path (Join-Path $PackageDir "net48") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $PackageDir "net7.0") | Out-Null

Copy-Item (Join-Path $Root "Icons\Numbat.png") (Join-Path $PackageDir "icon.png")

Copy-Item (Join-Path $Root "bin\Release\net48\Numbat.rhp") (Join-Path $PackageDir "net48\Numbat.rhp")
Copy-Item (Join-Path $Root "bin\Release\net7.0\Numbat.rhp") (Join-Path $PackageDir "net7.0\Numbat.rhp")

Push-Location $PackageDir
yak spec
$ManifestPath = Join-Path $PackageDir "manifest.yml"
$Manifest = Get-Content $ManifestPath -Raw
$Manifest = $Manifest -replace "(?m)^version:.*$", "version: $Version"
$Manifest = $Manifest -replace "url: <url>", "url: https://github.com/tomdufficy/Numbat"
$Manifest += "`r`nicon: icon.png`r`n"
Set-Content $ManifestPath $Manifest
yak build
Pop-Location