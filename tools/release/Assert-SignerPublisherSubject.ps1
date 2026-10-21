# Dosya Yolu: REPO_ROOT/tools/release/Assert-SignerPublisherSubject.ps1
# Amac: Authenticode imzali dosyanin beklenen yayinici sertifika subject kimligini dogrular
# Modul - PowerShell Tool
# Version: 1.1.0
# Aciklama: Yasal yayinci kimligine bagli fail-closed isim kontrolu; imza zinciri yerine gecmez
# Bagimli Oldugu Katman: Tool | Security

# @dev-id TINS-SIGN-012
# @purpose Enforce the independently reviewed legal publisher on all signed Turkuaz binaries.
# @reason A valid signature by an unrelated publisher could otherwise satisfy SignTool /pa.
# @contract SignTool verifies chain separately. Require the approved X.500 subject AND SHA-256 hash of the leaf X.509 certificate.
# @tests tests/scripts/Test-SignerPublisherSubject.ps1
function Assert-SignerPublisherSubject {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [object]$Signature,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$ExpectedPublisherSubject,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$ExpectedSignerCertificateSha256
    )

    if ([string]::IsNullOrWhiteSpace($ExpectedPublisherSubject)) {
        throw 'Expected publisher subject is missing.'
    }
    if ($ExpectedSignerCertificateSha256 -cnotmatch '^[0-9A-Fa-f]{64}$') {
        throw 'Approved signer certificate SHA-256 must be 64 hexadecimal characters.'
    }

    $actual = $null
    if ($null -ne $Signature -and $null -ne $Signature.SignerCertificate) {
        $actual = [string]$Signature.SignerCertificate.Subject
    }
    if ([string]::IsNullOrWhiteSpace($actual)) {
        throw 'Signing certificate subject is unavailable.'
    }
    if (-not $actual.Equals($ExpectedPublisherSubject, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Signing certificate publisher subject differs from the approved value.'
    }

    $certificate = $Signature.SignerCertificate
    if ($certificate -isnot [System.Security.Cryptography.X509Certificates.X509Certificate2]) {
        throw 'Signing certificate must be an X509Certificate2.'
    }
    $actualSha256 = $certificate.GetCertHashString([System.Security.Cryptography.HashAlgorithmName]::SHA256)
    if (-not $actualSha256.Equals($ExpectedSignerCertificateSha256, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Signing certificate SHA-256 does not match the approved certificate.'
    }
}
