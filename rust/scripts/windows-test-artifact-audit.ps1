# Dosya Yolu: REPO_ROOT/rust/scripts/windows-test-artifact-audit.ps1
# Amac: CI ornek kurulum paketinin SHA256, Authenticode ve Defender durumunu raporlamak
# Modul - PowerShell Windows QA Adapter
# Version: 0.1.1
# Aciklama: Gecici self-signed sertifikayi guvenilir kabul etmeden dosya butunlugunu olcer
# Bagimli Oldugu Katman: QA | Security

param(
    [Parameter(Mandatory = $true)]
    [string]$DistributionPath,
    [switch]$ScanWithDefender
)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Windows test artifact audit requires Windows and PowerShell 7.' }
$root = (Resolve-Path -LiteralPath $DistributionPath -ErrorAction Stop).ProviderPath
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw 'Test artifact directory is missing.'
}
$auditPath = Join-Path $root 'SECURITY-AUDIT.json'
$sumsPath = Join-Path $root 'SHA256SUMS.txt'
if ((Test-Path -LiteralPath $auditPath) -or (Test-Path -LiteralPath $sumsPath)) {
    throw 'Audit output already exists; refusing to overwrite.'
}

# BRK-WINDOWS-ARTIFACT-AUDIT-001: Test binaries must share the same ephemeral
# signer. A valid cryptographic signature is NOT a trusted publisher identity.
$signatures = @()
foreach ($name in @('TurkuazSetup.exe', 'TurkuazInstallerBackup.exe')) {
    $file = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Expected test installer missing: $name"
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $file
    $status = [string]$signature.Status
    $message = [string]$signature.StatusMessage
    $untrustedTestRoot = $message -match 'A certificate chain processed,\s*but terminated in a root certificate which is not trusted by the trust provider\.'
    if ($status -notin @('Valid', 'NotTrusted') -and
        -not ($status -eq 'UnknownError' -and $untrustedTestRoot)) {
        throw "Invalid Authenticode signature for $name (status=$status)."
    }
    if (-not $signature.SignerCertificate -or
        $signature.SignerCertificate.Subject -notlike '*CN=TurkuazInstaller CI Smoke*') {
        throw "Unexpected certificate subject for $name."
    }
    $signatures += [pscustomobject]@{
        File = $name
        Status = $status
        SignerSubject = $signature.SignerCertificate.Subject
        SignerThumbprint = $signature.SignerCertificate.Thumbprint
        PublisherTrusted = ($status -eq 'Valid')
    }
}
if ($signatures[0].SignerThumbprint -ne $signatures[1].SignerThumbprint) {
    throw 'Test setup pair has mismatched Authenticode signers.'
}
if ($signatures | Where-Object { $_.PublisherTrusted }) {
    throw 'The ephemeral CI signer was unexpectedly trusted on the runner.'
}

# BRK-WINDOWS-ARTIFACT-SCAN-001: Never claim that an unavailable Defender
# service scanned the package. A detection or changed artifact blocks upload.
$defender = 'not_run'
$defenderNote = 'Defender scan was not requested.'
if ($ScanWithDefender) {
    $defender = 'not_run_unavailable'
    $defenderNote = 'Defender was unavailable or could not complete this scan.'
    $commands = @('Get-MpComputerStatus', 'Start-MpScan', 'Get-MpThreatDetection')
    if (@($commands | Where-Object { -not (Get-Command $_ -ErrorAction SilentlyContinue) }).Count -eq 0) {
        try {
            $state = Get-MpComputerStatus -ErrorAction Stop
            if ($state.AMServiceEnabled -and $state.AntivirusEnabled) {
                Start-MpScan -ScanType CustomScan -ScanPath $root -ErrorAction Stop
                $hits = @(Get-MpThreatDetection -ErrorAction Stop | Where-Object {
                    $resources = [string]::Join(';', @($_.Resources))
                    $resources.IndexOf($root, [StringComparison]::OrdinalIgnoreCase) -ge 0
                })
                if ($hits.Count -gt 0) {
                    throw 'DEFENDER_DETECTION_IN_TEST_ARTIFACT'
                }
                $defender = 'scan_completed_no_reported_detection'
                $defenderNote = 'A single local Defender scan finished; it does not prove that the archive is safe.'
            }
        } catch {
            if ($_.Exception.Message -like '*DEFENDER_DETECTION_IN_TEST_ARTIFACT*') {
                throw 'Defender reported a detection in the test artifact; upload blocked.'
            }
            Write-Warning "Defender scan could not be confirmed: $($_.Exception.Message)"
        }
    }
}

$files = @(Get-ChildItem -LiteralPath $root -File -Force | Sort-Object Name)
if ($files.Count -lt 5) { throw 'Example distribution contains fewer files than expected.' }
$records = @()
foreach ($file in $files) {
    if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Unexpected reparse point in test artifact: $($file.Name)"
    }
    $records += [pscustomobject]@{
        Name = $file.Name
        SizeBytes = $file.Length
        SHA256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
foreach ($expected in @('TurkuazSetup.exe', 'TurkuazInstallerBackup.exe', 'manifest-trust.yaml', 'README-TR.txt')) {
    if (-not @($records | Where-Object { $_.Name -eq $expected }).Count) {
        throw "Missing required test artifact file: $expected"
    }
}
$report = [ordered]@{
    ArtifactRole = 'test-only'
    ProductionTrusted = $false
    GitCommit = [string]$env:GITHUB_SHA
    GitHubRunId = [string]$env:GITHUB_RUN_ID
    Authenticode = $signatures
    DefenderStatus = $defender
    DefenderNote = $defenderNote
    Files = $records
    Warning = 'SHA256 and ephemeral self-signed Authenticode checks are not a malware-free or trusted-publisher guarantee.'
}
$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($auditPath, ($report | ConvertTo-Json -Depth 8), $utf8)
$sumLines = @($records | ForEach-Object { "$($_.SHA256) *$($_.Name)" })
$sumLines += "$((Get-FileHash -LiteralPath $auditPath -Algorithm SHA256).Hash.ToLowerInvariant()) *SECURITY-AUDIT.json"
[IO.File]::WriteAllText($sumsPath, (($sumLines -join [Environment]::NewLine) + [Environment]::NewLine), $utf8)
# BRK-WINDOWS-ARTIFACT-SUMMARY-001: Make expected hashes visible on
# GitHub without asking users to bypass Chrome's download protection.
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    $summary = @(
        '### Windows example test artifact audit'
        ''
        '**TEST-ONLY / UNTRUSTED CI SIGNER / NOT A PRODUCTION RELEASE**'
        ''
        "Commit: $($env:GITHUB_SHA)"
        ''
        "Defender: $defender"
        ''
        '| Filename | SHA-256 |'
        '| --- | --- |'
    )
    $summary += @($records | ForEach-Object { "| $($_.Name) | $($_.SHA256) |" })
    $summary += ''
    $summary += 'SHA-256 proves file identity against this CI report, not malware safety. Chrome Safe Browsing may still warn.'
    [IO.File]::AppendAllText(
        $env:GITHUB_STEP_SUMMARY,
        (($summary -join [Environment]::NewLine) + [Environment]::NewLine),
        $utf8
    )
}
Write-Host "TEST_ARTIFACT_SHA256_AUDIT_PASS ($($records.Count) input files)"
Write-Host "TEST_ARTIFACT_AUTHENTICODE_EPHEMERAL_SIGNER_PASS"
Write-Host "TEST_ARTIFACT_DEFENDER_STATUS=$defender"
