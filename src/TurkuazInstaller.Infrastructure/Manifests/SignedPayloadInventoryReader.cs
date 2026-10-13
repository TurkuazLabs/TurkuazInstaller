// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Manifests/SignedPayloadInventoryReader.cs
// 📌 Amac: CMS imzali dosya SHA-256 envanterini mevcut trusted release kimligine baglar
// 📌 Modul - Tool CSharp
// Version: 1.0.2
// Aciklama: Detached signature, paket kimligi, surum, kanal, artifact digest ve dosya yollarini fail-closed dogrular
// Bagimli Oldugu Katman: Tool | Service | Domain

using System.Text.Json;
using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Infrastructure.Manifests;

public sealed record SignedPayloadInventoryFile(
    string RelativePath,
    ArtifactDigest Digest);

public sealed class SignedPayloadInventory
{
    internal SignedPayloadInventory(
        PackageId packageId,
        SemanticVersion version,
        ReleaseChannel channel,
        ArtifactDigest artifactDigest,
        IReadOnlyList<SignedPayloadInventoryFile> files)
    {
        PackageId = packageId;
        Version = version;
        Channel = channel;
        ArtifactDigest = artifactDigest;
        Files = files;
    }

    public PackageId PackageId { get; }
    public SemanticVersion Version { get; }
    public ReleaseChannel Channel { get; }
    public ArtifactDigest ArtifactDigest { get; }
    public IReadOnlyList<SignedPayloadInventoryFile> Files { get; }

    // This is signed baseline evidence, NOT installed disk integrity proof,
    // an exhaustive inventory of a Velopack package, or repair authority.
}

public sealed class SignedPayloadInventoryReader
{
    private const int SupportedSchemaVersion = 1;
    private const int MaximumFileCount = 4096;
    private const int MaximumRelativePathLength = 1024;
    private readonly IManifestSignatureVerifier _verifier;

    public SignedPayloadInventoryReader(IManifestSignatureVerifier verifier)
    {
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
    }

    public async Task<SignedPayloadInventory> ReadAsync(
        ReadOnlyMemory<byte> inventoryBytes,
        ReadOnlyMemory<byte> detachedSignature,
        PackageRelease expectedRelease,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expectedRelease);
        cancellationToken.ThrowIfCancellationRequested();

        if (inventoryBytes.IsEmpty ||
            inventoryBytes.Length > ManifestContentLimits.MaximumManifestBytes ||
            detachedSignature.IsEmpty ||
            detachedSignature.Length > ManifestContentLimits.MaximumDetachedSignatureBytes)
        {
            throw new InvalidDataException(
                "Signed payload inventory or detached signature is missing or exceeds limits.");
        }

        // Never parse untrusted bytes before the cryptographic trust gate.
        // The caller must separately establish that expectedRelease itself
        // came from the existing trusted signed-release workflow.
        var verification = await _verifier.VerifyAsync(
            inventoryBytes, detachedSignature,
            expectedRelease.PackageId, cancellationToken).ConfigureAwait(false);
        if (!verification.IsValid)
        {
            throw new InvalidDataException(
                "Signed payload inventory verification failed: " + verification.Message);
        }

        try
        {
            using var json = JsonDocument.Parse(
                inventoryBytes, new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Disallow,
                    AllowTrailingCommas = false,
                    MaxDepth = 8
                });
            var root = json.RootElement;
            RequireProperties(root,
                "schema_version", "package_id", "version", "channel",
                "artifact_sha256", "files");

            if (Required(root, "schema_version").GetInt32() != SupportedSchemaVersion)
            {
                throw new InvalidDataException(
                    "Signed payload inventory schema version is unsupported.");
            }

            var packageId = PackageId.Parse(RequiredString(root, "package_id"));
            var version = SemanticVersion.Parse(RequiredString(root, "version"));
            var channel = RequiredString(root, "channel");
            var artifactDigest = ArtifactDigest.ParseSha256(
                RequiredString(root, "artifact_sha256"));

