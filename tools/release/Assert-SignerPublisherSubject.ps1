# Dosya Yolu: REPO_ROOT/tools/release/Assert-SignerPublisherSubject.ps1
# Amac: Authenticode signer subject ve Artifact Signing profile EKU kimligini dogrular
# Modul - PowerShell Tool
# Version: 1.2.0
# Aciklama: Gunluk sertifika rotasyonunu engellemeden onayli profile EKU zorunlulugu
# Bagimli Oldugu Katman: Tool | Security

# @dev-id TINS-SIGN-012
# @purpose Enforce the approved publisher and immutable Artifact Signing certificate-profile identity.
# @reason Azure rotates short-lived leaf certificates daily; leaf thumbprints cannot be durably pinned.
# @contract SignTool validates trust separately. This checks exact subject and profile-specific EKU.
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
        [string]$ExpectedProfileEkuOid
    )

    if ([string]::IsNullOrWhiteSpace($ExpectedPublisherSubject)) {
        throw 'Expected publisher subject is missing.'
    }
    # The generic Public Trust 1.0 EKU is NOT unique to the approved profile.
    if ($ExpectedProfileEkuOid -cnotmatch '^1\.3\.6\.1\.4\.1\.311\.97\.(?:[0-9]+\.)+[0-9]+$' -or
        $ExpectedProfileEkuOid -eq '1.3.6.1.4.1.311.97.1.0') {
        throw 'Approved Artifact Signing profile EKU must be a profile-specific OID.'
    }

    $certificate = $null
    if ($null -ne $Signature) { $certificate = $Signature.SignerCertificate }
    if ($null -eq $certificate -or [string]::IsNullOrWhiteSpace([string]$certificate.Subject)) {
        throw 'Signing certificate subject is unavailable.'
    }
    $actualSubject = [string]$certificate.Subject
    if (-not $actualSubject.Equals($ExpectedPublisherSubject, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Signing certificate publisher subject differs from the approved value.'
    }
    if ($certificate -isnot [System.Security.Cryptography.X509Certificates.X509Certificate2]) {
        throw 'Signing certificate must be X509Certificate2.'
    }

    $foundProfile = $false
    foreach ($extension in $certificate.Extensions) {
        if ($extension.Oid.Value -ne '2.5.29.37') { continue }
        $enhanced = if ($extension -is [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]) {
            $extension
        } else {
            [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($extension, $extension.Critical)
        }
        foreach ($oid in $enhanced.EnhancedKeyUsages) {
            if ($oid.Value -ceq $ExpectedProfileEkuOid) { $foundProfile = $true }
        }
    }
    if (-not $foundProfile) {
        throw 'Signing certificate is missing the approved Artifact Signing profile EKU.'
    }
}
