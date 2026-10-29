# Dosya Yolu: REPO_ROOT/rust/scripts/windows-signed-bundle-smoke.ps1
# Amac: Windows CI'da detached CMS imzali offline bundle lifecycle'ini calistirmak
# Modul - PowerShell Acceptance Script
# Version: 0.2.4
# Aciklama: Ephemeral signer guveni eklemeden Authenticode setup akislarini dogrular
# Bagimli Oldugu Katman: QA | Tool
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$InstallerPath,
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$GuiStubPath,
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$MakensisPath
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$script:InstallerPath = (Resolve-Path -LiteralPath $InstallerPath -ErrorAction Stop).ProviderPath
$script:GuiStubPath = (Resolve-Path -LiteralPath $GuiStubPath -ErrorAction Stop).ProviderPath
$script:MakensisPath = (Resolve-Path -LiteralPath $MakensisPath -ErrorAction Stop).ProviderPath
$script:PackageId = 'ci.smoke'
$script:TargetPath = Join-Path $env:RUNNER_TEMP ('turkuaz-ci-target-' + [Guid]::NewGuid().ToString('N'))
$script:DataRoot = Join-Path $env:RUNNER_TEMP ('turkuaz-ci-data-' + [Guid]::NewGuid().ToString('N'))
$script:WorkDirectory = Join-Path $env:RUNNER_TEMP ('turkuaz-signed-bundle-smoke-' + [Guid]::NewGuid().ToString('N'))
$script:TestCertificate = $null
$script:SignToolPath = $null
$script:FixtureExecutable = Join-Path $script:WorkDirectory 'smoke-app.exe'

function Invoke-ExternalProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Step,
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [int]$TimeoutSeconds = 300
    )

    Write-Host "==> $Step (timeout: $TimeoutSeconds seconds)"
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FilePath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        if (!$process.Start()) {
            throw "Could not start external process '$Step'."
        }
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
            try {
                $process.Kill($true)
            }
            catch {
            }
            $null = $process.WaitForExit(15000)
            throw "External process '$Step' timed out after $TimeoutSeconds seconds."
        }
        $process.WaitForExit()
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        foreach ($line in ($stdout -split "\r?\n")) {
            if ($line) {
                Write-Host "    $line"
            }
        }
        foreach ($line in ($stderr -split "\r?\n")) {
            if ($line) {
                Write-Host "    [stderr] $line"
            }
        }
        $durationSeconds = $timer.Elapsed.TotalSeconds
        Write-Host ("    Finished in {0:N1}s." -f $durationSeconds)
        return [PSCustomObject]@{
            ExitCode = $process.ExitCode
            Output = ($stdout + [Environment]::NewLine + $stderr).Trim()
        }
    }
    finally {
        $process.Dispose()
    }
}

function Invoke-Installer {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Step,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $result = Invoke-ExternalProcess -Step $Step -FilePath $script:InstallerPath -Arguments $Arguments
    if ($result.ExitCode -ne 0) {
        throw "Installer command '$Step' exited with code $($result.ExitCode)."
    }
    return $result.Output
}

function Invoke-InstallerExpectFailure {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Step,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $result = Invoke-ExternalProcess -Step "$Step (expected rejection)" -FilePath $script:InstallerPath -Arguments $Arguments
    if ($result.ExitCode -eq 0) {
        throw "Installer unexpectedly accepted '$Step'."
    }
    return $result.Output
}

function Invoke-AppLaunchCheck {
    param([Parameter(Mandatory = $true)][string]$Label)

    $markerPath = Join-Path $script:WorkDirectory ('launch-' + [Guid]::NewGuid().ToString('N') + '.txt')
    try {
        $env:TURKUAZ_CI_SMOKE_MARKER = $markerPath
        Invoke-Installer -Step "launch $Label" -Arguments @(
            'launch', '--package-id', $script:PackageId,
            '--target', $script:TargetPath, '--data-root', $script:DataRoot
        ) | Out-Null

        for ($attempt = 0; $attempt -lt 100 -and !(Test-Path -LiteralPath $markerPath -PathType Leaf); $attempt++) {
            Start-Sleep -Milliseconds 100
        }
        if (!(Test-Path -LiteralPath $markerPath -PathType Leaf)) {
            throw "The launched application did not create its marker for $Label."
        }
        $marker = [IO.File]::ReadAllText($markerPath)
        if ($marker -ne 'launched') {
            throw "The launched application wrote an unexpected marker for $Label."
        }
        Write-Host '    Application launch marker verified.'
    }
    finally {
        Remove-Item Env:TURKUAZ_CI_SMOKE_MARKER -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $markerPath -Force -ErrorAction SilentlyContinue
    }
}

