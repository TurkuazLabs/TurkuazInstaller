# 📄 Dosya Yolu: /tools/release/Initialize-ProductionSigning.ps1
# 📌 Amac: Azure Artifact Signing, GitHub OIDC ve production environment altyapisini idempotent olarak hazirlar
# 📌 Modul - Tool PowerShell
# Version: 1.0.0
# Aciklama: Account, Entra app, immutable OIDC trust, GitHub environment, certificate profile ve minimum signer RBAC kurulumunu otomatiklestirir
# Bagimli Oldugu Katman: Tool | Config

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SubscriptionId,

    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $true)]
    [ValidateSet(
        "brazilsouth",
        "centralus",
        "eastus",
        "japaneast",
        "koreacentral",
        "northcentralus",
        "northeurope",
        "polandcentral",
        "southcentralus",
        "switzerlandnorth",
        "westcentralus",
        "westeurope",
        "westus",
        "westus2",
        "westus3")]
    [string]$Location,

    [Parameter(Mandatory = $true)]
    [string]$AccountName,

    [string]$CertificateProfileName,

    [ValidateSet(
        "PublicTrust",
        "PublicTrustTest",
        "PrivateTrust")]
    [string]$CertificateProfileType,

    [string]$IdentityValidationId,

    [switch]$ConfigureGitHub,

    [string]$ConfigPath = "config/release-signing.psd1"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-Command {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($null -eq (Get-Command -Name $Name -ErrorAction SilentlyContinue)) {
        throw ("Required command is not installed: {0}" -f $Name)
    }
}

function Invoke-AzJson {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [switch]$AllowFailure
    )

    $output = & az @Arguments 2>$null
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        if ($AllowFailure) {
            return $null
        }

        throw ("Azure CLI command failed: az {0}" -f ($Arguments -join " "))
    }

    $text = $output -join [Environment]::NewLine

    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    return ($text | ConvertFrom-Json)
}

function Invoke-GhJson {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = & gh @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw ("GitHub CLI command failed: gh {0}" -f ($Arguments -join " "))
    }

    $text = $output -join [Environment]::NewLine

    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    return ($text | ConvertFrom-Json)
}

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Value,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $Value |
        ConvertTo-Json -Depth 10 |
        Set-Content -LiteralPath $Path -Encoding utf8NoBOM
}

function Ensure-GitHubEnvironment {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Config
    )

    Assert-Command -Name "gh"

    & gh auth status | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI authentication is required."
    }

    $repository = "{0}/{1}" -f $Config.RepositoryOwner, $Config.RepositoryName
    $environmentPath = "repos/{0}/environments/{1}" -f $repository, $Config.EnvironmentName
    $apiHeader = "X-GitHub-Api-Version: {0}" -f $Config.GitHubApiVersion
    $environmentPayloadPath = Join-Path ([IO.Path]::GetTempPath()) ("turkuaz-environment-" + [guid]::NewGuid().ToString("N") + ".json")

    try {
        $environmentPayload = @{
            deployment_branch_policy = @{
                protected_branches = $false
                custom_branch_policies = $true
            }
        }

        Write-JsonFile -Value $environmentPayload -Path $environmentPayloadPath

        & gh api --method PUT -H $apiHeader $environmentPath --input $environmentPayloadPath | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "GitHub production environment could not be created or updated."
        }

        $policiesPath = "{0}/deployment-branch-policies" -f $environmentPath
        $policies = Invoke-GhJson -Arguments @(
            "api",
            "-H",
            $apiHeader,
            $policiesPath
        )

        $requiredPolicies = @(
            @{
                name = $Config.PreflightBranch
                type = "branch"
            },
            @{
                name = $Config.ReleaseTagPattern
                type = "tag"
            }
        )

        foreach ($requiredPolicy in $requiredPolicies) {
            $existing = @($policies.branch_policies) |
                Where-Object {
                    $_.name -ceq $requiredPolicy.name -and
                    $_.type -ceq $requiredPolicy.type
                } |
                Select-Object -First 1

            if ($null -ne $existing) {
                continue
            }

            $policyPayloadPath = Join-Path ([IO.Path]::GetTempPath()) ("turkuaz-policy-" + [guid]::NewGuid().ToString("N") + ".json")

            try {
                Write-JsonFile -Value $requiredPolicy -Path $policyPayloadPath

                & gh api --method POST -H $apiHeader $policiesPath --input $policyPayloadPath | Out-Null
                if ($LASTEXITCODE -ne 0) {
                    throw ("GitHub deployment policy could not be created: {0}" -f $requiredPolicy.name)
                }
            }
            finally {
                if (Test-Path -LiteralPath $policyPayloadPath) {
                    Remove-Item -LiteralPath $policyPayloadPath -Force
                }
            }
        }

        $oidcPath = "repos/{0}/actions/oidc/customization/sub" -f $repository
        $oidcPayloadPath = Join-Path ([IO.Path]::GetTempPath()) ("turkuaz-oidc-" + [guid]::NewGuid().ToString("N") + ".json")

        try {
            $oidcPayload = @{
                use_default = $true
                use_immutable_subject = $true
            }

            Write-JsonFile -Value $oidcPayload -Path $oidcPayloadPath

            & gh api --method PUT -H $apiHeader $oidcPath --input $oidcPayloadPath | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "GitHub immutable OIDC subject configuration failed."
            }
        }
        finally {
            if (Test-Path -LiteralPath $oidcPayloadPath) {
                Remove-Item -LiteralPath $oidcPayloadPath -Force
            }
        }
    }
    finally {
        if (Test-Path -LiteralPath $environmentPayloadPath) {
            Remove-Item -LiteralPath $environmentPayloadPath -Force
        }
    }
}

