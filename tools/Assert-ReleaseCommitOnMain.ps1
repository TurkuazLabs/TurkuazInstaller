# Dosya Yolu: REPO_ROOT/tools/Assert-ReleaseCommitOnMain.ps1
# Amac: Production release commitini origin/main tarihcesi ile dogrular
# Modul - PowerShell Tool
# Version: 1.0.0
# Aciklama: Yanlis daldan olusturulan stable tag'lerin signing asamasina gecmesini engeller
# Bagimli Oldugu Katman: Tool | Security | Testing

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-fA-F0-9]{40}$')]
    [string]$ReleaseCommit,

    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# @dev-id TINS-REL-020
# @purpose Check that the exact release checkout is reachable from remote main.
# @reason A matching SemVer tag can otherwise point to a divergent feature commit.
# @contract Requires full git history and origin/main; fail on missing ref or mismatch.
# @tests tests/scripts/Test-ReleaseCommitOnMain.ps1
if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container)) {
    throw 'Release repository root does not exist.'
}
$root = (Resolve-Path -LiteralPath $RepositoryRoot).ProviderPath

$head = & git -C $root rev-parse --verify HEAD
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($head)) {
    throw 'Cannot resolve release checkout HEAD.'
}
if ($head.Trim() -cne $ReleaseCommit.ToLowerInvariant()) {
    throw 'Release commit differs from checkout HEAD.'
}

$remoteMain = & git -C $root rev-parse --verify 'refs/remotes/origin/main^{commit}'
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($remoteMain)) {
    throw 'origin/main is unavailable. Full history checkout is required.'
}
& git -C $root merge-base --is-ancestor $ReleaseCommit 'refs/remotes/origin/main'
$check = $LASTEXITCODE
if ($check -eq 1) {
    throw 'Release commit is not an ancestor of origin/main.'
}
if ($check -ne 0) {
    throw 'Could not validate release commit ancestry.'
}

Write-Output "PASS: release commit is reachable from origin/main (ref checked)."
