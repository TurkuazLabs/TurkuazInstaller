# 📄 Dosya Yolu: /tools/New-CombinedDistribution.ps1
# 📌 Amac: NativeAOT bootstrap ve self-contained WinUI publish ciktilarini tek dagitim klasorunde birlestirir
# 📌 Modul - Tool PowerShell
# Version: 1.0.1
# Aciklama: Bootstrap'i distribution rootuna, WinUI dosyalarini app altina kopyalar ve zorunlu executable layoutunu dogrular
# Bagimli Oldugu Katman: Tool | Config

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BootstrapRoot,

    [Parameter(Mandatory = $true)]
    [string]$WinUiRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$BootstrapExecutableName = "TurkuazInstaller.Bootstrapper.exe"
$DesktopExecutableName = "TurkuazInstaller.WinUI.exe"
$AppDirectoryName = "app"

$resolvedBootstrap = (Resolve-Path -LiteralPath $BootstrapRoot).Path
$resolvedWinUi = (Resolve-Path -LiteralPath $WinUiRoot).Path
$outputFullPath = [System.IO.Path]::GetFullPath($OutputRoot)
$appRoot = Join-Path -Path $outputFullPath -ChildPath $AppDirectoryName

if (Test-Path -LiteralPath $outputFullPath) {
    Remove-Item -LiteralPath $outputFullPath -Recurse -Force
}

New-Item -ItemType Directory -Path $outputFullPath -Force | Out-Null
New-Item -ItemType Directory -Path $appRoot -Force | Out-Null

Get-ChildItem -LiteralPath $resolvedBootstrap -Force |
    ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $outputFullPath -Recurse -Force
    }

Get-ChildItem -LiteralPath $resolvedWinUi -Force |
    ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $appRoot -Recurse -Force
    }

$bootstrapPath = Join-Path -Path $outputFullPath -ChildPath $BootstrapExecutableName
$desktopPath = Join-Path -Path $appRoot -ChildPath $DesktopExecutableName

if (-not (Test-Path -LiteralPath $bootstrapPath)) {
    throw "Combined distribution bootstrap executable is missing."
}

if (-not (Test-Path -LiteralPath $desktopPath)) {
    throw "Combined distribution WinUI executable is missing."
}

Write-Host ("Combined distribution ready: {0}" -f $outputFullPath)
