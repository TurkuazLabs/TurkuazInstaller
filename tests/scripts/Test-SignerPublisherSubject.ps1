# Dosya Yolu: REPO_ROOT/tests/scripts/Test-SignerPublisherSubject.ps1
# Amac: Imzali artefactlarin yayinici X.500 subject sinirini test eder
# Modul - PowerShell Test
# Version: 1.0.1
# Aciklama: Dogru subject, farkli firma, bos/eksik imzaci ve eksik policy vakalari
# Bagimli Oldugu Katman: Tool | Testing

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).ProviderPath
. (Join-Path $root 'tools/release/Assert-SignerPublisherSubject.ps1')

$expected = 'CN=Turkuaz Testing, O=Example Ltd, C=TR'
$ok = [pscustomobject]@{ SignerCertificate = [pscustomobject]@{ Subject = $expected } }
Assert-SignerPublisherSubject -Signature $ok -ExpectedPublisherSubject $expected
$expectedLower = $expected.ToLowerInvariant()
Assert-SignerPublisherSubject -Signature $ok -ExpectedPublisherSubject $expectedLower

$other = [pscustomobject]@{ SignerCertificate = [pscustomobject]@{ Subject = 'CN=Other Software, O=Unknown, C=TR' } }
$negativeCases = @(
    @{ Name='different publisher'; Signature=$other; Expected=$expected; Message='differs from the approved value' },
    @{ Name='missing signer'; Signature=$null; Expected=$expected; Message='subject is unavailable' },
    @{ Name='missing certificate'; Signature=[pscustomobject]@{SignerCertificate=$null}; Expected=$expected; Message='subject is unavailable' },
    @{ Name='blank policy'; Signature=$ok; Expected=' '; Message='subject is missing' }
)
foreach ($case in $negativeCases) {
    $rejected = $false
    try {
        Assert-SignerPublisherSubject -Signature $case.Signature -ExpectedPublisherSubject $case.Expected
    }
    catch {
        $rejected = $_.Exception.Message.Contains($case.Message)
    }
    if (-not $rejected) { throw "Publisher subject contract allowed $($case.Name)." }
}

$release = Get-Content -LiteralPath (Join-Path $root '.github/workflows/release.yml') -Raw
$preflight = Get-Content -LiteralPath (Join-Path $root '.github/workflows/release-preflight.yml') -Raw
$policyKey = 'ARTIFACT_SIGNING_EXPECTED_PUBLISHER_SUBJECT'
foreach ($yaml in @($release,$preflight)) {
    if (-not $yaml.Contains($policyKey + ': ${{ vars.' + $policyKey + ' }}')) {
        throw 'Production workflow is missing the approved publisher environment policy.'
    }
    if (-not $yaml.Contains('-ExpectedPublisherSubject $env:ARTIFACT_SIGNING_EXPECTED_PUBLISHER_SUBJECT')) {
        throw 'Production verifier is not bound to approved publisher policy.'
    }
}
Write-Output 'PASS: approved subject, unapproved publisher, missing certificate and missing policy.'
