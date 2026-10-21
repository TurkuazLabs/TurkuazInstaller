# Dosya Yolu: REPO_ROOT/tools/release/Assert-SignerPublisherSubject.ps1
# Amac: Authenticode imzali dosyanin beklenen yayinici sertifika subject kimligini dogrular
# Modul - PowerShell Tool
# Version: 1.0.0
# Aciklama: Yasal yayinci kimligine bagli fail-closed isim kontrolu; imza zinciri yerine gecmez
# Bagimli Oldugu Katman: Tool | Security

# @dev-id TINS-SIGN-012
# @purpose Enforce the independently reviewed legal publisher on all signed Turkuaz binaries.
# @reason A valid signature by an unrelated publisher could otherwise satisfy SignTool /pa.
# @contract SignTool must verify the signature separately; this checks exact X.500 subject, ignoring case.
# @tests tests/scripts/Test-SignerPublisherSubject.ps1
function Assert-SignerPublisherSubject {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [object]$Signature,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$ExpectedPublisherSubject
    )

    if ([string]::IsNullOrWhiteSpace($ExpectedPublisherSubject)) {
        throw 'Expected publisher subject is missing.'
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
}
