<#
Dosya Yolu: REPO_ROOT/scripts/TurkuazInstaller-BranchCleanup.ps1
Amac: TurkuazInstaller Community ve Pro icin guvenli eski uzak dal denetimi ve arshivli temizligi
Modul - PowerShell
Version: 1.1.0
Aciklama: Varsayilan salt-okunur plan; -Apply yalniz birlesmis dallari, -ArchiveUnique ile benzersizleri arshivleyerek silebilir
Bagimli Oldugu Katman: Tool | Git | Documentation
#>

[CmdletBinding()]
param(
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
    [string] $RepoPath = (Join-Path $PSScriptRoot '..'),
    [switch] $Apply,
    [switch] $ArchiveUnique
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-RepoGit {
    param([Parameter(Mandatory = $true)][string[]] $GitArgs)

    $lines = @(& git -C $RepoPath @GitArgs 2>&1)
    $code = $LASTEXITCODE
    if ($code -ne 0) {
        throw ('git ' + ($GitArgs -join ' ') + " failed ($code): " + ($lines -join ' '))
    }
    return $lines
}

function Test-Ancestor {
    param([string] $OldRef, [string] $ProtectedRef)

    $null = & git -C $RepoPath merge-base --is-ancestor $OldRef $ProtectedRef 2>&1
    $code = $LASTEXITCODE
    if ($code -eq 0) { return $true }
    if ($code -eq 1) { return $false }
    throw "Cannot compare $OldRef with $ProtectedRef (exit $code)."
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git executable was not found in PATH.'
}
$RepoPath = (Resolve-Path -LiteralPath $RepoPath).Path
$origin = [string] (Invoke-RepoGit -GitArgs @('remote', 'get-url', 'origin') | Select-Object -First 1)
$origin = $origin.Trim()

if ($origin -match '^(?:https://github\.com/|git@github\.com:)(TurkuazLabs/TurkuazInstaller|TurkuazSoft/TurkuazInstaller-Pro)(?:\.git)?/?$') {
    $repoId = $Matches[1]
} else {
    throw "Unexpected origin; refusing to change another project: $origin"
}

if ($ArchiveUnique -and -not $Apply) {
    Write-Warning 'ArchiveUnique was selected without Apply: this is a read-only preview.'
}

# Fail early before any ref changes. In Apply mode, live GitHub open-PR
# status is required: do not delete a branch of a newly opened PR.
if ($Apply) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw 'GitHub CLI (gh) is required for -Apply to check current open PRs.'
    }
    $prJson = @(& gh pr list --repo $repoId --state open --limit 1000 --json number 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw ('Unable to validate open PRs: ' + ($prJson -join ' '))
    }
    $openPrs = @($prJson -join "`n" | ConvertFrom-Json)
    if ($openPrs.Count -gt 0) {
        throw "There are $($openPrs.Count) open PRs. Nothing was deleted."
    }
}

# Update only the remote-tracking references. User worktree and local
# branches are never checked out, reset, deleted or overwritten.
$null = Invoke-RepoGit -GitArgs @('fetch', '--prune', '--no-tags', 'origin', '+refs/heads/*:refs/remotes/origin/*')
$refs = @(Invoke-RepoGit -GitArgs @('for-each-ref', '--format=%(refname:short)|%(objectname)', 'refs/remotes/origin'))

$preserved = @('main', 'rewrite/rust-community', 'rewrite/rust-pro')
$protectedRefs = @('refs/remotes/origin/main')
$rustBranch = if ($repoId -eq 'TurkuazLabs/TurkuazInstaller') { 'rewrite/rust-community' } else { 'rewrite/rust-pro' }
$rustRef = "refs/remotes/origin/$rustBranch"
if (-not (Invoke-RepoGit -GitArgs @('rev-parse', '--verify', $rustRef))) {
    throw "Missing protected Rust branch $rustRef."
}
$protectedRefs += $rustRef

