# Dosya Yolu: REPO_ROOT/rust/scripts/windows-test-artifact-inventory.ps1
# Amac: Windows CI test artifact dosyalarini SHA-256 ve boyutla dogrulamak
# Modul - PowerShell QA Tool
# Version: 0.1.1
# Aciklama: CI checkout SHA ve kaynak head kimligiyle artifact dogrulamasi
# Bagimli Oldugu Katman: QA | Tool

# @dev-id TI-RC-ARTIFACT-001
# @purpose Record SHA-256 of every allowlisted test file and verify the
# uploaded/downloaded GitHub Actions ZIP payload by exact file identity.
# @contract No signing, publishing, fallback or arbitrary file discovery.
# The manifest is evidence from CI, not a cryptographic trust root.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Generate', 'Verify')]
    [string]$Mode,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-fA-F0-9]{40}$')]
    [string]$SourceCommit,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$InventoryPath,

    [string]$ExampleRoot,
    [string]$ArtifactRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_SHA -cnotmatch '^[a-fA-F0-9]{40} = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../target/release'))
$exampleFiles = @(
    'TurkuazSetup.exe',
    "turkuaz-installer-v$Version-windows-x64.exe",
    'manifest-trust.yaml',
    'README-TR.txt'
)
$artifactNames = [ordered]@{
    "turkuaz-installer-cli-v$Version-windows-x64" = @("turkuaz-installer-v$Version-windows-x64.exe")
    "turkuaz-setup-gui-stub-v$Version-windows-x64" = @("turkuaz-setup-gui-stub-v$Version-windows-x64.exe")
    "turkuaz-web-setup-test-v$Version-windows-x64" = @('TurkuazWebSetup-test.exe')
    "turkuaz-setup-builder-v$Version-windows-x64" = @("turkuaz-setup-builder-v$Version-windows-x64.exe")
    "turkuaz-example-test-setup-v$Version-windows-x64" = $exampleFiles
}
$inventoryArtifact = "turkuaz-test-artifact-inventory-v$Version-windows-x64"
$inventoryFileName = 'test-artifact-inventory.json'

function Assert-FilesExact {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string[]]$Expected
    )

    if (!(Test-Path -LiteralPath $Root -PathType Container)) {
        throw "Required artifact directory does not exist: $Root"
    }
    $found = @(Get-ChildItem -LiteralPath $Root -Force)
    $actualNames = @($found | ForEach-Object { $_.Name } | Sort-Object)
    $expectedNames = @($Expected | Sort-Object)
    if ($actualNames.Count -ne $expectedNames.Count) {
        throw "Artifact file count mismatch in $Root. Expected $($expectedNames.Count), found $($actualNames.Count)."
    }
    for ($i = 0; $i -lt $actualNames.Count; $i++) {
        if ($actualNames[$i] -cne $expectedNames[$i]) {
            throw "Artifact path mismatch in $Root. Expected '$($expectedNames[$i])', found '$($actualNames[$i])'."
        }
    }
    foreach ($name in $Expected) {
        $file = Get-Item -LiteralPath (Join-Path $Root $name) -Force -ErrorAction Stop
        if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Artifact entry is not a regular file: $name"
        }
        if ($file.Length -le 0) {
            throw "Artifact file is empty: $name"
        }
    }
}

