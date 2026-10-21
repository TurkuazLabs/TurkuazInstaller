# Dosya Yolu: REPO_ROOT/tests/scripts/Test-SignerPublisherSubject.ps1
# Amac: X.500 yayinci ve SHA256 sertifika parmak izi sinirlarini test eder
# Modul - PowerShell Test
# Version: 1.1.0
# Aciklama: Yalniz bellekte gecici test sertifikalari; gercek imza/trust kaniti degildir
# Bagimli Oldugu Katman: Tool | Testing

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).ProviderPath
. (Join-Path $root 'tools/release/Assert-SignerPublisherSubject.ps1')

# Synthetic test certificates exist only in process memory; never exported or trusted.
$subject = 'CN=Turkuaz CI Test, O=Example, C=TR'
$rsa1 = [System.Security.Cryptography.RSA]::Create(2048)
$rsa2 = [System.Security.Cryptography.RSA]::Create(2048)
$cert1 = $null
$cert2 = $null
try {
    $request1 = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
        $subject, $rsa1, [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $request2 = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
        $subject, $rsa2, [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $notBefore = [DateTimeOffset]::UtcNow.AddMinutes(-5)
    $notAfter = [DateTimeOffset]::UtcNow.AddDays(1)
    $cert1 = $request1.CreateSelfSigned($notBefore, $notAfter)
    $cert2 = $request2.CreateSelfSigned($notBefore, $notAfter)
    $fingerprint1 = $cert1.GetCertHashString([System.Security.Cryptography.HashAlgorithmName]::SHA256)
    $fingerprint2 = $cert2.GetCertHashString([System.Security.Cryptography.HashAlgorithmName]::SHA256)
    if ($fingerprint1 -eq $fingerprint2) { throw 'Unexpected duplicate ephemeral cert SHA256.' }

    $expected = $cert1.Subject
    $good = [pscustomobject]@{SignerCertificate=$cert1}
    $wrongCert = [pscustomobject]@{SignerCertificate=$cert2}
    Assert-SignerPublisherSubject -Signature $good -ExpectedPublisherSubject $expected -ExpectedSignerCertificateSha256 $fingerprint1
    Assert-SignerPublisherSubject -Signature $good -ExpectedPublisherSubject $expected.ToLowerInvariant() -ExpectedSignerCertificateSha256 $fingerprint1.ToLowerInvariant()

    $negative = @(
        @{ Name='same publisher but wrong cert'; Signature=$wrongCert; Subject=$expected; Hash=$fingerprint1; Message='does not match the approved certificate' },
        @{ Name='wrong publisher'; Signature=$good; Subject='CN=Someone Else'; Hash=$fingerprint1; Message='differs from the approved value' },
        @{ Name='missing signer'; Signature=$null; Subject=$expected; Hash=$fingerprint1; Message='subject is unavailable' },
        @{ Name='missing certificate'; Signature=[pscustomobject]@{SignerCertificate=$null}; Subject=$expected; Hash=$fingerprint1; Message='subject is unavailable' },
        @{ Name='blank publisher'; Signature=$good; Subject=' '; Hash=$fingerprint1; Message='subject is missing' },
        @{ Name='empty certificate policy'; Signature=$good; Subject=$expected; Hash=''; Message='must be 64 hexadecimal' },
        @{ Name='malformed certificate SHA256'; Signature=$good; Subject=$expected; Hash='badsha'; Message='must be 64 hexadecimal' }
    )
    foreach ($case in $negative) {
        $rejected = $false
        try {
            Assert-SignerPublisherSubject -Signature $case.Signature -ExpectedPublisherSubject $case.Subject -ExpectedSignerCertificateSha256 $case.Hash
        }
        catch { $rejected = $_.Exception.Message.Contains($case.Message) }
        if (-not $rejected) { throw "Publisher certificate contract allowed $($case.Name)." }
    }

    $release = Get-Content -LiteralPath (Join-Path $root '.github/workflows/release.yml') -Raw
    $preflight = Get-Content -LiteralPath (Join-Path $root '.github/workflows/release-preflight.yml') -Raw
    foreach ($yaml in @($release,$preflight)) {
        foreach ($key in @('ARTIFACT_SIGNING_EXPECTED_PUBLISHER_SUBJECT','ARTIFACT_SIGNING_EXPECTED_CERT_SHA256')) {
            if (-not $yaml.Contains($key + ': ${{ vars.' + $key + ' }}')) {
                throw "Production workflow is missing environment policy: $key."
            }
        }
        if (-not $yaml.Contains('-ExpectedPublisherSubject $env:ARTIFACT_SIGNING_EXPECTED_PUBLISHER_SUBJECT') -or
            -not $yaml.Contains('-ExpectedSignerCertificateSha256 $env:ARTIFACT_SIGNING_EXPECTED_CERT_SHA256')) {
            throw 'Production verifier is not bound to approved publisher and certificate SHA256.'
        }
    }
    Write-Output 'PASS: approved subject/cert, mismatched same-name cert, missing policy, wrong publisher.'
}
finally {
    if ($cert1) { $cert1.Dispose() }
    if ($cert2) { $cert2.Dispose() }
    $rsa1.Dispose()
    $rsa2.Dispose()
}
