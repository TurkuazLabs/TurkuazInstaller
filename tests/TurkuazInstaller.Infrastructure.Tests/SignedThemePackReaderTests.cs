// Dosya Yolu: tests/TurkuazInstaller.Infrastructure.Tests/SignedThemePackReaderTests.cs
// Amac: Paket imzasi, signer pini, PNG hash ve archive siniri icin gercek CMS testleri
// Modul - CSharp xUnit Tests
// Version: 0.1.0-qa
// Aciklama: Gecici test sertifikasi ile valid/tampered/cross-product/zip injection senaryolari
// Bagimli Oldugu Katman: Infrastructure | Security | Manifest Trust

using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using TurkuazInstaller.Domain.Manifests;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Infrastructure.Branding;
using TurkuazInstaller.Infrastructure.Manifests;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class SignedThemePackReaderTests
{
    private const string Package = "levelupgt.ragnar-classic-preview";
    // 1x1 fixture image. Never copied into a real product distribution.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+XGmYAAAAASUVORK5CYII=");

    [Fact]
    public async Task ReadsRealCmsSignedThemeAndAllScreenMappings()
    {
        using var cert = TestCertificate();
        var pack = BuildPack(cert);
        using var input = new MemoryStream(pack);
        var theme = await TrustedReader(cert).ReadAsync(input, PackageId.Parse(Package));
        Assert.Equal("turkuaz-fantasy", theme.ThemeId);
        Assert.Equal("0.1.0", theme.Version);
        Assert.Equal(3, theme.Assets.Count);
        Assert.Equal(5, theme.Screens.Count);
        Assert.Equal(Png, theme.ForScreen("welcome").ToArray());
        Assert.Equal(Png, theme.ForScreen("installing").ToArray());
    }

    [Fact]
    public async Task RejectsChangedPngEvenWhenSignedManifestStillMatchesOldImage()
    {
        using var cert = TestCertificate();
        var pack = BuildPack(cert, corruptAsset: true);
        using var input = new MemoryStream(pack);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            TrustedReader(cert).ReadAsync(input, PackageId.Parse(Package)));
    }

    [Fact]
    public async Task RejectsChangedManifestSignedByDifferentBytes()
    {
        using var cert = TestCertificate();
        var pack = BuildPack(cert, forgedManifest: true);
        using var input = new MemoryStream(pack);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            TrustedReader(cert).ReadAsync(input, PackageId.Parse(Package)));
    }

    [Fact]
    public async Task RejectsWrongPublisherCertificatePin()
    {
        using var signer = TestCertificate();
        using var other = TestCertificate();
        var pack = BuildPack(signer);
        using var input = new MemoryStream(pack);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            TrustedReader(other).ReadAsync(input, PackageId.Parse(Package)));
    }

    [Fact]
    public async Task RejectsCrossProductSubstitutionEvenWithGoodSignature()
    {
        using var cert = TestCertificate();
        var pack = BuildPack(cert);
        using var input = new MemoryStream(pack);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            TrustedReader(cert).ReadAsync(input, PackageId.Parse("another.product")));
    }

    [Theory]
    [InlineData("assets/../../payload.png")]
    [InlineData("assets/EXTRA.png")]
    [InlineData("rogue.exe")]
    public async Task RejectsExtraOrTraversalEntries(string extraName)
    {
        using var cert = TestCertificate();
        var pack = BuildPack(cert, extraEntry: extraName);
        using var input = new MemoryStream(pack);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            TrustedReader(cert).ReadAsync(input, PackageId.Parse(Package)));
    }

    [Fact]
    public async Task RejectsDuplicateEntryEvenIfContentsMatch()
    {
        using var cert = TestCertificate();
        var pack = BuildPack(cert, duplicateAsset: true);
        using var input = new MemoryStream(pack);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            TrustedReader(cert).ReadAsync(input, PackageId.Parse(Package)));
    }

    [Fact]
    public async Task RejectsMissingSignature()
    {
        using var cert = TestCertificate();
        var pack = BuildPack(cert, omitSignature: true);
        using var input = new MemoryStream(pack);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            TrustedReader(cert).ReadAsync(input, PackageId.Parse(Package)));
    }

    [Fact]
    public async Task RejectsUnsignedNewPropertyEvenIfSignedAsPartOfManifest()
    {
        using var cert = TestCertificate();
        var pack = BuildPack(cert, extraManifestProperty: true);
        using var input = new MemoryStream(pack);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            TrustedReader(cert).ReadAsync(input, PackageId.Parse(Package)));
    }

    private static SignedThemePackReader TrustedReader(X509Certificate2 certificate)
    {
        var policy = new ManifestTrustPolicy(
            certificate.Subject,
            certificate.GetCertHashString(HashAlgorithmName.SHA256).ToLowerInvariant());
        return new SignedThemePackReader(
            new CmsManifestSignatureVerifier(
                new FakeManifestTrustPolicyRepository(policy)));
    }

    private static X509Certificate2 TestCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=TurkuazInstaller Ephemeral Test Signing Only",
            key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature, critical: true));
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow.AddDays(2));
    }

    private static byte[] BuildPack(
        X509Certificate2 certificate,
        bool corruptAsset = false,
        bool forgedManifest = false,
        string? extraEntry = null,
        bool duplicateAsset = false,
        bool omitSignature = false,
        bool extraManifestProperty = false)
    {
        var assetPaths = new[] { "concept-c", "sunrise", "twilight" };
        var digest = Convert.ToHexStringLower(SHA256.HashData(Png));
        var manifestObject = new
        {
            schema_version = 1,
            format = "turkuaz.theme",
            package_id = Package,
            theme_id = "turkuaz-fantasy",
            theme_version = "0.1.0",
            assets = assetPaths.Select(id => new
            {
                id,
                sha256 = digest,
                size_bytes = Png.Length,
                width = 1,
                height = 1
            }).ToArray(),
            screens = new
            {
                welcome = "concept-c",
                options = "sunrise",
                installing = "twilight",
                complete = "sunrise",
                failed = "twilight"
            }
        };
        var originalManifest = JsonSerializer.SerializeToUtf8Bytes(manifestObject);
        var signature = Sign(originalManifest, certificate);
        var writtenManifest = originalManifest;
        if (forgedManifest)
        {
            writtenManifest = Encoding.UTF8.GetBytes(
                Encoding.UTF8.GetString(originalManifest)
                    .Replace("turkuaz-fantasy", "forged-theme", StringComparison.Ordinal));
        }
        if (extraManifestProperty)
        {
            writtenManifest = JsonSerializer.SerializeToUtf8Bytes(
                new Dictionary<string, object>
                {
                    ["schema_version"] = 1,
                    ["format"] = "turkuaz.theme",
                    ["package_id"] = Package,
                    ["theme_id"] = "turkuaz-fantasy",
                    ["theme_version"] = "0.1.0",
                    ["assets"] = manifestObject.assets,
                    ["screens"] = manifestObject.screens,
                    ["unsafe_override"] = true
                });
            signature = Sign(writtenManifest, certificate);
        }

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "manifest.json", writtenManifest);
            if (!omitSignature) Add(zip, "manifest.p7s", signature);
            foreach (var id in assetPaths)
            {
                var bytes = (byte[])Png.Clone();
                if (corruptAsset && id == "twilight") bytes[bytes.Length - 1] ^= 0x10;
                Add(zip, "assets/" + id + ".png", bytes);
            }
            if (duplicateAsset) Add(zip, "assets/concept-c.png", Png);
            if (extraEntry is not null) Add(zip, extraEntry, Png);
        }
        return buffer.ToArray();
    }

    private static void Add(ZipArchive zip, string name, byte[] data)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
        using var destination = entry.Open();
        destination.Write(data);
    }

    private static byte[] Sign(byte[] data, X509Certificate2 certificate)
    {
        var cms = new SignedCms(new ContentInfo(data), detached: true);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate)
        {
            IncludeOption = X509IncludeOption.EndCertOnly,
            DigestAlgorithm = new System.Security.Cryptography.Oid(
                "2.16.840.1.101.3.4.2.1")
        };
        cms.ComputeSignature(signer, silent: true);
        return cms.Encode();
    }
}
