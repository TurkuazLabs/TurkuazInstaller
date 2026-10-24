// Dosya Yolu: src/TurkuazInstaller.Infrastructure/Branding/SignedThemePackReader.cs
// Amac: Dis tema paketini CMS signature ve SHA-256 ile fail-closed dogrulamak
// Modul - CSharp Tool | Theme Pack
// Version: 0.2.0-qa
// Aciklama: ZIP tabanli .tpk formatinda sadece PNG; gizlilik/sifreleme iddiasi yok
// Bagimli Oldugu Katman: Infrastructure | Manifest Trust | Domain

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Infrastructure.Branding;

/// <summary>
/// Verified, bounded, in-memory PNG resources. This is NOT an encryption or DRM boundary.
/// Only pass these bytes to a renderer after the entire package has been verified.
/// </summary>
public sealed class VerifiedThemePack
{
    internal VerifiedThemePack(
        string themeId,
        string version,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> assets,
        IReadOnlyDictionary<string, string> screens)
    {
        ThemeId = themeId;
        Version = version;
        Assets = assets;
        Screens = screens;
    }

    public string ThemeId { get; }
    public string Version { get; }
    public IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Assets { get; }
    public IReadOnlyDictionary<string, string> Screens { get; }

    public ReadOnlyMemory<byte> ForScreen(string screen)
    {
        if (!Screens.TryGetValue(screen, out var assetId) ||
            !Assets.TryGetValue(assetId, out var content))
        {
            throw new KeyNotFoundException("The requested screen is not in the verified theme.");
        }

        return content;
    }
}

/// <summary>
/// Theme Pack v1: ZIP entries manifest.json, manifest.p7s, assets/{asset-id}.png.
/// Exact raw manifest bytes are detached CMS-signed; trust policy comes from the
/// installer, NEVER from a certificate/key provided by the theme package.
/// </summary>
public sealed class SignedThemePackReader : IVerifiedThemePackReader
{
    public const int MaxArchiveBytes = 80 * 1024 * 1024;
    public const int MaxAssetBytes = 16 * 1024 * 1024;
    private const int MaxManifestBytes = 32 * 1024;
    private const int MaxSignatureBytes = 16 * 1024;
    private const int MaxAssets = 8;

    private static readonly Regex AssetIdPattern =
        new(@"\A[a-z][a-z0-9-]{1,47}\z",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DigestPattern =
        new(@"\A[a-f0-9]{64}\z",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ScreenPattern =
        new(@"\A[a-z][a-z0-9-]{1,47}\z",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IManifestSignatureVerifier _signatureVerifier;

    public SignedThemePackReader(IManifestSignatureVerifier signatureVerifier)
    {
        _signatureVerifier = signatureVerifier ??
            throw new ArgumentNullException(nameof(signatureVerifier));
    }

    public async Task<VerifiedThemePack> ReadAsync(
        Stream source,
        PackageId expectedPackageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(expectedPackageId);
        cancellationToken.ThrowIfCancellationRequested();

        // Never decompress unbounded network input. Local staging can open a
        // seekable FileStream whose Length is verified before calling here.
        if (!source.CanRead || !source.CanSeek ||
            source.Length is <= 0 or > MaxArchiveBytes)
        {
            throw new InvalidDataException("Theme pack must be a bounded seekable archive.");
        }

        source.Position = 0;
        using var zip = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        if (zip.Entries.Count is < 3 or > MaxAssets + 2)
        {
            throw new InvalidDataException("Theme pack entry count is not permitted.");
        }

        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsSafeEntryName(entry.FullName) ||
                !entries.TryAdd(entry.FullName, entry) ||
                entry.Length < 1 || entry.Length > MaxAssetBytes ||
                entry.CompressedLength < 0 ||
                (entry.ExternalAttributes >> 16 & 0xF000) == 0xA000)
            {
                throw new InvalidDataException("Unsafe, duplicate, symbolic-link or oversized archive entry.");
            }
        }

        if (!entries.TryGetValue("manifest.json", out var manifestEntry) ||
            !entries.TryGetValue("manifest.p7s", out var signatureEntry))
        {
            throw new InvalidDataException("Theme pack must include manifest and detached signature.");
        }

        var manifestBytes = await ReadBoundedAsync(
            manifestEntry, MaxManifestBytes, cancellationToken);
        var signatureBytes = await ReadBoundedAsync(
            signatureEntry, MaxSignatureBytes, cancellationToken);

        // The repository-backed verifier validates CMS and the external
        // package-scoped publisher+certificate SHA-256 trust policy.
        var check = await _signatureVerifier.VerifyAsync(
            manifestBytes, signatureBytes, expectedPackageId, cancellationToken);
        if (!check.IsValid)
        {
            throw new InvalidDataException("Theme pack manifest signature or publisher pin is invalid.");
        }

        // No asset or UI metadata is trusted until verification succeeds.
        using var document = JsonDocument.Parse(manifestBytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 8
        });

        var root = document.RootElement;
        RequireFields(root, "schema_version", "format", "package_id", "theme_id",
            "theme_version", "assets", "screens");
        if (root.GetProperty("schema_version").GetInt32() != 1 ||
            root.GetProperty("format").GetString() != "turkuaz.theme" ||
            root.GetProperty("package_id").GetString() != expectedPackageId.Value)
        {
            throw new InvalidDataException("Signed theme schema, format or package identity mismatch.");
        }

        var themeId = RequiredId(root, "theme_id");
        var version = root.GetProperty("theme_version").GetString();
        if (version is null || SemanticVersion.Parse(version).ToString() != version)
        {
            throw new InvalidDataException("Signed theme version must be canonical SemVer.");
        }

        var assetSpecs = root.GetProperty("assets");
        if (assetSpecs.ValueKind != JsonValueKind.Array ||
            assetSpecs.GetArrayLength() is < 1 or > MaxAssets)
        {
            throw new InvalidDataException("Signed theme asset count is not permitted.");
        }

        var specById = new Dictionary<string, AssetSpec>(StringComparer.Ordinal);
        foreach (var asset in assetSpecs.EnumerateArray())
        {
            RequireFields(asset, "id", "sha256", "size_bytes", "width", "height");
            var id = RequiredId(asset, "id");
            var digest = asset.GetProperty("sha256").GetString();
            var length = asset.GetProperty("size_bytes").GetInt64();
            var width = asset.GetProperty("width").GetInt32();
            var height = asset.GetProperty("height").GetInt32();
            if (digest is null || !DigestPattern.IsMatch(digest) ||
                length is < 32 or > MaxAssetBytes ||
                width is < 1 or > 8192 || height is < 1 or > 8192 ||
                (long)width * height > 16_777_216 ||
                !specById.TryAdd(id, new AssetSpec(digest, length, width, height)))
            {
                throw new InvalidDataException("Signed theme asset metadata invalid or duplicated.");
            }
        }

        var screensElement = root.GetProperty("screens");
        if (screensElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Signed theme screen map must be an object.");
        }

        var screens = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in screensElement.EnumerateObject())
        {
            var name = property.Name;
            var value = property.Value.GetString();
            if (!ScreenPattern.IsMatch(name) || value is null ||
                !specById.ContainsKey(value) || !screens.TryAdd(name, value) ||
                screens.Count > 12)
            {
                throw new InvalidDataException("Signed theme screen mapping invalid or duplicated.");
            }
        }