function New-SignedBundle {
    param([Parameter(Mandatory = $true)][string]$Version)

    $sourceDirectory = Join-Path $script:WorkDirectory ('source-' + $Version)
    New-Item -ItemType Directory -Path $sourceDirectory -Force | Out-Null
    Copy-Item -LiteralPath $script:FixtureExecutable -Destination (Join-Path $sourceDirectory 'smoke-app.exe')
    [IO.File]::WriteAllText(
        (Join-Path $sourceDirectory 'release.txt'),
        ('v{0}{1}' -f $Version, [Environment]::NewLine),
        [Text.UTF8Encoding]::new($false)
    )

    $packagePath = Join-Path $script:WorkDirectory ('package-' + $Version + '.tzpkg')
    $manifestPath = Join-Path $script:WorkDirectory ('manifest-' + $Version + '.yaml')
    $signaturePath = Join-Path $script:WorkDirectory ('manifest-' + $Version + '.p7s')
    $bundlePath = Join-Path $script:WorkDirectory ('bundle-' + $Version + '.tzsetup')
    $artifactUri = ([Uri]::new([IO.Path]::GetFullPath($packagePath))).AbsoluteUri

    Invoke-Installer -Step "build package $Version" -Arguments @(
        'build-package', '--package-id', $script:PackageId,
        '--version', $Version, '--source', $sourceDirectory,
        '--output', $packagePath, '--executable', 'smoke-app.exe'
    ) | Out-Null

    Invoke-Installer -Step "describe package $Version" -Arguments @(
        'describe-package', '--package-id', $script:PackageId,
        '--version', $Version, '--channel', 'stable', '--package', $packagePath,
        '--uri', $artifactUri, '--target', $script:TargetPath, '--output', $manifestPath
    ) | Out-Null

    $manifestText = [IO.File]::ReadAllText($manifestPath)
    $rollbackManifest = [regex]::Replace($manifestText, '(?m)^  supported: false$', '  supported: true')
    if ($rollbackManifest -eq $manifestText) {
        throw "Could not enable the signed rollback policy for $Version."
    }
    [IO.File]::WriteAllText($manifestPath, $rollbackManifest, [Text.UTF8Encoding]::new($false))

    Write-Host "==> CMS-sign release manifest $Version"
    $contentInfo = [System.Security.Cryptography.Pkcs.ContentInfo]::new(
        [IO.File]::ReadAllBytes($manifestPath)
    )
    $signedCms = [System.Security.Cryptography.Pkcs.SignedCms]::new($contentInfo, $true)
    $cmsSigner = [System.Security.Cryptography.Pkcs.CmsSigner]::new($script:TestCertificate)
    $cmsSigner.IncludeOption = [System.Security.Cryptography.X509Certificates.X509IncludeOption]::EndCertOnly
    $cmsSigner.DigestAlgorithm = [System.Security.Cryptography.Oid]::new('2.16.840.1.101.3.4.2.1')
    $signedCms.ComputeSignature($cmsSigner)
    [IO.File]::WriteAllBytes($signaturePath, $signedCms.Encode())
    Write-Host "    CMS signature written for $Version."

    Invoke-Installer -Step "build signed offline bundle $Version" -Arguments @(
        'build-setup-bundle', '--package-id', $script:PackageId,
        '--manifest', $manifestPath, '--signature', $signaturePath,
        '--trust-store', $script:TrustStorePath, '--package', $packagePath,
        '--output', $bundlePath
    ) | Out-Null

    return [PSCustomObject]@{
        Version = $Version
        Package = $packagePath
        Manifest = $manifestPath
        Signature = $signaturePath
        Bundle = $bundlePath
    }
}

function Invoke-SignedSetupInstall {
    param([Parameter(Mandatory = $true)][string]$SetupPath)

    $arguments = @(
        'install-self', '--package-id', $script:PackageId,
        '--trust-store', $script:TrustStorePath,
        '--target', $script:TargetPath, '--data-root', $script:DataRoot,
        '--operation', 'install'
    )
    $result = Invoke-ExternalProcess -Step 'install from signed self-extracting setup' -FilePath $SetupPath -Arguments $arguments
    if ($result.ExitCode -ne 0) {
        throw "Signed self-extracting setup exited with code $($result.ExitCode)."
    }
}

