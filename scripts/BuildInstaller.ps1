param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$publishScript = Join-Path $PSScriptRoot "Publish.ps1"
$publishDirectory = Join-Path $repositoryRoot "artifacts\GlanceMD-win-x64"
$installerDirectory = Join-Path $repositoryRoot "artifacts\installer"
$installerScript = Join-Path $repositoryRoot "installer\GlanceMD.iss"
$installerIcon = Join-Path $repositoryRoot "src\GlanceMD.App\Assets\AppIcon.ico"

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must use major.minor.patch format, for example 1.0.0."
}

& $publishScript -Architecture x64
if ($LASTEXITCODE -ne 0) {
    throw "Application publishing failed with exit code $LASTEXITCODE."
}

$compilerCandidates = @(
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

$compiler = $compilerCandidates | Select-Object -First 1
if (-not $compiler) {
    throw "Inno Setup 6 was not found. Install it with: winget install --id JRSoftware.InnoSetup --exact"
}

New-Item -ItemType Directory -Path $installerDirectory -Force | Out-Null

& $compiler `
    "/DSourceDir=$publishDirectory" `
    "/DOutputDir=$installerDirectory" `
    "/DMyAppVersion=$Version" `
    "/DInstallerIcon=$installerIcon" `
    $installerScript

if ($LASTEXITCODE -ne 0) {
    throw "Installer compilation failed with exit code $LASTEXITCODE."
}

$installerPath = Join-Path $installerDirectory "GlanceMD-Setup-x64.exe"
$hash = Get-FileHash -LiteralPath $installerPath -Algorithm SHA256
$hashLine = "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($installerPath))"
Set-Content -LiteralPath "$installerPath.sha256" -Value $hashLine -Encoding ascii

Write-Host "Installer: $installerPath"
Write-Host "SHA-256:  $($hash.Hash)"
