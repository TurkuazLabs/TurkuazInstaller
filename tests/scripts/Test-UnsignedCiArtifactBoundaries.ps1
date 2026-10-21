# Dosya Yolu: REPO_ROOT/tests/scripts/Test-UnsignedCiArtifactBoundaries.ps1
# Amac: Windows Desktop CI imzasiz dagitimlarinin production assetleriyle karismasini onler
# Modul - PowerShell Test
# Version: 1.0.0
# Aciklama: x64/ARM64 unsigned artifact adi, 3 gun saklama ve signed release kontratini denetler
# Bagimli Oldugu Katman: Tool | Security | Testing

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).ProviderPath
$desktop = (Get-Content -LiteralPath (Join-Path $root '.github/workflows/windows-bootstrap.yml') -Raw).Replace("`r`n", "`n")
$release = Get-Content -LiteralPath (Join-Path $root '.github/workflows/release.yml') -Raw

# @dev-id TINS-ART-010
# @purpose Stop short-lived unsigned build downloads from appearing to be signed production releases.
# @reason Legacy Windows Desktop CI artifacts used indistinguishable, long-retained product-like names.
# @contract Both architectures must have explicit TEST-ONLY-UNSIGNED names and 3-day retention.
# @tests tests/scripts/Test-UnsignedCiArtifactBoundaries.ps1
foreach ($arch in @('x64', 'arm64')) {
    $expected = @"
      - name: Upload UNSIGNED test-only $arch distribution
        uses: actions/upload-artifact@v4
        with:
          name: TurkuazInstaller-TEST-ONLY-UNSIGNED-DISTRIBUTION-win-$arch
          path: artifacts/distribution/win-$arch/
          if-no-files-found: error
          retention-days: 3
"@
    if (-not $desktop.Contains($expected)) {
        throw "Unsigned CI distribution contract is missing for $arch."
    }

    if (-not $release.Contains("TurkuazInstaller-win-$arch.zip")) {
        throw "Signed production ZIP asset is missing for $arch."
    }
}
if ($desktop -match '(?m)^\s*name:\s*TurkuazInstaller-win-(?:x64|arm64)\s*$') {
    throw 'Legacy production-like Windows CI artifact name must not be used.'
}
if ($desktop -match '(?m)^\s*-\s*name:\s*Publish GitHub Release\s*$') {
    throw 'Windows Desktop CI must never publish a production GitHub Release.'
}
Write-Output 'PASS: unsigned CI distribution labels and retention remain separate from signed release assets.'
