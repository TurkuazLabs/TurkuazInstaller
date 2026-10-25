# 📄 Dosya Yolu: /tools/New-CombinedDistribution.ps1
# 📌 Amac: NativeAOT bootstrap, self-contained WinUI ve CLI publish ciktilarini tek dagitim klasorunde birlestirir
# 📌 Modul - Tool PowerShell
# Version: 1.2.0
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
    [string]$OutputRoot,

    # Optional: a package ALREADY signed by the reviewed theme publisher.
    # .tpe carries a separately signed .tpk inside the AES-GCM envelope.
    [string]$ThemePackPath
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
$themeSource = $null
$themeName = $null
if (-not [string]::IsNullOrWhiteSpace($ThemePackPath)) {
    $themeSource = Get-Item -LiteralPath $ThemePackPath -ErrorAction Stop
    if ($themeSource.PSIsContainer -or
        (($themeSource.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
        throw 'Theme input must be a regular, non-symlink file.'
    }

    $themeName = $themeSource.Name
    if ($themeName -cne 'theme.tpk' -and $themeName -cne 'theme.tpe') {
        throw 'Signed/encrypted theme input must be named theme.tpk or theme.tpe.'
    }

    $limit = if ($themeName -ceq 'theme.tpe') { 83886208L } else { 83886080L }
    if ($themeSource.Length -lt 32 -or $themeSource.Length -gt $limit) {
        throw 'Theme file is empty/truncated or exceeds its bounded runtime limit.'
    }

    $existingTheme = Join-Path $resolvedWinUi $themeName
    if (Test-Path -LiteralPath $existingTheme) {
        throw 'Theme file collision: WinUI publish already has a same-named theme.'
    }
}
$themeHash = if ($null -ne $themeSource) {
    (Get-FileHash -LiteralPath $themeSource.FullName -Algorithm SHA256).Hash
} else { $null }

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

if ($null -ne $themeSource) {
    $destination = Join-Path -Path $appRoot -ChildPath $themeName
    if (Test-Path -LiteralPath $destination) {
        throw 'Refusing to overwrite an existing theme in combined distribution.'
    }
    Copy-Item -LiteralPath $themeSource.FullName -Destination $destination -ErrorAction Stop
    $copiedHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    if ($copiedHash -cne $themeHash) {
        throw 'Copied theme differs from the signed/encrypted input.'
    }
    Write-Host ("THEME_SIDECAR_INCLUDED {0} sha256={1}" -f $themeName, $copiedHash)
    Write-Host 'THEME_SIDECAR_NOT_AUTHORIZED: runtime must validate publisher CMS, product and release identity.'
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
