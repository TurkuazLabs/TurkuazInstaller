// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Manifests/VelopackPayloadInventoryBuilder.cs
// 📌 Amac: Hash ile dogrulanmis Velopack full nupkg iceriginden imzalanabilir dosya envanteri uretir
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: lib/app payload dosyalarini bounded SHA-256 ile tarar, deterministic JSON uretir; imzalama veya recovery yapmaz
// Bagimli Oldugu Katman: Tool | Domain

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Infrastructure.Manifests;

public sealed class VelopackPayloadInventoryBuilder
{
    private const string PayloadPrefix = "lib/app/";
    private const string InstalledPrefix = "current/";
    private const string FullPackageSuffix = "-full.nupkg";
    private const int MaximumArchiveEntries = 10000;
    private const int MaximumPayloadFiles = 4096;
    private const long MaximumSingleFileBytes = 256L * 1024 * 1024;
    private const long MaximumTotalPayloadBytes = 1024L * 1024 * 1024;
    private const int BufferSize = 81920;

    // Returns unsigned, reproducible bytes. The release tool must sign these
    // exact bytes with an approved key, out of process. This code never signs.
    public async Task<byte[]> BuildUnsignedAsync(
        string verifiedFullPackagePath,
        PackageRelease trustedRelease,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verifiedFullPackagePath);
        ArgumentNullException.ThrowIfNull(trustedRelease);
        cancellationToken.ThrowIfCancellationRequested();

        if (!verifiedFullPackagePath.EndsWith(
                FullPackageSuffix, StringComparison.OrdinalIgnoreCase) ||
            !trustedRelease.Artifact.Uri.AbsolutePath.EndsWith(
                FullPackageSuffix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Installed payload inventory requires a Velopack full nupkg.");
        }

        await using var source = new FileStream(
            verifiedFullPackagePath,
            FileMode.Open, FileAccess.Read, FileShare.Read,
            BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

        if (source.Length != trustedRelease.Artifact.SizeBytes)
        {
            throw new InvalidDataException(
                "Velopack inventory source size differs from trusted release.");
        }

        var archiveHash = await SHA256.HashDataAsync(source, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(
                Convert.ToHexString(archiveHash),
                trustedRelease.Artifact.Digest.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Velopack inventory source SHA-256 differs from trusted release.");
        }

        source.Position = 0;
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count > MaximumArchiveEntries)
        {
            throw new InvalidDataException(
                "Velopack inventory ZIP contains too many entries.");
        }

        var seenArchiveNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenInstalledNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<(string Path, string Sha256)>();
        long totalBytes = 0;

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var isDirectory = entry.FullName.EndsWith("/", StringComparison.Ordinal);
            var archiveName = isDirectory ? entry.FullName[..^1] : entry.FullName;
            SignedPayloadInventoryReader.ValidatePath(archiveName);
            if (!seenArchiveNames.Add(archiveName))
            {
                throw new InvalidDataException(
                    "Velopack inventory ZIP has case-insensitive duplicate entries.");
            }

            // The nupkg is a ZIP, and a symlink entry must not become a
            // trusted installed regular file or escape the install root.
            var unixFileType = (entry.ExternalAttributes >> 16) & 0xF000;
            if (unixFileType == 0xA000 ||
                unixFileType != 0 && unixFileType != 0x8000 &&
                unixFileType != 0x4000)
            {
                throw new InvalidDataException(
                    "Velopack inventory ZIP contains unsupported file types.");
            }

            if (archiveName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase) &&
                !entry.FullName.StartsWith(PayloadPrefix, StringComparison.Ordinal))
            {
                if (!isDirectory || !string.Equals(
                        archiveName, "lib", StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Velopack inventory ZIP contains an unsupported lib layout.");
                }
            }

            if (isDirectory || !entry.FullName.StartsWith(
                    PayloadPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (files.Count >= MaximumPayloadFiles ||
                entry.Length > MaximumSingleFileBytes ||
                entry.Length > MaximumTotalPayloadBytes - totalBytes)
            {
                throw new InvalidDataException(
                    "Velopack inventory payload exceeds bounded limits.");
            }

            var installedName = InstalledPrefix +
                entry.FullName[PayloadPrefix.Length..];
            SignedPayloadInventoryReader.ValidatePath(installedName);
            if (!seenInstalledNames.Add(installedName))
            {
                throw new InvalidDataException(
                    "Velopack inventory ZIP contains conflicting installed paths.");
            }

            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using var input = entry.Open();
            var buffer = new byte[BufferSize];
            long fileBytes = 0;
            while (true)
            {
                var read = await input.ReadAsync(buffer, cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
                if (read > MaximumSingleFileBytes - fileBytes ||
                    read > MaximumTotalPayloadBytes - totalBytes)
                {
                    throw new InvalidDataException(
                        "Velopack inventory expanded payload exceeds bounded limits.");
                }
                digest.AppendData(buffer, 0, read);
                fileBytes += read;
                totalBytes += read;
            }
            if (fileBytes != entry.Length)
            {
                throw new InvalidDataException(
                    "Velopack inventory decompressed file length differs from ZIP metadata.");
            }
            files.Add((installedName,
                Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant()));
        }

        if (files.Count == 0)
        {
            throw new InvalidDataException(
                "Velopack full package has no lib/app payload files.");
        }

        files.Sort((a, b) => StringComparer.Ordinal.Compare(a.Path, b.Path));
        var serialized = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema_version = 1,
            package_id = trustedRelease.PackageId.Value,
            version = trustedRelease.Version.ToString(),
            channel = trustedRelease.Channel.ToString().ToLowerInvariant(),
            artifact_sha256 = trustedRelease.Artifact.Digest.Sha256,
            files = files.Select(file => new
            {
                path = file.Path,
                sha256 = file.Sha256
            }).ToArray()
        });

        if (serialized.Length > ManifestContentLimits.MaximumManifestBytes)
        {
            throw new InvalidDataException(
                "Generated Velopack inventory exceeds signed manifest size limit.");
        }
        return serialized;
    }
}
