# Dosya Yolu: tools/diagnostics/Test-LocalGuiThemeRejection.ps1
# Amac: Gercek Windows WinUI oturumunda hatali theme.tpk seciminin acilisi fail-closed engelledigini dogrulamak
# Modul - PowerShell Windows GUI Smoke
# Version: 0.1.0-qa
# Aciklama: Yalniz disposable hosted runner, user profile ve fixture temizligi, production sertifikasi gerektirmez
# Bagimli Oldugu Katman: CI | Diagnostics | WinUI | Security

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$DistributionRoot,
    [ValidateRange(5, 90)][int]$WindowWaitSeconds = 35
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $IsWindows -or [string]::IsNullOrWhiteSpace($env:RUNNER_TEMP) -or
    [string]::IsNullOrWhiteSpace($env:GITHUB_ACTIONS)) {
    throw 'Invalid-theme Windows GUI smoke runs only on disposable GitHub Actions Windows runners.'
}

$root = (Resolve-Path -LiteralPath $DistributionRoot).Path
$appPath = Join-Path $root 'app/TurkuazInstaller.WinUI.exe'
$bootstrap = Join-Path $root 'TurkuazInstaller.Bootstrapper.exe'
$badTheme = Join-Path $root 'app/theme.tpk'
$configRoot = Join-Path $env:LOCALAPPDATA 'TurkuazInstaller/config'
$profile = Join-Path $configRoot 'ui-profile.yml'
$startupLog = Join-Path $env:LOCALAPPDATA 'TurkuazInstaller/logs/winui-startup.log'

if (-not (Test-Path -LiteralPath $appPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $bootstrap -PathType Leaf)) {
    throw 'Distribution is missing WinUI or bootstrap binaries.'
}
if (Test-Path -LiteralPath $badTheme) {
    throw 'Refusing to overwrite any existing theme.tpk.'
}

$profileWasPresent = Test-Path -LiteralPath $profile -PathType Leaf
$profileOriginal = if ($profileWasPresent) { [IO.File]::ReadAllBytes($profile) } else { $null }
$bootstrapProcess = $null
$uiProcess = $null

try {
    New-Item -ItemType Directory -Path $configRoot -Force | Out-Null
    # Profile intentionally selects a product for which no production theme
    # signing key is enrolled. A junk ZIP MUST be refused, never rendered.
    @"
schema_version: 1
culture: tr-TR
branding:
  window_title: TurkuazInstaller Theme Rejection QA
  theme_package_id: levelupgt.ragnar-classic-preview
"@ | Set-Content -LiteralPath $profile -Encoding utf8 -NoNewline
    [IO.File]::WriteAllBytes($badTheme, [byte[]](1..96))

    $earlier = @(
        Get-CimInstance Win32_Process -Filter "Name = 'TurkuazInstaller.WinUI.exe'" |
            Where-Object { $_.ExecutablePath -eq $appPath } |
            ForEach-Object { [int]$_.ProcessId }
    )
    $bootstrapProcess = Start-Process -FilePath $bootstrap -WorkingDirectory $root -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds($WindowWaitSeconds)
    $rejected = $false
    $seenPid = 0
    while ([DateTime]::UtcNow -lt $deadline) {
        $candidates = @(Get-CimInstance Win32_Process -Filter "Name = 'TurkuazInstaller.WinUI.exe'" |
            Where-Object {
                $_.ExecutablePath -eq $appPath -and
                $earlier -notcontains [int]$_.ProcessId
            })
        foreach ($entry in $candidates) {
            $uiProcess = Get-Process -Id $entry.ProcessId -ErrorAction SilentlyContinue
            $seenPid = [int]$entry.ProcessId
            if (-not (Test-Path -LiteralPath $startupLog -PathType Leaf)) { continue }
            $pidPattern = '(?:^|\s)pid=' + [regex]::Escape([string]$seenPid) + '(?:\s|$)'
            $lines = @(Get-Content -LiteralPath $startupLog -Tail 120 |
                Where-Object { $_ -match $pidPattern })
            if (@($lines | Where-Object {
                $_ -match '(?:^|\s)stage=StartupControllerCompleted(?:\s|$)' -or
                $_ -match '(?:^|\s)stage=MainWindowReady(?:\s|$)'
            }).Count -gt 0) {
                throw 'SECURITY REGRESSION: invalid theme allowed installer startup controller.'
            }
            if (@($lines | Where-Object {
                $_ -match '(?:^|\s)stage=SignedThemePackRejected(?:\s|$)'
            }).Count -gt 0) {
                $rejected = $true
                break
            }
        }
        if ($rejected) { break }
        if ($null -ne $bootstrapProcess -and $bootstrapProcess.HasExited -and
            $bootstrapProcess.ExitCode -ne 0) {
            throw 'Theme QA bootstrap exited unsuccessfully.'
        }
        Start-Sleep -Milliseconds 400
    }
    if (-not $rejected) {
        throw 'No SignedThemePackRejected event from actual Windows WinUI process.'
    }
    Write-Host 'TURKUAZ_INSTALLER_BAD_THEME_REJECTED_BEFORE_CONTROLLER_OK'
    Write-Host 'TURKUAZ_INSTALLER_BAD_THEME_GUI_FAIL_CLOSED_OK'
}
finally {
    if ($null -ne $uiProcess -and -not $uiProcess.HasExited) {
        Stop-Process -Id $uiProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if ($null -ne $bootstrapProcess -and -not $bootstrapProcess.HasExited) {
        Stop-Process -Id $bootstrapProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $badTheme) {
        Remove-Item -LiteralPath $badTheme -Force -ErrorAction SilentlyContinue
    }
    if ($profileWasPresent) {
        [IO.File]::WriteAllBytes($profile, $profileOriginal)
    }
    elseif (Test-Path -LiteralPath $profile) {
        Remove-Item -LiteralPath $profile -Force -ErrorAction SilentlyContinue
    }
}
