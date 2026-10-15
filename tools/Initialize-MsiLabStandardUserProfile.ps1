# 📄 Dosya Yolu: /tools/Initialize-MsiLabStandardUserProfile.ps1
# 📌 Amac: CI'da non-admin Windows tokeninin gercek SID profiline ait user klasorlerini kullanir
# 📌 Modul - Tool PowerShell
# Version: 1.0.0
# Aciklama: Runner admin ortam degiskenleri sizmasini reddeder ve SID-backed profil konumunu dogrular
# Bagimli Oldugu Katman: Tool | Config | CI

function Initialize-MsiLabStandardUserProfile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExpectedSid
    )

    if (-not $IsWindows) {
        throw "Windows standard-user profile helper must run on Windows."
    }
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [System.Security.Principal.WindowsPrincipal]::new($identity)
    if ($identity.User.Value -cne $ExpectedSid -or
        $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Standard-user token does not match requested SID."
    }

    $key = "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\$ExpectedSid"
    $value = Get-ItemProperty -LiteralPath $key -Name ProfileImagePath -ErrorAction Stop
    if ([string]::IsNullOrWhiteSpace([string]$value.ProfileImagePath)) {
        throw "Windows SID profile path is missing."
    }
    $profileRoot = [IO.Path]::GetFullPath(
        [Environment]::ExpandEnvironmentVariables([string]$value.ProfileImagePath))
    $localRoot = Join-Path $profileRoot "AppData\Local"
    $roamingRoot = Join-Path $profileRoot "AppData\Roaming"
    $tempRoot = Join-Path $localRoot "Temp"

    if (-not (Test-Path -LiteralPath $profileRoot -PathType Container) -or
        -not (Test-Path -LiteralPath $localRoot -PathType Container)) {
        throw "The standard-user Windows profile was not loaded."
    }

    # Child logon with credentials can inherit runneradmin's environment.
    # Resolve local profile paths from the authenticated SID, never that env.
    $env:USERPROFILE = $profileRoot
    $env:LOCALAPPDATA = $localRoot
    $env:APPDATA = $roamingRoot
    $env:TEMP = $tempRoot
    $env:TMP = $tempRoot
    New-Item -ItemType Directory -Path $tempRoot -Force -ErrorAction Stop | Out-Null

    $resolved = [IO.Path]::GetFullPath(
        [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData))
    $comparison = [StringComparison]::OrdinalIgnoreCase
    if (-not $resolved.Equals([IO.Path]::GetFullPath($localRoot), $comparison)) {
        throw "Authenticated user LocalAppData conflicts with the SID profile."
    }

    return $localRoot
}
