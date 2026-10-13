// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/SignedPayloadInventoryReaderTests.cs
// 📌 Amac: Imzali installed-payload envanterinin pinned CMS, release identity ve path validation testlerini yapar
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Ephemeral LAB sertifikasi, tamper, yanlis digest, duplicate ve gercek disk SHA-256 karsilastirmasi
// Bagimli Oldugu Katman: Tool | Service | Domain

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Manifests;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;
using TurkuazInstaller.Infrastructure.Tools;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class SignedPayloadInventoryReaderTests
{
    private static readonly PackageId Package =
        PackageId.Parse("turkuazlabs.turkuazinstaller.e2e");
    private static readonly byte[] ArtifactBytes =
        Encoding.UTF8.GetBytes("LAB-only-full-nupkg");
    private static readonly byte[] InstalledVersionOne =
        Encoding.UTF8.GetBytes("1.0.0");

    [Fact]
    public async Task ReadAsync_VerifiesRealDetachedSignatureAndExactReleaseIdentity()
    {
        using var certificate = CreateCertificate();
        var release = Release();
        var json = Inventory();
        var result = await Reader(certificate).ReadAsync(
            json, Sign(json, certificate), release, CancellationToken.None);

        Assert.Equal(release.PackageId, result.PackageId);
        Assert.Equal(release.Version, result.Version);
        Assert.Equal(release.Channel, result.Channel);
        Assert.Equal(release.Artifact.Digest, result.ArtifactDigest);
        var file = Assert.Single(result.Files);
        Assert.Equal("current/version.txt", file.RelativePath);
        Assert.Equal(Digest(InstalledVersionOne), file.Digest);
    }

    [Fact]
    public async Task ReadAsync_RejectsChangedBytesAfterSigning()
    {
        using var certificate = CreateCertificate();
        var json = Inventory();
        var signature = Sign(json, certificate);
        var modified = Inventory(version: "2.0.1");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => Reader(certificate).ReadAsync(
                modified, signature, Release(), CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_RejectsWrongPinnedCertificate()
    {
        using var certificate = CreateCertificate();
        var json = Inventory();
        var invalidReader = new SignedPayloadInventoryReader(
            new CmsManifestSignatureVerifier(
                new FakeManifestTrustPolicyRepository(
                    new ManifestTrustPolicy(
                        certificate.Subject, new string('0', 64)))));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => invalidReader.ReadAsync(
                json, Sign(json, certificate), Release(), CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_RejectsMissingSignature()
    {
        using var certificate = CreateCertificate();
        await Assert.ThrowsAsync<InvalidDataException>(
            () => Reader(certificate).ReadAsync(
                Inventory(), ReadOnlyMemory<byte>.Empty,
                Release(), CancellationToken.None));
    }

    [Theory]
    [InlineData("2.0.1", "stable")]
    [InlineData("2.0.0", "beta")]
    public async Task ReadAsync_RejectsWrongVersionOrChannel(
        string version, string channel)
    {
        using var certificate = CreateCertificate();
        var bytes = Inventory(version: version, channel: channel);
        await Assert.ThrowsAsync<InvalidDataException>(
            () => Reader(certificate).ReadAsync(
                bytes, Sign(bytes, certificate), Release(), CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_RejectsSignedInventoryForDifferentPackage()
    {
        using var certificate = CreateCertificate();
        var bytes = Inventory(packageId: "other.package");
        await Assert.ThrowsAsync<InvalidDataException>(
            () => Reader(certificate).ReadAsync(
                bytes, Sign(bytes, certificate), Release(), CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_RejectsSignedInventoryForDifferentPackageArtifact()
    {
        using var certificate = CreateCertificate();
        var bytes = Inventory(artifactDigest: new string('0', 64));
        await Assert.ThrowsAsync<InvalidDataException>(
            () => Reader(certificate).ReadAsync(
                bytes, Sign(bytes, certificate), Release(), CancellationToken.None));
    }

    [Theory]
    [InlineData("../external.txt")]
    [InlineData("current/../external.txt")]
    [InlineData("current//version.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("C:/Windows/win.ini")]
    [InlineData("current\\version.txt")]
    [InlineData("CON.txt")]
    [InlineData("current/trailing.")]
    [InlineData("current/ADS:name")]
    public async Task ReadAsync_RejectsSignedInventoryWithUnsafePaths(string path)
    {
        using var certificate = CreateCertificate();
        var bytes = Inventory(paths: new[] { path });
        await Assert.ThrowsAsync<InvalidDataException>(
            () => Reader(certificate).ReadAsync(
                bytes, Sign(bytes, certificate), Release(), CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_RejectsCaseInsensitiveDuplicatePathsEvenOnLinux()
    {
        using var certificate = CreateCertificate();
        var bytes = Inventory(paths: new[]
        {
            "current/version.txt", "CURRENT/VERSION.TXT"
        });
        await Assert.ThrowsAsync<InvalidDataException>(
            () => Reader(certificate).ReadAsync(
                bytes, Sign(bytes, certificate), Release(), CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_RejectsDuplicateSignedJsonFields()
    {
        using var certificate = CreateCertificate();
        var original = Encoding.UTF8.GetString(Inventory());
        var duplicated = original.Replace(
            "\"schema_version\":1,",
            "\"schema_version\":1,\"schema_version\":1,",
            StringComparison.Ordinal);
        Assert.NotEqual(original, duplicated);
        var bytes = Encoding.UTF8.GetBytes(duplicated);
        await Assert.ThrowsAsync<InvalidDataException>(
            () => Reader(certificate).ReadAsync(
                bytes, Sign(bytes, certificate), Release(), CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_RejectsEmptySignedInventory()
    {
        using var certificate = CreateCertificate();
        var bytes = Inventory(paths: Array.Empty<string>());
        await Assert.ThrowsAsync<InvalidDataException>(
            () => Reader(certificate).ReadAsync(
                bytes, Sign(bytes, certificate), Release(), CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_RejectsOversizedContentBeforeVerification()
    {
        using var certificate = CreateCertificate();
        await Assert.ThrowsAsync<InvalidDataException>(
            () => Reader(certificate).ReadAsync(
                new byte[ManifestContentLimits.MaximumManifestBytes + 1],
                new byte[] { 1 }, Release(), CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_SignedReferenceCanBeComparedToDiskWithoutChangingFiles()
    {
        using var certificate = CreateCertificate();
        var inventoryBytes = Inventory();
        var inventory = await Reader(certificate).ReadAsync(
            inventoryBytes, Sign(inventoryBytes, certificate),
            Release(), CancellationToken.None);

        var root = Path.Combine(
            Path.GetTempPath(), "TurkuazInstaller-SignedInventory-LAB",
            Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "current"));
            var path = Path.Combine(root, "current", "version.txt");
            await File.WriteAllTextAsync(path, "2.0.0");
            var before = await File.ReadAllBytesAsync(path);
            var evidence = await new InstallerPayloadEvidenceService(
                new InstalledPayloadHashTool()).AssessAsync(
                inventory.PackageId, root,
                inventory.Files.Select(file => new InstallerExpectedPayloadFile(
                    file.RelativePath, file.Digest)).ToArray(),
                CancellationToken.None);

            var result = Assert.Single(evidence.Files);
            Assert.Equal(InstallerPayloadFileStatus.DigestMismatch, result.Status);
            Assert.Equal(before, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static SignedPayloadInventoryReader Reader(X509Certificate2 certificate)
        => new(new CmsManifestSignatureVerifier(
            new FakeManifestTrustPolicyRepository(
                new ManifestTrustPolicy(
                    certificate.Subject,
                    certificate.GetCertHashString(HashAlgorithmName.SHA256)
                        .ToLowerInvariant()))));

    private static PackageRelease Release()
        => new(Package, SemanticVersion.Parse("2.0.0"),
            ReleaseChannel.Stable,
            new ArtifactDescriptor(
                new Uri("https://example.invalid/fixture-full.nupkg"),
                Digest(ArtifactBytes), ArtifactBytes.Length));

    private static byte[] Inventory(
        string? packageId = null,
        string version = "2.0.0",
        string channel = "stable",
        string? artifactDigest = null,
        string[]? paths = null)
    {
        var files = (paths ?? new[] { "current/version.txt" })
            .Select(path => new
            {
                path,
                sha256 = Digest(InstalledVersionOne).Sha256
            })
            .ToArray();

        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema_version = 1,
            package_id = packageId ?? Package.Value,
            version,
            channel,
            artifact_sha256 = artifactDigest ?? Digest(ArtifactBytes).Sha256,
            files
        });
    }

    private static ArtifactDigest Digest(byte[] bytes)
        => ArtifactDigest.ParseSha256(
            Convert.ToHexString(SHA256.HashData(bytes)));

    private static X509Certificate2 CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=TurkuazInstaller Signed Payload LAB",
            rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature, critical: true));
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));
    }

    private static byte[] Sign(byte[] bytes, X509Certificate2 certificate)
    {
        var cms = new SignedCms(new ContentInfo(bytes), detached: true);
        cms.ComputeSignature(new CmsSigner(
            SubjectIdentifierType.IssuerAndSerialNumber, certificate)
        {
            IncludeOption = X509IncludeOption.EndCertOnly
        }, silent: true);
        return cms.Encode();
    }
}
