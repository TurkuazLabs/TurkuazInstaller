# Dosya Yolu: REPO_ROOT/rust/scripts/windows-uac-resume-smoke.ps1
# Amac: UAC yeniden acilis parametrelerinin onaysiz yetkili islemlere izin vermedigini dogrulamak
# Modul - PowerShell QA Script
# Version: 0.1.0
# Aciklama: Gercek UAC penceresi acmadan reddedilen argumanlari process olarak test eder
# Bagimli Oldugu Katman: QA | Tool

param(
    [Parameter(Mandatory = $true)]
    [string]$SetupPath
)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'This smoke test requires Windows and PowerShell 7.' }
$setup = (Resolve-Path -LiteralPath $SetupPath -ErrorAction Stop).ProviderPath

function Assert-UacResumeRejected {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Scenario,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $setup
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    foreach ($argument in $Arguments) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) { throw "Could not start $Scenario." }
    try {
        if (-not $process.WaitForExit(15000)) {
            $process.Kill($true)
            throw "Invalid UAC resume did not stop: $Scenario"
        }
        if ($process.ExitCode -eq 0) {
            throw "Invalid UAC resume was accepted: $Scenario"
        }
    } finally {
        $process.Dispose()
    }
    Write-Host "UAC_RESUME_REJECTED: $Scenario"
}

$base = [string[]]@(
    'resume-local-wizard',
    '--package-id', 'qa.uac',
    '--target', 'C:\Program Files\qa.uac',
    '--start-menu', '1',
    '--desktop', '0',
    '--language', 'tr'
)

Assert-UacResumeRejected -Scenario 'consent-smuggling-license' -Arguments ([string[]]($base + @('--accept-license', '1')))
Assert-UacResumeRejected -Scenario 'consent-smuggling-recycle' -Arguments ([string[]]($base + @('--recycle-approved', '1')))

$wrongFlag = [string[]]$base.Clone()
$wrongFlag[1] = '--target'
Assert-UacResumeRejected -Scenario 'flag-order' -Arguments $wrongFlag

$wrongTarget = [string[]]$base.Clone()
$wrongTarget[4] = 'relative\qa.uac'
Assert-UacResumeRejected -Scenario 'relative-target' -Arguments $wrongTarget

$traversalTarget = [string[]]$base.Clone()
$traversalTarget[4] = 'C:\Program Files\..\qa.uac'
Assert-UacResumeRejected -Scenario 'traversal-target' -Arguments $traversalTarget

$invalidBoolean = [string[]]$base.Clone()
$invalidBoolean[6] = 'yes'
Assert-UacResumeRejected -Scenario 'invalid-boolean' -Arguments $invalidBoolean

$invalidLanguage = [string[]]$base.Clone()
$invalidLanguage[10] = 'xx'
Assert-UacResumeRejected -Scenario 'invalid-language' -Arguments $invalidLanguage

Write-Host 'UAC_RESUME_ARGUMENT_SMOKE_PASS'
