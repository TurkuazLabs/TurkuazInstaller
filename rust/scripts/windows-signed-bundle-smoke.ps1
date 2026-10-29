# Dosya Yolu: REPO_ROOT/rust/scripts/windows-signed-bundle-smoke.ps1
# Amac: Windows CI'da detached CMS imzali offline bundle lifecycle'ini calistirmak
# Modul - PowerShell Acceptance Script
# Version: 0.1.0
# Aciklama: Runner'a ozel test sertifikasi ile install/update/repair/rollback/launch/uninstall akislarini dogrular
# Bagimli Oldugu Katman: QA | Tool
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$InstallerPath
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$script:InstallerPath = (Resolve-Path -LiteralPath $InstallerPath -ErrorAction Stop).ProviderPath
$script:PackageId = 'ci.smoke'
$script:TargetPath = Join-Path $env:RUNNER_TEMP ('turkuaz-ci-target-' + [Guid]::NewGuid().ToString('N'))
$script:DataRoot = Join-Path $env:RUNNER_TEMP ('turkuaz-ci-data-' + [Guid]::NewGuid().ToString('N'))
$script:WorkDirectory = Join-Path $env:RUNNER_TEMP ('turkuaz-signed-bundle-smoke-' + [Guid]::NewGuid().ToString('N'))
$script:TestCertificate = $null
$script:FixtureExecutable = Join-Path $script:WorkDirectory 'smoke-app.exe'

function Invoke-Installer {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Step,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Write-Host "==> $Step"
    $lines = @(& $script:InstallerPath @Arguments 2>&1 | ForEach-Object { $_.ToString() })
    $exitCode = $LASTEXITCODE
    foreach ($line in $lines) {
        Write-Host "    $line"
    }
    if ($exitCode -ne 0) {
        throw "Installer command '$Step' exited with code $exitCode."
    }
    return ($lines -join [Environment]::NewLine)
}

function Invoke-InstallerExpectFailure {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Step,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Write-Host "==> $Step (expected rejection)"
    $lines = @(& $script:InstallerPath @Arguments 2>&1 | ForEach-Object { $_.ToString() })
    $exitCode = $LASTEXITCODE
    foreach ($line in $lines) {
        Write-Host "    $line"
    }
    if ($exitCode -eq 0) {
        throw "Installer unexpectedly accepted '$Step'."
    }
    return ($lines -join [Environment]::NewLine)
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

    $contentInfo = [System.Security.Cryptography.Pkcs.ContentInfo]::new(
        [IO.File]::ReadAllBytes($manifestPath)
    )
    $signedCms = [System.Security.Cryptography.Pkcs.SignedCms]::new($contentInfo, $true)
    $cmsSigner = [System.Security.Cryptography.Pkcs.CmsSigner]::new($script:TestCertificate)
    $cmsSigner.IncludeOption = [System.Security.Cryptography.X509Certificates.X509IncludeOption]::EndCertOnly
    $cmsSigner.DigestAlgorithm = [System.Security.Cryptography.Oid]::new('2.16.840.1.101.3.4.2.1')
    $signedCms.ComputeSignature($cmsSigner)
    [IO.File]::WriteAllBytes($signaturePath, $signedCms.Encode())

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

function Get-CertificateSubjectForPolicy {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

public static class TurkuazCertSubject
{
    [StructLayout(LayoutKind.Sequential)]
    private struct CertNameBlob
    {
        public uint cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", EntryPoint = "CertNameToStrW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint CertNameToStrW(uint encoding, ref CertNameBlob name, uint type, StringBuilder output, uint outputChars);

    public static string GetSubject(X509Certificate2 certificate)
    {
        byte[] encoded = certificate.SubjectName.RawData;
        GCHandle pinned = GCHandle.Alloc(encoded, GCHandleType.Pinned);
        try
        {
            CertNameBlob name = new CertNameBlob();
            name.cbData = (uint)encoded.Length;
            name.pbData = pinned.AddrOfPinnedObject();
            const uint type = 0x02000003;
            uint length = CertNameToStrW(1, ref name, type, null, 0);
            if (length < 2 || length > 16384)
                throw new InvalidOperationException("Could not size the certificate subject.");
            StringBuilder output = new StringBuilder((int)length);
            uint written = CertNameToStrW(1, ref name, type, output, length);
            if (written == 0 || written > length)
                throw new InvalidOperationException("Could not format the certificate subject.");
            return output.ToString();
        }
        finally
        {
            pinned.Free();
        }
    }
}
'@
    return [TurkuazCertSubject]::GetSubject($script:TestCertificate)
}

try {
    if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
        throw 'RUNNER_TEMP is required for this CI smoke test.'
    }
    New-Item -ItemType Directory -Path $script:WorkDirectory -Force | Out-Null
    Write-Host "Using temporary smoke directory: $script:WorkDirectory"

    Add-Type -AssemblyName System.Security
    Add-Type -AssemblyName System.Security.Cryptography.Pkcs

    $script:TestCertificate = New-SelfSignedCertificate -Subject 'CN=TurkuazInstaller CI Smoke,O=TurkuazLabs,C=US' -CertStoreLocation 'Cert:\CurrentUser\My' -Type CodeSigningCert -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -NotAfter (Get-Date).AddDays(2)

    $subject = Get-CertificateSubjectForPolicy
    $hasher = [System.Security.Cryptography.SHA256]::Create()
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
    Write-Host '==> compile launch fixture'
    & rustc --edition=2021 $fixtureSource -o $script:FixtureExecutable
    $rustcExitCode = $LASTEXITCODE
    if ($rustcExitCode -ne 0) {
        throw "Could not compile the launch fixture; rustc exit code $rustcExitCode."
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

    Write-Host 'Signed offline Windows lifecycle smoke passed.'
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
}
