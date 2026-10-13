// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Operations/InstallerPayloadEvidenceService.cs
// 📌 Amac: Kurulu dosyalarin gercek SHA-256 olcumunu verilen referanslarla salt-okunur karsilastirir
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Eksik, uyumsuz ve eslesen dosyalari ayirir; rapor onarim veya kilit kaldirma izni vermez
// Bagimli Oldugu Katman: Service | Domain | Tool

using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Application.Operations;

public sealed class InstallerPayloadEvidenceService
{
    private readonly IInstalledPayloadHashTool _hashTool;

    public InstallerPayloadEvidenceService(IInstalledPayloadHashTool hashTool)
    {
        _hashTool = hashTool ?? throw new ArgumentNullException(nameof(hashTool));
    }

    public async Task<InstallerPayloadEvidence> AssessAsync(
        PackageId packageId,
        string installRoot,
        IReadOnlyList<InstallerExpectedPayloadFile> expectedFiles,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(installRoot);
        ArgumentNullException.ThrowIfNull(expectedFiles);
        cancellationToken.ThrowIfCancellationRequested();

        if (expectedFiles.Count == 0)
        {
            throw new ArgumentException(
                "Payload comparison requires at least one expected file.",
                nameof(expectedFiles));
        }

        // Reject malformed input before reading any file. These expectations
        // are caller-supplied; this service cannot authenticate their source.
        var comparison = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var knownPaths = new HashSet<string>(comparison);
        foreach (var file in expectedFiles)
        {
            ArgumentNullException.ThrowIfNull(file);
            ArgumentNullException.ThrowIfNull(file.ExpectedDigest);
            var normalized = NormalizeRelativePath(file.RelativePath);
            if (!knownPaths.Add(normalized))
            {
                throw new ArgumentException(
                    "Duplicate payload evidence path.", nameof(expectedFiles));
            }
        }

        var report = new List<InstallerPayloadFileEvidence>(expectedFiles.Count);
        var root = Path.GetFullPath(installRoot);
        foreach (var expected in expectedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = NormalizeRelativePath(expected.RelativePath);
            var actual = await _hashTool.ComputeSha256Async(
                    root, relativePath, cancellationToken)
                .ConfigureAwait(false);
            var status = actual is null
                ? InstallerPayloadFileStatus.Missing
                : string.Equals(
                    actual, expected.ExpectedDigest.Sha256,
                    StringComparison.OrdinalIgnoreCase)
                    ? InstallerPayloadFileStatus.MatchesSuppliedDigest
                    : InstallerPayloadFileStatus.DigestMismatch;

            report.Add(new InstallerPayloadFileEvidence(
                relativePath,
                expected.ExpectedDigest.Sha256,
                actual,
                status));
        }

        return new InstallerPayloadEvidence(
            packageId, root, report.AsReadOnly());
    }

    private static string NormalizeRelativePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (Path.IsPathRooted(path) ||
            path.Contains(':') ||
            path.Contains('\0'))
        {
            throw new ArgumentException(
                "Payload evidence path must be relative.");
        }

        var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.None);
        if (parts.Any(part =>
                string.IsNullOrWhiteSpace(part) ||
                part is "." or ".." ||
                part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new ArgumentException(
                "Payload evidence path has invalid segments.");
        }

        return string.Join('/', parts);
    }
}
