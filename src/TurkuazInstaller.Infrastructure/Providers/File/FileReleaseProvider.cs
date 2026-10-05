// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/File/FileReleaseProvider.cs
// 📌 Amac: Air-gapped ve local test senaryolari icin dosyadan signed installer manifesti okur
// 📌 Modul - Tool CSharp
// Version: 1.1.0
// Aciklama: Local manifestin yanindaki .p7s detached signature dosyasini zorunlu tutar ve verification sonrasi typed release modeline donusturur
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Contracts.Releases;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;

namespace TurkuazInstaller.Infrastructure.Providers.File;

public sealed class FileReleaseProvider : IReleaseProvider
{
    private const string SignatureMissingMessage =
        "Detached installer manifest signature is required.";

    private readonly FileReleaseProviderOptions _options;
    private readonly VerifiedManifestReader
        _verifiedManifestReader;

    public FileReleaseProvider(
        InstallerManifestReader manifestReader,
        IManifestSignatureVerifier signatureVerifier,
        FileReleaseProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(
            manifestReader);
        ArgumentNullException.ThrowIfNull(
            signatureVerifier);
        ArgumentNullException.ThrowIfNull(
            options);

        _verifiedManifestReader =
            new VerifiedManifestReader(
                manifestReader,
                signatureVerifier);

        _options =
            options;
    }

    public async Task<PackageRelease?> GetLatestReleaseAsync(
        PackageId packageId,
        ReleaseChannel channel,
        CancellationToken cancellationToken)
    {
        var path =
            channel == ReleaseChannel.Stable
                ? _options.StableManifestPath
                : _options.BetaManifestPath;

        if (!System.IO.File.Exists(path))
        {
            return null;
        }

        var signaturePath =
            ManifestSignatureConventions
                .GetDetachedSignaturePath(
                    path);

        if (!System.IO.File.Exists(signaturePath))
        {
            throw new InvalidDataException(
                SignatureMissingMessage);
        }

        var manifestContent =
            await System.IO.File
                .ReadAllBytesAsync(
                    path,
                    cancellationToken)
                .ConfigureAwait(false);

        var detachedSignature =
            await System.IO.File
                .ReadAllBytesAsync(
                    signaturePath,
                    cancellationToken)
                .ConfigureAwait(false);

        var parsed =
            await _verifiedManifestReader
                .ReadAsync(
                    manifestContent,
                    detachedSignature,
                    packageId,
                    cancellationToken)
                .ConfigureAwait(false);

        return ProviderValidation.MatchRequest(
            parsed,
            packageId,
            channel);
    }
}
