# Dosya Yolu: REPO_ROOT/tests/scripts/Test-SignerPublisherSubject.ps1
# Amac: Azure Artifact Signing EKU profil kimligini imzaci kimliginden dogrular
# Modul - PowerShell Test
# Version: 1.3.0
# Aciklama: Gunluk rotasyon kabul, farkli profile/yayinci ve bos policy ret testleri
# Bagimli Oldugu Katman: Tool | Testing

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).ProviderPath
. (Join-Path $root 'tools/release/Assert-SignerPublisherSubject.ps1')

# Ephemeral, in-process non-trusted test certificates. Nothing written to disk.
$subject = 'CN=Turkuaz CI Test, O=Example, C=TR'
$approvedProfile = '1.3.6.1.4.1.311.97.999999.111111.222222'
$unapprovedProfile = '1.3.6.1.4.1.311.97.999999.333333.444444'
$keys = @()
$certificates = @()
try {
    $profiles = @(
        @{ Profile=$approvedProfile; PublicTrust=$true; CodeSigning=$true },
        @{ Profile=$approvedProfile; PublicTrust=$true; CodeSigning=$true },
        @{ Profile=$unapprovedProfile; PublicTrust=$true; CodeSigning=$true },
        @{ Profile=$approvedProfile; PublicTrust=$false; CodeSigning=$true },
        @{ Profile=$approvedProfile; PublicTrust=$true; CodeSigning=$false }
    )
    foreach ($profile in $profiles) {
        $rsa = [System.Security.Cryptography.RSA]::Create(2048)
        $keys += $rsa
        $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
            $subject, $rsa, [System.Security.Cryptography.HashAlgorithmName]::SHA256,
            [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $oids = [System.Security.Cryptography.OidCollection]::new()
        [void]$oids.Add([System.Security.Cryptography.Oid]::new($profile.Profile))
        if ($profile.PublicTrust) {
            [void]$oids.Add([System.Security.Cryptography.Oid]::new('1.3.6.1.4.1.311.97.1.0'))
        }
        if ($profile.CodeSigning) {
            [void]$oids.Add([System.Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3'))
        }
        $request.CertificateExtensions.Add(
            [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($oids, $false))
        $certificates += $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddDays(1))
    }
    $first = [pscustomobject]@{SignerCertificate=$certificates[0]}
    $rotated = [pscustomobject]@{SignerCertificate=$certificates[1]}
    $otherProfile = [pscustomobject]@{SignerCertificate=$certificates[2]}
    $privateTrust = [pscustomobject]@{SignerCertificate=$certificates[3]}
    $notCodeSigning = [pscustomobject]@{SignerCertificate=$certificates[4]}

    $approvedSubject = $certificates[0].Subject
    if ($certificates[0].Thumbprint -eq $certificates[1].Thumbprint) {
        throw 'Test requires rotated certificates with distinct thumbprints.'
    }
    Assert-SignerPublisherSubject -Signature $first -ExpectedPublisherSubject $approvedSubject -ExpectedProfileEkuOid $approvedProfile
    Assert-SignerPublisherSubject -Signature $rotated -ExpectedPublisherSubject $approvedSubject.ToLowerInvariant() -ExpectedProfileEkuOid $approvedProfile

    $negative = @(
        @{ Name='same subject, wrong profile'; Signature=$otherProfile; Subject=$approvedSubject; Oid=$approvedProfile; Message='missing the approved' },
        @{ Name='profile correct but no Public Trust'; Signature=$privateTrust; Subject=$approvedSubject; Oid=$approvedProfile; Message='missing the Azure Artifact Signing Public Trust' },
        @{ Name='profile correct but not code signing'; Signature=$notCodeSigning; Subject=$approvedSubject; Oid=$approvedProfile; Message='missing the code-signing EKU' },
        @{ Name='wrong publisher'; Signature=$first; Subject='CN=Someone Else'; Oid=$approvedProfile; Message='differs from the approved value' },
        @{ Name='missing signer'; Signature=$null; Subject=$approvedSubject; Oid=$approvedProfile; Message='subject is unavailable' },
        @{ Name='missing certificate'; Signature=[pscustomobject]@{SignerCertificate=$null}; Subject=$approvedSubject; Oid=$approvedProfile; Message='subject is unavailable' },
        @{ Name='blank publisher'; Signature=$first; Subject=' '; Oid=$approvedProfile; Message='subject is missing' },
        @{ Name='empty EKU policy'; Signature=$first; Subject=$approvedSubject; Oid=''; Message='profile-specific OID' },
        @{ Name='wrong EKU prefix'; Signature=$first; Subject=$approvedSubject; Oid='1.2.3'; Message='profile-specific OID' },
        @{ Name='generic public EKU'; Signature=$first; Subject=$approvedSubject; Oid='1.3.6.1.4.1.311.97.1.0'; Message='profile-specific OID' }
    )
    foreach ($case in $negative) {
        $rejected = $false
        try {
            Assert-SignerPublisherSubject -Signature $case.Signature -ExpectedPublisherSubject $case.Subject -ExpectedProfileEkuOid $case.Oid
        }
        catch { $rejected = $_.Exception.Message.Contains($case.Message) }
        if (-not $rejected) { throw "Profile EKU policy allowed $($case.Name)." }
    }

    $release = Get-Content -LiteralPath (Join-Path $root '.github/workflows/release.yml') -Raw
    $preflight = Get-Content -LiteralPath (Join-Path $root '.github/workflows/release-preflight.yml') -Raw
    foreach ($yaml in @($release,$preflight)) {
        foreach ($key in @('ARTIFACT_SIGNING_EXPECTED_PUBLISHER_SUBJECT','ARTIFACT_SIGNING_EXPECTED_PROFILE_EKU')) {
            if (-not $yaml.Contains($key + ': ${{ vars.' + $key + ' }}')) {
                throw "Production workflow is missing environment policy: $key."
            }
        }
        if (-not $yaml.Contains('-ExpectedPublisherSubject $env:ARTIFACT_SIGNING_EXPECTED_PUBLISHER_SUBJECT') -or
            -not $yaml.Contains('-ExpectedProfileEkuOid $env:ARTIFACT_SIGNING_EXPECTED_PROFILE_EKU')) {
            throw 'Production verifier is not bound to approved publisher and profile EKU.'
        }
    }
    Write-Output 'PASS: daily rotation; profile identity, Public Trust, code signing and policy fail-closed.'
}
finally {
    foreach ($cert in $certificates) { $cert.Dispose() }
    foreach ($key in $keys) { $key.Dispose() }
}
