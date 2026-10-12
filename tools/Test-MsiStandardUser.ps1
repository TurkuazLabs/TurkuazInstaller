# 📄 Dosya Yolu: /tools/Test-MsiStandardUser.ps1
# 📌 Amac: Gecici GitHub Windows ortaminda admin olmayan ayri hesapla PerUser MSI testini calistirir
# 📌 Modul - Tool PowerShell
# Version: 1.0.1
# Aciklama: Tek kullanimlik standard user, erisim izinleri, smoke testi ve guvenli cleanup
# Bagimli Oldugu Katman: Tool | Config | CI

[CmdletBinding()]
param(
    [string]$ConfigPath = "config/velopack-e2e.psd1",
    [string]$ArtifactsRoot = "artifacts/installer-msi-scope-probe",
    [ValidateRange(120, 300)] [int]$TimeoutSeconds = 240
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $IsWindows -or $env:GITHUB_ACTIONS -cne "true" -or
    $env:RUNNER_ENVIRONMENT -cne "github-hosted" -or
    [string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
    throw "Creating standard-user LAB accounts is forbidden outside disposable GitHub-hosted Windows."
}

$who = [System.Security.Principal.WindowsPrincipal]::new(
    [System.Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $who.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Standard-user account setup requires an elevated, disposable CI runner."
}

$config = Import-PowerShellDataFile -LiteralPath (Resolve-Path -LiteralPath $ConfigPath)
$perUser = @($config.MsiProbeScopes | Where-Object { $_.Name -ceq "PerUser" })
if ($perUser.Count -ne 1 -or [bool]$perUser[0].RequiresElevation) {
    throw "PerUser MSI lab scope config is invalid."
}

$root = [System.IO.Path]::GetFullPath($ArtifactsRoot)
$package = @(Get-ChildItem -LiteralPath (Join-Path $root "PerUser") -Filter "*.msi" -File)
if ($package.Count -ne 1) {
    throw "Standard-user lab requires exactly one PerUser MSI."
}

$nonce = [guid]::NewGuid().ToString("N")
$name = "timsilab" + $nonce.Substring(0, 8)
$stage = Join-Path $env:ProgramData ("TurkuazInstallerMSILab-" + $nonce)
$accountCreated = $false
$password = ConvertTo-SecureString (
    "Aa9!" + [Convert]::ToBase64String(
        [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
) -AsPlainText -Force
try {
    $params = @{
        Name = $name
        Password = $password
        AccountNeverExpires = $true
        PasswordNeverExpires = $true
        Description = "Temporary hosted Windows MSI verification account"
    }
    $user = New-LocalUser @params
    $accountCreated = $true
    $sid = $user.SID.Value

    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    $child = Join-Path $stage "Test-MsiStandardUserChild.ps1"
    $childMsi = Join-Path $stage "lab-peruser.msi"
    $childConfig = Join-Path $stage "velopack-e2e.psd1"
    $report = Join-Path $stage "result.txt"
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Test-MsiStandardUserChild.ps1") -Destination $child -Force
    Copy-Item -LiteralPath $package[0].FullName -Destination $childMsi -Force
    Copy-Item -LiteralPath (Resolve-Path -LiteralPath $ConfigPath).Path -Destination $childConfig -Force

    & icacls.exe $stage /grant "*$($sid):(OI)(CI)M" /T | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not grant test user access to isolated MSI staging root."
    }

    $identity = "$env:COMPUTERNAME\$name"
    $credential = [System.Management.Automation.PSCredential]::new($identity, $password)
    $shell = (Get-Command pwsh.exe -ErrorAction Stop).Source
    $args = '-NoProfile -NonInteractive -File "' + $child + '"' +
        ' -PackagePath "' + $childMsi + '"' +
        ' -ConfigPath "' + $childConfig + '"' +
        ' -ReportPath "' + $report + '"' +
        ' -ExpectedSid "' + $sid + '"'

    Write-Host "INSTALLER_MSI_NONADMIN_STARTED"
    $childOut = Join-Path $stage "child-stdout.txt"
    $childErr = Join-Path $stage "child-stderr.txt"
    $launch = @{
        FilePath = $shell
        Credential = $credential
        WorkingDirectory = $stage
        ArgumentList = $args
        PassThru = $true
        RedirectStandardOutput = $childOut
        RedirectStandardError = $childErr
        ErrorAction = "Stop"
    }
    $process = Start-Process @launch
    try {
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $null = $process.WaitForExit(5000)
            throw "Standard-user MSI process timed out."
        }
        if ($process.ExitCode -ne 0) {
            if (Test-Path -LiteralPath $childErr -PathType Leaf) {
                Get-Content -LiteralPath $childErr -Tail 28 | ForEach-Object { Write-Host "NONADMIN_ERROR: $_" }
            }
            $installLog = Join-Path $stage "standard-install.log"
            if (Test-Path -LiteralPath $installLog -PathType Leaf) {
                Get-Content -LiteralPath $installLog -Tail 28 | ForEach-Object { Write-Host "NONADMIN_MSI_LOG: $_" }
            }
            throw "Standard-user MSI process failed with exit=$($process.ExitCode)."
        }
    }
    finally {
        $process.Dispose()
    }

    if (-not (Test-Path -LiteralPath $report -PathType Leaf) -or
        (Get-Content -LiteralPath $report -Raw).Trim() -cne
            "INSTALLER_MSI_NONADMIN_PERUSER_LIFECYCLE_OK") {
        throw "Standard-user MSI child did not produce a passing report."
    }
    Write-Host "INSTALLER_MSI_NONADMIN_PERUSER_LIFECYCLE_OK"
}
finally {
    if ($accountCreated) {
        try { Remove-LocalUser -Name $name -ErrorAction Stop }
        catch { Write-Warning "Temporary LAB account removal failed: $_" }
    }
    if (Test-Path -LiteralPath $stage) {
        try { Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction Stop }
        catch { Write-Warning "LAB staging cleanup failed: $_" }
    }
}
