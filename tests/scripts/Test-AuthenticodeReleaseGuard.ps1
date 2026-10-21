# Dosya Yolu: REPO_ROOT/tests/scripts/Test-AuthenticodeReleaseGuard.ps1
# Amac: Signed release icin zorunlu combined EXE kontrolunun fail-closed davranisini dogrular
# Modul - PowerShell Test
# Version: 1.0.0
# Aciklama: Eksik/bos dosya, gecersiz imza ve release workflow guard kontrati
# Bagimli Oldugu Katman: Tool | Security | Testing

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $IsWindows) { throw 'Windows runner is required for release Authenticode guard.' }
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).ProviderPath
$verifier = Join-Path $repoRoot 'tools/Assert-AuthenticodeRelease.ps1'
if (-not (Test-Path -LiteralPath $verifier -PathType Leaf)) { throw 'Missing release verifier.' }

$workflow = Get-Content -LiteralPath (Join-Path $repoRoot '.github/workflows/release.yml') -Raw
$requiredCalls = @(
    './tools/Assert-AuthenticodeRelease.ps1 -ReleaseRoot artifacts/release/win-x64/distribution -RequireCombinedLayout',
    './tools/Assert-AuthenticodeRelease.ps1 -ReleaseRoot artifacts/release/win-arm64/distribution -RequireCombinedLayout'
)
foreach ($call in $requiredCalls) {
    if (-not $workflow.Contains($call)) {
        throw 'Production release is not enforcing required combined EXE layout for both architectures.'
    }
}

$work = Join-Path ([IO.Path]::GetTempPath()) ('Turkuaz İmza Test ' + [Guid]::NewGuid().ToString('N'))
$root = Join-Path $work 'distribution'
[IO.Directory]::CreateDirectory($root) | Out-Null
try {
    $bootstrap = Join-Path $root 'TurkuazInstaller.Bootstrapper.exe'
    $app = Join-Path $root 'app/TurkuazInstaller.WinUI.exe'
    $cli = Join-Path $root 'cli/TurkuazInstaller.Cli.exe'
    [IO.Directory]::CreateDirectory((Split-Path -Parent $app)) | Out-Null
    [IO.Directory]::CreateDirectory((Split-Path -Parent $cli)) | Out-Null
    [IO.File]::WriteAllBytes($bootstrap, [byte[]](0x4d, 0x5a))

    $missingRejected = $false
    try { & $verifier -ReleaseRoot $root -RequireCombinedLayout | Out-Null }
    catch {
        $missingRejected = $_.Exception.Message.Contains('Required combined release executable missing')
    }
    if (-not $missingRejected) { throw 'Missing WinUI or CLI was not rejected by the mandatory layout gate.' }

    [IO.File]::WriteAllBytes($app, [byte[]](0x4d, 0x5a))
    [IO.File]::WriteAllBytes($cli, [byte[]]::new(0))
    $emptyRejected = $false
    try { & $verifier -ReleaseRoot $root -RequireCombinedLayout | Out-Null }
    catch {
        $emptyRejected = $_.Exception.Message.Contains('Required combined release executable is empty')
    }
    if (-not $emptyRejected) { throw 'Empty CLI was not rejected by the mandatory layout gate.' }

    # All mandatory paths now exist; SignTool verification must still reject invalid binaries.
    [IO.File]::WriteAllBytes($cli, [byte[]](0x4d, 0x5a))
    $windowsKitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
    $signTool = @(Get-ChildItem -Path $windowsKitsRoot -Filter signtool.exe -File -Recurse |
        Where-Object { $_.DirectoryName -match '\\x64$' } |
        Sort-Object FullName -Descending | Select-Object -First 1)
    if ($signTool.Count -eq 0) { throw 'This Windows test requires an installed x64 SignTool.' }

    $originalHashes = @(@($bootstrap, $app, $cli) | ForEach-Object {
        (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash
    })
    $invalidRejected = $false
    try { & $verifier -ReleaseRoot $root -RequireCombinedLayout | Out-Null }
    catch { $invalidRejected = $true }
    if (-not $invalidRejected) { throw 'Invalid unsigned/test fixture passed production signature verification.' }

    $currentHashes = @(@($bootstrap, $app, $cli) | ForEach-Object {
        (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash
    })
    if (($originalHashes -join ',') -ne ($currentHashes -join ',')) {
        throw 'Release verifier modified fixture files.'
    }
    Write-Output 'PASS: mandatory x64/ARM64 workflow calls, missing/empty binaries, invalid signature rejection.'
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