function Set-GitHubEnvironmentConfiguration {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Config,

        [Parameter(Mandatory = $true)]
        [string]$ClientId,

        [Parameter(Mandatory = $true)]
        [string]$TenantId,

        [Parameter(Mandatory = $true)]
        [string]$Subscription,

        [Parameter(Mandatory = $true)]
        [string]$Endpoint,

        [Parameter(Mandatory = $true)]
        [string]$SigningAccount,

        [Parameter(Mandatory = $true)]
        [string]$ProfileName
    )

    $repository = "{0}/{1}" -f $Config.RepositoryOwner, $Config.RepositoryName
    $environmentName = $Config.EnvironmentName

    $secrets = @{
        AZURE_CLIENT_ID = $ClientId
        AZURE_TENANT_ID = $TenantId
        AZURE_SUBSCRIPTION_ID = $Subscription
    }

    foreach ($entry in $secrets.GetEnumerator()) {
        & gh secret set $entry.Key --repo $repository --env $environmentName --body ([string]$entry.Value)
        if ($LASTEXITCODE -ne 0) {
            throw ("GitHub environment secret could not be set: {0}" -f $entry.Key)
        }
    }

    $variables = @{
        ARTIFACT_SIGNING_ENDPOINT = $Endpoint
        ARTIFACT_SIGNING_ACCOUNT_NAME = $SigningAccount
        ARTIFACT_SIGNING_CERT_PROFILE_NAME = $ProfileName
        ARTIFACT_SIGNING_TIMESTAMP_URL = $Config.TimestampUrl
    }

    foreach ($entry in $variables.GetEnumerator()) {
        & gh variable set $entry.Key --repo $repository --env $environmentName --body ([string]$entry.Value)
        if ($LASTEXITCODE -ne 0) {
            throw ("GitHub environment variable could not be set: {0}" -f $entry.Key)
        }
    }
}

Assert-Command -Name "az"

$config = Import-PowerShellDataFile -LiteralPath $ConfigPath

if ([string]::IsNullOrWhiteSpace($CertificateProfileName)) {
    $CertificateProfileName = $config.DefaultCertificateProfileName
}

if ([string]::IsNullOrWhiteSpace($CertificateProfileType)) {
    $CertificateProfileType = $config.DefaultCertificateProfileType
}

if ($AccountName.Length -lt 3 -or
    $AccountName.Length -gt 24 -or
    $AccountName -notmatch "^[A-Za-z][A-Za-z0-9]{2,23}$") {
    throw "Artifact Signing account name must be 3-24 alphanumeric characters and start with a letter."
}

if ($CertificateProfileName.Length -lt 5 -or
    $CertificateProfileName.Length -gt 100 -or
    $CertificateProfileName -notmatch "^[A-Za-z][A-Za-z0-9-]{3,98}[A-Za-z0-9]$" -or
    $CertificateProfileName.Contains("--", [StringComparison]::Ordinal)) {
    throw "Certificate profile name does not satisfy Artifact Signing naming rules."
}

