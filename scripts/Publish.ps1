param(
    [ValidateSet("x64", "arm64")]
    [string]$Architecture = "x64"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repositoryRoot "artifacts"
$publishDirectory = Join-Path $artifactRoot "GlanceMD-win-$Architecture"
$archivePath = Join-Path $artifactRoot "GlanceMD-win-$Architecture.zip"

$env:DOTNET_CLI_HOME = Join-Path $repositoryRoot ".dotnet-home"
$env:NUGET_PACKAGES = Join-Path $repositoryRoot ".packages"
$env:WINAPP_CLI_TELEMETRY_OPTOUT = "1"

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

dotnet publish (Join-Path $repositoryRoot "src/GlanceMD.App/GlanceMD.App.csproj") `
    -c Release `
    -r "win-$Architecture" `
    --self-contained true `
    -p:Platform=$Architecture `
    -p:WindowsPackageType=None `
    -p:WindowsAppSDKSelfContained=true `
    -p:PublishReadyToRun=false `
    -p:PublishTrimmed=false `
    -o $publishDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $archivePath -CompressionLevel Optimal
$hash = Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
$hashLine = "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($archivePath))"
Set-Content -LiteralPath "$archivePath.sha256" -Value $hashLine -Encoding ascii

Write-Host "Published: $publishDirectory"
Write-Host "Archive:   $archivePath"
Write-Host "SHA-256:   $($hash.Hash)"
