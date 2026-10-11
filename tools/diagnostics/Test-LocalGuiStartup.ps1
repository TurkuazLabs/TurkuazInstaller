# 📄 Dosya Yolu: /tools/diagnostics/Test-LocalGuiStartup.ps1
# 📌 Amac: Gercek unsigned Installer bootstrap ile WinUI penceresini izole Windows CI icinde kontrol eder
# 📌 Modul - Tool PowerShell
# Version: 0.1.0
# Aciklama: Kullanici kurulumunu degistirmez; CI disposable Windows GUI smoke ve fault diagnostics
# Bagimli Oldugu Katman: CI | Diagnostics | Tool

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$DistributionRoot,
    [ValidateRange(5, 90)][int]$WindowWaitSeconds = 35
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or [string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
    throw 'Desktop GUI smoke requires disposable Windows Actions runner.'
}
$root = (Resolve-Path -LiteralPath $DistributionRoot).Path
$bootstrap = Join-Path $root 'TurkuazInstaller.Bootstrapper.exe'
$appPath = Join-Path $root 'app/TurkuazInstaller.WinUI.exe'
if (-not (Test-Path -LiteralPath $bootstrap -PathType Leaf) -or
    -not (Test-Path -LiteralPath $appPath -PathType Leaf)) {
    throw 'Combined distribution layout lacks bootstrap or WinUI EXE.'
}
$startup = Get-Date
$bootstrapProcess = $null
$uiProcess = $null
try {
    $bootstrapProcess = Start-Process -FilePath $bootstrap -WorkingDirectory $root -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds($WindowWaitSeconds)
    do {
        $candidate = @(Get-CimInstance Win32_Process -Filter "Name = 'TurkuazInstaller.WinUI.exe'" -ErrorAction Stop |
            Where-Object { $_.ExecutablePath -eq $appPath })
        foreach ($entry in $candidate) {
            $process = Get-Process -Id $entry.ProcessId -ErrorAction SilentlyContinue
            if ($null -ne $process) {
                $uiProcess = $process
                if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
                    Write-Host 'TURKUAZ_INSTALLER_REAL_WINUI_WINDOW_OK'
                    exit 0
                }
            }
        }
        if ($bootstrapProcess.HasExited -and $bootstrapProcess.ExitCode -ne 0) {
            throw "Bootstrapper failed before desktop window: exit=$($bootstrapProcess.ExitCode)"
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "WinUI failed to create any main window within $WindowWaitSeconds seconds."
}
finally {
    $log = Join-Path $env:LOCALAPPDATA 'TurkuazInstaller/logs/bootstrap-startup.log'
    if (Test-Path -LiteralPath $log) {
        Get-Content -LiteralPath $log -Tail 12 | ForEach-Object { Write-Host "BOOTSTRAP_LOCAL_LOG $_" }
    }
    if ($null -ne $uiProcess -and -not $uiProcess.HasExited) {
        Stop-Process -Id $uiProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if ($null -ne $bootstrapProcess -and -not $bootstrapProcess.HasExited) {
        Stop-Process -Id $bootstrapProcess.Id -Force -ErrorAction SilentlyContinue
    }
    # Only dump fault records for the test fixture, not the runner's
    # unrelated application events.
    try {
        Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=$startup.AddSeconds(-3); Level=2} -ErrorAction Stop |
            Where-Object { $_.Message -match 'TurkuazInstaller\.(?:WinUI|Bootstrapper)' } |
            Select-Object -First 6 |
            ForEach-Object { Write-Warning ("GUI_SMOKE_WINDOWS_FAULT event={0} msg={1}" -f $_.Id, ($_.Message -replace "\r?\n", ' ')) }
    }
    catch { }
}
