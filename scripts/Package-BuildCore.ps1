$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $projectRoot "bin\win-x64\publish"
$releaseDir = Join-Path $projectRoot "bin\releases"
$zipPath = Join-Path $releaseDir "BuildCore-1.0.0-win-x64.zip"

if (-not (Test-Path $publishDir)) {
    throw "Publish directory not found: $publishDir"
}

if (-not (Test-Path (Join-Path $publishDir "BuildCore.exe"))) {
    throw "BuildCore.exe was not found in the publish directory."
}

New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host ""
Write-Host "BuildCore 1.0.0 package created:"
Write-Host $zipPath
Write-Host ""
