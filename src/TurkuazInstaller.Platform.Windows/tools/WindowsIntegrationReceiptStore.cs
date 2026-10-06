// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsIntegrationReceiptStore.cs
// 📌 Amac: Windows integration receipt bilgisini package-scoped atomik JSON dosyasinda saklar
// 📌 Modul - Repo CSharp
// Version: 1.0.0
// Aciklama: Apply/reconcile ve uninstall cleanup icin shortcut hash ve protocol scheme listesini kalici tutar
//
// Bagimli Oldugu Katman: Repo | Tool

using System.Text.Json;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsIntegrationReceiptStore
{
    private const string TemporarySuffix = ".tmp";

    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            WriteIndented = true
        };

    private readonly string _rootDirectory;

    public WindowsIntegrationReceiptStore(
        string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            rootDirectory);

        _rootDirectory =
            Path.GetFullPath(
                rootDirectory);
    }

    public async Task<WindowsIntegrationReceipt?> GetAsync(
        PackageId packageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            packageId);

        var path =
            GetPath(
                packageId);

        if (!File.Exists(
                path))
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
                .DeserializeAsync<ReceiptDocument>(
                    stream,
                    SerializerOptions,
                    cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidDataException(
                "Windows integration receipt is empty.");

        var receiptPackageId =
            PackageId.Parse(
                document.PackageId);

        if (receiptPackageId != packageId)
        {
            throw new InvalidDataException(
                "Windows integration receipt package id mismatch.");
        }

        return new WindowsIntegrationReceipt(
            receiptPackageId,
            document.Shortcuts
                .Select(
                    item =>
                        new WindowsShortcutReceipt(
                            item.ActionId,
                            Path.GetFullPath(
                                item.Path),
                            NormalizeSha256(
                                item.Sha256)))
                .ToArray(),
            document.ProtocolSchemes
                .Select(
                    value =>
                        value.Trim()
                            .ToLowerInvariant())
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    public async Task SaveAsync(
        WindowsIntegrationReceipt receipt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            receipt);

        Directory.CreateDirectory(
            _rootDirectory);

        var path =
            GetPath(
                receipt.PackageId);

        var temporaryPath =
            string.Concat(
                path,
                TemporarySuffix);

        var document =
            new ReceiptDocument
            {
                PackageId =
                    receipt.PackageId.Value,
                Shortcuts =
                    receipt.Shortcuts
                        .Select(
                            item =>
                                new ShortcutDocument
                                {
                                    ActionId =
                                        item.ActionId,
                                    Path =
                                        Path.GetFullPath(
                                            item.Path),
                                    Sha256 =
                                        NormalizeSha256(
                                            item.Sha256)
                                })
                        .ToList(),
                ProtocolSchemes =
                    receipt.ProtocolSchemes
                        .Select(
                            value =>
                                value.Trim()
                                    .ToLowerInvariant())
                        .Distinct(
                            StringComparer.OrdinalIgnoreCase)
                        .ToList()
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

        if (File.Exists(
                path))
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
            _rootDirectory,
            string.Concat(
                packageId.Value,
                ".json"));
    }

    private static string NormalizeSha256(
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value);

        var normalized =
            value.Trim()
                .ToLowerInvariant();

        if (
            normalized.Length != 64 ||
            normalized.Any(
                character =>
                    !Uri.IsHexDigit(
                        character)))
        {
            throw new InvalidDataException(
                "Windows integration shortcut SHA-256 is invalid.");
        }

        return normalized;
    }

    private sealed class ReceiptDocument
    {
        public string PackageId { get; init; } =
            string.Empty;

        public List<ShortcutDocument> Shortcuts { get; init; } =
            new();

        public List<string> ProtocolSchemes { get; init; } =
            new();
    }

    private sealed class ShortcutDocument
    {
        public string ActionId { get; init; } =
            string.Empty;

        public string Path { get; init; } =
            string.Empty;

        public string Sha256 { get; init; } =
            string.Empty;
    }
}
