// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Manifests/VerifiedManifestReader.cs
// 📌 Amac: Ham manifest byte'larini detached signature dogrulamasindan sonra typed YAML parserina iletir
// 📌 Modul - Tool CSharp
// Version: 1.1.0
// Aciklama: Imza verification basarisizken YAML parse edilmesini engeller ve manifest encoding'ini strict UTF-8 olarak sabitler
//
// Bagimli Oldugu Katman: Tool | Service

using System.Text;
using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Infrastructure.Manifests;

public sealed class VerifiedManifestReader
{
    private static readonly UTF8Encoding StrictUtf8 =
        new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    private readonly InstallerManifestReader _manifestReader;
    private readonly IManifestSignatureVerifier
        _signatureVerifier;

    public VerifiedManifestReader(
        InstallerManifestReader manifestReader,
        IManifestSignatureVerifier signatureVerifier)
    {
        ArgumentNullException.ThrowIfNull(
            manifestReader);
        ArgumentNullException.ThrowIfNull(
            signatureVerifier);

        _manifestReader =
            manifestReader;
        _signatureVerifier =
            signatureVerifier;
    }

    public async Task<PackageRelease> ReadAsync(
        ReadOnlyMemory<byte> manifestContent,
        ReadOnlyMemory<byte> detachedSignature,
        PackageId requestedPackageId,
        CancellationToken cancellationToken)
    {
        var verification =
            await _signatureVerifier
                .VerifyAsync(
                    manifestContent,
                    detachedSignature,
                    requestedPackageId,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!verification.IsValid)
        {
            throw new InvalidDataException(
                verification.Message);
        }

        string yaml;

        try
        {
            yaml =
                StrictUtf8.GetString(
                    manifestContent.Span);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                "Installer manifest must be valid UTF-8.",
                exception);
        }

        return _manifestReader.Read(
            yaml);
    }
}
