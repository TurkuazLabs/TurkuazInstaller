# 📄 Dosya Yolu: /tools/New-CombinedDistribution.ps1
# 📌 Amac: NativeAOT bootstrap, self-contained WinUI ve CLI publish ciktilarini tek dagitim klasorunde birlestirir
# 📌 Modul - Tool PowerShell
# Version: 1.1.0
# Aciklama: Bootstrap'i roota, WinUI'yi app altina, CLI'yi cli altina kopyalar ve zorunlu executable layoutunu dogrular
# Bagimli Oldugu Katman: Tool | Config

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BootstrapRoot,

    [Parameter(Mandatory = $true)]
    [string]$WinUiRoot,

    [Parameter(Mandatory = $true)]
    [string]$CliRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$BootstrapExecutableName = "TurkuazInstaller.Bootstrapper.exe"
$DesktopExecutableName = "TurkuazInstaller.WinUI.exe"
$CliExecutableName = "TurkuazInstaller.Cli.exe"
$AppDirectoryName = "app"
$CliDirectoryName = "cli"

$resolvedBootstrap = (Resolve-Path -LiteralPath $BootstrapRoot).Path
$resolvedWinUi = (Resolve-Path -LiteralPath $WinUiRoot).Path
$resolvedCli = (Resolve-Path -LiteralPath $CliRoot).Path
$outputFullPath = [System.IO.Path]::GetFullPath($OutputRoot)
$appRoot = Join-Path -Path $outputFullPath -ChildPath $AppDirectoryName
$cliRoot = Join-Path -Path $outputFullPath -ChildPath $CliDirectoryName

if (Test-Path -LiteralPath $outputFullPath) {
    Remove-Item -LiteralPath $outputFullPath -Recurse -Force
}

New-Item -ItemType Directory -Path $outputFullPath -Force | Out-Null
New-Item -ItemType Directory -Path $appRoot -Force | Out-Null
New-Item -ItemType Directory -Path $cliRoot -Force | Out-Null

Get-ChildItem -LiteralPath $resolvedBootstrap -Force |
    ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $outputFullPath -Recurse -Force
    }

Get-ChildItem -LiteralPath $resolvedWinUi -Force |
    ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $appRoot -Recurse -Force
    }

Get-ChildItem -LiteralPath $resolvedCli -Force |
    ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $cliRoot -Recurse -Force
    }

$bootstrapPath = Join-Path -Path $outputFullPath -ChildPath $BootstrapExecutableName
$desktopPath = Join-Path -Path $appRoot -ChildPath $DesktopExecutableName
$cliPath = Join-Path -Path $cliRoot -ChildPath $CliExecutableName

if (-not (Test-Path -LiteralPath $bootstrapPath)) {
    throw "Combined distribution bootstrap executable is missing."
}

if (-not (Test-Path -LiteralPath $desktopPath)) {
    throw "Combined distribution WinUI executable is missing."
}

if (-not (Test-Path -LiteralPath $cliPath)) {
    throw "Combined distribution CLI executable is missing."
}

Write-Host ("Combined distribution ready: {0}" -f $outputFullPath)
