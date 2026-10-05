// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Manifests/InstallerManifestReader.cs
// 📌 Amac: Community installer manifest YAML metnini tam typed PackageRelease modeline donusturur
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Package, artifact, Authenticode, install policy, prerequisite, preserve path ve rollback alanlarini runtime modeline tasir
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Prerequisites;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TurkuazInstaller.Infrastructure.Manifests;

public sealed class InstallerManifestReader
{
    private const int SupportedSchemaVersion = 1;
    private const string StableChannel = "stable";
    private const string BetaChannel = "beta";
    private const string FullInstallMode = "full";
    private const string AuthenticodeAlgorithm = "authenticode";

    private readonly IDeserializer _deserializer;

    public InstallerManifestReader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(
                UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public PackageRelease Read(
        string yaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaml);

        var document =
            _deserializer.Deserialize<ManifestDocument>(yaml)
            ?? throw new FormatException(
                "Installer manifest is empty.");

        if (document.SchemaVersion != SupportedSchemaVersion)
        {
            throw new FormatException(
                "Installer manifest schema version is not supported.");
        }

        ValidateRequiredSections(document);

        var packageId =
            PackageId.Parse(
                document.Package.Id);

        var version =
            SemanticVersion.Parse(
                document.Package.Version);

        var channel =
            ParseChannel(
                document.Package.Channel);

        var signature =
            ParseSignature(
                document.Artifact.Signature);

        var artifact =
            new ArtifactDescriptor(
                new Uri(
                    document.Artifact.Uri,
                    UriKind.Absolute),
                ArtifactDigest.ParseSha256(
                    document.Artifact.Sha256),
                document.Artifact.SizeBytes,
                signature);

        var prerequisites =
            document.Install.Prerequisites
                .Select(
                    item =>
                        new Prerequisite(
                            item.Id,
                            item.Version))
                .ToArray();

        var preservePaths =
            document.Install.PreservePaths
                .Select(
                    value =>
                    {
                        ArgumentException.ThrowIfNullOrWhiteSpace(
                            value);
                        return value.Trim();
                    })
                .ToArray();

        var install =
            new PackageInstallPolicy(
                ParseInstallMode(
                    document.Install.Mode),
                document.Install.Target,
                prerequisites,
                preservePaths);

        var rollback =
            new PackageRollbackPolicy(
                document.Rollback.Supported,
                document.Rollback.PreviousVersionRequired);

        return new PackageRelease(
            packageId,
            version,
            channel,
            artifact,
            install,
            rollback);
    }

    private static void ValidateRequiredSections(
        ManifestDocument document)
    {
        if (string.IsNullOrWhiteSpace(
                document.Install.Mode))
        {
            throw new FormatException(
                "Installer manifest install mode is required.");
        }

        if (string.IsNullOrWhiteSpace(
                document.Install.Target))
        {
            throw new FormatException(
                "Installer manifest install target is required.");
        }
    }

    private static ReleaseChannel ParseChannel(
        string value)
    {
        return value
            .Trim()
            .ToLowerInvariant() switch
        {
            StableChannel =>
                ReleaseChannel.Stable,
            BetaChannel =>
                ReleaseChannel.Beta,
            _ =>
                throw new FormatException(
                    "Installer manifest release channel is not supported.")
        };
    }

    private static PackageInstallMode ParseInstallMode(
        string value)
    {
        return value
            .Trim()
            .ToLowerInvariant() switch
        {
            FullInstallMode =>
                PackageInstallMode.Full,
            _ =>
                throw new FormatException(
                    "Installer manifest install mode is not supported by Stable v1.")
        };
    }

    private static ArtifactSignatureDescriptor? ParseSignature(
        SignatureSection? signature)
    {
        if (signature is null)
        {
            return null;
        }

        return signature.Algorithm
            .Trim()
            .ToLowerInvariant() switch
        {
            AuthenticodeAlgorithm =>
                new ArtifactSignatureDescriptor(
                    ArtifactSignatureAlgorithm.Authenticode),
            _ =>
                throw new FormatException(
                    "Installer manifest artifact signature algorithm is not supported.")
        };
    }

    private sealed class ManifestDocument
    {
        public int SchemaVersion { get; init; }

        public PackageSection Package { get; init; } =
            new();

        public ArtifactSection Artifact { get; init; } =
            new();

        public InstallSection Install { get; init; } =
            new();

        public RollbackSection Rollback { get; init; } =
            new();
    }

    private sealed class PackageSection
    {
        public string Id { get; init; } =
            string.Empty;

        public string Version { get; init; } =
            string.Empty;

        public string Channel { get; init; } =
            string.Empty;
    }

    private sealed class ArtifactSection
    {
        public string Uri { get; init; } =
            string.Empty;

        public string Sha256 { get; init; } =
            string.Empty;

        public long SizeBytes { get; init; }

        public SignatureSection? Signature { get; init; }
    }

    private sealed class SignatureSection
    {
        public string Algorithm { get; init; } =
            string.Empty;
    }

    private sealed class InstallSection
    {
        public string Mode { get; init; } =
            string.Empty;

        public string Target { get; init; } =
            string.Empty;

        public List<PrerequisiteSection> Prerequisites { get; init; } =
            new();

        public List<string> PreservePaths { get; init; } =
            new();
    }

    private sealed class PrerequisiteSection
    {
        public string Id { get; init; } =
            string.Empty;

        public string Version { get; init; } =
            string.Empty;
    }

    private sealed class RollbackSection
    {
        public bool Supported { get; init; }

        public bool PreviousVersionRequired { get; init; }
    }
}
