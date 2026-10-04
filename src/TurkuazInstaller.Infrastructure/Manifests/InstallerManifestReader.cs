// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Manifests/InstallerManifestReader.cs
// 📌 Amac: Community installer manifest YAML metnini typed PackageRelease modeline donusturur
// 📌 Modul - Tool CSharp
// Version: 0.4.0
// Aciklama: Provider adapterlarinin ortak kullandigi schema, package, channel ve artifact parseridir
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TurkuazInstaller.Infrastructure.Manifests;

public sealed class InstallerManifestReader
{
    private const int SupportedSchemaVersion = 1;
    private readonly IDeserializer _deserializer;

    public InstallerManifestReader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    public PackageRelease Read(string yaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaml);

        var document = _deserializer.Deserialize<ManifestDocument>(yaml)
            ?? throw new FormatException("Installer manifest is empty.");

        if (document.SchemaVersion != SupportedSchemaVersion)
        {
            throw new FormatException("Installer manifest schema version is not supported.");
        }

        var packageId = PackageId.Parse(document.Package.Id);
        var version = SemanticVersion.Parse(document.Package.Version);
        var channel = ParseChannel(document.Package.Channel);
        var artifact = new ArtifactDescriptor(
            new Uri(document.Artifact.Uri, UriKind.Absolute),
            ArtifactDigest.ParseSha256(document.Artifact.Sha256),
            document.Artifact.SizeBytes);

        return new PackageRelease(packageId, version, channel, artifact);
    }

    private static ReleaseChannel ParseChannel(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "stable" => ReleaseChannel.Stable,
            "beta" => ReleaseChannel.Beta,
            _ => throw new FormatException("Installer manifest release channel is not supported.")
        };
    }

    private sealed class ManifestDocument
    {
        public int SchemaVersion { get; init; }
        public PackageSection Package { get; init; } = new();
        public ArtifactSection Artifact { get; init; } = new();
    }

    private sealed class PackageSection
    {
        public string Id { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public string Channel { get; init; } = string.Empty;
    }

    private sealed class ArtifactSection
    {
        public string Uri { get; init; } = string.Empty;
        public string Sha256 { get; init; } = string.Empty;
        public long SizeBytes { get; init; }
    }
}
