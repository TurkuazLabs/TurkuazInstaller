# Dosya Yolu: REPO_ROOT/tests/scripts/Test-AuthenticodeReleaseGuard.ps1
# Amac: Imzali release icin combined EXE ve mimari fail-closed davranisini dogrular
# Modul - PowerShell Test
# Version: 1.1.0
# Aciklama: Eksik/bos dosya, yanlis PE machine, bozuk PE, gecersiz imza ve workflow kontrati
# Bagimli Oldugu Katman: Tool | Security | Testing

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Windows runner is required for release Authenticode guard.' }

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).ProviderPath
$verifier = Join-Path $repoRoot 'tools/Assert-AuthenticodeRelease.ps1'
if (-not (Test-Path -LiteralPath $verifier -PathType Leaf)) { throw 'Missing release verifier.' }

$workflow = Get-Content -LiteralPath (Join-Path $repoRoot '.github/workflows/release.yml') -Raw
$requiredCalls = @(
    './tools/Assert-AuthenticodeRelease.ps1 -ReleaseRoot artifacts/release/win-x64/distribution -RequireCombinedLayout -ExpectedArchitecture x64',
    './tools/Assert-AuthenticodeRelease.ps1 -ReleaseRoot artifacts/release/win-arm64/distribution -RequireCombinedLayout -ExpectedArchitecture arm64'
)
foreach ($call in $requiredCalls) {
    if (-not $workflow.Contains($call)) { throw 'Production release must enforce complete, correctly typed executables for both architectures.' }
}

function Write-TestPe {
    param([string]$Path, [ValidateSet('x64', 'arm64')][string]$Architecture)
    # Harmless nonfunctional PE header; it is never executed or signed.
    $data = [byte[]]::new(256)
    $data[0] = 0x4d
    $data[1] = 0x5a
    $data[0x3c] = 0x80
    $data[0x80] = 0x50
    $data[0x81] = 0x45
    $machine = if ($Architecture -eq 'x64') { 0x8664 } else { 0xaa64 }
    $data[0x84] = [byte]($machine -band 0xff)
    $data[0x85] = [byte](($machine -shr 8) -band 0xff)
    [IO.File]::WriteAllBytes($Path, $data)
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

    Write-TestPe -Path $bootstrap -Architecture x64
    $missingRejected = $false
    try { & $verifier -ReleaseRoot $root -RequireCombinedLayout -ExpectedArchitecture x64 | Out-Null }
    catch { $missingRejected = $_.Exception.Message.Contains('Required combined release executable missing') }
    if (-not $missingRejected) { throw 'Missing WinUI/CLI not rejected.' }

    Write-TestPe -Path $app -Architecture x64
    [IO.File]::WriteAllBytes($cli, [byte[]]::new(0))
    $emptyRejected = $false
    try { & $verifier -ReleaseRoot $root -RequireCombinedLayout -ExpectedArchitecture x64 | Out-Null }
    catch { $emptyRejected = $_.Exception.Message.Contains('Required combined release executable is empty') }
    if (-not $emptyRejected) { throw 'Empty CLI not rejected.' }

    Write-TestPe -Path $cli -Architecture arm64
    $mixedRejected = $false
    try { & $verifier -ReleaseRoot $root -RequireCombinedLayout -ExpectedArchitecture x64 | Out-Null }
    catch { $mixedRejected = $_.Exception.Message.Contains('PE architecture mismatch') }
    if (-not $mixedRejected) { throw 'ARM64 CLI inside x64 distribution was accepted.' }

    $wrongTargetRejected = $false
    try { & $verifier -ReleaseRoot $root -RequireCombinedLayout -ExpectedArchitecture arm64 | Out-Null }
    catch { $wrongTargetRejected = $_.Exception.Message.Contains('PE architecture mismatch') }
    if (-not $wrongTargetRejected) { throw 'x64 Bootstrapper inside ARM64 distribution was accepted.' }

    Write-TestPe -Path $cli -Architecture x64
    $missingArchRejected = $false
    try { & $verifier -ReleaseRoot $root -RequireCombinedLayout | Out-Null }
    catch { $missingArchRejected = $_.Exception.Message.Contains('ExpectedArchitecture is mandatory') }
    if (-not $missingArchRejected) { throw 'Production layout check without expected architecture was accepted.' }

    [IO.File]::WriteAllBytes($cli, [byte[]](0x4d,0x5a))
    $brokenPeRejected = $false
    try { & $verifier -ReleaseRoot $root -RequireCombinedLayout -ExpectedArchitecture x64 | Out-Null }
    catch { $brokenPeRejected = $_.Exception.Message.Contains('Invalid PE header offset') }
    if (-not $brokenPeRejected) { throw 'Truncated PE header was not rejected.' }
    Write-TestPe -Path $cli -Architecture x64

    $windowsKitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
    $signTool = @(Get-ChildItem -Path $windowsKitsRoot -Filter signtool.exe -File -Recurse |
        Where-Object { $_.DirectoryName -match '\\x64$' } |
        Sort-Object FullName -Descending | Select-Object -First 1)
    if ($signTool.Count -eq 0) { throw 'Windows test requires installed x64 SignTool.' }

    $paths = @($bootstrap,$app,$cli)
    $before = @($paths | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash })
    $unsignedRejected = $false
    try { & $verifier -ReleaseRoot $root -RequireCombinedLayout -ExpectedArchitecture x64 | Out-Null }
    catch { $unsignedRejected = $true }
    if (-not $unsignedRejected) { throw 'Unsigned synthetic PE passed SignTool.' }

    # Only the negative test catches SignTool's expected native exit code.
    $global:LASTEXITCODE = 0
    $after = @($paths | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash })
    if (($before -join ',') -ne ($after -join ',')) { throw 'Release verifier changed the binaries.' }
    Write-Output 'PASS: mandatory x64/ARM64 PE type, full layout, malformed EXE and invalid signature rejection.'
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
