# 📄 Dosya Yolu: /config/release-signing.psd1
# 📌 Amac: Production signing bootstrap, OIDC ve release environment sabitlerini merkezilestirir
# 📌 Modul - Config PowerShell
# Version: 1.0.0
# Aciklama: Azure Artifact Signing ve GitHub production environment icin tek kaynak konfigurasyonudur
# Bagimli Oldugu Katman: Config | Tool

@{
    RepositoryOwner = "TurkuazLabs"
    RepositoryOwnerId = "326224284"
    RepositoryName = "TurkuazInstaller"
    RepositoryId = "1388165383"

    EnvironmentName = "production"
    ReleaseTagPattern = "v*.*.*"
    PreflightBranch = "main"

    ResourceProvider = "Microsoft.CodeSigning"
    ArtifactSigningExtension = "artifact-signing"
    SignerRoleName = "Artifact Signing Certificate Profile Signer"
    ApplicationDisplayName = "TurkuazInstaller GitHub Release Signer"
    FederatedCredentialName = "turkuazinstaller-production"
    OidcIssuer = "https://token.actions.githubusercontent.com/"
    OidcAudience = "api://AzureADTokenExchange"

    DefaultCertificateProfileName = "TurkuazInstallerPublic"
    DefaultCertificateProfileType = "PublicTrust"
    DefaultSku = "Basic"
    TimestampUrl = "http://timestamp.acs.microsoft.com"

    GitHubApiVersion = "2026-03-10"

    RegionEndpoints = @{
        brazilsouth = "https://brs.codesigning.azure.net"
        centralus = "https://cus.codesigning.azure.net"
        eastus = "https://eus.codesigning.azure.net"
        japaneast = "https://jpe.codesigning.azure.net"
        koreacentral = "https://krc.codesigning.azure.net"
        northcentralus = "https://ncus.codesigning.azure.net"
        northeurope = "https://neu.codesigning.azure.net"
        polandcentral = "https://plc.codesigning.azure.net"
        southcentralus = "https://scus.codesigning.azure.net"
        switzerlandnorth = "https://swn.codesigning.azure.net"
        westcentralus = "https://wcus.codesigning.azure.net"
        westeurope = "https://weu.codesigning.azure.net"
        westus = "https://wus.codesigning.azure.net"
        westus2 = "https://wus2.codesigning.azure.net"
        westus3 = "https://wus3.codesigning.azure.net"
    }
}
