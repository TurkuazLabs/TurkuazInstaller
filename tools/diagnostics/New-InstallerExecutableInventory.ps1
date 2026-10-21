# Dosya Yolu: REPO_ROOT/tools/diagnostics/New-InstallerExecutableInventory.ps1
# Amac: Installer dagitimindaki uc EXE icin degistirilemez LAB kimlik kaydi olusturur
# Modul - PowerShell Tool
# Version: 1.0.0
# Aciklama: Guvenilmeyen girdilerden calistirma yapmadan SHA256 ve Authenticode kaniti toplar
# Bagimli Oldugu Katman: Tool | Security | Testing

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$DistributionRoot,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputPath,

    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$SourceCommit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 or later is required.' }
if (-not [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)) {
    throw 'Windows-only diagnostic; no files were executed.'
}
if (-not (Test-Path -LiteralPath $DistributionRoot -PathType Container)) {
    throw 'DistributionRoot must be an existing directory.'
}
if (-not [IO.Path]::IsPathFullyQualified($OutputPath)) {
    throw 'OutputPath must be absolute.'
}

$root = (Resolve-Path -LiteralPath $DistributionRoot).ProviderPath.TrimEnd([IO.Path]::DirectorySeparatorChar)
$destination = [IO.Path]::GetFullPath($OutputPath)
$comparison = [StringComparison]::OrdinalIgnoreCase
if ($destination.Equals($root, $comparison) -or
    $destination.StartsWith(($root + [IO.Path]::DirectorySeparatorChar), $comparison)) {
    throw 'Evidence must be stored outside the distribution.'
}
if (Test-Path -LiteralPath $destination) {
    throw 'Existing evidence must not be replaced.'
}
$parent = Split-Path -Parent $destination
if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    throw 'Evidence parent must already exist.'
}

$relativePaths = @(
    'TurkuazInstaller.Bootstrapper.exe',
    'app/TurkuazInstaller.WinUI.exe',
    'cli/TurkuazInstaller.Cli.exe'
)
$results = @(
    foreach ($relative in $relativePaths) {
        $path = Join-Path $root $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing installer executable: $relative"
        }
        $directory = Split-Path -Parent $path
        foreach ($candidate in @($root, $directory, $path)) {
            $info = Get-Item -LiteralPath $candidate -Force
            if (($info.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Reparse point is not accepted for $relative"
            }
        }
        $file = Get-Item -LiteralPath $path -Force
        if ($file.Length -le 0) { throw "Empty executable: $relative" }
        $first = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        $signature = Get-AuthenticodeSignature -LiteralPath $path
        $second = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        $fileAfter = Get-Item -LiteralPath $path -Force
        if ($first -ne $second -or $file.Length -ne $fileAfter.Length) {
            throw "File changed during evidence collection: $relative"
        }
        [pscustomobject]@{
            relative_path = $relative.Replace('\', '/')
            sha256 = $second
            size_bytes = $fileAfter.Length
            authenticode_status = [string]$signature.Status
        }
    }
)

$record = [ordered]@{
    schema_version = 1
    diagnostic_only = $true
    antivirus_scan_performed = $false
    installer_executed = $false
    production_acceptance = 'NOT_ACCEPTED'
    source_commit_unverified = if ($SourceCommit) { $SourceCommit.ToLowerInvariant() } else { $null }
    collection_time_utc = [DateTimeOffset]::UtcNow.ToString('o')
    executables = $results
}
$json = $record | ConvertTo-Json -Depth 6
$stream = [IO.FileStream]::new(
    $destination, [IO.FileMode]::CreateNew,
    [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $writer = [IO.StreamWriter]::new($stream, [Text.UTF8Encoding]::new($false))
    try { $writer.Write($json); $writer.Flush() }
    finally { $writer.Dispose() }
}
finally { $stream.Dispose() }
Write-Output "Read-only LAB evidence written: $destination (NOT_ACCEPTED)"
