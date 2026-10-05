// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/repositories/JsonInstallStateRepository.cs
// 📌 Amac: InstalledPackageState verisini paket bazli JSON dosyalarinda atomik olarak saklar ve siler
// 📌 Modul - Repo CSharp
// Version: 1.0.0
// Aciklama: Application state portunu dosya sistemi storage adapteriyle Get, Save ve Delete davranislariyla uygular
//
// Bagimli Oldugu Katman: Repo

using System.Text.Json;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Domain.State;

namespace TurkuazInstaller.Infrastructure.Repositories;

public sealed class JsonInstallStateRepository
    : IInstallStateRepository
{
    private const string JsonExtension = ".json";
    private const string TemporarySuffix = ".tmp";

    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            WriteIndented = true
        };

    private readonly JsonInstallStateRepositoryOptions _options;

    public JsonInstallStateRepository(
        JsonInstallStateRepositoryOptions options)
    {
        _options = options;
    }

    public async Task<InstalledPackageState?> GetAsync(
        PackageId packageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packageId);

        var path = GetStatePath(packageId);

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
                .DeserializeAsync<StateDocument>(
                    stream,
                    SerializerOptions,
                    cancellationToken)
                .ConfigureAwait(false);

        if (document is null)
        {
            throw new InvalidDataException(
                "Install state document is empty.");
        }

        return new InstalledPackageState(
            PackageId.Parse(
                document.PackageId),
            SemanticVersion.Parse(
                document.Version),
            document.Channel,
            document.TargetPath);
    }

    public async Task SaveAsync(
        InstalledPackageState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);

        Directory.CreateDirectory(
            _options.RootDirectory);

        var path =
            GetStatePath(
                state.PackageId);

        var temporaryPath =
            string.Concat(
                path,
                TemporarySuffix);

        var document =
            new StateDocument
            {
                PackageId =
                    state.PackageId.Value,
                Version =
                    state.Version.ToString(),
                Channel =
                    state.Channel,
                TargetPath =
                    state.TargetPath
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
            GetStatePath(
                packageId);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetStatePath(
        PackageId packageId)
    {
        return Path.Combine(
            _options.RootDirectory,
            string.Concat(
                packageId.Value,
                JsonExtension));
    }

    private sealed class StateDocument
    {
        public string PackageId { get; init; } =
            string.Empty;

        public string Version { get; init; } =
            string.Empty;

        public ReleaseChannel Channel { get; init; }

        public string TargetPath { get; init; } =
            string.Empty;
    }
}
