# 📄 Dosya Yolu: /tools/Assert-AuthenticodeRelease.ps1
# 📌 Amac: Release dizinindeki TurkuazInstaller executable ve assembly dosyalarinin Authenticode imzalarini SignTool ile dogrular
# 📌 Modul - Tool PowerShell
# Version: 1.0.0
# Aciklama: Default Authentication Policy ve timestamp warning kontrolu ile imzasiz veya gecersiz release binary'sini reddeder
# Bagimli Oldugu Katman: Tool

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$windowsKitsRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"

$signTool = Get-ChildItem -Path $windowsKitsRoot -Filter "signtool.exe" -File -Recurse |
    Where-Object { $_.DirectoryName -match "\\x64$" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if ($null -eq $signTool) {
    throw "SignTool.exe could not be found in the Windows SDK."
}

$files = Get-ChildItem -LiteralPath $ReleaseRoot -File -Recurse |
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
