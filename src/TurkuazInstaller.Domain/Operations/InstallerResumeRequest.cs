// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Operations/InstallerResumeRequest.cs
// 📌 Amac: Reboot sonrasi ayni installer operasyonunu guvenli sekilde yeniden kurmak icin kalici request snapshotini tasir
// 📌 Modul - Domain CSharp
// Version: 1.0.0
// Aciklama: Operation, expected release kimligi ve signed manifest kaynaklarini typed modelde toplar
//
// Bagimli Oldugu Katman: Service | Repo | Tool

using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Domain.Operations;

public sealed record InstallerResumeRequest
{
    public InstallerResumeRequest(
        PackageId packageId,
        InstallerOperationType operation,
        SemanticVersion expectedVersion,
        ArtifactDigest expectedArtifactDigest,
        ReleaseChannel channel,
        string manifestSource,
        string? rollbackManifestSource,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(
            packageId);
        ArgumentNullException.ThrowIfNull(
            expectedVersion);
        ArgumentNullException.ThrowIfNull(
            expectedArtifactDigest);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            manifestSource);

        PackageId = packageId;
        Operation = operation;
        ExpectedVersion = expectedVersion;
        ExpectedArtifactDigest = expectedArtifactDigest;
        Channel = channel;
        ManifestSource = manifestSource.Trim();
        RollbackManifestSource =
            string.IsNullOrWhiteSpace(
                rollbackManifestSource)
                ? null
                : rollbackManifestSource.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    public PackageId PackageId { get; }

    public InstallerOperationType Operation { get; }

    public SemanticVersion ExpectedVersion { get; }

    public ArtifactDigest ExpectedArtifactDigest { get; }

    public ReleaseChannel Channel { get; }

    public string ManifestSource { get; }

    public string? RollbackManifestSource { get; }

    public DateTimeOffset CreatedAtUtc { get; }
}
