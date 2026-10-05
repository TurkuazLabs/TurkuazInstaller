# 📄 Dosya Yolu: /tools/Invoke-VelopackE2E.ps1
# 📌 Amac: Gercek vpk toolu ile iki Velopack release uretir ve TurkuazInstaller E2E runnerini calistirir
# 📌 Modul - Tool PowerShell
# Version: 1.0.2
# Aciklama: Local tool install, fixture publish, v1/v2 package uretimi ve install-update-repair-rollback-uninstall zincirini tek kalite kapisinda koordine eder
# Bagimli Oldugu Katman: Tool | Config

[CmdletBinding()]
param(
    [string]$ConfigPath = "config/velopack-e2e.psd1",
    [string]$ArtifactsRoot = "artifacts/velopack-e2e"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$configFullPath = (Resolve-Path -LiteralPath $ConfigPath).Path
$config = Import-PowerShellDataFile -LiteralPath $configFullPath

$root = [System.IO.Path]::GetFullPath($ArtifactsRoot)
$toolRoot = Join-Path -Path $root -ChildPath "tools"
$payloadRoot = Join-Path -Path $root -ChildPath "payload"
$releaseRoot = Join-Path -Path $root -ChildPath "releases"
$inputRoot = Join-Path -Path $root -ChildPath "inputs"
$installRoot = Join-Path -Path $root -ChildPath "install"
$stagingRoot = Join-Path -Path $root -ChildPath "staging"
$velopackTemp = Join-Path -Path $root -ChildPath "velopack-temp"

if (Test-Path -LiteralPath $root) {
    Remove-Item -LiteralPath $root -Recurse -Force
}

New-Item -ItemType Directory -Path $toolRoot -Force | Out-Null
New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
New-Item -ItemType Directory -Path $inputRoot -Force | Out-Null
New-Item -ItemType Directory -Path $velopackTemp -Force | Out-Null

dotnet tool install vpk --tool-path $toolRoot --version $config.VpkVersion

if ($LASTEXITCODE -ne 0) {
    throw "vpk tool installation failed."
}

$vpkPath = Join-Path -Path $toolRoot -ChildPath "vpk.exe"

if (-not (Test-Path -LiteralPath $vpkPath)) {
    throw "vpk executable was not installed."
}

dotnet publish tests/TurkuazInstaller.Velopack.E2E.Fixture/TurkuazInstaller.Velopack.E2E.Fixture.csproj --configuration Release --runtime win-x64 --self-contained false --output $payloadRoot

if ($LASTEXITCODE -ne 0) {
    throw "Velopack E2E fixture publish failed."
}

$markerPath = Join-Path -Path $payloadRoot -ChildPath $config.VersionMarkerFile
$env:VELOPACK_TEMP = $velopackTemp

function Invoke-Pack {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    Set-Content -LiteralPath $markerPath -Value $Version -NoNewline -Encoding utf8

    $packOutput = & $vpkPath --legacyConsole true --yes true --skip-updates true --verbose true pack --runtime win-x64 --packId $config.PackId --packTitle $config.PackTitle --packVersion $Version --packDir $payloadRoot --mainExe $config.MainExecutable --outputDir $releaseRoot --delta none --skipVeloAppCheck true 2>&1
    $packExitCode = $LASTEXITCODE

    $packOutput |
        ForEach-Object {
            Write-Host $_
        }

    if ($packExitCode -ne 0) {
        throw ("vpk pack failed for version {0}." -f $Version)
    }

    $setup = Get-ChildItem -LiteralPath $releaseRoot -File |
        Where-Object { $_.Name -like "*Setup.exe" } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    $full = Get-ChildItem -LiteralPath $releaseRoot -File |
        Where-Object {
            $_.Name -like "*-full.nupkg" -and
            $_.Name.Contains($Version, [System.StringComparison]::OrdinalIgnoreCase)
        } |
        Select-Object -First 1

    if ($null -eq $setup) {
        throw ("Velopack Setup.exe was not produced for version {0}." -f $Version)
    }

    if ($null -eq $full) {
        throw ("Velopack full nupkg was not produced for version {0}." -f $Version)
    }

    return [pscustomobject]@{
        Setup = $setup.FullName
        Full = $full.FullName
    }
}

$v1 = Invoke-Pack -Version $config.VersionOne

$v1Setup = Join-Path -Path $inputRoot -ChildPath "v1-Setup.exe"
$v1Full = Join-Path -Path $inputRoot -ChildPath "v1-full.nupkg"

Copy-Item -LiteralPath $v1.Setup -Destination $v1Setup -Force
Copy-Item -LiteralPath $v1.Full -Destination $v1Full -Force

$v2 = Invoke-Pack -Version $config.VersionTwo
$v2Full = Join-Path -Path $inputRoot -ChildPath "v2-full.nupkg"

Copy-Item -LiteralPath $v2.Full -Destination $v2Full -Force

dotnet run --project tests/TurkuazInstaller.Velopack.E2E.Runner/TurkuazInstaller.Velopack.E2E.Runner.csproj --configuration Release -- $v1Setup $v1Full $v2Full $installRoot $stagingRoot $config.VersionMarkerFile

if ($LASTEXITCODE -ne 0) {
    throw "TurkuazInstaller real Velopack E2E runner failed."
}

Write-Host "TURKUAZ_INSTALLER_REAL_VELOPACK_E2E_OK"
