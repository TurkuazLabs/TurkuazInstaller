# 📄 Dosya Yolu: /tools/Assert-ReleaseVersion.ps1
# 📌 Amac: Git tag surumu ile Directory.Build.props urun surumunun birebir eslesmesini dogrular
# 📌 Modul - Tool PowerShell
# Version: 1.0.0
# Aciklama: Yanlis tag veya yanlis binary surumuyle production release olusturulmasini engeller
# Bagimli Oldugu Katman: Tool | Config

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Tag,

    [Parameter(Mandatory = $true)]
    [string]$PropsPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

[xml]$props = Get-Content -LiteralPath $PropsPath -Raw
$version = [string]$props.Project.PropertyGroup.Version

if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Version was not found in Directory.Build.props."
}

$expectedTag = "v$version"

if ($Tag -ne $expectedTag) {
    throw ("Release tag {0} does not match product version {1}." -f $Tag, $version)
}

Write-Host ("Release version validated: {0}" -f $Tag)
