# Dosya Yolu: REPO_ROOT/tests/scripts/Test-AuthenticodeLabEvidence.ps1
# Amac: Windows imza kanit toplayicisinin guvenli salt okunur davranisini test eder
# Modul - PowerShell Test
# Version: 1.0.0
# Aciklama: Imzali sistem dosyasi, rapor semasi ve olumsuz girdileri dogrular
# Bagimli Oldugu Katman: Tool | Testing

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $IsWindows) { throw 'Test requires a Windows runner.' }
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).ProviderPath
$collector = Join-Path $repoRoot 'tools/diagnostics/Get-AuthenticodeLabEvidence.ps1'
if (-not (Test-Path -LiteralPath $collector -PathType Leaf)) { throw 'Missing collector.' }
$fixture = (Get-Process -Id $PID).Path
if (-not (Test-Path -LiteralPath $fixture -PathType Leaf)) { throw 'PowerShell executable fixture missing.' }

$work = Join-Path ([IO.Path]::GetTempPath()) ('turkuaz-av-evidence-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($work) | Out-Null
try {
    $resultPath = Join-Path $work 'case.json'
    $before = (Get-FileHash -LiteralPath $fixture -Algorithm SHA256).Hash.ToLowerInvariant()
    & $collector -ArtifactPath $fixture -CaseId 'REV-01' -OutputPath $resultPath
    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) { throw 'Evidence report not created.' }
    $report = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if ($report.schema_version -ne 1 -or $report.case_id -ne 'REV-01') { throw 'Invalid report identity.' }
    if ($report.artifact_sha256 -ne $before) { throw 'SHA256 did not match source.' }
    if ($report.release_acceptance -ne 'NOT_ACCEPTED' -or $report.installer_winverifytrust_result -ne 'NOT_RUN') {
        throw 'Diagnostic was incorrectly marked as production-verification evidence.'
    }
    if ([string]::IsNullOrWhiteSpace($report.windows_authenticode_status)) { throw 'Signature status missing.' }
    $after = (Get-FileHash -LiteralPath $fixture -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($before -ne $after) { throw 'Source fixture changed.' }

    $originalReport = [IO.File]::ReadAllBytes($resultPath)
    $rejectedOverwrite = $false
    try { & $collector -ArtifactPath $fixture -CaseId 'REV-01' -OutputPath $resultPath | Out-Null }
    catch { $rejectedOverwrite = $true }
    if (-not $rejectedOverwrite) { throw 'Existing report overwrite was not rejected.' }
    if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($originalReport)) -ne [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($resultPath)))) {
        throw 'Existing report bytes changed.'
    }

    $rejectedMissing = $false
    try { & $collector -ArtifactPath (Join-Path $work 'missing.exe') -CaseId 'REV-01' -OutputPath (Join-Path $work 'missing.json') | Out-Null }
    catch { $rejectedMissing = $true }
    if (-not $rejectedMissing) { throw 'Missing artifact was not rejected.' }
    if (Test-Path -LiteralPath (Join-Path $work 'missing.json')) { throw 'Missing artifact created evidence.' }

    Write-Output 'PASS: diagnostic schema, immutable fixture, overwrite guard, missing-file guard.'
}
finally { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue }
