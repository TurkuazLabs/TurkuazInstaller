// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Operations/JsonOperationJournalRepository.cs
// 📌 Amac: Package bazli installer operation journal snapshotini atomik JSON dosyasinda saklar
// 📌 Modul - Repo CSharp
// Version: 1.2.0
// Aciklama: Domain value objectlerini explicit DTO ile serialize eder ve reboot prerequisite checkpointini geri okuyabilir
//
// Bagimli Oldugu Katman: Repo

using System.Text.Json;
using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Infrastructure.Operations;

public sealed class JsonOperationJournalRepository
    : IInstallerOperationJournalRepository
{
    private const string JsonExtension = ".json";
    private const string TemporarySuffix = ".tmp";

    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            WriteIndented = true
        };

    private readonly JsonOperationJournalRepositoryOptions _options;

    public JsonOperationJournalRepository(
        JsonOperationJournalRepositoryOptions options)
    {
        _options = options;
    }

    public async Task<InstallerOperationJournalEntry?> GetAsync(
        PackageId packageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packageId);

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
                .DeserializeAsync<JournalDocument>(
                    stream,
                    SerializerOptions,
                    cancellationToken)
                .ConfigureAwait(false);

        if (document is null)
        {
            throw new InvalidDataException(
                "Installer operation journal document is empty.");
        }

        return new InstallerOperationJournalEntry(
            document.OperationId,
            PackageId.Parse(
                document.PackageId),
            document.Operation,
            document.Version,
            document.TargetPath,
            document.Phase,
            document.StartedAtUtc,
            document.UpdatedAtUtc,
            document.Failure,
            document.PendingPrerequisiteId);
    }

    public async Task SaveAsync(
        InstallerOperationJournalEntry entry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Directory.CreateDirectory(
            _options.RootDirectory);

        var path =
            GetPath(
                entry.PackageId);

        var temporaryPath =
            string.Concat(
                path,
                TemporarySuffix);

        var document =
            new JournalDocument
            {
                OperationId = entry.OperationId,
                PackageId = entry.PackageId.Value,
                Operation = entry.Operation,
                Version = entry.Version,
                TargetPath = entry.TargetPath,
                Phase = entry.Phase,
                StartedAtUtc = entry.StartedAtUtc,
                UpdatedAtUtc = entry.UpdatedAtUtc,
                Failure = entry.Failure,
                PendingPrerequisiteId =
                    entry.PendingPrerequisiteId
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
        ArgumentNullException.ThrowIfNull(packageId);
        cancellationToken.ThrowIfCancellationRequested();

        var path =
            GetPath(
                packageId);

        if (File.Exists(path))
        {
            File.Delete(path);
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

    private sealed class JournalDocument
    {
        public Guid OperationId { get; init; }

        public string PackageId { get; init; } =
            string.Empty;

        public InstallerOperationType Operation { get; init; }

        public string? Version { get; init; }

        public string TargetPath { get; init; } =
            string.Empty;

        public InstallerOperationPhase Phase { get; init; }

        public DateTimeOffset StartedAtUtc { get; init; }

        public DateTimeOffset UpdatedAtUtc { get; init; }

        public string? Failure { get; init; }

        public string? PendingPrerequisiteId { get; init; }
    }
}