            if (packageId != expectedRelease.PackageId ||
                !version.Equals(expectedRelease.Version) ||
                !string.Equals(
                    channel, expectedRelease.Channel.ToString().ToLowerInvariant(),
                    StringComparison.Ordinal) ||
                artifactDigest != expectedRelease.Artifact.Digest)
            {
                throw new InvalidDataException(
                    "Signed payload inventory does not match the trusted release identity.");
            }

            var fileArray = Required(root, "files");
            if (fileArray.ValueKind != JsonValueKind.Array ||
                fileArray.GetArrayLength() == 0 ||
                fileArray.GetArrayLength() > MaximumFileCount)
            {
                throw new InvalidDataException(
                    "Signed payload inventory file count is invalid.");
            }

            // Case-insensitive duplicate rejection on every platform keeps
            // a portable inventory unambiguous on Windows file systems.
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = new List<SignedPayloadInventoryFile>(fileArray.GetArrayLength());
            foreach (var file in fileArray.EnumerateArray())
            {
                RequireProperties(file, "path", "sha256");
                var path = RequiredString(file, "path");
                ValidatePath(path);
                if (!paths.Add(path))
                {
                    throw new InvalidDataException(
                        "Signed payload inventory includes duplicate file paths.");
                }
                files.Add(new SignedPayloadInventoryFile(
                    path, ArtifactDigest.ParseSha256(RequiredString(file, "sha256"))));
            }

            return new SignedPayloadInventory(
                packageId, version, expectedRelease.Channel,
                artifactDigest, files.AsReadOnly());
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Signed payload inventory JSON is malformed.", exception);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException(
                "Signed payload inventory typed field is malformed.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException(
                "Signed payload inventory JSON field has the wrong type.", exception);
        }
    }

    private static JsonElement Required(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            throw new InvalidDataException(
                "Signed payload inventory is missing field: " + name);
        }
        return value;
    }

    private static string RequiredString(JsonElement element, string name)
    {
        var value = Required(element, name);
        if (value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException(
                "Signed payload inventory field must be a nonempty string: " + name);
        }
        return value.GetString()!;
    }

    private static void RequireProperties(
        JsonElement element, params string[] allowedNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "Signed payload inventory expected a JSON object.");
        }

        var allowed = new HashSet<string>(allowedNames, StringComparer.Ordinal);
        var observed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name) ||
                !observed.Add(property.Name))
            {
                throw new InvalidDataException(
                    "Signed payload inventory contains unexpected or duplicate JSON fields.");
            }
        }
        if (observed.Count != allowed.Count)
        {
            throw new InvalidDataException(
                "Signed payload inventory required JSON fields are missing.");
        }
    }

    internal static void ValidatePath(string path)
    {
        if (path.Length > MaximumRelativePathLength ||
            path.StartsWith("/", StringComparison.Ordinal) ||
            path.Contains('\\') ||
            path.Contains(':') ||
            path.Any(ch => ch < 0x20 || ch is '<' or '>' or '"' or '|' or '?' or '*'))
        {
            throw new InvalidDataException(
                "Signed payload inventory path is not portable or relative.");
        }

        foreach (var part in path.Split('/', StringSplitOptions.None))
        {
            if (string.IsNullOrWhiteSpace(part) ||
                part is "." or ".." ||
                part.EndsWith('.') || part.EndsWith(' ') ||
                IsWindowsDeviceName(part))
            {
                throw new InvalidDataException(
                    "Signed payload inventory path contains an unsafe segment.");
            }
        }
    }

    private static bool IsWindowsDeviceName(string segment)
    {
        var stem = segment.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" ||
            (stem.Length == 4 &&
             ((stem.StartsWith("COM", StringComparison.Ordinal) ||
               stem.StartsWith("LPT", StringComparison.Ordinal)) &&
              stem[3] is >= '1' and <= '9'));
    }
}
