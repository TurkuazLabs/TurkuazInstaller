# 📄 Dosya Yolu: /tools/New-ReleaseChecksums.ps1
# 📌 Amac: Release asset dosyalari icin sirali SHA-256 checksum manifesti uretir
# 📌 Modul - Tool PowerShell
# Version: 1.0.0
# Aciklama: Son kullanicinin yayinlanan ZIP dosyalarini release disinda da dogrulayabilmesi icin SHA256SUMS.txt olusturur
# Bagimli Oldugu Katman: Tool

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AssetDirectory,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path -LiteralPath $AssetDirectory).Path
$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)

$lines = Get-ChildItem -LiteralPath $root -File |
    Where-Object { $_.FullName -ne $outputFullPath } |
    Sort-Object Name |
    ForEach-Object {
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "{0} *{1}" -f $hash, $_.Name
    }

$parent = Split-Path -Parent $outputFullPath

if (-not [string]::IsNullOrWhiteSpace($parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}

[System.IO.File]::WriteAllLines(
    $outputFullPath,
    $lines,
    [System.Text.UTF8Encoding]::new($false)
)

Write-Host ("Checksum manifest created: {0}" -f $OutputPath)
