# Dosya Yolu: REPO_ROOT/rust/scripts/windows-real-installer-uat.ps1
# Amac: Guncel TurkuazInstaller Studio ve CLI icin gercek test kurulumu olusturmak
# Modul - PowerShell Windows Integration QA
# Version: 0.1.2
# Aciklama: Ornek ci.smoke yerine ayni derlemenin Studio ve CLI ikilisini native setup'a paketler
# Bagimli Oldugu Katman: QA | Tool

param(
    [Parameter(Mandatory = $true)]
    [string]$CliPath,
    [Parameter(Mandatory = $true)]
    [string]$StudioPath,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if (-not $IsWindows) { throw 'Windows PowerShell 7 is required.' }
$cli = (Resolve-Path -LiteralPath $CliPath -ErrorAction Stop).ProviderPath
$studio = (Resolve-Path -LiteralPath $StudioPath -ErrorAction Stop).ProviderPath
$pkgId = 'turkuaz.installer.tools'
$targetOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $targetOutput) {
    throw 'Refusing to overwrite an existing integrated test artifact directory.'
}
$work = Join-Path $env:RUNNER_TEMP ('turkuaz-full-suite-' + [guid]::NewGuid().ToString('N'))
$source = Join-Path $work 'source'
$package = Join-Path $work 'tools.tzpkg'
$testTarget = Join-Path $work 'turkuaz.installer.tools'
New-Item -ItemType Directory -Path $source -Force | Out-Null
try {
    Copy-Item -LiteralPath $cli -Destination (Join-Path $source 'TurkuazInstallerCLI.exe')
    Copy-Item -LiteralPath $studio -Destination (Join-Path $source 'TurkuazInstallerStudio.exe')
    New-Item -ItemType Directory -Path $targetOutput -ErrorAction Stop | Out-Null
    $setup = Join-Path $targetOutput 'TurkuazInstaller-TestSetup.exe'

    & $cli build-package --package-id $pkgId --version $Version --source $source --output $package --executable TurkuazInstallerStudio.exe
    if ($LASTEXITCODE -ne 0) { throw 'Packaging actual Studio and CLI binaries failed.' }
    # Verify the actual archive contains BOTH tools before creating its setup.
    $zip = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        $archiveNames = @($zip.Entries | ForEach-Object { $_.FullName })
        foreach ($name in @('TurkuazInstallerCLI.exe', 'TurkuazInstallerStudio.exe')) {
            if ($archiveNames -notcontains "payload/$name") {
                throw "Actual tools archive is missing payload/$name"
            }
        }
        Write-Host 'FULL_SUITE_ARCHIVE_CONTAINS_BOTH_TOOLS_PASS'
    } finally {
        $zip.Dispose()
    }
    & $cli build-native-setup --stub $studio --package $package --output $setup
    if ($LASTEXITCODE -ne 0) { throw 'Building actual TurkuazInstaller test setup failed.' }
    if (!(Test-Path -LiteralPath $setup -PathType Leaf) -or (Get-Item -LiteralPath $setup).Length -lt 1024) {
        throw 'Integrated setup executable is missing or unexpectedly small.'
    }

    # BRK-REAL-SETUP-UAT-001: Install only in unique ephemeral RUNNER_TEMP.
    # Never touch any existing machine or user installation.
    & $setup install-local-self --target $testTarget
    if ($LASTEXITCODE -ne 0) { throw 'Integrated setup could not install the actual tools.' }
    Write-Host 'FULL_SUITE_TEMP_INSTALL_PASS'
    $deployedFiles = @(Get-ChildItem -LiteralPath $testTarget -File -Recurse |
        ForEach-Object { $_.FullName.Substring($testTarget.Length).TrimStart('\') })
    Write-Host "FULL_SUITE_DEPLOYED_FILE_COUNT=$($deployedFiles.Count)"
    Write-Host "FULL_SUITE_DEPLOYED_NAMES=$($deployedFiles -join ',')"
    foreach ($name in @('TurkuazInstallerCLI.exe', 'TurkuazInstallerStudio.exe')) {
        $expected = (Get-FileHash -LiteralPath (Join-Path $source $name) -Algorithm SHA256).Hash
        $actualPath = Join-Path $testTarget $name
        if (!(Test-Path -LiteralPath $actualPath -PathType Leaf)) {
            # Do not disable or bypass endpoint protection to pass the test.
            if (Get-Command Get-MpThreatDetection -ErrorAction SilentlyContinue) {
                try {
                    $hits = @(Get-MpThreatDetection -ErrorAction Stop | Where-Object {
                        $resources = [string]::Join(';', @($_.Resources))
                        $resources.IndexOf($testTarget, [StringComparison]::OrdinalIgnoreCase) -ge 0
                    })
                    Write-Host "FULL_SUITE_DEFENDER_TARGET_DETECTION_COUNT=$($hits.Count)"
                } catch {
                    Write-Warning 'Defender detection status could not be read.'
                }
            }
            throw "The installed tool is missing: $name"
        }
        $actual = (Get-FileHash -LiteralPath $actualPath -Algorithm SHA256).Hash
        if ($actual -ne $expected) { throw "The installed tool differs from source build: $name" }
        Write-Host "FULL_SUITE_INSTALLED_SHA256_PASS: $name"
    }
    if (!(Test-Path -LiteralPath (Join-Path $testTarget '.turkuaz-local-install.json') -PathType Leaf)) {
        throw 'The installed tools are missing their local install receipt.'
    }
    Write-Host 'FULL_SUITE_RECEIPT_PASS'
    & (Join-Path $testTarget 'TurkuazInstallerStudio.exe') --version
    if ($LASTEXITCODE -ne 0) { throw 'Installed Studio version check failed.' }
    Write-Host 'FULL_SUITE_STUDIO_VERSION_PASS'

    # Default CLI must not silently overwrite an occupied target.
    & $setup install-local-self --target $testTarget 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) { throw 'Native CLI silently overwrote an existing target.' }
    & $cli uninstall-local --package-id $pkgId --target $testTarget --confirm-uninstall YES
    if ($LASTEXITCODE -ne 0) { throw 'Receipt-checked test uninstall failed.' }
    if (Test-Path -LiteralPath (Join-Path $testTarget 'TurkuazInstallerStudio.exe')) {
        throw 'Test uninstall left an owned Studio executable.'
    }

    # Copy runnable tools for direct Studio or CLI testing without invoking setup.
    foreach ($name in @('TurkuazInstallerCLI.exe', 'TurkuazInstallerStudio.exe')) {
        Copy-Item -LiteralPath (Join-Path $source $name) -Destination (Join-Path $targetOutput $name)
    }
    $readme = @"
TurkuazInstaller - Güncel Windows geliştirme test paketi

Bu paket ci.smoke örneği DEĞİLDİR. Aynı CI commit'inde derlenmiş gerçek
TurkuazInstaller Studio ve CLI dosyalarını ve bunları kuran native
TurkuazInstaller-TestSetup.exe dosyasını içerir.

Studio: TurkuazInstallerStudio.exe (doğrudan açılabilir)
CLI: TurkuazInstallerCLI.exe (--help, --version komutlarını destekler)
Test Setup: TurkuazInstaller-TestSetup.exe (test klasörüne kurulum sihirbazı)

Sürüm: $Version
Commit: $env:GITHUB_SHA

YALNIZCA GELİŞTİRME VE UAT TESTİ İÇİNDİR. Production yayını değildir.
Windows yayıncı imzası ve SmartScreen itibarı garantisi yoktur.
Chrome dosyayı tehlikeli diyerek engelliyorsa güvenlik korumasını aşmayın.
Dosya SHA-256 değerlerini GitHub Actions run özeti ile karşılaştırın.
Kurulum için yalnızca ayrılmış Windows test VM'i kullanın.
Mevcut klasörü geri dönüşüme taşıma ve UAC onayı gerçek Windows UAT
kapsamındadır; CI testleri bunların yerine geçmez.
"@
    $enc = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText((Join-Path $targetOutput 'README-TR.txt'), $readme, $enc)
    $manifest = [ordered]@{
        ArtifactRole = 'full-suite-test-only'
        Product = 'TurkuazInstaller'
        PackageId = $pkgId
        Version = $Version
        GitCommit = [string]$env:GITHUB_SHA
        NativeInstallerSelfTest = 'passed'
        ExistingTargetDefaultGuard = 'passed'
        ProductionRelease = $false
        AuthenticodePublisher = 'not_verified_for_production'
        WindowsUat = 'not_run'
        Features = @('Studio PR69', 'CLI PR70', 'Recycle PR71', 'UAC PR72')
    }
    [IO.File]::WriteAllText(
        (Join-Path $targetOutput 'BUILD-MANIFEST.json'),
        ($manifest | ConvertTo-Json -Depth 5),
        $enc
    )
    $items = @(Get-ChildItem -LiteralPath $targetOutput -File | Sort-Object Name)
    $hashLines = @($items | ForEach-Object {
        "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()) *$($_.Name)"
    })
    [IO.File]::WriteAllText(
        (Join-Path $targetOutput 'SHA256SUMS.txt'),
        (($hashLines -join [Environment]::NewLine) + [Environment]::NewLine),
        $enc
    )
    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
        $summary = @(
            '### TurkuazInstaller full integrated Windows UAT build'
            ''
            'TEST ONLY - unsigned/not production trusted'
            ''
            "Commit: $env:GITHUB_SHA"
            "Product version: $Version"
            'Native tool installer: build/install/hash check/default-no-overwrite/uninstall PASS'
            ''
            '| File | SHA-256 |'
            '| --- | --- |'
        )
        $summary += $items | ForEach-Object {
            "| $($_.Name) | $((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()) |"
        }
        $summary += ''
        $summary += 'Hashes verify CI file identity, not malware safety or Windows publisher trust.'
        [IO.File]::AppendAllText(
            $env:GITHUB_STEP_SUMMARY,
            (($summary -join [Environment]::NewLine) + [Environment]::NewLine),
            $enc
        )
    }
    Write-Host 'FULL_TURKUAZINSTALLER_NATIVE_SETUP_BUILD_INSTALL_VERIFY_UNINSTALL_PASS'
    Write-Host "FULL_SUITE_TEST_ARTIFACT_FILE_COUNT=$($items.Count)"
}
finally {
    if (Test-Path -LiteralPath $work) {
        try {
            Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction Stop
        } catch {
            # CI's disposable RUNNER_TEMP will be removed after the job.
            # Cleanup must not hide the original validation failure.
            Write-Warning "Ephemeral test cleanup was deferred: $($_.Exception.Message)"
        }
    }
}
