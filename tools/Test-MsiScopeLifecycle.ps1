# 📄 Dosya Yolu: /tools/Test-MsiScopeLifecycle.ps1
# 📌 Amac: Gecici Windows CI hostunda her lab MSI scope icin install ve uninstall testlerini tekrar kullanir
# 📌 Modul - Tool PowerShell
# Version: 1.1.0
# Aciklama: Tek parametreli PerUser/PerMachine lifecycle, izole target, gercek install/uninstall ve dosya dogrulama
# Bagimli Oldugu Katman: Tool | Config | CI

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Scope,
    [string]$ConfigPath = "config/velopack-e2e.psd1",
    [string]$ArtifactsRoot = "artifacts/installer-msi-scope-probe",
    [ValidateRange(20, 240)]
    [int]$TimeoutSeconds = 90
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $IsWindows -or -not $env:GITHUB_ACTIONS -or -not $env:RUNNER_TEMP) {
    throw "Only disposable GitHub Windows CI hosts are allowed."
}

$config = Import-PowerShellDataFile -LiteralPath (Resolve-Path -LiteralPath $ConfigPath)
$matchingScopes = @($config.MsiProbeScopes | Where-Object { $_.Name -ceq $Scope })
if ($matchingScopes.Count -ne 1) {
    throw "Unknown or duplicate MSI probe scope: $Scope"
}
$scopeProfile = $matchingScopes[0]

if ([bool]$scopeProfile.RequiresElevation) {
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [System.Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "PerMachine MSI E2E needs an elevated disposable runner."
    }
}

$root = [System.IO.Path]::GetFullPath($ArtifactsRoot)
$msis = @(Get-ChildItem -LiteralPath (Join-Path $root $Scope) -Filter "*.msi" -File)
if ($msis.Count -ne 1) {
    throw "Expected exactly one $Scope fixture MSI."
}

$folderEnum = [System.Enum]::Parse(
    [System.Environment+SpecialFolder],
    [string]$scopeProfile.SpecialFolder)
$targetBase = [System.Environment]::GetFolderPath($folderEnum)
if ([string]::IsNullOrWhiteSpace($targetBase)) {
    throw "Install base path is empty for $Scope"
}
if (-not [string]::IsNullOrWhiteSpace([string]$scopeProfile.Subdirectory)) {
    $targetBase = Join-Path $targetBase ([string]$scopeProfile.Subdirectory)
}
$runId = [guid]::NewGuid().ToString("N")
$installTarget = Join-Path $targetBase "$($config.MsiProbeInstallPrefix)-$($Scope.ToLowerInvariant())-$runId"
$installPropertyPrefix = "$($config.MsiInstallDirectoryProperty)="
$logDirectory = Join-Path $env:RUNNER_TEMP "installer-msi-lab-$runId"
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

if (Test-Path -LiteralPath $installTarget) {
    throw "Isolated MSI install target already exists."
}

$msi = $msis[0].FullName
$installSucceeded = $false
$uninstallSucceeded = $false

function Write-LabMsiLogTail {
    param(
        [string]$LogPath,
        [string]$Operation
    )

    if (-not (Test-Path -LiteralPath $LogPath -PathType Leaf)) {
        Write-Warning "MSI lab log missing for $Operation"
        return
    }

    Write-Host "INSTALLER_MSI_LAB_LOG_TAIL_BEGIN $Operation"
    Get-Content -LiteralPath $LogPath -Tail 90 |
        ForEach-Object {
            Write-Host "MSI_LAB_LOG: $_"
        }
    Write-Host "INSTALLER_MSI_LAB_LOG_TAIL_END $Operation"
}