$plan = New-Object 'System.Collections.Generic.List[object]'
foreach ($line in $refs) {
    if ($line -notmatch '^origin/(.+)\|([a-f0-9]{40})$') { continue }
    $branch = $Matches[1]
    $sha = $Matches[2]
    if ($preserved -contains $branch -or $branch -eq 'HEAD') { continue }

    $branchRef = "refs/remotes/origin/$branch"
    $included = $false
    foreach ($base in $protectedRefs) {
        if (Test-Ancestor -OldRef $branchRef -ProtectedRef $base) {
            $included = $true
            break
        }
    }

    $action = if ($included) { 'DELETE_MERGED' }
        elseif ($ArchiveUnique) { 'ARCHIVE_TAG_THEN_DELETE' }
        else { 'KEEP_UNIQUE' }
    $plan.Add([pscustomobject]@{
        Branch = $branch
        Sha = $sha
        Class = if ($included) { 'Reachable' } else { 'Unique' }
        Action = $action
    })
}

Write-Host "Repository: $repoId"
Write-Host "Protected: main, $rustBranch"
Write-Host ('Mode: ' + $(if ($Apply) { 'APPLY' } else { 'PLAN (remote branches/tags unchanged)' }))
$plan | Sort-Object Branch | Format-Table -AutoSize | Out-Host
Write-Host "Evaluated: $($plan.Count); merged: $( @($plan | Where-Object Class -eq 'Reachable').Count ); unique: $( @($plan | Where-Object Class -eq 'Unique').Count )."

if (-not $Apply) {
    Write-Host 'No REMOTE branches/tags were changed. Fetch refreshed local origin/* tracking refs.'
    Write-Host 'Use -Apply only after reviewing this plan.'
    return
}

$archivePrefix = 'archive/branch-cleanup-20261101'
$done = 0
foreach ($entry in $plan) {
    if ($entry.Action -eq 'KEEP_UNIQUE') { continue }
    $name = [string] $entry.Branch
    $sha = [string] $entry.Sha

    if ($entry.Action -eq 'ARCHIVE_TAG_THEN_DELETE') {
        # Always verify a durable remote tag points to exactly the old SHA
        # before deleting a unique branch. Tags do NOT match release v*.
        $tag = "$archivePrefix/$name"
        $tagRef = "refs/tags/$tag"
        $existingRemote = @(& git -C $RepoPath ls-remote --tags origin $tagRef 2>&1)
        if ($LASTEXITCODE -ne 0) {
            throw "Could not query remote archive tag $tagRef. No branch deleted for $name."
        }
        if ($existingRemote.Count -gt 0) {
            $remoteSha = (([string] $existingRemote[0]) -split "`t")[0]
            if ($remoteSha -ne $sha) {
                throw "Archive tag $tagRef points to a different commit; aborting."
            }
        } else {
            $localTag = @(& git -C $RepoPath rev-parse --verify $tagRef 2>&1)
            if ($LASTEXITCODE -eq 0 -and ([string] $localTag[0]).Trim() -ne $sha) {
                throw "Local archive tag $tagRef points to a different commit."
            }
            if ($LASTEXITCODE -ne 0) {
                $null = Invoke-RepoGit -GitArgs @('tag', $tag, $sha)
            }
            $null = Invoke-RepoGit -GitArgs @('push', 'origin', "${tagRef}:${tagRef}")
        }
        $verifyTag = @(& git -C $RepoPath ls-remote --tags origin $tagRef 2>&1)
        if ($LASTEXITCODE -ne 0 -or $verifyTag.Count -ne 1) {
            throw "Remote tag verification failed for $tagRef; branch was NOT deleted."
        }
        $verifySha = (([string] $verifyTag[0]) -split "`t")[0]
        if ($verifySha -ne $sha) {
            throw "Remote archive tag has an unexpected SHA: $tagRef."
        }
    }

    # Atomic remote ref lease protects against someone updating the branch
    # after this plan was produced. Never use a blind force push.
    $lease = "--force-with-lease=refs/heads/${name}:$sha"
    $null = Invoke-RepoGit -GitArgs @('push', $lease, 'origin', ":refs/heads/$name")
    $stillThere = @(& git -C $RepoPath ls-remote --heads origin "refs/heads/$name" 2>&1)
    if ($LASTEXITCODE -ne 0 -or $stillThere.Count -gt 0) {
        throw "Deletion could not be confirmed for $name. Stop and inspect the remote."
    }
    $done++
    Write-Host "Deleted: $name"
}

Write-Host "Confirmed remote branch deletions: $done."
Write-Host 'Protected main/Rust refs and all local branches remain unchanged.'