// Dosya Yolu: src/TurkuazInstaller.Infrastructure/Branding/VerifiedThemePackSourceFactory.cs
// Amac: Derlemede secilen TPK/TPE paket turunu fail-closed dogrulama okuyucusuna baglamak
// Modul - CSharp Tool/Adapter
// Version: 0.1.0-qa
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
        IThemeDecryptionKeyProvider? authorizedKeys = null)
    {
        ArgumentNullException.ThrowIfNull(trustedSignedReader);

        return transport switch
        {
            ThemePackTransportMode.SignedTpk =>
                new VerifiedThemePackSource("theme.tpk", trustedSignedReader),

            ThemePackTransportMode.EncryptedTpe =>
                new VerifiedThemePackSource("theme.tpe",
                    new EncryptedSignedThemePackReader(
                        trustedSignedReader,
                        authorizedKeys ?? new UnavailableThemeKeyProvider())),

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
