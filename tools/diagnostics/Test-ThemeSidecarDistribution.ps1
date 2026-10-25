# Dosya Yolu: tools/diagnostics/Test-ThemeSidecarDistribution.ps1
# Amac: Signed/Encrypted theme sidecar dosyasinin app/ konumuna eksiksiz kopyalandigini kontrol eder
# Modul - PowerShell CI Tool
# Version: 0.1.0-qa
# Aciklama: Synthetic unsigned bytes only; proves layout and failure safety, NOT cryptographic acceptance
# Bagimli Oldugu Katman: Tool | CI | Packaging

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$packager = Join-Path $PSScriptRoot '../New-CombinedDistribution.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('turkuaz-theme-layout-' + [guid]::NewGuid().ToString('N'))
$bootstrap = Join-Path $root 'publish/bootstrap'
$winui = Join-Path $root 'publish/winui'
$cli = Join-Path $root 'publish/cli'
$out = Join-Path $root 'distribution'
$asset = Join-Path $root 'asset'

function CheckFailure([scriptblock]$action, [string]$testName) {
    $failed = $false
    try { & $action } catch { $failed = $true }
    if (-not $failed) { throw "Expected fail-closed layout refusal: $testName" }
    Write-Host "THEME_LAYOUT_REJECT_OK $testName"
}

try {
    @($bootstrap, $winui, $cli, $asset) | ForEach-Object {
        New-Item -ItemType Directory -Path $_ -Force | Out-Null
    }
    [IO.File]::WriteAllBytes((Join-Path $bootstrap 'TurkuazInstaller.Bootstrapper.exe'), [byte[]](1..64))
    [IO.File]::WriteAllBytes((Join-Path $winui 'TurkuazInstaller.WinUI.exe'), [byte[]](1..64))
    [IO.File]::WriteAllBytes((Join-Path $cli 'TurkuazInstaller.Cli.exe'), [byte[]](1..64))

    foreach ($name in @('theme.tpk', 'theme.tpe')) {
        $source = Join-Path $asset $name
        [byte[]]$payload = 0..255
        [IO.File]::WriteAllBytes($source, $payload)
        & $packager -BootstrapRoot $bootstrap -WinUiRoot $winui -CliRoot $cli -OutputRoot $out -ThemePackPath $source
        $copied = Join-Path (Join-Path $out 'app') $name
        if (-not (Test-Path -LiteralPath $copied -PathType Leaf)) {
            throw "Signed/encrypted sidecar was not placed beside WinUI: $name"
        }
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -cne
            (Get-FileHash -LiteralPath $copied -Algorithm SHA256).Hash) {
            throw "Sidecar bytes changed in distribution: $name"
        }
        $other = if ($name -eq 'theme.tpk') { 'theme.tpe' } else { 'theme.tpk' }
        if (Test-Path -LiteralPath (Join-Path (Join-Path $out 'app') $other)) {
            throw "Unexpected second sidecar in distribution: $other"
        }
        Write-Host "THEME_LAYOUT_COPY_OK $name"
    }

    $bad = Join-Path $asset 'not-theme.tpk'
    [IO.File]::WriteAllBytes($bad, [byte[]](1..64))
    CheckFailure {
        & $packager -BootstrapRoot $bootstrap -WinUiRoot $winui -CliRoot $cli -OutputRoot $out -ThemePackPath $bad
    } 'unapproved-filename'

    $missing = Join-Path $asset 'theme.tpk-missing'
    CheckFailure {
        & $packager -BootstrapRoot $bootstrap -WinUiRoot $winui -CliRoot $cli -OutputRoot $out -ThemePackPath $missing
    } 'missing-file'

    $short = Join-Path $asset 'theme.tpk'
    [IO.File]::WriteAllBytes($short, [byte[]](1..4))
    CheckFailure {
        & $packager -BootstrapRoot $bootstrap -WinUiRoot $winui -CliRoot $cli -OutputRoot $out -ThemePackPath $short
    } 'truncated-file'

    [IO.File]::WriteAllBytes($short, [byte[]](1..64))
    [IO.File]::WriteAllBytes((Join-Path $winui 'theme.tpk'), [byte[]](1..64))
    CheckFailure {
        & $packager -BootstrapRoot $bootstrap -WinUiRoot $winui -CliRoot $cli -OutputRoot $out -ThemePackPath $short
    } 'publish-input-collision'

    Write-Host 'THEME_SIDECAR_LAYOUT_TESTS_OK'
}
finally {
    if (Test-Path -LiteralPath $root) {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }
}