$normalizedLocation = $Location.ToLowerInvariant()
if (-not $config.RegionEndpoints.ContainsKey($normalizedLocation)) {
    throw ("Unsupported Artifact Signing region: {0}" -f $Location)
}

$endpoint = [string]$config.RegionEndpoints[$normalizedLocation]

$account = Invoke-AzJson -Arguments @(
    "account",
    "show",
    "--output",
    "json"
)

if ($null -eq $account) {
    throw "Azure CLI login is required."
}

& az account set --subscription $SubscriptionId
if ($LASTEXITCODE -ne 0) {
    throw "Azure subscription could not be selected."
}

$account = Invoke-AzJson -Arguments @(
    "account",
    "show",
    "--output",
    "json"
)

$tenantId = [string]$account.tenantId

& az provider register --namespace $config.ResourceProvider
if ($LASTEXITCODE -ne 0) {
    throw "Artifact Signing resource provider registration failed."
}

$providerState = $null
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    $provider = Invoke-AzJson -Arguments @(
        "provider",
        "show",
        "--namespace",
        $config.ResourceProvider,
        "--output",
        "json"
    )

    $providerState = [string]$provider.registrationState
    if ($providerState -ceq "Registered") {
        break
    }

    Start-Sleep -Seconds 2
}

if ($providerState -cne "Registered") {
    throw "Microsoft.CodeSigning resource provider is not registered."
}

$extension = Invoke-AzJson -Arguments @(
    "extension",
    "show",
    "--name",
    $config.ArtifactSigningExtension,
    "--output",
    "json"
) -AllowFailure

if ($null -eq $extension) {
    & az extension add --name $config.ArtifactSigningExtension
}
else {
    & az extension update --name $config.ArtifactSigningExtension
}

if ($LASTEXITCODE -ne 0) {
    throw "Artifact Signing Azure CLI extension setup failed."
}

$resourceGroup = Invoke-AzJson -Arguments @(
    "group",
    "show",
    "--name",
    $ResourceGroupName,
    "--output",
    "json"
) -AllowFailure

if ($null -eq $resourceGroup) {
    $resourceGroup = Invoke-AzJson -Arguments @(
        "group",
        "create",
        "--name",
        $ResourceGroupName,
        "--location",
        $normalizedLocation,
        "--output",
        "json"
    )
}

$signingAccount = Invoke-AzJson -Arguments @(
    "artifact-signing",
    "show",
    "--resource-group",
    $ResourceGroupName,
    "--account-name",
    $AccountName,
    "--output",
    "json"
) -AllowFailure

if ($null -eq $signingAccount) {
    $signingAccount = Invoke-AzJson -Arguments @(
        "artifact-signing",
        "create",
        "--resource-group",
        $ResourceGroupName,
        "--account-name",
        $AccountName,
        "--location",
        $normalizedLocation,
        "--sku",
        $config.DefaultSku,
        "--output",
        "json"
    )
}

$applications = Invoke-AzJson -Arguments @(
    "ad",
    "app",
    "list",
    "--display-name",
    $config.ApplicationDisplayName,
    "--output",
    "json"
)

$application = @($applications) | Select-Object -First 1

if ($null -eq $application) {
    $application = Invoke-AzJson -Arguments @(
        "ad",
        "app",
        "create",
        "--display-name",
        $config.ApplicationDisplayName,
        "--sign-in-audience",
        "AzureADMyOrg",
        "--output",
        "json"
    )
}

$clientId = [string]$application.appId

$servicePrincipal = Invoke-AzJson -Arguments @(
    "ad",
    "sp",
    "show",
    "--id",
    $clientId,
    "--output",
    "json"
) -AllowFailure

if ($null -eq $servicePrincipal) {
    $servicePrincipal = Invoke-AzJson -Arguments @(
        "ad",
        "sp",
        "create",
        "--id",
        $clientId,
        "--output",
        "json"
    )
}

$servicePrincipalObjectId = [string]$servicePrincipal.id

$oidcSubject = "repo:{0}@{1}/{2}@{3}:environment:{4}" -f
    $config.RepositoryOwner,
    $config.RepositoryOwnerId,
    $config.RepositoryName,
    $config.RepositoryId,
    $config.EnvironmentName

$federatedCredentials = Invoke-AzJson -Arguments @(
    "ad",
    "app",
    "federated-credential",
    "list",
    "--id",
    $clientId,
    "--output",
    "json"
)

