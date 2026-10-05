# 📄 Dosya Yolu: /tools/Assert-ReproducibleArchive.ps1
# 📌 Amac: Ayni publish klasorunden uretilen iki deterministic ZIP dosyasinin SHA-256 degerini karsilastirir
# 📌 Modul - Tool PowerShell
# Version: 1.0.0
# Aciklama: Dagitilan release archive formatinin ayni input byte'lari icin tekrar uretilebilir oldugunu dogrular
# Bagimli Oldugu Katman: Tool

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDirectory,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [Parameter(Mandatory = $true)]
    [string]$PackagerPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$resolvedSource = (Resolve-Path -LiteralPath $SourceDirectory).Path
$resolvedPackager = (Resolve-Path -LiteralPath $PackagerPath).Path
$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("turkuaz-archive-" + [guid]::NewGuid().ToString("N"))
$firstPath = Join-Path $tempRoot "first.zip"
$secondPath = Join-Path $tempRoot "second.zip"

try {
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

    python $resolvedPackager --input $resolvedSource --output $firstPath
    if ($LASTEXITCODE -ne 0) {
        throw "Birinci deterministic archive olusturulamadi."
    }

    python $resolvedPackager --input $resolvedSource --output $secondPath
    if ($LASTEXITCODE -ne 0) {
        throw "Ikinci deterministic archive olusturulamadi."
    }

    $firstHash = (Get-FileHash -LiteralPath $firstPath -Algorithm SHA256).Hash
    $secondHash = (Get-FileHash -LiteralPath $secondPath -Algorithm SHA256).Hash

    if ($firstHash -cne $secondHash) {
        throw "Ayni input icin release archive SHA-256 degerleri farkli."
    }

    $outputDirectory = Split-Path -Parent $outputFullPath
    if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
        New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    }

    Copy-Item -LiteralPath $firstPath -Destination $outputFullPath -Force
    Write-Host ("Deterministic release archive validated: {0}" -f $firstHash)
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
