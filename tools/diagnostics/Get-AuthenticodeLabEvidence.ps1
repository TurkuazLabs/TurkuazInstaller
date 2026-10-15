# Dosya Yolu: REPO_ROOT/tools/diagnostics/Get-AuthenticodeLabEvidence.ps1
# Amac: Disposable Windows VM Authenticode LAB kanitlarini salt okunur toplar
# Modul - PowerShell Tool
# Version: 1.0.1
# Aciklama: Dosya ozeti, Windows native Authenticode sonucu ve guvenlik ortamini JSON olarak raporlar
# Bagimli Oldugu Katman: Tool | Security | Testing

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ArtifactPath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^REV-(0[1-9]|10|11)$')]
    [string]$CaseId,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 or newer is required.' }
if (-not [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
    throw 'Windows-only evidence collector; no changes performed.'
}
if (-not (Test-Path -LiteralPath $ArtifactPath -PathType Leaf)) {
    throw 'ArtifactPath must identify an existing file.'
}
$resolved = (Resolve-Path -LiteralPath $ArtifactPath).ProviderPath
if (-not [System.IO.Path]::IsPathFullyQualified($OutputPath)) {
    throw 'OutputPath must be absolute.'
}
$destination = [System.IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $destination) {
    throw 'Evidence output already exists; refusing overwrite.'
}
if (-not (Test-Path -LiteralPath (Split-Path -Parent $destination) -PathType Container)) {
    throw 'Evidence output parent directory must already exist.'
}
$started = [DateTimeOffset]::UtcNow
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$signature = Get-AuthenticodeSignature -LiteralPath $resolved
$sw.Stop()
$cert = $signature.SignerCertificate
$sha = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash.ToLowerInvariant()
$os = [System.Environment]::OSVersion.VersionString

$evidence = [ordered]@{
    schema_version = 1
    case_id = $CaseId
    collection_time_utc = $started.ToString('o')
    os_version = $os
    powershell_version = $PSVersionTable.PSVersion.ToString()
    artifact_sha256 = $sha
    artifact_size_bytes = (Get-Item -LiteralPath $resolved).Length
    windows_authenticode_status = [string]$signature.Status
    windows_authenticode_status_message = [string]$signature.StatusMessage
    signer_subject = if ($null -ne $cert) { [string]$cert.Subject } else { $null }
    signer_cert_sha256 = if ($null -ne $cert) { $cert.GetCertHashString([System.Security.Cryptography.HashAlgorithmName]::SHA256).ToLowerInvariant() } else { $null }
    signer_not_before_utc = if ($null -ne $cert) { $cert.NotBefore.ToUniversalTime().ToString('o') } else { $null }
    signer_not_after_utc = if ($null -ne $cert) { $cert.NotAfter.ToUniversalTime().ToString('o') } else { $null }
    powershell_signature_check_elapsed_ms = $sw.ElapsedMilliseconds
    installer_winverifytrust_result = 'NOT_RUN'
    installer_stage_apply_evidence = 'NOT_RUN'
    revocation_endpoint_state = 'NOT_RECORDED'
    cache_state = 'NOT_RECORDED'
    release_acceptance = 'NOT_ACCEPTED'
    warning = 'Get-AuthenticodeSignature is NOT the TurkuazInstaller WinVerifyTrust verifier. This diagnostic cannot approve a release.'
}
$json = $evidence | ConvertTo-Json -Depth 5
# Atomic create-new semantics: no existing evidence file may be replaced.
$stream = [System.IO.FileStream]::new(
    $destination,
    [System.IO.FileMode]::CreateNew,
    [System.IO.FileAccess]::Write,
    [System.IO.FileShare]::None)
try {
    $writer = [System.IO.StreamWriter]::new($stream, [System.Text.UTF8Encoding]::new($false))
    try { $writer.Write($json); $writer.Flush() }
    finally { $writer.Dispose() }
}
finally { $stream.Dispose() }
Write-Output "LAB evidence created: $destination (release acceptance: NOT_ACCEPTED)"
