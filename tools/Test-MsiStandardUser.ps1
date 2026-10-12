# 📄 Dosya Yolu: /tools/Test-MsiStandardUser.ps1
# 📌 Amac: Gecici GitHub Windows ortaminda admin olmayan ayri hesapla PerUser MSI testini calistirir
# 📌 Modul - Tool PowerShell
# Version: 1.1.1
# Aciklama: Standard user MSI policy sonucu ve Velopack Setup lifecycle testi, tek hesaptan ve cleanup ile
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
$setupFiles = @(Get-ChildItem -LiteralPath (Join-Path $root "PerUser") -Filter "*Setup.exe" -File)
if ($setupFiles.Count -ne 1) {
    throw "Standard-user lab requires exactly one PerUser Setup.exe."
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
        Description = "Temporary MSI LAB user"
    }
    $user = New-LocalUser @params
    $accountCreated = $true
    $sid = $user.SID.Value

    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    $child = Join-Path $stage "Test-MsiStandardUserChild.ps1"
    $setupChild = Join-Path $stage "Test-SetupStandardUserChild.ps1"
    $childMsi = Join-Path $stage "lab-peruser.msi"
    $setupExe = Join-Path $stage "lab-peruser-Setup.exe"
    $childConfig = Join-Path $stage "velopack-e2e.psd1"
    $msiReport = Join-Path $stage "msi-result.txt"
    $setupReport = Join-Path $stage "setup-result.txt"
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Test-MsiStandardUserChild.ps1") -Destination $child -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Test-SetupStandardUserChild.ps1") -Destination $setupChild -Force
    Copy-Item -LiteralPath $package[0].FullName -Destination $childMsi -Force
    Copy-Item -LiteralPath $setupFiles[0].FullName -Destination $setupExe -Force
    Copy-Item -LiteralPath (Resolve-Path -LiteralPath $ConfigPath).Path -Destination $childConfig -Force

    & icacls.exe $stage /grant "*$($sid):(OI)(CI)M" /T | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not grant test user access to isolated MSI staging root."
    }

    $identity = "$env:COMPUTERNAME\$name"
    $credential = [System.Management.Automation.PSCredential]::new($identity, $password)
    $shell = (Get-Command pwsh.exe -ErrorAction Stop).Source
    function Invoke-NonAdminLabChild {
        param(
            [string]$ScriptFile,
            [string]$InstallerFile,
            [string]$OutputReport,
            [string]$Label
        )

        $childArgs = '-NoProfile -NonInteractive -File "' + $ScriptFile + '"' +
            ' -PackagePath "' + $InstallerFile + '"' +
            ' -ConfigPath "' + $childConfig + '"' +
            ' -ReportPath "' + $OutputReport + '"' +
            ' -ExpectedSid "' + $sid + '"'
        $childOut = Join-Path $stage "$Label-stdout.txt"
        $childErr = Join-Path $stage "$Label-stderr.txt"
        $launch = @{
            FilePath = $shell
            Credential = $credential
            WorkingDirectory = $stage
            ArgumentList = $childArgs
            PassThru = $true
            RedirectStandardOutput = $childOut
            RedirectStandardError = $childErr
            ErrorAction = "Stop"
        }
        Write-Host "INSTALLER_NONADMIN_LAB_START $Label"
        $process = Start-Process @launch
        try {
            if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
                $process.Kill($true)
                $null = $process.WaitForExit(5000)
                throw "$Label standard-user process timed out."
            }
            if ($process.ExitCode -ne 0) {
                if (Test-Path -LiteralPath $childOut -PathType Leaf) {
                    Get-Content -LiteralPath $childOut -Tail 35 |
                        ForEach-Object { Write-Host "NONADMIN_STDOUT: $_" }
                }
                if (Test-Path -LiteralPath $childErr -PathType Leaf) {
                    Get-Content -LiteralPath $childErr -Tail 30 |
                        ForEach-Object { Write-Host "NONADMIN_ERROR: $_" }
                }
                $installLog = Join-Path $stage "standard-install.log"
                if (Test-Path -LiteralPath $installLog -PathType Leaf) {
                    Get-Content -LiteralPath $installLog -Tail 28 |
                        ForEach-Object { Write-Host "NONADMIN_MSI_LOG: $_" }
                }
                throw "$Label standard-user process failed exit=$($process.ExitCode)."
            }
        }
        finally {
            $process.Dispose()
        }
        if (-not (Test-Path -LiteralPath $OutputReport -PathType Leaf)) {
            throw "$Label standard-user report was not written."
        }
        return (Get-Content -LiteralPath $OutputReport -Raw).Trim()
    }

    $msiResult = Invoke-NonAdminLabChild -ScriptFile $child -InstallerFile $childMsi -OutputReport $msiReport -Label "MSI"
    if ($msiResult -ceq "INSTALLER_MSI_NONADMIN_POLICY_BLOCK_1625") {
        Write-Host "INSTALLER_MSI_NONADMIN_POLICY_BLOCK_1625"
    }
    elseif ($msiResult -ceq "INSTALLER_MSI_NONADMIN_PERUSER_LIFECYCLE_OK") {
        Write-Host "INSTALLER_MSI_NONADMIN_PERUSER_LIFECYCLE_OK"
    }
    else {
        throw "Unexpected standard-user MSI outcome."
    }

    $setupResult = Invoke-NonAdminLabChild -ScriptFile $setupChild -InstallerFile $setupExe -OutputReport $setupReport -Label "Setup"
    if ($setupResult -cne "INSTALLER_SETUP_NONADMIN_PERUSER_LIFECYCLE_OK") {
        throw "Non-admin user-scope Setup did not pass."
    }
    Write-Host "INSTALLER_SETUP_NONADMIN_PERUSER_LIFECYCLE_OK"
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
