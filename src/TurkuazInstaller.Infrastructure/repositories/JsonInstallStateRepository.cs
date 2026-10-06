// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/repositories/JsonInstallStateRepository.cs
// 📌 Amac: InstalledPackageState verisini paket bazli JSON dosyalarinda atomik olarak saklar ve siler
// 📌 Modul - Repo CSharp
// Version: 1.1.0
// Aciklama: Package Get/Save/Delete yaninda committed JSON state dosyalarini deterministik package id sirasinda listeler
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

        return await ReadStateAsync(
                stream,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<InstalledPackageState>> ListAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(
                _options.RootDirectory))
        {
            return Array.Empty<InstalledPackageState>();
        }

        var paths =
            Directory
                .EnumerateFiles(
                    _options.RootDirectory,
                    string.Concat(
                        "*",
                        JsonExtension),
                    SearchOption.TopDirectoryOnly)
                .OrderBy(
                    Path.GetFileName,
                    StringComparer.Ordinal)
                .ToArray();

        var states =
            new List<InstalledPackageState>(
                paths.Length);

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var stream =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096,
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan);

            var state =
                await ReadStateAsync(
                        stream,
                        cancellationToken)
                    .ConfigureAwait(false);

            var expectedFileName =
                string.Concat(
                    state.PackageId.Value,
                    JsonExtension);

            if (
                !string.Equals(
                    Path.GetFileName(
                        path),
                    expectedFileName,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Install state file name does not match package id.");
            }

            states.Add(
                state);
        }

        return states.AsReadOnly();
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

    private static async Task<InstalledPackageState> ReadStateAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
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
