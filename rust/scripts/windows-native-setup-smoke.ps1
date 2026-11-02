# Dosya Yolu: REPO_ROOT/rust/scripts/windows-native-setup-smoke.ps1
# Amac: Rust Standart Kurulum EXE paketi icin Windows kurulum E2E testi
# Modul - PowerShell QA Script
# Version: 0.1.0
# Aciklama: NSIS ve sertifika olmadan olustur, kur, icerigi dogrula, bozulmayi reddet
# Bagimli Oldugu Katman: QA | Tool
param(
    [Parameter(Mandatory = $true)]
    [string]$CliPath,
    [Parameter(Mandatory = $true)]
    [string]$StudioStubPath,
    [switch]$KeepArtifacts
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if (-not $IsWindows) { throw 'Windows PowerShell 7 required.' }
$cli = (Resolve-Path -LiteralPath $CliPath -ErrorAction Stop).ProviderPath
$studio = (Resolve-Path -LiteralPath $StudioStubPath -ErrorAction Stop).ProviderPath
$base = Join-Path $env:TEMP ('turkuaz-rust-native-' + [guid]::NewGuid().ToString('N'))
$source = Join-Path $base 'source'
$package = Join-Path $base 'application.tzpkg'
$setup = Join-Path $base 'TurkuazSetup.exe'
$target = Join-Path $base 'installed'
$badTarget = Join-Path $base 'tampered-install'
$payload = 'Unsigned Native Rust Installer QA'

New-Item -ItemType Directory -Path $source -ErrorAction Stop | Out-Null
try {
    Set-Content -LiteralPath (Join-Path $source 'hello.txt') -Value $payload -Encoding UTF8
    & $cli build-package --package-id qa.native --version 1.0.0 --source $source --output $package
    if ($LASTEXITCODE -ne 0) { throw 'Rust package builder failed.' }

    & $cli build-native-setup --stub $studio --package $package --output $setup
    if ($LASTEXITCODE -ne 0) { throw 'Native Rust setup build failed.' }
    $file = [IO.File]::ReadAllBytes($setup)
    if ($file.Length -lt 100 -or $file[0] -ne 77 -or $file[1] -ne 90) {
        throw 'Native setup is not a Windows MZ executable.'
    }
    Write-Host 'NATIVE_RUST_SETUP_BUILD_PASS'

    $process = Start-Process -FilePath $setup -ArgumentList @('install-local-self', '--target', "`"$target`"") -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Native Rust setup exited with code $($process.ExitCode)."
    }
    $installedFile = Join-Path $target 'hello.txt'
    if (-not (Test-Path -LiteralPath $installedFile -PathType Leaf)) {
        throw 'Native Rust setup did not extract the application.'
    }
    $text = Get-Content -LiteralPath $installedFile -Raw -Encoding UTF8
    if (-not $text.Contains($payload)) {
        throw 'Native Rust setup extracted unexpected payload bytes.'
    }
    Write-Host 'NATIVE_RUST_SETUP_INSTALL_PASS'

    # A forged/tampered package must never install.
    $tampered = Join-Path $base 'tampered.exe'
    $file[$file.Length - 49] = $file[$file.Length - 49] -bxor 1
    [IO.File]::WriteAllBytes($tampered, $file)
    $process = Start-Process -FilePath $tampered -ArgumentList @('install-local-self', '--target', "`"$badTarget`"") -Wait -PassThru
    if ($process.ExitCode -eq 0 -or (Test-Path -LiteralPath $badTarget)) {
        throw 'Tampered unsigned Rust setup was accepted.'
    }
    Write-Host 'NATIVE_RUST_SETUP_TAMPER_REJECTED'

    $process = Start-Process -FilePath $setup -ArgumentList @('install-local-self', '--target', "`"$target`"") -Wait -PassThru
    if ($process.ExitCode -eq 0) { throw 'Existing install folder was overwritten.' }
    Write-Host 'NATIVE_RUST_SETUP_OVERWRITE_REJECTED'
}
finally {
    if ($KeepArtifacts) {
        Write-Host "TEST_ARTIFACTS: $base"
    } elseif (Test-Path -LiteralPath $base) {
        Remove-Item -LiteralPath $base -Recurse -Force
    }
}
