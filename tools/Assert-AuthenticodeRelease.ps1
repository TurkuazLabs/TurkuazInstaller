# 📄 Dosya Yolu: /tools/Assert-AuthenticodeRelease.ps1
# 📌 Amac: Release dizinindeki TurkuazInstaller executable ve assembly dosyalarinin Authenticode imzalarini SignTool ile dogrular
# 📌 Modul - Tool PowerShell
# Version: 1.1.0
# Aciklama: Default Authentication Policy ve timestamp warning kontrolu ile imzasiz veya gecersiz release binary'sini reddeder
# Bagimli Oldugu Katman: Tool

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseRoot,
    [switch]$RequireCombinedLayout
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ReleaseRoot -PathType Container)) {
    throw "ReleaseRoot must be an existing directory."
}
$root = (Resolve-Path -LiteralPath $ReleaseRoot).ProviderPath

# @dev-id TINS-SIGN-010
# @purpose Require all three combined-distribution executables before trusting any release.
# @reason SignTool could previously pass when only one matching signed binary existed.
# @contract Production must request this guard for each architecture; signing probe remains compatible.
# @tests tests/scripts/Test-AuthenticodeReleaseGuard.ps1
if ($RequireCombinedLayout) {
    $requiredExecutables = @(
        'TurkuazInstaller.Bootstrapper.exe',
        'app/TurkuazInstaller.WinUI.exe',
        'cli/TurkuazInstaller.Cli.exe'
    )
    foreach ($relativePath in $requiredExecutables) {
        $fullPath = Join-Path -Path $root -ChildPath $relativePath
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "Required combined release executable missing: $relativePath"
        }
        $file = Get-Item -LiteralPath $fullPath -Force
        if ($file.Length -le 0) {
            throw "Required combined release executable is empty: $relativePath"
        }
        foreach ($candidate in @($root, (Split-Path -Parent $fullPath), $fullPath)) {
            $info = Get-Item -LiteralPath $candidate -Force
            if (($info.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Reparse point is not accepted for release executable: $relativePath"
            }
        }
    }
}

$windowsKitsRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"

$signTool = Get-ChildItem -Path $windowsKitsRoot -Filter "signtool.exe" -File -Recurse |
    Where-Object { $_.DirectoryName -match "\\x64$" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if ($null -eq $signTool) {
    throw "SignTool.exe could not be found in the Windows SDK."
}

$files = Get-ChildItem -LiteralPath $root -File -Recurse |
    Where-Object {
        $_.Name -like "TurkuazInstaller*.exe" -or
        $_.Name -like "TurkuazInstaller*.dll"
    } |
    Sort-Object FullName

if ($files.Count -eq 0) {
    throw "No TurkuazInstaller binary was found for Authenticode verification."
}

foreach ($file in $files) {
    & $signTool.FullName verify /pa /v /tw $file.FullName

    if ($LASTEXITCODE -ne 0) {
        throw ("Authenticode verification failed: {0}" -f $file.FullName)
    }
}

Write-Host ("Authenticode verification succeeded for {0} files." -f $files.Count)