try {
    if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
        throw 'RUNNER_TEMP is required for this CI smoke test.'
    }
    New-Item -ItemType Directory -Path $script:WorkDirectory -Force | Out-Null
    Write-Host "Using temporary smoke directory: $script:WorkDirectory"

    Write-Host '==> load certificate and CMS assemblies'
    Add-Type -AssemblyName System.Security
    Add-Type -AssemblyName System.Security.Cryptography.Pkcs

    Write-Host '==> create ephemeral test certificate'
    $script:TestCertificate = New-SelfSignedCertificate -Subject 'CN=TurkuazInstaller CI Smoke,O=TurkuazLabs,C=US' -CertStoreLocation 'Cert:\CurrentUser\My' -Type CodeSigningCert -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -NotAfter (Get-Date).AddDays(2)

    Write-Host '    Ephemeral test certificate created.'
    Write-Host '==> keep the ephemeral signer out of trusted stores'

    $programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    $sdkBin = Join-Path $programFilesX86 'Windows Kits\10\bin'
    Write-Host '==> locate Windows SDK x64 signtool.exe'
    if (!(Test-Path -LiteralPath $sdkBin -PathType Container)) {
        throw "Windows SDK bin directory was not found: $sdkBin"
    }
    $sdkVersions = Get-ChildItem -LiteralPath $sdkBin -Directory | Sort-Object Name -Descending
    foreach ($sdkVersion in $sdkVersions) {
        $candidate = Join-Path $sdkVersion.FullName 'x64\signtool.exe'
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            $script:SignToolPath = $candidate
            break
        }
    }
    if (!$script:SignToolPath) {
        throw 'Windows SDK x64 signtool.exe was not found in a versioned SDK directory.'
    }

    Write-Host '==> prepare deterministic trust policy for the fixed CI signer subject'
    $subject = 'CN=TurkuazInstaller CI Smoke, O=TurkuazLabs, C=US'
    $hasher= [System.Security.Cryptography.SHA256]::Create()
    try {
        $certificatePin = [BitConverter]::ToString($hasher.ComputeHash($script:TestCertificate.RawData)).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $hasher.Dispose()
    }

    $script:TrustStorePath = Join-Path $script:WorkDirectory 'manifest-trust.yaml'
    $trustYaml = @"
schema_version: 1
packages:
  - id: $script:PackageId
    publisher_subject: "$subject"
    certificate_sha256: "$certificatePin"
"@
    [IO.File]::WriteAllText($script:TrustStorePath, $trustYaml, [Text.UTF8Encoding]::new($false))

    $fixtureSource = Join-Path $script:WorkDirectory 'smoke-app.rs'
    $rustSource = @'
use std::{env, fs};

