// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/repositories/YamlManifestTrustPolicyRepository.cs
// 📌 Amac: Package bazli manifest publisher trust policy bilgisini dis YAML trust store'dan okur
// 📌 Modul - Repo CSharp
// Version: 1.1.0
// Aciklama: Manifest disindaki trust anchor kaynagini schema, package id, duplicate entry, publisher subject ve sertifika SHA-256 pini ile fail-closed dogrular
//
// Bagimli Oldugu Katman: Repo | Service

using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Domain.Manifests;
using TurkuazInstaller.Domain.Products;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TurkuazInstaller.Infrastructure.Repositories;

public sealed class YamlManifestTrustPolicyRepository
    : IManifestTrustPolicyRepository
{
    private const int SupportedSchemaVersion = 1;

    private readonly YamlManifestTrustPolicyRepositoryOptions
        _options;

    private readonly IDeserializer _deserializer;

    public YamlManifestTrustPolicyRepository(
        YamlManifestTrustPolicyRepositoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        _options =
            options;

        _deserializer =
            new DeserializerBuilder()
                .WithNamingConvention(
                    UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
    }

    public async Task<ManifestTrustPolicy?> GetAsync(
        PackageId packageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            packageId);

        if (!File.Exists(_options.FilePath))
        {
            return null;
        }

        var yaml =
            await File
                .ReadAllTextAsync(
                    _options.FilePath,
                    cancellationToken)
                .ConfigureAwait(false);

        var document =
            _deserializer
                .Deserialize<TrustStoreDocument>(
                    yaml)
            ?? throw new FormatException(
                "Manifest trust store is empty.");

        if (
            document.SchemaVersion !=
            SupportedSchemaVersion)
        {
            throw new FormatException(
                "Manifest trust store schema version is not supported.");
        }

        var seenPackageIds =
            new HashSet<string>(
                StringComparer.Ordinal);

        ManifestTrustPolicy? match =
            null;

        foreach (var item in document.Packages)
        {
            var configuredPackageId =
                PackageId.Parse(
                    item.Id);

            if (
                !seenPackageIds.Add(
                    configuredPackageId.Value))
            {
                throw new FormatException(
                    string.Concat(
                        "Manifest trust store contains duplicate package id: ",
                        configuredPackageId.Value));
            }

            if (
                string.Equals(
                    configuredPackageId.Value,
                    packageId.Value,
                    StringComparison.Ordinal))
            {
                match =
                    new ManifestTrustPolicy(
                        item.PublisherSubject,
                        item.CertificateSha256);
            }
        }

        return match;
    }

    private sealed class TrustStoreDocument
    {
        public int SchemaVersion { get; init; }

        public List<PackageTrustDocument> Packages { get; init; } =
            new();
    }

    private sealed class PackageTrustDocument
    {
        public string Id { get; init; } =
            string.Empty;

        public string PublisherSubject { get; init; } =
            string.Empty;

        public string CertificateSha256 { get; init; } =
            string.Empty;
    }
}
