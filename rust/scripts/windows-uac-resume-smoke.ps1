# Dosya Yolu: REPO_ROOT/rust/scripts/windows-uac-resume-smoke.ps1
# Amac: UAC yeniden acilis parametrelerinin onaysiz yetkili islemlere izin vermedigini dogrulamak
# Modul - PowerShell QA Script
# Version: 0.1.2
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
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedError
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $setup
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.StandardErrorEncoding = [System.Text.Encoding]::UTF8
    foreach ($argument in $Arguments) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) { throw "Could not start $Scenario." }
    try {
        $stderrReader = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(15000)) {
            $process.Kill($true)
            throw "Invalid UAC resume did not stop: $Scenario"
        }
        $stderrText = $stderrReader.GetAwaiter().GetResult()
        if ($process.ExitCode -eq 0) {
            throw "Invalid UAC resume was accepted: $Scenario"
        }
        if (-not $stderrText.Contains($ExpectedError)) {
            throw "UAC resume failed for an unexpected reason: $Scenario"
        }
    } finally {
        $process.Dispose()
    }
    Write-Host "UAC_RESUME_POLICY_REJECTED: $Scenario"
}

$base = [string[]]@(
    'resume-local-wizard',
    '--package-id', 'qa.uac',
    '--target', 'C:\Program Files\qa.uac',
    '--start-menu', '1',
    '--desktop', '0',
    '--language', 'tr'
)

Assert-UacResumeRejected -Scenario 'consent-smuggling-license' -Arguments ([string[]]($base + @('--accept-license', '1'))) -ExpectedError 'UAC yeniden başlatma parametreleri geçersiz.'
Assert-UacResumeRejected -Scenario 'consent-smuggling-recycle' -Arguments ([string[]]($base + @('--recycle-approved', '1'))) -ExpectedError 'UAC yeniden başlatma parametreleri geçersiz.'

$wrongFlag = [string[]]$base.Clone()
$wrongFlag[1] = '--target'
Assert-UacResumeRejected -Scenario 'flag-order' -Arguments $wrongFlag -ExpectedError 'UAC parametre sırası geçersiz.'

$wrongTarget = [string[]]$base.Clone()
$wrongTarget[4] = 'relative\qa.uac'
Assert-UacResumeRejected -Scenario 'relative-target' -Arguments $wrongTarget -ExpectedError 'UAC paket kimliği veya hedef klasör yolu geçersiz.'

$traversalTarget = [string[]]$base.Clone()
$traversalTarget[4] = 'C:\Program Files\..\qa.uac'
Assert-UacResumeRejected -Scenario 'traversal-target' -Arguments $traversalTarget -ExpectedError 'UAC paket kimliği veya hedef klasör yolu geçersiz.'

$invalidBoolean = [string[]]$base.Clone()
$invalidBoolean[6] = 'yes'
Assert-UacResumeRejected -Scenario 'invalid-boolean' -Arguments $invalidBoolean -ExpectedError 'Geçersiz UAC tercih değeri.'

$invalidLanguage = [string[]]$base.Clone()
$invalidLanguage[10] = 'xx'
Assert-UacResumeRejected -Scenario 'invalid-language' -Arguments $invalidLanguage -ExpectedError 'Geçersiz UAC dili.'

Write-Host 'UAC_RESUME_ARGUMENT_SMOKE_PASS'
