$ErrorActionPreference = "Stop"

$scriptRoot = $PSScriptRoot
$repoRoot = Split-Path -Parent $scriptRoot
$projectRoot = Join-Path $repoRoot "BuildCore"
$publishDir = Join-Path $projectRoot "bin\win-x64\publish"
$installerScript = Join-Path $scriptRoot "BuildCore-Setup.iss"
$releaseDir = Join-Path $projectRoot "bin\releases"
$installerPath = Join-Path $releaseDir "BuildCore-1.0.0-Setup.exe"

if (-not (Test-Path $publishDir)) {
    throw "Publish directory not found: $publishDir"
}

if (-not (Test-Path (Join-Path $publishDir "BuildCore.exe"))) {
    throw "BuildCore.exe was not found in the publish directory. Publish BuildCore first."
}

if (-not (Test-Path (Join-Path $projectRoot "Assets\BuildCore.ico"))) {
    throw "BuildCore.ico was not found at $projectRoot\Assets\BuildCore.ico"
}

if (-not (Test-Path $installerScript)) {
    throw "Installer script not found: $installerScript"
}

$compilerCandidates = @(
    (Join-Path $env:ProgramFiles "Inno Setup 7\ISCC.exe"),
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 7\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe")
)

$compiler = $compilerCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $compiler) {
    throw "Inno Setup compiler (ISCC.exe) was not found. Install Inno Setup 7 and run this script again."
}

New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null

if (Test-Path $installerPath) {
    Remove-Item $installerPath -Force
}

& $compiler $installerScript

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path $installerPath)) {
    throw "Installer was not created: $installerPath"
}

Write-Host ""
Write-Host "BuildCore 1.0.0 installer created:"
Write-Host $installerPath
Write-Host ""