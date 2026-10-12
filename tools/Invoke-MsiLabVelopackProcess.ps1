# 📄 Dosya Yolu: /tools/Invoke-MsiLabVelopackProcess.ps1
# 📌 Amac: Non-admin Velopack Setup/Update.exe islemleri icin tek test process adaptorunu paylasir
# 📌 Modul - Tool PowerShell
# Version: 1.0.0
# Aciklama: Windows child process, timeout, exit code ve sinirli tani ciktilarini fail-closed denetler
# Bagimli Oldugu Katman: Tool | Config | CI

function Invoke-MsiLabVelopackProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [string]$File,
        [Parameter(Mandatory = $true)] [string[]]$Arguments,
        [Parameter(Mandatory = $true)] [string]$Operation,
        [ValidateRange(30, 180)] [int]$TimeoutSeconds = 90
    )

    $info = [System.Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $File
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($arg in $Arguments) {
        $info.ArgumentList.Add($arg)
    }

    $process = [System.Diagnostics.Process]::Start($info)
    if ($null -eq $process) {
        throw "Velopack $Operation could not start."
    }

    try {
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $null = $process.WaitForExit(5000)
            throw "Velopack $Operation timed out."
        }

        $outText = $stdoutTask.GetAwaiter().GetResult()
        $errText = $stderrTask.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) {
            if (-not [string]::IsNullOrWhiteSpace($outText)) {
                Write-Host "VELO_LAB_STDOUT: $($outText.Substring([Math]::Max(0, $outText.Length - 3000)))"
            }
            if (-not [string]::IsNullOrWhiteSpace($errText)) {
                Write-Warning "VELO_LAB_STDERR: $($errText.Substring([Math]::Max(0, $errText.Length - 3000)))"
            }
            throw "Velopack $Operation failed with exit code $($process.ExitCode)."
        }
    }
    finally {
        $process.Dispose()
    }
}
