# Dosya Yolu: REPO_ROOT/rust/scripts/windows-rc-verify-download.ps1
# Amac: Windows'a indirilmis test artifact'larini CI SHA-256 kontratiyla kontrol etmek
# Modul - PowerShell QA Tool
# Version: 0.1.0
# Aciklama: Actions disinda test dosyalarinin isim, boyut ve SHA-256 on kontrolu
# Bagimli Oldugu Katman: QA | Tool

# @dev-id TI-RC-ARTIFACT-002
# @purpose Reuse the CI-verified test-artifact inventory checker on Windows
# without requiring the caller to set GitHub Actions environment variables.
# @contract This is an unsigned integrity check, not publisher identity or
# attestation. No installer is executed, no certificate trust is changed.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ArtifactRoot,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-fA-F0-9]{40}$')]
    [string]$SourceCommit,

    [Parameter(Mandatory = $false)]
    [ValidatePattern('^[a-fA-F0-9]{40}$')]
    [string]$ExpectedCheckoutCommit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = [IO.Path]::GetFullPath($ArtifactRoot)
$inventoryFolder = "turkuaz-test-artifact-inventory-v$Version-windows-x64"
$inventoryFile = Join-Path (Join-Path $root $inventoryFolder) 'test-artifact-inventory.json'
if (!(Test-Path -LiteralPath $inventoryFile -PathType Leaf)) {
    throw "Expected downloaded test artifact inventory is missing: $inventoryFile"
}
$inventory = Get-Content -LiteralPath $inventoryFile -Raw -ErrorAction Stop | ConvertFrom-Json
if ($inventory.schema_version -ne 1 -or $inventory.test_only -cne $true -or
    $inventory.package_version -cne $Version -or
    $inventory.source_commit -cne $SourceCommit.ToLowerInvariant()) {
    throw 'Downloaded test artifact inventory does not match the expected PR source commit, version or schema.'
}

$checkout = [string]$inventory.checkout_commit
if ($checkout -cnotmatch '^[a-f0-9]{40}$') {
    throw 'Downloaded test artifact inventory has invalid checkout commit.'
}
if (![string]::IsNullOrWhiteSpace($ExpectedCheckoutCommit)) {
    if ($checkout -cne $ExpectedCheckoutCommit.ToLowerInvariant()) {
        throw 'Downloaded test artifact inventory does not match the independently provided CI checkout commit.'
    }
} elseif ($env:GITHUB_ACTIONS -eq 'true') {
    if ($env:GITHUB_SHA -cnotmatch '^[a-fA-F0-9]{40}$' -or
        $checkout -cne $env:GITHUB_SHA.ToLowerInvariant()) {
        throw 'Downloaded test artifact inventory does not match the current Actions checkout.'
    }
} else {
    Write-Warning 'The checkout commit was read from an unsigned test inventory. For independent provenance, supply -ExpectedCheckoutCommit from a separately verified source.'
}

$checker = Join-Path $PSScriptRoot 'windows-test-artifact-inventory.ps1'
if (!(Test-Path -LiteralPath $checker -PathType Leaf)) {
    throw "Shared CI test artifact verifier was not found: $checker"
}

$previousSha = [Environment]::GetEnvironmentVariable('GITHUB_SHA', 'Process')
try {
    # The shared checker consumes GITHUB_SHA, but only for its checkout
    # equality check. The user-supplied SourceCommit is independently checked.
    $env:GITHUB_SHA = $checkout
    & $checker -Mode Verify -Version $Version -SourceCommit $SourceCommit `
        -InventoryPath $inventoryFile -ArtifactRoot $root
} finally {
    if ($null -eq $previousSha) {
        Remove-Item Env:GITHUB_SHA -ErrorAction SilentlyContinue
    } else {
        $env:GITHUB_SHA = $previousSha
    }
}
Write-Host 'Local RC artifact integrity check PASS (TEST-ONLY, unsigned).'
