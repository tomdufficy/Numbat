$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$PackageDir = Join-Path $Root "Package"

dotnet build (Join-Path $Root "Numbat.csproj") -c Release

if (Test-Path $PackageDir) {
    Remove-Item $PackageDir -Recurse -Force
}

New-Item -ItemType Directory -Path $PackageDir | Out-Null
New-Item -ItemType Directory -Path (Join-Path $PackageDir "net48") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $PackageDir "net7.0") | Out-Null

Copy-Item (Join-Path $Root "bin\Release\net48\Numbat.rhp") (Join-Path $PackageDir "net48\Numbat.rhp")
Copy-Item (Join-Path $Root "bin\Release\net7.0\Numbat.rhp") (Join-Path $PackageDir "net7.0\Numbat.rhp")

Push-Location $PackageDir
yak spec
yak build
Pop-Location