fn main() {
    let marker = env::var_os("TURKUAZ_CI_SMOKE_MARKER").expect("marker path is required");
    fs::write(marker, "launched").expect("could not write the launch marker");
}
'@
    [IO.File]::WriteAllText($fixtureSource, $rustSource, [Text.UTF8Encoding]::new($false))
    $rustcResult = Invoke-ExternalProcess -Step 'compile launch fixture' -FilePath 'rustc' -Arguments @(
        '--edition=2021', $fixtureSource, '-o', $script:FixtureExecutable
    )
    if ($rustcResult.ExitCode -ne 0) {
        throw "Could not compile the launch fixture; rustc exit code $($rustcResult.ExitCode)."
    }

    $versionOne = New-SignedBundle -Version '1.0.0'
    $versionTwo = New-SignedBundle -Version '1.1.0'

    Invoke-Installer -Step 'install version 1.0.0 from offline bundle' -Arguments @(
        'install-bundle', '--package-id', $script:PackageId,
        '--bundle', $versionOne.Bundle, '--trust-store', $script:TrustStorePath,
        '--target', $script:TargetPath, '--data-root', $script:DataRoot,
        '--operation', 'install'
    ) | Out-Null

    New-Item -ItemType Directory -Path $script:TargetPath -Force | Out-Null
    $sentinelPath = Join-Path $script:TargetPath 'keep-me.txt'
    [IO.File]::WriteAllText($sentinelPath, 'unmanaged', [Text.UTF8Encoding]::new($false))
    Invoke-AppLaunchCheck -Label 'version 1.0.0'

    Invoke-Installer -Step 'update to version 1.1.0 from offline bundle' -Arguments @(
        'install-bundle', '--package-id', $script:PackageId,
        '--bundle', $versionTwo.Bundle, '--trust-store', $script:TrustStorePath,
        '--target', $script:TargetPath, '--data-root', $script:DataRoot,
        '--operation', 'update'
    ) | Out-Null

    $verification = Invoke-Installer -Step 'verify version 1.1.0' -Arguments @(
        'verify', '--package-id', $script:PackageId,
        '--target', $script:TargetPath, '--data-root', $script:DataRoot
    )
    if ($verification -notmatch 'version=1\.1\.0\b') {
        throw 'The active deployment is not version 1.1.0 after update.'
    }
    $directoryMatch = [regex]::Match($verification, '(?m)\bdirectory=(.+)$')
    if (!$directoryMatch.Success) {
        throw 'Could not identify the active package directory from verify output.'
    }
    $activeDirectory = $directoryMatch.Groups[1].Value.Trim()
    $releasePath = Join-Path $activeDirectory 'release.txt'
    if (!(Test-Path -LiteralPath $releasePath -PathType Leaf)) {
        throw 'The active deployment is missing release.txt.'
    }
    [IO.File]::WriteAllText($releasePath, 'tampered', [Text.UTF8Encoding]::new($false))
    Invoke-InstallerExpectFailure -Step 'reject tampered deployment during verify' -Arguments @(
        'verify', '--package-id', $script:PackageId,
        '--target', $script:TargetPath, '--data-root', $script:DataRoot
    ) | Out-Null

    Invoke-Installer -Step 'repair version 1.1.0 from offline bundle' -Arguments @(
        'install-bundle', '--package-id', $script:PackageId,
        '--bundle', $versionTwo.Bundle, '--trust-store', $script:TrustStorePath,
        '--target', $script:TargetPath, '--data-root', $script:DataRoot,
        '--operation', 'repair'
    ) | Out-Null
    $verification = Invoke-Installer -Step 'verify repaired version 1.1.0' -Arguments @(
        'verify', '--package-id', $script:PackageId,
        '--target', $script:TargetPath, '--data-root', $script:DataRoot
    )
    if ($verification -notmatch 'version=1\.1\.0\b') {
        throw 'Repair did not restore version 1.1.0.'
    }

    Invoke-Installer -Step 'rollback to version 1.0.0 under signed policy' -Arguments @(
        'install-bundle', '--package-id', $script:PackageId,
        '--bundle', $versionTwo.Bundle, '--trust-store', $script:TrustStorePath,
        '--target', $script:TargetPath, '--data-root', $script:DataRoot,
        '--operation', 'rollback'
    ) | Out-Null
    $verification = Invoke-Installer -Step 'verify rolled back version 1.0.0' -Arguments @(
        'verify', '--package-id', $script:PackageId,
        '--target', $script:TargetPath, '--data-root', $script:DataRoot
    )
    if ($verification -notmatch 'version=1\.0\.0\b') {
        throw 'Rollback did not restore version 1.0.0.'
    }
    Invoke-AppLaunchCheck -Label 'version 1.0.0 after rollback'

    Invoke-Installer -Step 'uninstall managed package' -Arguments @(
        'uninstall', '--package-id', $script:PackageId,
        '--target', $script:TargetPath, '--data-root', $script:DataRoot,
        '--confirm-uninstall', 'YES'
    ) | Out-Null
    $listOutput = Invoke-Installer -Step 'confirm package is uninstalled' -Arguments @(
        'list', '--data-root', $script:DataRoot
    )
    if ($listOutput -notmatch 'No installed packages\.') {
        throw 'The package is still listed after uninstall.'
    }
    if (!(Test-Path -LiteralPath $sentinelPath -PathType Leaf) -or
        [IO.File]::ReadAllText($sentinelPath) -ne 'unmanaged') {
        throw 'Uninstall removed or changed the unmanaged sentinel file.'
    }

    $distributionDirectory = Join-Path $script:WorkDirectory 'distribution'
    Invoke-Installer -Step 'build primary and NSIS backup distribution pair' -Arguments @(
        'build-distribution-pair', '--package-id', $script:PackageId,
        '--stub', $script:GuiStubPath, '--bundle', $versionTwo.Bundle,
        '--trust-store', $script:TrustStorePath,
        '--output-dir', $distributionDirectory, '--makensis', $script:MakensisPath
    ) | Out-Null

    $primarySetup = Join-Path $distributionDirectory 'TurkuazSetup.exe'
    $backupSetup = Join-Path $distributionDirectory 'TurkuazInstallerBackup.exe'
    if (!(Test-Path -LiteralPath $primarySetup -PathType Leaf) -or
        !(Test-Path -LiteralPath $backupSetup -PathType Leaf)) {
        throw 'The distribution pair did not produce both setup executables.'
    }

    $signResult = Invoke-ExternalProcess -Step 'sign primary setup with the ephemeral CI certificate' -FilePath $script:SignToolPath -Arguments @(
        'sign', '/fd', 'SHA256', '/sha1', $script:TestCertificate.Thumbprint, '/s', 'My', $primarySetup
    )
    if ($signResult.ExitCode -ne 0) {
        throw "signtool sign failed with exit code $($signResult.ExitCode)."
    }

    Write-Host '==> inspect Authenticode signature without adding the test signer to a trusted store'
    $authenticodeSignature = Get-AuthenticodeSignature -LiteralPath $primarySetup
    $authenticodeStatus = [string]$authenticodeSignature.Status
    $statusMessage = [string]$authenticodeSignature.StatusMessage
    $untrustedRootOnly = $statusMessage -match 'A certificate chain processed,\s*but terminated in a root certificate which is not trusted by the trust provider\.'
    if ($authenticodeStatus -notin @('Valid', 'NotTrusted') -and
        !($authenticodeStatus -eq 'UnknownError' -and $untrustedRootOnly)) {
        throw "The primary setup Authenticode signature is invalid: status=$authenticodeStatus message=$statusMessage"
    }
    if (!$authenticodeSignature.SignerCertificate -or
        $authenticodeSignature.SignerCertificate.Thumbprint -ne $script:TestCertificate.Thumbprint) {
        throw 'The primary setup Authenticode signer does not match the ephemeral CI certificate.'
    }
    Write-Host "    Authenticode digest and signer identity verified; status=$authenticodeStatus."

    Invoke-SignedSetupInstall -SetupPath $primarySetup
    $listOutput = Invoke-Installer -Step 'confirm install-self package state' -Arguments @(
        'list', '--data-root', $script:DataRoot
    )
    if ($listOutput -notmatch 'ci\.smoke') {
        throw 'The signed self-extracting setup did not install the package.'
    }
    $verification = Invoke-Installer -Step 'verify install-self deployment' -Arguments @(
        'verify', '--package-id', $script:PackageId,
        '--target', $script:TargetPath, '--data-root', $script:DataRoot
    )
    if ($verification -notmatch 'version=1\.1\.0\b') {
        throw 'The signed self-extracting setup installed the wrong version.'
    }
    Invoke-AppLaunchCheck -Label 'version 1.1.0 from signed self-extracting setup'
    Invoke-Installer -Step 'uninstall self-extractor smoke package' -Arguments @(
        'uninstall', '--package-id', $script:PackageId,
        '--target', $script:TargetPath, '--data-root', $script:DataRoot,
        '--confirm-uninstall', 'YES'
    ) | Out-Null
    $listOutput = Invoke-Installer -Step 'confirm self-extractor smoke package is uninstalled' -Arguments @(
        'list', '--data-root', $script:DataRoot
    )
    if ($listOutput -notmatch 'No installed packages\.') {
        throw 'The self-extractor smoke package remains installed.'
    }
    if (!(Test-Path -LiteralPath $sentinelPath -PathType Leaf) -or
        [IO.File]::ReadAllText($sentinelPath) -ne 'unmanaged') {
        throw 'The second uninstall removed or changed the unmanaged sentinel file.'
    }

    Write-Host 'Signed offline bundle and self-extracting distribution smoke passed.'
}
finally {
    Remove-Item Env:TURKUAZ_CI_SMOKE_MARKER -ErrorAction SilentlyContinue
    if ($script:TestCertificate) {
        $certificatePath = 'Cert:\CurrentUser\My\' + $script:TestCertificate.Thumbprint
        Remove-Item -LiteralPath $certificatePath -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $script:WorkDirectory) {
        Remove-Item -LiteralPath $script:WorkDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
    foreach ($temporaryPath in @($script:TargetPath, $script:DataRoot)) {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
