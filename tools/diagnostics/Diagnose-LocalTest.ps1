# 📄 Dosya Yolu: /tools/diagnostics/Diagnose-LocalTest.ps1
# 📌 Amac: Imzasiz TurkuazInstaller ZIP dosyasinin dogru acilip acilmadigini uygulama calistirmadan dogrulamak
# 📌 Modul - Tool PowerShell
# Version: 0.1.1
# Aciklama: Sadece test paketi dosya/Windows kontrolu; izinlari degistirmez, AV/SmartScreen atlatmaz, ag kullanmaz
# Bagimli Oldugu Katman: Tool | CI | Diagnostics

[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$paths = @(
    'TurkuazInstaller.Bootstrapper.exe',
    'app/TurkuazInstaller.WinUI.exe',
    'cli/TurkuazInstaller.Cli.exe'
)
foreach ($item in $paths) {
    $file = Join-Path $root $item
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "LOCAL_TEST_LAYOUT_ERROR: Missing $item. You may have extracted only the outer GitHub artifact ZIP."
    }
    $info = Get-Item -LiteralPath $file
    if ($info.Length -lt 1024) { throw "LOCAL_TEST_LAYOUT_ERROR: Empty/truncated $item" }
    Write-Host "LOCAL_TEST_FILE_PRESENT $item ($($info.Length) bytes)"
}
$winuiFiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'app') -File -ErrorAction Stop)
if ($winuiFiles.Count -lt 10) {
    throw 'LOCAL_TEST_LAYOUT_ERROR: app/ native WinUI dependencies appear incomplete.'
}
if ([Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
    Write-Host 'LOCAL_TEST_LAYOUT_ONLY: Windows x64 is required to launch.'
    exit 0
}
$osVersion = [Environment]::OSVersion.Version
$architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
Write-Host ("LOCAL_TEST_ENVIRONMENT WindowsVersion={0} Architecture={1}" -f $osVersion, $architecture)
if ($osVersion.Major -ne 10 -or $osVersion.Build -lt 17763) {
    throw 'LOCAL_TEST_OS_UNSUPPORTED: Requires Windows 10 build 17763 or newer.'
}
if ($architecture -ne [System.Runtime.InteropServices.Architecture]::X64) {
    throw 'LOCAL_TEST_ARCH_UNSUPPORTED: This package is Windows x64 only.'
}
$bootstrap = Join-Path $root 'TurkuazInstaller.Bootstrapper.exe'
$auth = Get-AuthenticodeSignature -LiteralPath $bootstrap
Write-Host "LOCAL_TEST_BOOTSTRAP_SIGNATURE_STATUS $($auth.Status)"
Write-Host 'LOCAL_TEST_LAYOUT_OK'
Write-Host 'Launch only in a disposable Windows test VM, by double-clicking TurkuazInstaller.Bootstrapper.exe from this fully extracted folder.'
Write-Host 'Do not disable SmartScreen, antivirus, execution policy or security warnings to run this unsigned test build.'
Write-Host 'If there is no window, inspect Event Viewer > Windows Logs > Application for a TurkuazInstaller.WinUI.exe failure.'
