# Dosya Yolu: REPO_ROOT/tests/scripts/Test-InstallerExecutableInventory.ps1
# Amac: Gercek kurulum yapmadan dagitim EXE kanit uretimini dogrular
# Modul - PowerShell Test
# Version: 1.0.0
# Aciklama: Uc dosya SHA256, cikti korunumu, eksik dosya ve harici rapor zorunlulugu
# Bagimli Oldugu Katman: Tool | Testing

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Windows-only acceptance test.' }

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).ProviderPath
$script = Join-Path $repoRoot 'tools/diagnostics/New-InstallerExecutableInventory.ps1'
if (-not (Test-Path -LiteralPath $script -PathType Leaf)) { throw 'Evidence generator missing.' }
$source = (Get-Process -Id $PID).Path
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw 'Windows test fixture missing.' }

$work = Join-Path ([IO.Path]::GetTempPath()) ('turkuaz-distribution-evidence-' + [guid]::NewGuid().ToString('N'))
$distribution = Join-Path $work 'distribution'
$outputDir = Join-Path $work 'evidence'
$null = New-Item -ItemType Directory -Path $distribution, $outputDir -Force
try {
    $relativePaths = @(
        'TurkuazInstaller.Bootstrapper.exe',
        'app/TurkuazInstaller.WinUI.exe',
        'cli/TurkuazInstaller.Cli.exe'
    )
    foreach ($relative in $relativePaths) {
        $dest = Join-Path $distribution $relative
        $null = New-Item -ItemType Directory -Path (Split-Path -Parent $dest) -Force
        Copy-Item -LiteralPath $source -Destination $dest -ErrorAction Stop
    }
    $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    $output = Join-Path $outputDir 'executable-inventory.json'
    $sourceSha = ('a' * 40)
    & $script -DistributionRoot $distribution -OutputPath $output -SourceCommit $sourceSha | Out-Null
    $report = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
    if ($report.schema_version -ne 1 -or $report.production_acceptance -ne 'NOT_ACCEPTED' -or
        $report.antivirus_scan_performed -ne $false -or $report.installer_executed -ne $false) {
        throw 'Invalid diagnostic-only output contract.'
    }
    if ($report.source_commit_unverified -ne $sourceSha) { throw 'Source ref not recorded.' }
    if ($report.executables.Count -ne 3) { throw 'Expected exactly three executable identities.' }
    foreach ($i in 0..2) {
        if ($report.executables[$i].relative_path -ne $relativePaths[$i] -or
            $report.executables[$i].sha256 -ne $hash -or
            $report.executables[$i].size_bytes -le 0 -or
            [string]::IsNullOrWhiteSpace($report.executables[$i].authenticode_status)) {
            throw "Invalid file identity at index $i"
        }
    }
    $oldBytes = [IO.File]::ReadAllBytes($output)
    $failedOverwrite = $false
    try { & $script -DistributionRoot $distribution -OutputPath $output | Out-Null }
    catch { $failedOverwrite = $true }
    if (-not $failedOverwrite) { throw 'Existing evidence file was not rejected.' }
    if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($oldBytes)) -ne [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($output)))) {
        throw 'Existing evidence file changed.'
    }

    $inside = Join-Path $distribution 'should-not-write.json'
    $failedInside = $false
    try { & $script -DistributionRoot $distribution -OutputPath $inside | Out-Null }
    catch { $failedInside = $true }
    if (-not $failedInside -or (Test-Path -LiteralPath $inside)) {
        throw 'Evidence allowed inside distribution.'
    }

    $missingFile = Join-Path $distribution 'cli/TurkuazInstaller.Cli.exe'
    Remove-Item -LiteralPath $missingFile -Force
    $missingOutput = Join-Path $outputDir 'missing.json'
    $failedMissing = $false
    try { & $script -DistributionRoot $distribution -OutputPath $missingOutput | Out-Null }
    catch { $failedMissing = $true }
    if (-not $failedMissing -or (Test-Path -LiteralPath $missingOutput)) {
        throw 'Incomplete distribution produced evidence.'
    }
    Write-Output 'PASS: triple EXE inventory, protected evidence, missing file, no execution.'
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
