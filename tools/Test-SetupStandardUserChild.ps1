# 📄 Dosya Yolu: /tools/Test-SetupStandardUserChild.ps1
# 📌 Amac: Standart Windows hesabi ile Velopack Setup.exe per-user kurulumu ve kaldirmayi dogrular
# 📌 Modul - Tool PowerShell
# Version: 1.2.0
# Aciklama: MSI'yi reddeden sunucu ilkesinden bagimsiz gercek user-scope Setup kabul testi
# Bagimli Oldugu Katman: Tool | Config | CI

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$PackagePath,
    [Parameter(Mandatory = $true)] [string]$ConfigPath,
    [Parameter(Mandatory = $true)] [string]$ReportPath,
    [Parameter(Mandatory = $true)] [string]$ExpectedSid
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [System.Security.Principal.WindowsPrincipal]::new($identity)
if (-not $IsWindows -or $identity.User.Value -cne $ExpectedSid -or
    $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Velopack Setup child requires the intended non-admin Windows token."
}

$config = Import-PowerShellDataFile -LiteralPath $ConfigPath
. (Join-Path $PSScriptRoot "Initialize-MsiLabStandardUserProfile.ps1")
$userRoot = Initialize-MsiLabStandardUserProfile -ExpectedSid $ExpectedSid
if ([string]::IsNullOrWhiteSpace($userRoot)) {
    throw "Windows standard-user LocalAppData not initialized."
}
$target = Join-Path (Join-Path $userRoot "Programs") (
    "$($config.MsiProbeInstallPrefix)-setup-" + [guid]::NewGuid().ToString("N"))
$setup = [IO.Path]::GetFullPath($PackagePath)
if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) {
    throw "Non-admin Setup test EXE is missing."
}

. (Join-Path $PSScriptRoot "Invoke-MsiLabVelopackProcess.ps1")

$passed = $false
$update = Join-Path $target "Update.exe"
try {
    Invoke-MsiLabVelopackProcess -File $setup -Operation "install" -Arguments @(
        "--silent", "--installto", $target
    )
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    $app = Join-Path $target "current/$($config.MainExecutable)"
    $marker = Join-Path $target "current/$($config.VersionMarkerFile)"
    while ((-not (Test-Path -LiteralPath $update -PathType Leaf) -or
        -not (Test-Path -LiteralPath $app -PathType Leaf)) -and
        [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 300
    }
    if (-not (Test-Path -LiteralPath $update -PathType Leaf) -or
        -not (Test-Path -LiteralPath $app -PathType Leaf) -or
        -not (Test-Path -LiteralPath $marker -PathType Leaf)) {
        throw "Non-admin Setup did not create complete per-user application files."
    }
    if ((Get-Content -LiteralPath $marker -Raw).Trim() -cne $config.VersionOne) {
        throw "Non-admin Setup version marker mismatch."
    }

    $passed = $true
}
finally {
    if (Test-Path -LiteralPath $update -PathType Leaf) {
        try {
            Invoke-MsiLabVelopackProcess -File $update -Operation "uninstall" -Arguments @(
                "--silent", "--rootDir", $target, "uninstall"
            )
        }
        catch {
            $passed = $false
            Write-Warning "Non-admin Setup uninstall failed: $_"
        }
    }
    else {
        $passed = $false
    }
}

$until = [DateTime]::UtcNow.AddSeconds(30)
while (((Test-Path -LiteralPath (Join-Path $target "current")) -or
    (Test-Path -LiteralPath $update)) -and [DateTime]::UtcNow -lt $until) {
    Start-Sleep -Milliseconds 300
}
if ((Test-Path -LiteralPath $update) -or
    (Test-Path -LiteralPath (Join-Path $target "current"))) {
    $passed = $false
}

if (-not $passed) {
    throw "Non-admin Velopack Setup lifecycle failed."
}
[IO.File]::WriteAllText(
    $ReportPath, "INSTALLER_SETUP_NONADMIN_PERUSER_LIFECYCLE_OK")