$existingFederatedCredential = @($federatedCredentials) |
    Where-Object { $_.name -ceq $config.FederatedCredentialName } |
    Select-Object -First 1

if ($null -eq $existingFederatedCredential) {
    $credentialPath = Join-Path ([IO.Path]::GetTempPath()) ("turkuaz-fic-" + [guid]::NewGuid().ToString("N") + ".json")

    try {
        $credential = @{
            name = $config.FederatedCredentialName
            issuer = $config.OidcIssuer
            subject = $oidcSubject
            description = "TurkuazInstaller production signing from GitHub Actions production environment"
            audiences = @(
                $config.OidcAudience
            )
        }

        Write-JsonFile -Value $credential -Path $credentialPath

        & az ad app federated-credential create --id $clientId --parameters $credentialPath | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Azure federated identity credential creation failed."
        }
    }
    finally {
        if (Test-Path -LiteralPath $credentialPath) {
            Remove-Item -LiteralPath $credentialPath -Force
        }
    }
}
elseif ([string]$existingFederatedCredential.subject -cne $oidcSubject) {
    throw "Existing federated credential subject does not match the immutable GitHub OIDC subject."
}

if ($ConfigureGitHub) {
    Ensure-GitHubEnvironment -Config $config
}

$profile = Invoke-AzJson -Arguments @(
    "artifact-signing",
    "certificate-profile",
    "show",
    "--resource-group",
    $ResourceGroupName,
    "--account-name",
    $AccountName,
    "--profile-name",
    $CertificateProfileName,
    "--output",
    "json"
) -AllowFailure

if ($null -eq $profile -and
    -not [string]::IsNullOrWhiteSpace($IdentityValidationId)) {
    $profile = Invoke-AzJson -Arguments @(
        "artifact-signing",
        "certificate-profile",
        "create",
        "--resource-group",
        $ResourceGroupName,
        "--account-name",
        $AccountName,
        "--profile-name",
        $CertificateProfileName,
        "--profile-type",
        $CertificateProfileType,
        "--identity-validation-id",
        $IdentityValidationId,
        "--output",
        "json"
    )
}

$profileScope = "/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.CodeSigning/codeSigningAccounts/{2}/certificateProfiles/{3}" -f
    $SubscriptionId,
    $ResourceGroupName,
    $AccountName,
    $CertificateProfileName

if ($null -ne $profile) {
    $roleAssignments = Invoke-AzJson -Arguments @(
        "role",
        "assignment",
        "list",
        "--assignee-object-id",
        $servicePrincipalObjectId,
        "--scope",
        $profileScope,
        "--output",
        "json"
    )

    $signerAssignment = @($roleAssignments) |
        Where-Object { $_.roleDefinitionName -ceq $config.SignerRoleName } |
        Select-Object -First 1

    if ($null -eq $signerAssignment) {
        & az role assignment create --assignee-object-id $servicePrincipalObjectId --assignee-principal-type ServicePrincipal --role $config.SignerRoleName --scope $profileScope | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Artifact Signing Certificate Profile Signer role assignment failed."
        }
    }
}

if ($ConfigureGitHub) {
    $githubConfigurationParameters = @{
        Config = $config
        ClientId = $clientId
        TenantId = $tenantId
        Subscription = $SubscriptionId
        Endpoint = $endpoint
        SigningAccount = $AccountName
        ProfileName = $CertificateProfileName
    }

    Set-GitHubEnvironmentConfiguration @githubConfigurationParameters
}

Write-Host ""
Write-Host "Production signing bootstrap status:"
Write-Host ("  Azure account: {0}" -f $AccountName)
Write-Host ("  Endpoint: {0}" -f $endpoint)
Write-Host ("  Entra client id: {0}" -f $clientId)
Write-Host ("  OIDC subject: {0}" -f $oidcSubject)

if ($null -eq $profile) {
    Write-Host ""
    Write-Host "MANUAL STEP REQUIRED:"
    Write-Host "Complete Artifact Signing identity validation in Azure Portal."
    Write-Host "Then rerun this script with -IdentityValidationId <guid>."
}
else {
    Write-Host ("  Certificate profile: {0}" -f $CertificateProfileName)
    Write-Host ("  Signer role: {0}" -f $config.SignerRoleName)
    Write-Host ""
    Write-Host "Run the GitHub 'Production Signing Preflight' workflow before creating v1.0.0."
}
