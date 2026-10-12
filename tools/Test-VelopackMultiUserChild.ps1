# 📄 Dosya Yolu: /tools/Test-VelopackMultiUserChild.ps1
# 📌 Amac: Iki farkli Windows kullanicisinda Velopack kurulum veri ve uninstall izolasyonunu dogrular
# 📌 Modul - Tool PowerShell
# Version: 1.0.0
# Aciklama: Kurulum, kontrol, kaldirma ve diger hesapta yokluk asamalari paylasilan Tool ile calisir
# Bagimli Oldugu Katman: Tool | Config | CI

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [ValidateSet("Absent", "Install", "Verify", "Uninstall")] [string]$Phase,
    [Parameter(Mandatory = $true)] [ValidatePattern("^[a-f0-9]{16,32}$")] [string]$RunId,
    [Parameter(Mandatory = $true)] [string]$PackagePath,
    [Parameter(Mandatory = $true)] [string]$ConfigPath,
    [Parameter(Mandatory = $true)] [string]$ReportPath,
    [Parameter(Mandatory = $true)] [string]$ExpectedSid
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "Initialize-MsiLabStandardUserProfile.ps1")
. (Join-Path $PSScriptRoot "Invoke-MsiLabVelopackProcess.ps1")
$userRoot = Initialize-MsiLabStandardUserProfile -ExpectedSid $ExpectedSid
$config = Import-PowerShellDataFile -LiteralPath $ConfigPath
$installBase = Join-Path $userRoot "Programs"
$productName = "$($config.MsiProbeInstallPrefix)-multi-$RunId"
$target = Join-Path $installBase $productName
$updater = Join-Path $target "Update.exe"
$app = Join-Path $target "current/$($config.MainExecutable)"
$version = Join-Path $target "current/$($config.VersionMarkerFile)"
$dataDirectory = Join-Path $userRoot (Join-Path $productName $config.UserDataDirectory)
$dataMarker = Join-Path $dataDirectory $config.UserDataMarkerFile
$markerContent = "$RunId|$ExpectedSid"
$setup = [IO.Path]::GetFullPath($PackagePath)

if ($Phase -eq "Absent") {
    if (Test-Path -LiteralPath $target -PathType Container -or
        Test-Path -LiteralPath $dataMarker -PathType Leaf) {
        throw "This user already has another user's isolated installation/data."
    }
}
elseif ($Phase -eq "Install") {
    if (Test-Path -LiteralPath $target) {
        throw "Multi-user installation target already exists."
    }
    Invoke-MsiLabVelopackProcess -File $setup -Operation "multiuser-install" -Arguments @(
        "--silent", "--installto", $target
    )
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ((-not (Test-Path -LiteralPath $updater -PathType Leaf) -or
        -not (Test-Path -LiteralPath $app -PathType Leaf)) -and
        [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 300
    }
    if (-not (Test-Path -LiteralPath $updater -PathType Leaf) -or
        -not (Test-Path -LiteralPath $app -PathType Leaf) -or
        -not (Test-Path -LiteralPath $version -PathType Leaf)) {
        throw "Multi-user fixture installation incomplete."
    }
    if ((Get-Content -LiteralPath $version -Raw).Trim() -cne $config.VersionOne) {
        throw "Multi-user fixture version marker differs from signed package."
    }
    New-Item -Path $dataDirectory -ItemType Directory -Force | Out-Null
    [IO.File]::WriteAllText($dataMarker, $markerContent)
}
elseif ($Phase -eq "Verify" -or $Phase -eq "Uninstall") {
    if (-not (Test-Path -LiteralPath $updater -PathType Leaf) -or
        -not (Test-Path -LiteralPath $app -PathType Leaf) -or
        -not (Test-Path -LiteralPath $version -PathType Leaf)) {
        throw "Expected per-user install disappeared after another account's operation."
    }
    if ((Get-Content -LiteralPath $version -Raw).Trim() -cne $config.VersionOne) {
        throw "Another user's install/update unexpectedly changed this installation."
    }
    if (-not (Test-Path -LiteralPath $dataMarker -PathType Leaf) -or
        [IO.File]::ReadAllText($dataMarker) -cne $markerContent) {
        throw "Per-user data marker was missing or changed by another account."
    }

    if ($Phase -eq "Uninstall") {
        Invoke-MsiLabVelopackProcess -File $updater -Operation "multiuser-uninstall" -Arguments @(
            "--silent", "--rootDir", $target, "uninstall"
        )
        $deadline = [DateTime]::UtcNow.AddSeconds(30)
        while (((Test-Path -LiteralPath $updater -PathType Leaf) -or
            (Test-Path -LiteralPath (Join-Path $target "current") -PathType Container)) -and
            [DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Milliseconds 300
        }
        if ((Test-Path -LiteralPath $updater -PathType Leaf) -or
            (Test-Path -LiteralPath (Join-Path $target "current") -PathType Container)) {
            throw "Multi-user uninstall left application files."
        }
        if (-not (Test-Path -LiteralPath $dataMarker -PathType Leaf) -or
            [IO.File]::ReadAllText($dataMarker) -cne $markerContent) {
            throw "Uninstall removed or modified the separate per-user data marker."
        }
    }
}
else {
    throw "Unrecognized multi-user lifecycle phase."
}

[IO.File]::WriteAllText($ReportPath, "INSTALLER_MULTIUSER_$($Phase.ToUpperInvariant())_OK")
