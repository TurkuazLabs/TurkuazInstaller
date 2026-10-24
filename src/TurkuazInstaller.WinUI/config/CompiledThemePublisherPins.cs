// Dosya Yolu: src/TurkuazInstaller.WinUI/config/CompiledThemePublisherPins.cs
// Amac: Gorunur tema icin salt-okunur, uygulama tarafindan belirlenmis trust anchor
// Modul - CSharp Config
// Version: 0.1.0-qa
// Aciklama: Uretim imzacisi onaylanmadan hicbir harici tema kendine guven veremez
// Bagimli Oldugu Katman: Infrastructure | Signing Policy

using TurkuazInstaller.Domain.Manifests;
using TurkuazInstaller.Infrastructure.Branding;

namespace TurkuazInstaller.WinUI.Config;

/// <summary>
/// Trust for THEME packs is intentionally separate from user-writable
/// manifest-trust.yml (used for existing application update manifests).
///
/// No real publisher key/pin has been provisioned for this feature branch.
/// Thus, an opted-in theme fails closed until a reviewed signing identity is
/// compiled into a publisher-controlled distribution.
/// Never read trust roots from theme.tpk, the package directory, an environment
/// variable or the per-user YAML store.
/// </summary>
internal static class CompiledThemePublisherPins
{
    public static PinnedThemeTrustPolicyRepository Create()
    {
        // Empty allowlist by default is deliberate and prevents the user from
        // teaching a local unsigned theme to trust an arbitrary certificate.
        // Production onboarding requires review, pinned public certificate
        // fingerprint and an Authenticode-signed installer build.
        return new PinnedThemeTrustPolicyRepository(
            Array.Empty<KeyValuePair<string, ManifestTrustPolicy>>());
    }
}
