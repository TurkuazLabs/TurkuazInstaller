// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Operations/JsonInstallerResumeRequestRepository.cs
// 📌 Amac: Package bazli reboot resume request snapshotini atomik JSON dosyasinda saklar
// 📌 Modul - Repo CSharp
// Version: 1.0.0
// Aciklama: Signed manifest yeniden cozumleme bilgisi ve expected release kimligini reboot boyunca kalici tutar
//
// Bagimli Oldugu Katman: Repo

using System.Text.Json;
using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Infrastructure.Operations;

public sealed class JsonInstallerResumeRequestRepository
    : IInstallerResumeRequestRepository
{
    private const string JsonExtension = ".json";
    private const string TemporarySuffix = ".tmp";

    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            WriteIndented = true
        };

    private readonly JsonInstallerResumeRequestRepositoryOptions _options;

    public JsonInstallerResumeRequestRepository(
        JsonInstallerResumeRequestRepositoryOptions options)
    {
        _options = options;
    }

    public async Task<InstallerResumeRequest?> GetAsync(
        PackageId packageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            packageId);

        var path =
            GetPath(
                packageId);

        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream =
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);

        var document =
            await JsonSerializer
                .DeserializeAsync<ResumeDocument>(
                    stream,
                    SerializerOptions,
                    cancellationToken)
                .ConfigureAwait(false);

        if (document is null)
        {
            throw new InvalidDataException(
                "Installer resume request document is empty.");
        }

        return new InstallerResumeRequest(
            PackageId.Parse(
                document.PackageId),
            document.Operation,
            SemanticVersion.Parse(
                document.ExpectedVersion),
            ArtifactDigest.ParseSha256(
                document.ExpectedArtifactSha256),
            document.Channel,
            document.ManifestSource,
            document.RollbackManifestSource,
            document.CreatedAtUtc);
    }

    public async Task SaveAsync(
        InstallerResumeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        Directory.CreateDirectory(
            _options.RootDirectory);

        var path =
            GetPath(
                request.PackageId);

        var temporaryPath =
            string.Concat(
                path,
                TemporarySuffix);

        var document =
            new ResumeDocument
            {
                PackageId =
                    request.PackageId.Value,
                Operation =
                    request.Operation,
                ExpectedVersion =
                    request.ExpectedVersion.ToString(),
                ExpectedArtifactSha256 =
                    request.ExpectedArtifactDigest.Sha256,
                Channel =
                    request.Channel,
                ManifestSource =
                    request.ManifestSource,
                RollbackManifestSource =
                    request.RollbackManifestSource,
                CreatedAtUtc =
                    request.CreatedAtUtc
            };

        await using (
            var stream =
                new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.Asynchronous))
        {
            await JsonSerializer
                .SerializeAsync(
                    stream,
                    document,
                    SerializerOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            await stream
                .FlushAsync(
                    cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(
            temporaryPath,
            path,
            overwrite: true);
    }

    public Task DeleteAsync(
        PackageId packageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            packageId);
        cancellationToken.ThrowIfCancellationRequested();

        var path =
            GetPath(
                packageId);

        if (File.Exists(path))
        {
            File.Delete(
                path);
        }

        return Task.CompletedTask;
    }

    private string GetPath(
        PackageId packageId)
    {
        return Path.Combine(
            _options.RootDirectory,
            string.Concat(
                packageId.Value,
                JsonExtension));
    }

    private sealed class ResumeDocument
    {
        public string PackageId { get; init; } =
            string.Empty;

        public InstallerOperationType Operation { get; init; }

        public string ExpectedVersion { get; init; } =
            string.Empty;

        public string ExpectedArtifactSha256 { get; init; } =
            string.Empty;

        public ReleaseChannel Channel { get; init; }

        public string ManifestSource { get; init; } =
            string.Empty;

        public string? RollbackManifestSource { get; init; }

        public DateTimeOffset CreatedAtUtc { get; init; }
    }
}