function Invoke-MsiChecked {
    param(
        [string[]]$Arguments,
        [string]$Operation,
        [string]$LogPath
    )

    $info = [System.Diagnostics.ProcessStartInfo]::new()
    $info.FileName = Join-Path $env:SystemRoot "System32/msiexec.exe"
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    # Windows Installer parses PUBLICPROPERTY="value with spaces" itself.
    # ProcessStartInfo.ArgumentList instead wraps the entire assignment in
    # quotes, which can be interpreted as a malformed msiexec command line.
    $commandParts = foreach ($argument in $Arguments) {
        if ($argument.Contains('"')) {
            throw "MSI lab argument contains unexpected quotation characters."
        }
        if ($argument.StartsWith($installPropertyPrefix, [StringComparison]::Ordinal)) {
            $target = $argument.Substring($installPropertyPrefix.Length)
            $installPropertyPrefix + '"' + $target + '"'
        }
        elseif ($argument.StartsWith("/")) {
            $argument
        }
        else {
            '"' + $argument + '"'
        }
    }
    if ($LogPath.Contains('"')) {
        throw "MSI lab log path contains invalid quotes."
    }
    $info.Arguments = (($commandParts + @("/l*v", ('"' + $LogPath + '"'))) -join " ")

    Write-Host "INSTALLER_MSI_LAB_START $Operation (timeout=$TimeoutSeconds)"
    Write-Host "INSTALLER_MSI_LAB_ARGUMENT_MODE explicit-property-value-quotes"
    $process = [System.Diagnostics.Process]::Start($info)
    if ($null -eq $process) {
        throw "msiexec process could not start for $Operation"
    }
    try {
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $process.WaitForExit(5000) | Out-Null
            Write-LabMsiLogTail -LogPath $LogPath -Operation $Operation
            throw "MSI $Operation timeout"
        }
        if ($process.ExitCode -ne 0) {
            Write-LabMsiLogTail -LogPath $LogPath -Operation $Operation
            throw "MSI $Operation failed, exit=$($process.ExitCode)"
        }
        Write-Host "INSTALLER_MSI_MACHINE_PROCESS_OK $Operation"
    }
    finally {
        $process.Dispose()
    }
}

try {
    Invoke-MsiChecked -Operation "install" -LogPath (Join-Path $logDirectory "install.log") -Arguments @(
        "/i", $msi, "/qn", "/norestart", "$installPropertyPrefix$installTarget"
    )
    $installSucceeded = $true

    $updater = Join-Path $installTarget "Update.exe"
    $fixture = Join-Path $installTarget "current/$($config.MainExecutable)"
    if (-not (Test-Path -LiteralPath $updater -PathType Leaf)) {
        throw "Installed $Scope test app missing Update.exe."
    }
    if (-not (Test-Path -LiteralPath $fixture -PathType Leaf)) {
        throw "Installed $Scope test app missing fixture executable."
    }

    $marker = Join-Path $installTarget "current/$($config.VersionMarkerFile)"
    if (-not (Test-Path -LiteralPath $marker -PathType Leaf) -or
        [System.IO.File]::ReadAllText($marker).Trim() -cne $config.VersionOne) {
        throw "Installed MSI package content marker mismatch."
    }
    Write-Host "INSTALLER_MSI_SCOPE_INSTALLED_OK $Scope"
}
finally {
    try {
        Invoke-MsiChecked -Operation "uninstall" -LogPath (Join-Path $logDirectory "uninstall.log") -Arguments @(
            "/x", $msi, "/qn", "/norestart"
        )
        $uninstallSucceeded = $true
    }
    catch {
        Write-Warning "MSI cleanup failed. Inspect CI-only msiexec logs: $_"
    }
}

if (-not $installSucceeded -or -not $uninstallSucceeded) {
    throw "$Scope MSI install/uninstall was not fully successful."
}

$deadline = [DateTime]::UtcNow.AddSeconds(30)
do {
    $hasActive = Test-Path -LiteralPath (Join-Path $installTarget "current")
    $hasUpdater = Test-Path -LiteralPath (Join-Path $installTarget "Update.exe")
    if (-not $hasActive -and -not $hasUpdater) { break }
    Start-Sleep -Milliseconds 500
} while ([DateTime]::UtcNow -lt $deadline)

if ($hasActive -or $hasUpdater) {
    throw "$Scope MSI uninstall left active application binaries."
}

Write-Host "INSTALLER_MSI_SCOPE_LIFECYCLE_OK $Scope"