function Get-Record {
    param([string]$Artifact, [string]$Root, [string]$RelativePath)

    $file = Get-Item -LiteralPath (Join-Path $Root $RelativePath) -ErrorAction Stop
    return [ordered]@{
        artifact = $Artifact
        path = $RelativePath
        size_bytes = [int64]$file.Length
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

if ($Mode -eq 'Generate') {
    if ([string]::IsNullOrWhiteSpace($ExampleRoot)) {
        throw 'Generate requires ExampleRoot.'
    }
    if (Test-Path -LiteralPath $InventoryPath) {
        throw 'Refusing to overwrite an existing artifact inventory.'
    }
    $records = @()
    foreach ($artifact in $artifactNames.Keys) {
        $root = switch -Wildcard ($artifact) {
            'turkuaz-web-setup-test-*' { $env:RUNNER_TEMP; break }
            'turkuaz-example-test-setup-*' { $ExampleRoot; break }
            default { $releaseRoot }
        }
        # The release directory contains other build outputs: inspect only the
        # explicitly named file(s). The example distribution is an exact tree.
        if ($artifact -like 'turkuaz-example-test-setup-*') {
            Assert-FilesExact -Root $root -Expected $artifactNames[$artifact]
        } else {
            foreach ($name in $artifactNames[$artifact]) {
                $info = Get-Item -LiteralPath (Join-Path $root $name) -Force -ErrorAction Stop
                if ($info.PSIsContainer -or $info.Length -le 0 -or
                    ($info.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                    throw "Expected a non-empty regular test artifact: $name"
                }
            }
        }
        foreach ($name in $artifactNames[$artifact]) {
            $records += Get-Record -Artifact $artifact -Root $root -RelativePath $name
        }
    }

    # PR workflows build the synthetic merge checkout, not only the PR head.
    $checkoutCommit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or
        $checkoutCommit.ToLowerInvariant() -cne $env:GITHUB_SHA.ToLowerInvariant()) {
        throw 'GitHub checkout commit differs from the tested build revision.'
    }
    $inventory = [ordered]@{
        schema_version = 1
        test_only = $true
        package_version = $Version
        source_commit = $SourceCommit.ToLowerInvariant()
        checkout_commit = $checkoutCommit.ToLowerInvariant()
        files = $records
    }
    $serialized = ConvertTo-Json -InputObject $inventory -Depth 6
    [IO.File]::WriteAllText(
        [IO.Path]::GetFullPath($InventoryPath),
        $serialized + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false)
    )
    Write-Host "Generated TEST-ONLY SHA-256 inventory for $($records.Count) files across $($artifactNames.Count) artifacts."
    return
}

if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    throw 'Verify requires ArtifactRoot.'
}
if (!(Test-Path -LiteralPath $InventoryPath -PathType Leaf)) {
    throw 'Downloaded artifact SHA-256 inventory is missing.'
}
$root = [IO.Path]::GetFullPath($ArtifactRoot)
Assert-FilesExact -Root (Join-Path $root $inventoryArtifact) -Expected @($inventoryFileName)
$expectedInventoryPath = [IO.Path]::GetFullPath((Join-Path (Join-Path $root $inventoryArtifact) $inventoryFileName))
if (![string]::Equals([IO.Path]::GetFullPath($InventoryPath), $expectedInventoryPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Verify must read the round-tripped downloaded inventory, not a local source copy.'
}

$inventory = Get-Content -LiteralPath $expectedInventoryPath -Raw | ConvertFrom-Json
if ($inventory.schema_version -ne 1 -or $inventory.test_only -cne $true -or
    $inventory.package_version -cne $Version -or
    $inventory.source_commit -cne $SourceCommit.ToLowerInvariant() -or
    $inventory.checkout_commit -cne $env:GITHUB_SHA.ToLowerInvariant()) {
    throw 'Test artifact inventory provenance or schema mismatch.'
}
$expectedFolders = @($artifactNames.Keys) + @($inventoryArtifact)
$actualFolders = @(Get-ChildItem -LiteralPath $root -Force)
if ($actualFolders.Count -ne $expectedFolders.Count -or
    @($actualFolders | Where-Object { !$_.PSIsContainer -or
        ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) }).Count -ne 0) {
    throw 'Downloaded artifact root must contain only the expected directories.'
}
$expectedCount = 0
foreach ($artifact in $artifactNames.Keys) {
    if (!(Test-Path -LiteralPath (Join-Path $root $artifact) -PathType Container)) {
        throw "Missing downloaded artifact directory: $artifact"
    }
    Assert-FilesExact -Root (Join-Path $root $artifact) -Expected $artifactNames[$artifact]
    $expectedCount += @($artifactNames[$artifact]).Count
}
foreach ($entry in $actualFolders) {
    if ($expectedFolders -cnotcontains $entry.Name) {
        throw "Unexpected downloaded artifact: $($entry.Name)"
    }
}
$entries = @($inventory.files)
if ($entries.Count -ne $expectedCount) {
    throw "Inventory entry count mismatch. Expected $expectedCount, found $($entries.Count)."
}
$seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($entry in $entries) {
    $artifact = [string]$entry.artifact
    $fileName = [string]$entry.path
    if (!$artifactNames.Contains($artifact) -or
        $artifactNames[$artifact] -cnotcontains $fileName -or
        !$seen.Add("$artifact/$fileName")) {
        throw 'Inventory contains unknown or duplicate artifact file.'
    }
    if ([string]$entry.sha256 -cnotmatch '^[a-f0-9]{64}$' -or
        [long]$entry.size_bytes -le 0) {
        throw 'Inventory contains invalid size or SHA-256.'
    }
    $actual = Get-Record -Artifact $artifact -Root (Join-Path $root $artifact) -RelativePath $fileName
    if ($actual.size_bytes -ne [long]$entry.size_bytes -or
        $actual.sha256 -cne [string]$entry.sha256) {
        throw "Downloaded artifact file differs from inventory: $artifact/$fileName"
    }
}
Write-Host "Verified $($entries.Count) TEST-ONLY files, $($artifactNames.Count) artifact ZIP payloads, exact names, lengths and SHA-256."
) {
    throw 'A valid GitHub checkout commit SHA is required for artifact provenance.'
}

$releaseRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../target/release'))
$exampleFiles = @(
    'TurkuazSetup.exe',
    "turkuaz-installer-v$Version-windows-x64.exe",
    'manifest-trust.yaml',
    'README-TR.txt'
)
$artifactNames = [ordered]@{
    "turkuaz-installer-cli-v$Version-windows-x64" = @("turkuaz-installer-v$Version-windows-x64.exe")
    "turkuaz-setup-gui-stub-v$Version-windows-x64" = @("turkuaz-setup-gui-stub-v$Version-windows-x64.exe")
    "turkuaz-web-setup-test-v$Version-windows-x64" = @('TurkuazWebSetup-test.exe')
    "turkuaz-setup-builder-v$Version-windows-x64" = @("turkuaz-setup-builder-v$Version-windows-x64.exe")
    "turkuaz-example-test-setup-v$Version-windows-x64" = $exampleFiles
}
$inventoryArtifact = "turkuaz-test-artifact-inventory-v$Version-windows-x64"
$inventoryFileName = 'test-artifact-inventory.json'

function Assert-FilesExact {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string[]]$Expected
    )

    if (!(Test-Path -LiteralPath $Root -PathType Container)) {
        throw "Required artifact directory does not exist: $Root"
    }
    $found = @(Get-ChildItem -LiteralPath $Root -Force)
    $actualNames = @($found | ForEach-Object { $_.Name } | Sort-Object)
    $expectedNames = @($Expected | Sort-Object)
    if ($actualNames.Count -ne $expectedNames.Count) {
        throw "Artifact file count mismatch in $Root. Expected $($expectedNames.Count), found $($actualNames.Count)."
    }
    for ($i = 0; $i -lt $actualNames.Count; $i++) {
        if ($actualNames[$i] -cne $expectedNames[$i]) {
            throw "Artifact path mismatch in $Root. Expected '$($expectedNames[$i])', found '$($actualNames[$i])'."
        }
    }
    foreach ($name in $Expected) {
        $file = Get-Item -LiteralPath (Join-Path $Root $name) -Force -ErrorAction Stop
        if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Artifact entry is not a regular file: $name"
        }
        if ($file.Length -le 0) {
            throw "Artifact file is empty: $name"
        }
    }
}

