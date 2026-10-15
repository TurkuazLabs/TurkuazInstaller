# 📄 Dosya Yolu: /tools/Test-WindowsPilotPreflight.ps1
# 📌 Amac: Windows combined distribution dosyalarini kurulum yapmadan once dogrular
# 📌 Modul - Tool PowerShell
# Version: 1.0.1
# Aciklama: Sadece metadata, PE architecture, SHA-256 ve Authenticode okur; installer calistirmaz
# Bagimli Oldugu Katman: Tool | Config

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DistributionRoot,
    [Parameter(Mandatory = $true)]
    [ValidateSet("x64", "arm64")]
    [string]$Architecture,
    [switch]$RequireSigned
)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw "Windows pilot preflight must run on Windows."
}
$minimumWindowsBuild = [version]"10.0.17763.0"
$observedOS = [Environment]::OSVersion.Version
if ($observedOS -lt $minimumWindowsBuild) {
    throw "Windows build $observedOS is below minimum $minimumWindowsBuild."
}
$root = [IO.Path]::GetFullPath($DistributionRoot)
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw "Distribution root does not exist: $root"
}

function Get-PeArchitecture {
    param([Parameter(Mandatory = $true)][string]$Path)
    $stream = [IO.File]::Open(
        $Path, [IO.FileMode]::Open, [IO.FileAccess]::Read,
        [IO.FileShare]::Read)
    try {
        $reader = [IO.BinaryReader]::new($stream)
        if ($stream.Length -lt 0x40 -or $reader.ReadUInt16() -ne 0x5A4D) {
            throw "File is not a valid PE executable: $Path"
        }
        $stream.Position = 0x3c
        $peOffset = $reader.ReadInt32()
        if ($peOffset -lt 0x40 -or $peOffset -gt $stream.Length - 6) {
            throw "Invalid PE header offset: $Path"
        }
        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550) {
            throw "Invalid PE signature: $Path"
        }
        $machine = $reader.ReadUInt16()
        switch ($machine) {
            0x8664 { return "x64" }
            0xAA64 { return "arm64" }
            default {
                throw ("Unsupported PE machine 0x{0:X4}: {1}" -f $machine, $Path)
            }
        }
    }
    finally {
        $stream.Dispose()
    }
}

$requiredExecutables = @(
    "TurkuazInstaller.Bootstrapper.exe",
    "app/TurkuazInstaller.WinUI.exe",
    "cli/TurkuazInstaller.Cli.exe"
)
$results = @(
    foreach ($relativePath in $requiredExecutables) {
        $path = Join-Path $root $relativePath
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Combined distribution is missing required executable: $relativePath"
        }
        $file = Get-Item -LiteralPath $path
        if ($file.Length -le 0) {
            throw "Combined distribution contains empty executable: $relativePath"
        }
        $machine = Get-PeArchitecture -Path $path
        if ($machine -ne $Architecture) {
            throw ("Wrong PE architecture for {0}: {1} (expected {2})" -f $relativePath, $machine, $Architecture)
        }
        $signature = Get-AuthenticodeSignature -LiteralPath $path
        $signatureStatus = $signature.Status.ToString()
        if ($RequireSigned -and $signatureStatus -ne "Valid") {
            throw ("Required Authenticode signature invalid for {0}: {1}" -f $relativePath, $signatureStatus)
        }
        [pscustomobject]@{
            path = $relativePath
            size_bytes = $file.Length
            pe_architecture = $machine
            sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
            authenticode_status = $signatureStatus
        }
    }
)
[pscustomobject]@{
    schema_version = 1
    diagnostic_only = $true
    executed_installer = $false
    production_acceptance = $false
    distribution_root = $root
    windows_version = $observedOS.ToString()
    expected_architecture = $Architecture
    require_signed = [bool]$RequireSigned
    executables = $results
} | ConvertTo-Json -Depth 6
