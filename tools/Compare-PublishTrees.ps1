# 📄 Dosya Yolu: /tools/Compare-PublishTrees.ps1
# 📌 Amac: Iki bagimsiz publish dizininin dosya listesi ve SHA-256 icerigini birebir karsilastirir
# 📌 Modul - Tool PowerShell
# Version: 1.0.0
# Aciklama: Stable release reproducibility kapisinda eksik, fazla veya farkli binary ciktilarini reddeder
# Bagimli Oldugu Katman: Tool

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$FirstRoot,

    [Parameter(Mandatory = $true)]
    [string]$SecondRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-PublishIndex {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    $resolvedRoot = (Resolve-Path -LiteralPath $Root).Path
    $index = @{}

    Get-ChildItem -LiteralPath $resolvedRoot -File -Recurse |
        Sort-Object FullName |
        ForEach-Object {
            $relativePath = [System.IO.Path]::GetRelativePath(
                $resolvedRoot,
                $_.FullName
            ).Replace("\", "/")

            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()

            $index[$relativePath] = [PSCustomObject]@{
                Hash = $hash
                Length = $_.Length
            }
        }

    return $index
}

$first = Get-PublishIndex -Root $FirstRoot
$second = Get-PublishIndex -Root $SecondRoot

$firstNames = @($first.Keys | Sort-Object)
$secondNames = @($second.Keys | Sort-Object)

$missingFromSecond = @($firstNames | Where-Object { -not $second.ContainsKey($_) })
$missingFromFirst = @($secondNames | Where-Object { -not $first.ContainsKey($_) })

if ($missingFromSecond.Count -gt 0 -or $missingFromFirst.Count -gt 0) {
    if ($missingFromSecond.Count -gt 0) {
        Write-Error ("Second publish is missing: " + ($missingFromSecond -join ", "))
    }

    if ($missingFromFirst.Count -gt 0) {
        Write-Error ("First publish is missing: " + ($missingFromFirst -join ", "))
    }

    throw "Publish file lists are not reproducible."
}

$differences = @()

foreach ($name in $firstNames) {
    $left = $first[$name]
    $right = $second[$name]

    if ($left.Hash -ne $right.Hash -or $left.Length -ne $right.Length) {
        $differences += $name
    }
}

if ($differences.Count -gt 0) {
    Write-Error ("Publish content differs: " + ($differences -join ", "))
    throw "Publish content is not reproducible."
}

Write-Host ("Reproducibility validation succeeded for {0} files." -f $firstNames.Count)
