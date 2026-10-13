// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/tools/InstalledPayloadHashTool.cs
// 📌 Amac: Kurulum root altindaki normal dosyalari diske yazmadan SHA-256 ile olcer
// 📌 Modul - Tool CSharp
// Version: 1.0.1
// Aciklama: Traversal ve reparse point reddeder, dosya okunamazsa fail-closed hata yayar
// Bagimli Oldugu Katman: Tool | Service

using System.Security.Cryptography;
using TurkuazInstaller.Contracts.Operations;

namespace TurkuazInstaller.Infrastructure.Tools;

public sealed class InstalledPayloadHashTool : IInstalledPayloadHashTool
{
    private const int ReadBufferSize = 81920;

    public async Task<string?> ComputeSha256Async(
        string installRoot,
        string relativePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        cancellationToken.ThrowIfCancellationRequested();

        if (Path.IsPathRooted(relativePath) ||
            relativePath.Contains(':') ||
            relativePath.Contains('\0'))
        {
            throw new ArgumentException(
                "Hash target must be a relative path.", nameof(relativePath));
        }

        var parts = relativePath.Split(
            new[] { '/', '\\' }, StringSplitOptions.None);
        if (parts.Any(part =>
                string.IsNullOrWhiteSpace(part) ||
                part is "." or ".." ||
                part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new ArgumentException(
                "Hash target path contains invalid segments.", nameof(relativePath));
        }

        var root = Path.GetFullPath(installRoot);
        var candidate = root;
        foreach (var part in parts)
        {
            candidate = Path.Combine(candidate, part);
        }
        candidate = Path.GetFullPath(candidate);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var rootPrefix = Path.EndsInDirectorySeparator(root)
            ? root : root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, comparison))
        {
            throw new ArgumentException(
                "Hash target escaped the install root.", nameof(relativePath));
        }

        // Reject reparse points on the root ancestry and every child.
        // This is a read-only observation, not an atomic or race-free
        // ownership proof. It never authorizes a repair or delete.
        var ancestor = new DirectoryInfo(root);
        while (ancestor is not null)
        {
            if (!ancestor.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Installed payload root is missing.");
            }
            if ((ancestor.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException(
                    "Installed payload root uses a reparse point.");
            }
            ancestor = ancestor.Parent;
        }

        var parent = root;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            parent = Path.Combine(parent, parts[i]);
            if (!Directory.Exists(parent))
            {
                return null;
            }
            if ((File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException(
                    "Installed payload subdirectory uses a reparse point.");
            }
        }

        if (!File.Exists(candidate))
        {
            return null;
        }
        var attributes = File.GetAttributes(candidate);
        if ((attributes & FileAttributes.ReparsePoint) != 0 ||
            (attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException(
                "Installed payload target is not a regular file.");
        }

        await using var stream = new FileStream(
            candidate, FileMode.Open, FileAccess.Read, FileShare.Read,
            ReadBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var digest = await SHA256.HashDataAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
