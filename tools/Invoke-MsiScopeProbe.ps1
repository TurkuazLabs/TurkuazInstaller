# 📄 Dosya Yolu: /tools/Invoke-MsiScopeProbe.ps1
# 📌 Amac: Ortak Velopack MSI PerUser ve PerMachine paketleme kabiliyetini lab ortaminda dogrular
# 📌 Modul - Tool PowerShell
# Version: 1.3.0
# Aciklama: Gercek kurulum/UAC yapmadan pinned vpk ile iki farkli MSI scope paketi olusturur
# Bagimli Oldugu Katman: Tool | Config

[CmdletBinding()]
param(
    [string]$ConfigPath = "config/velopack-e2e.psd1",
    [string]$ArtifactsRoot = "artifacts/installer-msi-scope-probe"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $IsWindows -or
    $env:GITHUB_ACTIONS -cne "true" -or
    $env:RUNNER_ENVIRONMENT -cne "github-hosted" -or
    [string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
    throw "MSI lab requires a disposable GitHub-hosted Windows runner; self-hosted FURY is forbidden."
}

$config = Import-PowerShellDataFile -LiteralPath (Resolve-Path -LiteralPath $ConfigPath)
$root = [System.IO.Path]::GetFullPath($ArtifactsRoot)
if (Test-Path -LiteralPath $root) {
    Remove-Item -LiteralPath $root -Recurse -Force
}

$tools = Join-Path $root "tools"
$payload = Join-Path $root "payload"
$env:VELOPACK_TEMP = Join-Path $root "tmp"
foreach ($folder in @($tools, $payload, $env:VELOPACK_TEMP)) {
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
}

dotnet tool install vpk --tool-path $tools --version $config.VpkVersion
if ($LASTEXITCODE -ne 0) {
    throw "Pinned vpk install failed."
}

$vpk = Join-Path $tools "vpk.exe"
if (-not (Test-Path -LiteralPath $vpk -PathType Leaf)) {
    throw "Pinned vpk binary missing."
}

dotnet publish tests/TurkuazInstaller.Velopack.E2E.Fixture/TurkuazInstaller.Velopack.E2E.Fixture.csproj --configuration Release --runtime win-x64 --self-contained false --output $payload
if ($LASTEXITCODE -ne 0) {
    throw "MSI scope fixture publish failed."
}

$fixtureExe = Join-Path $payload $config.MainExecutable
if (-not (Test-Path -LiteralPath $fixtureExe -PathType Leaf)) {
    throw "MSI scope test fixture missing."
}

Set-Content -LiteralPath (Join-Path $payload $config.VersionMarkerFile) -Value $config.VersionOne -NoNewline -Encoding utf8
$allDigests = @()

foreach ($scopeProfile in $config.MsiProbeScopes) {
    $scope = [string]$scopeProfile.Name
    $output = Join-Path $root $scope
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $packageId = "$($config.MsiProbePackageId).$($scope.ToLowerInvariant())"
    $packLog = & $vpk --legacyConsole true --yes true --skip-updates true pack `
        --runtime win-x64 `
        --packId $packageId `
        --packTitle $config.MsiProbeTitle `
        --packVersion $config.VersionOne `
        --msiVersion "$($config.VersionOne).0" `
        --packDir $payload `
        --mainExe $config.MainExecutable `
        --outputDir $output `
        --delta none `
        --skipVeloAppCheck true `
        --msi `
        --instLocation $scope 2>&1
    $packLog | ForEach-Object { Write-Host $_ }

    if ($LASTEXITCODE -ne 0) {
        throw "Velopack MSI pack failed: $scope"
    }

    $msi = @(Get-ChildItem -LiteralPath $output -Filter "*.msi" -File)
    if ($msi.Count -ne 1 -or $msi[0].Length -lt 4096) {
        throw "Expected exactly one nonempty $scope MSI."
    }

    $header = [byte[]]::new(8)
    $stream = [System.IO.File]::OpenRead($msi[0].FullName)
    try {
        if ($stream.Read($header, 0, 8) -ne 8) {
            throw "MSI binary is truncated: $scope"
        }
    }
    finally {
        $stream.Dispose()
    }
    $expectedHeader = [byte[]](0xD0,0xCF,0x11,0xE0,0xA1,0xB1,0x1A,0xE1)
    for ($i=0; $i -lt $expectedHeader.Length; $i++) {
        if ($header[$i] -ne $expectedHeader[$i]) {
            throw "MSI storage header invalid: $scope"
        }
    }

    $digest = (Get-FileHash -LiteralPath $msi[0].FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $allDigests += "$scope sha256=$digest bytes=$($msi[0].Length)"
    Write-Host "INSTALLER_MSI_SCOPE_PACKAGE_OK $scope sha256=$digest"
}

$reportPath = Join-Path $root "MSI-SCOPE-LAB-ONLY.txt"
$report = @(
    "TURKUAZINSTALLER MSI SCOPE PACKAGING PROBE"
    "LAB ONLY - UNSIGNED; PACKAGE HASHES ONLY"
    "CI validates isolated installation, registered context, and uninstall separately."
    "No interactive standard-user UAC, multi-user lifecycle, or production readiness is claimed."
) + $allDigests
[System.IO.File]::WriteAllLines($reportPath, $report, [System.Text.UTF8Encoding]::new($false))
Write-Host "INSTALLER_MSI_SCOPE_PACKAGING_PROBE_OK"
