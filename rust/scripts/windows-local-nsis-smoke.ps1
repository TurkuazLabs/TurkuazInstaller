# Dosya Yolu: REPO_ROOT/rust/scripts/windows-local-nsis-smoke.ps1
# Amac: Imzasiz yerel NSIS test EXE'sini derlemek ve yalniz izole hedefe kurmak
# Modul - PowerShell Acceptance Script
# Version: 0.1.0
# Aciklama: Imzali production workflow'a dokunmadan NSIS template E2E smoke
# Bagimli Oldugu Katman: QA | Tool
param(
    [string]$MakensisPath = '',
    [switch]$KeepArtifacts
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if (-not $IsWindows) { throw 'Windows 10/11 and PowerShell 7 required.' }
if (-not $env:LOCALAPPDATA -or -not $env:TEMP) { throw 'Windows per-user test folders are unavailable.' }

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).ProviderPath
$template = Join-Path $repoRoot 'packaging/TurkuazLocalTest.nsi'
if (-not (Test-Path -LiteralPath $template -PathType Leaf)) { throw 'NSIS source template missing.' }

if ($MakensisPath) {
    $makensis = (Resolve-Path -LiteralPath $MakensisPath).ProviderPath
} else {
    $makensis = $null
    foreach ($var in @('ProgramFiles(x86)', 'ProgramFiles')) {
        $root = [Environment]::GetEnvironmentVariable($var)
        if ($root) {
            $candidate = Join-Path $root 'NSIS/makensis.exe'
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { $makensis = $candidate; break }
        }
    }
    if (-not $makensis) {
        $exe = Get-Command 'makensis.exe' -ErrorAction SilentlyContinue
        if ($exe) { $makensis = $exe.Source }
    }
    if (-not $makensis) { throw 'NSIS makensis.exe bulunamadi; once NSIS kurun veya -MakensisPath kullanin.' }
}

$id = [guid]::NewGuid().ToString('N')
$packageId = "qa.local.$id"
$stage = Join-Path $env:TEMP "turkuaz-nsis-qa-$id"
$payload = Join-Path $stage 'payload'
$exePath = Join-Path $stage 'LOCAL-TEST-ONLY.exe'
$target = Join-Path (Join-Path $env:LOCALAPPDATA 'TurkuazInstaller/LocalTests') $packageId

if ((Test-Path -LiteralPath $stage) -or (Test-Path -LiteralPath $target)) {
    throw 'Unexpected test collision; existing directories must not be modified.'
}

$createdStage = $false
try {
    New-Item -ItemType Directory -Path $payload -Force -ErrorAction Stop | Out-Null
    $createdStage = $true
    Set-Content -LiteralPath (Join-Path $payload 'hello.txt') -Value "TurkuazInstaller local NSIS smoke: $id" -Encoding UTF8

    & $makensis "/DSOURCE_DIR=$payload" "/DOUTPUT_PATH=$exePath" "/DAPP_NAME=TurkuazLocalQA" "/DPACKAGE_ID=$packageId" $template
    if ($LASTEXITCODE -ne 0) { throw "NSIS compilation failed (exit $LASTEXITCODE)." }
    if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) { throw 'NSIS test EXE was not created.' }
    $bytes = [IO.File]::ReadAllBytes($exePath)
    if ($bytes.Length -lt 128 -or $bytes[0] -ne 77 -or $bytes[1] -ne 90) {
        throw 'Local test EXE does not have an MZ header.'
    }
    Write-Host 'LOCAL_NSIS_COMPILE_PASS'

    # Unique PackageId guarantees the silent install uses a fresh per-user path.
    $process = Start-Process -FilePath $exePath -ArgumentList @('/S') -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Local test install failed (exit $($process.ExitCode))." }
    $installedFile = Join-Path $target 'hello.txt'
    if (-not (Test-Path -LiteralPath $installedFile -PathType Leaf)) {
        throw 'NSIS silent install did not create expected application file.'
    }
    $text = Get-Content -LiteralPath $installedFile -Raw -Encoding UTF8
    if (-not $text.Contains($id)) { throw 'Installed payload does not match smoke fixture.' }
    Write-Host 'LOCAL_NSIS_INSTALL_PASS'
    Write-Host 'LOCAL_NSIS_TEST_ONLY_PASS'
}
finally {
    if ($KeepArtifacts) {
        Write-Host "TEST_ARTIFACTS: $stage"
        Write-Host "TEST_INSTALL: $target"
    } else {
        # Remove only two unique folders created by this smoke test, never any parent.
        if ($createdStage -and (Test-Path -LiteralPath $stage)) {
            Remove-Item -LiteralPath $stage -Recurse -Force
        }
        if (Test-Path -LiteralPath $target) {
            Remove-Item -LiteralPath $target -Recurse -Force
        }
    }
}
