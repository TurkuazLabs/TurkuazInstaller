# Dosya Yolu: REPO_ROOT/rust/scripts/windows-native-setup-smoke.ps1
# Amac: Rust Standart Kurulum EXE paketi icin Windows kurulum E2E testi
# Modul - PowerShell QA Script
# Version: 0.1.1
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

    # A user-created file must survive a receipt-checked local uninstall.
    $receipt = Join-Path $target '.turkuaz-local-install.json'
    if (-not (Test-Path -LiteralPath $receipt -PathType Leaf)) {
        throw 'Owned local install receipt is missing.'
    }
    $userFile = Join-Path $target 'user-notes.txt'
    Set-Content -LiteralPath $userFile -Value 'user-owned fixture' -Encoding UTF8
    & $cli uninstall-local --package-id 'qa.native' --target $target --confirm-uninstall YES
    if ($LASTEXITCODE -ne 0) {
        throw 'Receipt-checked local uninstall failed.'
    }
    if ((Test-Path -LiteralPath $installedFile) -or
        (Test-Path -LiteralPath $receipt) -or
        -not (Test-Path -LiteralPath $userFile)) {
        throw 'Local uninstall did not preserve user data or remove only owned files.'
    }
    Write-Host 'NATIVE_RUST_SETUP_UNINSTALL_PRESERVES_USER_FILES'

    # Old installations without a receipt must never be auto-cleaned.
    $unknown = Join-Path $base 'foreign-install'
    New-Item -ItemType Directory -Path $unknown -ErrorAction Stop | Out-Null
    $unknownFile = Join-Path $unknown 'unowned.txt'
    Set-Content -LiteralPath $unknownFile -Value 'do not delete' -Encoding UTF8
    & $cli uninstall-local --package-id 'qa.native' --target $unknown --confirm-uninstall YES
    if ($LASTEXITCODE -eq 0 -or -not (Test-Path -LiteralPath $unknownFile)) {
        throw 'Unrecognized install was modified or uninstalled without a receipt.'
    }
    Write-Host 'NATIVE_RUST_SETUP_UNTRACKED_INSTALL_REJECTED'

    # Tampering with an owned file blocks uninstall before deletion.
    $changedTarget = Join-Path $base 'changed-install'
    $process = Start-Process -FilePath $setup -ArgumentList @('install-local-self', '--target', ('"' + $changedTarget + '"')) -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw 'Changed-file fixture install failed.'
    }
    $changedFile = Join-Path $changedTarget 'hello.txt'
    Set-Content -LiteralPath $changedFile -Value 'changed' -Encoding UTF8
    & $cli uninstall-local --package-id 'qa.native' --target $changedTarget --confirm-uninstall YES
    if ($LASTEXITCODE -eq 0 -or -not (Test-Path -LiteralPath $changedFile) -or
        -not (Test-Path -LiteralPath (Join-Path $changedTarget '.turkuaz-local-install.json'))) {
        throw 'Modified payload was deleted or receipt vanished.'
    }
    Write-Host 'NATIVE_RUST_SETUP_CHANGED_PAYLOAD_PROTECTED'
}
finally {
    if ($KeepArtifacts) {
        Write-Host "TEST_ARTIFACTS: $base"
    } elseif (Test-Path -LiteralPath $base) {
        Remove-Item -LiteralPath $base -Recurse -Force
    }
}