        if (screens.Count < 1)
        {
            throw new InvalidDataException("Signed theme must map at least one screen.");
        }

        // No undeclared files, directory entries, path traversal or hidden extra
        // content: the ZIP inventory must equal the exact signed manifest set.
        var expectedEntries = new HashSet<string>(StringComparer.Ordinal)
        {
            "manifest.json", "manifest.p7s"
        };
        foreach (var id in specById.Keys)
        {
            expectedEntries.Add("assets/" + id + ".png");
        }
        if (!expectedEntries.SetEquals(entries.Keys))
        {
            throw new InvalidDataException("Archive inventory differs from signed theme asset list.");
        }

        var verifiedAssets = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
        foreach (var pair in specById)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = entries["assets/" + pair.Key + ".png"];
            var spec = pair.Value;
            if (entry.Length != spec.Length)
            {
                throw new InvalidDataException("Signed theme asset length differs from ZIP inventory.");
            }

            var bytes = await ReadBoundedAsync(entry, MaxAssetBytes, cancellationToken);
            var actualHash = SHA256.HashData(bytes);
            var expectedHash = Convert.FromHexString(spec.Digest);
            if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash) ||
                !HasPngDimensions(bytes, spec.Width, spec.Height))
            {
                throw new InvalidDataException("Signed theme asset integrity or PNG dimensions failed.");
            }

            verifiedAssets.Add(pair.Key, new ReadOnlyMemory<byte>(bytes));
        }

        return new VerifiedThemePack(
            themeId, version,
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, ReadOnlyMemory<byte>>(verifiedAssets),
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(screens));
    }

    private static string RequiredId(JsonElement value, string name)
    {
        var id = value.GetProperty(name).GetString();
        if (id is null || !AssetIdPattern.IsMatch(id))
        {
            throw new InvalidDataException("Theme identifier is invalid.");
        }
        return id;
    }

    private static bool IsSafeEntryName(string path)
    {
        if (path is "manifest.json" or "manifest.p7s") return true;
        if (!path.StartsWith("assets/", StringComparison.Ordinal) ||
            !path.EndsWith(".png", StringComparison.Ordinal))
        {
            return false;
        }
        return AssetIdPattern.IsMatch(path["assets/".Length..^".png".Length]);
    }

    private static void RequireFields(JsonElement value, params string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Signed theme document must be an object.");
        }
        var known = new HashSet<string>(fields, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!known.Contains(property.Name) || !seen.Add(property.Name))
            {
                throw new InvalidDataException("Unknown or duplicate signed theme property.");
            }
        }
        if (seen.Count != known.Count)
        {
            throw new InvalidDataException("Required signed theme properties are missing.");
        }
    }

    private static bool HasPngDimensions(byte[] content, int expectedWidth, int expectedHeight)
    {
        ReadOnlySpan<byte> magic = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        if (content.Length < 33 ||
            !content.AsSpan(0, 8).SequenceEqual(magic) ||
            !content.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            return false;
        }
        var width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(content.AsSpan(16, 4));
        var height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(content.AsSpan(20, 4));
        return width == expectedWidth && height == expectedHeight;
    }

    private static async Task<byte[]> ReadBoundedAsync(
        ZipArchiveEntry entry, int maxBytes, CancellationToken cancellationToken)
    {
        if (entry.Length < 1 || entry.Length > maxBytes)
        {
            throw new InvalidDataException("Archive entry is outside its size limit.");
        }

        await using var source = entry.Open();
        using var destination = new MemoryStream();
        var block = new byte[8192];
        int count;
        while ((count = await source.ReadAsync(block.AsMemory(), cancellationToken)) > 0)
        {
            if (destination.Length + count > maxBytes)
            {
                throw new InvalidDataException("Decompressed archive entry exceeds size limit.");
            }
            destination.Write(block, 0, count);
        }
        if (destination.Length != entry.Length)
        {
            throw new InvalidDataException("Incomplete theme archive entry.");
        }
        return destination.ToArray();
    }

    private sealed record AssetSpec(string Digest, long Length, int Width, int Height);
}
