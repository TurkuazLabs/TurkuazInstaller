// Dosya Yolu: src/TurkuazInstaller.WinUI/config/CompiledBrandedReleasePolicy.cs
// Amac: Marka derlemelerinde manifest kaynagini ve release publisher pinini sabitlemek
// Modul - CSharp Config
// Version: 0.1.0-qa
// Aciklama: Missing production publisher/config fails closed; no user YAML enrollment
// Bagimli Oldugu Katman: Infrastructure | Domain | Windows Composition

using TurkuazInstaller.Domain.Manifests;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Infrastructure.Branding;

namespace TurkuazInstaller.WinUI.Config;

/// <summary>
/// This is part of the reviewed, signed BRAND binary, not user configuration.
/// Do not configure through user-writable YAML, environment variables, .tpk
/// content or a command line. Community mode never requests this policy.
/// </summary>
internal static class CompiledBrandedReleasePolicy
{
    // Real production values require explicit publisher approval and signing.
    // Deliberate NULLs prevent accidentally publishing a branded installer
    // whose main installation manifest is controlled by the local user.
    private const string? ApprovedManifestUrl = null;
    private const string? PublisherSubject = null;
    private const string? PublisherCertificateSha256 = null;

    public static (string ManifestUrl, PinnedThemeTrustPolicyRepository Trust) Create(
        PackageId boundProduct)
    {
        ArgumentNullException.ThrowIfNull(boundProduct);

        if (!Uri.TryCreate(ApprovedManifestUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrWhiteSpace(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.Query) ||
            string.IsNullOrWhiteSpace(PublisherSubject) ||
            string.IsNullOrWhiteSpace(PublisherCertificateSha256))
        {
            throw new InvalidOperationException(
                "Branded installer requires a compiled HTTPS manifest URL and trusted publisher certificate pin.");
        }

        var publisher = new ManifestTrustPolicy(
            PublisherSubject, PublisherCertificateSha256);
        return (uri.AbsoluteUri, new PinnedThemeTrustPolicyRepository(
            new Dictionary<string, ManifestTrustPolicy>(StringComparer.Ordinal)
            {
                [boundProduct.Value] = publisher
            }));
    }
}
