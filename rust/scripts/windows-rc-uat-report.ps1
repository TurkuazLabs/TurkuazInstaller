# Dosya Yolu: REPO_ROOT/rust/scripts/windows-rc-uat-report.ps1
# Amac: Izole Windows RC kabul testlerine kanit sablonu olusturmak
# Modul - PowerShell QA Tool
# Version: 0.1.0
# Aciklama: Yerel RC dosya kontrolunden sonra manuel testleri NOT_RUN kaydeder
# Bagimli Oldugu Katman: QA | Documentation

# @dev-id TI-RC-UAT-001
# @purpose Record a minimal test-only Windows environment and immutable
# artifact identity, then generate a manual acceptance report.
# @contract No installer launch, elevation, system change, secret collection,
# claim of manual test success, or replacement of publisher trust.
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

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^https://github\.com/TurkuazLabs/TurkuazInstaller/actions/runs/[1-9][0-9]*$')]
    [string]$RunUrl,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputPath,

    [ValidatePattern('^[a-fA-F0-9]{40}$')]
    [string]$ExpectedCheckoutCommit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1/.NET can reject ADS syntax during GetFullPath,
# so inspect the raw final filename before path normalization.
# A drive-letter colon is not part of the final filename.
if ([IO.Path]::GetFileName($OutputPath).Contains(':')) {
    throw 'Manual UAT report output must not use an NTFS alternate data stream.'
}
$root = [IO.Path]::GetFullPath($ArtifactRoot)
$output = [IO.Path]::GetFullPath($OutputPath)
$separator = [string][IO.Path]::DirectorySeparatorChar
$rootPrefix = $root.TrimEnd([IO.Path]::DirectorySeparatorChar) + $separator
if ([string]::Equals($root, $output, [StringComparison]::OrdinalIgnoreCase) -or
    $output.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to write manual UAT evidence inside the downloaded artifact tree.'
}
if (![string]::Equals([IO.Path]::GetExtension($output), '.json', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Manual UAT report output must be a JSON file.'
}
if (Test-Path -LiteralPath $output) {
    throw 'Refusing to overwrite existing UAT report.'
}
$parent = [IO.Path]::GetDirectoryName($output)
if (!(Test-Path -LiteralPath $parent -PathType Container)) {
    throw 'The manual UAT report parent directory must already exist.'
}
$directory = [IO.DirectoryInfo]::new($parent)
while ($null -ne $directory) {
    $info = Get-Item -LiteralPath $directory.FullName -Force -ErrorAction Stop
    if (!$info.PSIsContainer -or
        ($info.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Manual UAT report output parent must not contain junctions or symlinks.'
    }
    $directory = $directory.Parent
}

$verification = Join-Path $PSScriptRoot 'windows-rc-verify-download.ps1'
if (!(Test-Path -LiteralPath $verification -PathType Leaf)) {
    throw 'Downloaded RC verifier is required to make an acceptance report.'
}
$parameters = @{
    ArtifactRoot = $root
    Version = $Version
    SourceCommit = $SourceCommit
}
if (![string]::IsNullOrWhiteSpace($ExpectedCheckoutCommit)) {
    $parameters.ExpectedCheckoutCommit = $ExpectedCheckoutCommit
}
& $verification @parameters

$inventoryPath = Join-Path (Join-Path $root "turkuaz-test-artifact-inventory-v$Version-windows-x64") 'test-artifact-inventory.json'
$inventory = Get-Content -LiteralPath $inventoryPath -Raw -ErrorAction Stop | ConvertFrom-Json
$cases = @(
    'artifact-identity-and-signature'
    'setup-studio-layout-and-dpi'
    'language-eula-back-next-esc'
    'current-user-install-hkcu'
    'all-users-uac-cancel'
    'all-users-uac-approve-hklm'
    'shortcuts-and-installed-apps'
    'running-app-uninstall-retry'
    'locked-payload-uninstall'
    'foreign-user-files-and-junctions'
    'signed-nonproduction-lifecycle'
    'real-publisher-trust-and-smartscreen'
)
$manualCases = @(
    foreach ($caseId in $cases) {
        [ordered]@{
            id = $caseId
            status = 'NOT_RUN'
            evidence = ''
            notes = ''
        }
    }
)
$checkoutIndependentlyPinned = ![string]::IsNullOrWhiteSpace($ExpectedCheckoutCommit)
$record = [ordered]@{
    schema_version = 1
    test_only = $true
    manual_acceptance = 'NOT_RUN'
    instructions = 'Enter actual observations only after isolated Windows 10/11 manual GUI and UAC tests. Never infer acceptance from this template.'
    ci_run_url = $RunUrl
    package_version = $Version
    source_commit = $SourceCommit.ToLowerInvariant()
    checkout_commit_from_unsigned_inventory = [string]$inventory.checkout_commit
    checkout_commit_independently_pinned = $checkoutIndependentlyPinned
    inventory_sha256 = (Get-FileHash -LiteralPath $inventoryPath -Algorithm SHA256).Hash.ToLowerInvariant()
    local_integrity = 'PASS_UNSIGNED_ONLY'
    os_version = [Environment]::OSVersion.Version.ToString()
    is_64bit_os = [Environment]::Is64BitOperatingSystem
    powershell_version = $PSVersionTable.PSVersion.ToString()
    collected_utc = [DateTime]::UtcNow.ToString('o')
    manual_cases = $manualCases
}
$json = ConvertTo-Json -InputObject $record -Depth 6
$bytes = [Text.UTF8Encoding]::new($false).GetBytes($json + [Environment]::NewLine)
$stream = [IO.File]::Open($output, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Flush()
} finally {
    $stream.Dispose()
}
Write-Host 'Generated TEST-ONLY Windows RC manual acceptance report; every manual case remains NOT_RUN.'
