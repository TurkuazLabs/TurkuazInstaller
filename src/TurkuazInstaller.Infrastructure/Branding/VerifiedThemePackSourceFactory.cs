// Dosya Yolu: src/TurkuazInstaller.Infrastructure/Branding/VerifiedThemePackSourceFactory.cs
// Amac: Derlemede secilen TPK/TPE paket turunu fail-closed dogrulama okuyucusuna baglamak
// Modul - CSharp Tool/Adapter
// Version: 0.2.0-qa
// Aciklama: Sifreli modda anahtar yetkisi yoksa imzasiz TPK'ye dusmez
// Bagimli Oldugu Katman: Infrastructure | Cryptography | View

using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Infrastructure.Branding;

public enum ThemePackTransportMode
{
    SignedTpk = 1,
    EncryptedTpe = 2
}

public sealed record VerifiedThemePackSource(
    string FileName,
    IVerifiedThemePackReader Reader);

public static class VerifiedThemePackSourceFactory
{
    public static VerifiedThemePackSource Create(
        ThemePackTransportMode transport,
        SignedThemePackReader trustedSignedReader,
        IThemeDecryptionKeyProvider? authorizedKeys = null,
        PinnedThemeRelease? releasePin = null)
    {
        ArgumentNullException.ThrowIfNull(trustedSignedReader);

        // Verify CMS/GCM first, and THEN match the exact reviewed theme
        // identity/version. Signed old releases must not be substituted.
        IVerifiedThemePackReader CheckRelease(IVerifiedThemePackReader reader) =>
            releasePin is null ? reader : new PinnedThemeReleaseReader(reader, releasePin);

        return transport switch
        {
            ThemePackTransportMode.SignedTpk =>
                new VerifiedThemePackSource(
                    "theme.tpk", CheckRelease(trustedSignedReader)),

            ThemePackTransportMode.EncryptedTpe =>
                new VerifiedThemePackSource(
                    "theme.tpe", CheckRelease(new EncryptedSignedThemePackReader(
                        trustedSignedReader,
                        authorizedKeys ?? new UnavailableThemeKeyProvider()))),

            _ => throw new ArgumentOutOfRangeException(nameof(transport),
                "Only recognized signed or authenticated-encrypted theme modes are permitted.")
        };
    }

    // This is deliberately NOT a decryption bypass or dummy default key.
    // A product requiring encryption without an authorized key provider
    // must show a fail-closed UI error instead of falling back to theme.tpk.
    private sealed class UnavailableThemeKeyProvider : IThemeDecryptionKeyProvider
    {
        public ValueTask<byte[]?> ResolveKeyAsync(
            PackageId expectedPackageId, string keyId,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(expectedPackageId);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<byte[]?>(null);
        }
    }
}