function Get-Record {
    param([string]$Artifact, [string]$Root, [string]$RelativePath)

    $file = Get-Item -LiteralPath (Join-Path $Root $RelativePath) -ErrorAction Stop
    return [ordered]@{
        artifact = $Artifact
        path = $RelativePath
        size_bytes = [int64]$file.Length
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

if ($Mode -eq 'Generate') {
    if ([string]::IsNullOrWhiteSpace($ExampleRoot)) {
        throw 'Generate requires ExampleRoot.'
    }
    if (Test-Path -LiteralPath $InventoryPath) {
        throw 'Refusing to overwrite an existing artifact inventory.'
    }
    $records = @()
    foreach ($artifact in $artifactNames.Keys) {
        $root = switch -Wildcard ($artifact) {
            'turkuaz-web-setup-test-*' { $env:RUNNER_TEMP; break }
            'turkuaz-example-test-setup-*' { $ExampleRoot; break }
            default { $releaseRoot }
        }
        # The release directory contains other build outputs: inspect only the
        # explicitly named file(s). The example distribution is an exact tree.
        if ($artifact -like 'turkuaz-example-test-setup-*') {
            Assert-FilesExact -Root $root -Expected $artifactNames[$artifact]
        } else {
            foreach ($name in $artifactNames[$artifact]) {
                $info = Get-Item -LiteralPath (Join-Path $root $name) -Force -ErrorAction Stop
                if ($info.PSIsContainer -or $info.Length -le 0 -or
                    ($info.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                    throw "Expected a non-empty regular test artifact: $name"
                }
            }
        }
        foreach ($name in $artifactNames[$artifact]) {
            $records += Get-Record -Artifact $artifact -Root $root -RelativePath $name
        }
    }

    $inventory = [ordered]@{
        schema_version = 1
        test_only = $true
        package_version = $Version
        source_commit = $SourceCommit.ToLowerInvariant()
        files = $records
    }
    $serialized = ConvertTo-Json -InputObject $inventory -Depth 6
    [IO.File]::WriteAllText(
        [IO.Path]::GetFullPath($InventoryPath),
        $serialized + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false)
    )
    Write-Host "Generated TEST-ONLY SHA-256 inventory for $($records.Count) files across $($artifactNames.Count) artifacts."
    return
}

if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    throw 'Verify requires ArtifactRoot.'
}
if (!(Test-Path -LiteralPath $InventoryPath -PathType Leaf)) {
    throw 'Downloaded artifact SHA-256 inventory is missing.'
}
$root = [IO.Path]::GetFullPath($ArtifactRoot)
Assert-FilesExact -Root (Join-Path $root $inventoryArtifact) -Expected @($inventoryFileName)
$expectedInventoryPath = [IO.Path]::GetFullPath((Join-Path (Join-Path $root $inventoryArtifact) $inventoryFileName))
if (![string]::Equals([IO.Path]::GetFullPath($InventoryPath), $expectedInventoryPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Verify must read the round-tripped downloaded inventory, not a local source copy.'
}

$inventory = Get-Content -LiteralPath $expectedInventoryPath -Raw | ConvertFrom-Json
if ($inventory.schema_version -ne 1 -or $inventory.test_only -cne $true -or
    $inventory.package_version -cne $Version -or
    $inventory.source_commit -cne $SourceCommit.ToLowerInvariant()) {
    throw 'Test artifact inventory provenance or schema mismatch.'
}
$expectedFolders = @($artifactNames.Keys) + @($inventoryArtifact)
$actualFolders = @(Get-ChildItem -LiteralPath $root -Force)
if ($actualFolders.Count -ne $expectedFolders.Count -or
    @($actualFolders | Where-Object { !$_.PSIsContainer -or
        ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) }).Count -ne 0) {
    throw 'Downloaded artifact root must contain only the expected directories.'
}
$expectedCount = 0
foreach ($artifact in $artifactNames.Keys) {
    if (!(Test-Path -LiteralPath (Join-Path $root $artifact) -PathType Container)) {
        throw "Missing downloaded artifact directory: $artifact"
    }
    Assert-FilesExact -Root (Join-Path $root $artifact) -Expected $artifactNames[$artifact]
    $expectedCount += @($artifactNames[$artifact]).Count
}
foreach ($entry in $actualFolders) {
    if ($expectedFolders -cnotcontains $entry.Name) {
        throw "Unexpected downloaded artifact: $($entry.Name)"
    }
}
$entries = @($inventory.files)
if ($entries.Count -ne $expectedCount) {
    throw "Inventory entry count mismatch. Expected $expectedCount, found $($entries.Count)."
}
$seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($entry in $entries) {
    $artifact = [string]$entry.artifact
    $fileName = [string]$entry.path
    if (!$artifactNames.Contains($artifact) -or
        $artifactNames[$artifact] -cnotcontains $fileName -or
        !$seen.Add("$artifact/$fileName")) {
        throw 'Inventory contains unknown or duplicate artifact file.'
    }
    if ([string]$entry.sha256 -cnotmatch '^[a-f0-9]{64}$' -or
        [long]$entry.size_bytes -le 0) {
        throw 'Inventory contains invalid size or SHA-256.'
    }
    $actual = Get-Record -Artifact $artifact -Root (Join-Path $root $artifact) -RelativePath $fileName
    if ($actual.size_bytes -ne [long]$entry.size_bytes -or
        $actual.sha256 -cne [string]$entry.sha256) {
        throw "Downloaded artifact file differs from inventory: $artifact/$fileName"
    }
}
Write-Host "Verified $($entries.Count) TEST-ONLY files, $($artifactNames.Count) artifact ZIP payloads, exact names, lengths and SHA-256."
