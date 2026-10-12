# 📄 Dosya Yolu: /tools/Test-MsiStandardUserChild.ps1
# 📌 Amac: Gecici CI standard-user oturumunda gercek PerUser MSI install ve uninstall smoke
# 📌 Modul - Tool PowerShell
# Version: 1.1.0
# Aciklama: Admin olmayan Windows tokeni ve kullaniciya ozgu MSI kaydini fail-closed dogrular
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
    throw "Standard-user MSI child must run under the intended non-admin token."
}

$config = Import-PowerShellDataFile -LiteralPath $ConfigPath
$profile = @($config.MsiProbeScopes | Where-Object { $_.Name -ceq "PerUser" })
if ($profile.Count -ne 1 -or $profile[0].RequiresElevation) {
    throw "PerUser MSI scope configuration is invalid."
}

$root = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::LocalApplicationData)
if ([string]::IsNullOrWhiteSpace($root)) {
    throw "LocalAppData profile has not initialized for the standard user."
}
$target = Join-Path (Join-Path $root $profile[0].Subdirectory) (
    "$($config.MsiProbeInstallPrefix)-standard-" + [guid]::NewGuid().ToString("N"))
$logRoot = Split-Path -Parent $ReportPath
$installerLog = Join-Path $logRoot "standard-install.log"
$uninstallLog = Join-Path $logRoot "standard-uninstall.log"
$propertyName = [string]$config.MsiInstallDirectoryProperty
$msi = [System.IO.Path]::GetFullPath($PackagePath)
$msiexec = Join-Path $env:SystemRoot "System32/msiexec.exe"

function Invoke-UserMsi {
    param([string]$Action, [string]$LogPath)
    $processInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $processInfo.FileName = $msiexec
    $processInfo.CreateNoWindow = $true
    $processInfo.UseShellExecute = $false
    if ($Action -ceq "install") {
        $processInfo.Arguments = '/i "' + $msi + '" /qn /norestart ' +
            $propertyName + '="' + $target + '" /l*v "' + $LogPath + '"'
    }
    elseif ($Action -ceq "uninstall") {
        $processInfo.Arguments = '/x "' + $msi + '" /qn /norestart /l*v "' + $LogPath + '"'
    }
    else {
        throw "Unsupported MSI operation."
    }
    $process = [System.Diagnostics.Process]::Start($processInfo)
    if ($null -eq $process) { throw "msiexec did not start." }
    try {
        if (-not $process.WaitForExit(90000)) {
            $process.Kill($true)
            $null = $process.WaitForExit(5000)
            throw "Standard-user MSI $Action timed out."
        }
        if ($process.ExitCode -eq 1625 -and $Action -ceq "install") {
            return 1625
        }
        if ($process.ExitCode -ne 0) {
            throw "Standard-user MSI $Action returned $($process.ExitCode)."
        }
        return 0
    }
    finally {
        $process.Dispose()
    }
}

function Get-UserMsiAssignment {
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $null
    $view = $null
    $record = $null
    try {
        $database = $installer.GetType().InvokeMember(
            "OpenDatabase", "InvokeMethod", $null, $installer, @($msi, 0))
        $view = $database.GetType().InvokeMember(
            "OpenView", "InvokeMethod", $null, $database,
            @("SELECT Value FROM Property WHERE Property = 'ProductCode'"))
        $null = $view.GetType().InvokeMember(
            "Execute", "InvokeMethod", $null, $view, $null)
        $record = $view.GetType().InvokeMember(
            "Fetch", "InvokeMethod", $null, $view, $null)
        if ($null -eq $record) { throw "MSI ProductCode not found." }
        $code = [string]$record.GetType().InvokeMember(
            "StringData", "GetProperty", $null, $record, @(1))
        return [string]$installer.GetType().InvokeMember(
            "ProductInfo", "GetProperty", $null, $installer, @($code, "AssignmentType"))
    }
    finally {
        foreach ($object in @($record, $view, $database, $installer)) {
            if ($null -ne $object -and
                [System.Runtime.InteropServices.Marshal]::IsComObject($object)) {
                [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($object)
            }
        }
    }
}

$installed = $false
$passed = $false
try {
    $installExit = Invoke-UserMsi -Action install -LogPath $installerLog
    if ($installExit -eq 1625) {
        if (Test-Path -LiteralPath (Join-Path $target "Update.exe") -PathType Leaf) {
            throw "Policy-blocked MSI nevertheless installed Update.exe."
        }
        [System.IO.File]::WriteAllText(
            $ReportPath, "INSTALLER_MSI_NONADMIN_POLICY_BLOCK_1625")
        exit 0
    }
    $installed = $true

    $app = Join-Path $target "current/$($config.MainExecutable)"
    $updater = Join-Path $target "Update.exe"
    $marker = Join-Path $target "current/$($config.VersionMarkerFile)"
    if (-not (Test-Path -LiteralPath $app -PathType Leaf) -or
        -not (Test-Path -LiteralPath $updater -PathType Leaf) -or
        -not (Test-Path -LiteralPath $marker -PathType Leaf)) {
        throw "Standard-user MSI did not install all expected files."
    }
    if ((Get-Content -LiteralPath $marker -Raw).Trim() -cne $config.VersionOne) {
        throw "Standard-user MSI marker mismatched."
    }
    $assignment = Get-UserMsiAssignment
    if ($assignment -cne [string]$profile[0].MsiAssignmentType) {
        throw "Standard-user MSI assignment type was $assignment."
    }
    $passed = $true
}
finally {
    if ($installed) {
        try { Invoke-UserMsi -Action uninstall -LogPath $uninstallLog }
        catch {
            $passed = $false
            Write-Warning "Standard-user MSI uninstall failed: $_"
        }
    }
    if ($passed -and ((Test-Path -LiteralPath (Join-Path $target "Update.exe")) -or
        (Test-Path -LiteralPath (Join-Path $target "current")))) {
        $passed = $false
    }
    if ($passed) {
        [System.IO.File]::WriteAllText(
            $ReportPath, "INSTALLER_MSI_NONADMIN_PERUSER_LIFECYCLE_OK")
    }
}

if (-not $passed) { throw "Standard-user MSI lifecycle was not accepted." }
