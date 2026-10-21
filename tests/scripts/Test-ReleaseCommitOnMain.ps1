# Dosya Yolu: REPO_ROOT/tests/scripts/Test-ReleaseCommitOnMain.ps1
# Amac: Stable commit/main provenans kontrolunu yalitilmis git testinde dogrular
# Modul - PowerShell Test
# Version: 1.0.0
# Aciklama: Dogru ref, yanlis checkout ve ayrismis commit senaryolari
# Bagimli Oldugu Katman: Tool | Testing

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).ProviderPath
$guard = Join-Path $repoRoot 'tools/Assert-ReleaseCommitOnMain.ps1'
if (-not (Test-Path -LiteralPath $guard -PathType Leaf)) {
    throw 'Release ref guard is missing.'
}

$work = Join-Path ([IO.Path]::GetTempPath()) ('Turkuaz Stable Git Test ' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($work) | Out-Null
try {
    & git -C $work init -b main | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot initialize test repository.' }
    & git -C $work -c user.name=TurkuazCI -c user.email=ci@example.invalid commit --allow-empty -m 'initial' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot create test base commit.' }

    & git -C $work checkout -b side | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot create side branch.' }
    & git -C $work -c user.name=TurkuazCI -c user.email=ci@example.invalid commit --allow-empty -m 'side' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot commit on side branch.' }
    $sideCommit = (& git -C $work rev-parse HEAD).Trim()

    & git -C $work checkout main | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot switch to main.' }
    & git -C $work -c user.name=TurkuazCI -c user.email=ci@example.invalid commit --allow-empty -m 'main' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot commit on main.' }
    $mainCommit = (& git -C $work rev-parse HEAD).Trim()

    & git -C $work update-ref refs/remotes/origin/main $mainCommit
    if ($LASTEXITCODE -ne 0) { throw 'Cannot create origin/main test ref.' }

    & $guard -RepositoryRoot $work -ReleaseCommit $mainCommit | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Valid main release check did not pass.' }

    $incorrectHeadRejected = $false
    try { & $guard -RepositoryRoot $work -ReleaseCommit $sideCommit | Out-Null }
    catch { $incorrectHeadRejected = $_.Exception.Message.Contains('differs from checkout HEAD') }
    if (-not $incorrectHeadRejected) { throw 'Mismatched checkout commit accepted.' }

    & git -C $work checkout side | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot switch back to side.' }
    $sideRejected = $false
    try { & $guard -RepositoryRoot $work -ReleaseCommit $sideCommit | Out-Null }
    catch { $sideRejected = $_.Exception.Message.Contains('not an ancestor of origin/main') }
    if (-not $sideRejected) { throw 'Divergent tag was allowed for signing.' }

    & git -C $work update-ref -d refs/remotes/origin/main
    if ($LASTEXITCODE -ne 0) { throw 'Cannot delete test remote ref.' }
    $missingRefRejected = $false
    try { & $guard -RepositoryRoot $work -ReleaseCommit $sideCommit | Out-Null }
    catch { $missingRefRejected = $_.Exception.Message.Contains('origin/main is unavailable') }
    if (-not $missingRefRejected) { throw 'Missing trusted main ref was accepted.' }

    $global:LASTEXITCODE = 0
    Write-Output 'PASS: main ancestry, wrong checkout, divergent tag, missing ref.'
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
