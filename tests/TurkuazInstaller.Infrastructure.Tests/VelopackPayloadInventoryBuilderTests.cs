// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/VelopackPayloadInventoryBuilderTests.cs
// 📌 Amac: Full nupkg payload envanterinin determinism, archive trust ve path guvenlik testlerini yapar
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Lab ZIP fixture ile unsigned inventory, CMS imza baglama, tekrarlanabilirlik ve fail-closed reddi test eder
// Bagimli Oldugu Katman: Tool | Service | Domain

using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Manifests;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class VelopackPayloadInventoryBuilderTests
{
    private static readonly PackageId Package =
        PackageId.Parse("turkuazlabs.turkuazinstaller.e2e");
    private static readonly byte[] FileOne = Encoding.UTF8.GetBytes("fixture-v2");
    private static readonly byte[] FileTwo = Encoding.UTF8.GetBytes("fixture-library");

    [Fact]
    public async Task BuildUnsignedAsync_GeneratesDeterministicPayloadAndIgnoresNugetMetadata()
    {
        var path = BuildFixture(new[]
        {
            ("lib/app/sub/lib.dll", FileTwo),
            ("package/metadata.psmdcp", Encoding.UTF8.GetBytes("metadata")),
            ("lib/app/version.txt", FileOne),
            ("fixture.nuspec", Encoding.UTF8.GetBytes("nuspec"))
        });
        try
        {
            var release = Release(path);
            var builder = new VelopackPayloadInventoryBuilder();
            var first = await builder.BuildUnsignedAsync(
                path, release, CancellationToken.None);
            var second = await builder.BuildUnsignedAsync(
                path, release, CancellationToken.None);
            Assert.Equal(first, second);

            using var cert = CreateCertificate();
            var signature = Sign(first, cert);
            var inventory = await Reader(cert).ReadAsync(
                first, signature, release, CancellationToken.None);

            Assert.Equal(2, inventory.Files.Count);
            Assert.Equal("current/sub/lib.dll", inventory.Files[0].RelativePath);
            Assert.Equal(Sha256(FileTwo), inventory.Files[0].Digest.Sha256);
            Assert.Equal("current/version.txt", inventory.Files[1].RelativePath);
            Assert.Equal(Sha256(FileOne), inventory.Files[1].Digest.Sha256);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task BuildUnsignedAsync_RejectsArchiveChangedAfterDigestWasRecorded()
    {
        var path = BuildFixture(new[] { ("lib/app/version.txt", FileOne) });
        try
        {
            var release = Release(path);
            await using (var stream = new FileStream(
                path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                stream.Position = stream.Length - 1;
                var old = stream.ReadByte();
                stream.Position = stream.Length - 1;
                stream.WriteByte(old ^ 0x01);
            }

            await Assert.ThrowsAsync<InvalidDataException>(
                () => new VelopackPayloadInventoryBuilder().BuildUnsignedAsync(
                    path, release, CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("../escape.exe")]
    [InlineData("lib/app/../../escape.exe")]
    [InlineData("lib/app/current/../escape.exe")]
    [InlineData("lib/app/CON.txt")]
    [InlineData("lib/app/sub\\escape.exe")]
    [InlineData("lib/app/secret:stream")]
    [InlineData("lib/app/trailing.")]
    public async Task BuildUnsignedAsync_RejectsUnsafeZipPaths(string member)
    {
        var path = BuildFixture(new[]
        {
            (member, FileOne),
            ("lib/app/valid.txt", FileTwo)
        });
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => new VelopackPayloadInventoryBuilder().BuildUnsignedAsync(
                    path, Release(path), CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task BuildUnsignedAsync_RejectsAmbiguousCaseInsensitiveDuplicateFiles()
    {
        var path = BuildFixture(new[]
        {
            ("lib/app/Version.txt", FileOne),
            ("lib/app/version.txt", FileTwo)
        });
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => new VelopackPayloadInventoryBuilder().BuildUnsignedAsync(
                    path, Release(path), CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task BuildUnsignedAsync_RejectsUnknownLibTargetFrameworkRatherThanOmittingFiles()
    {
        var path = BuildFixture(new[]
        {
            ("lib/net8.0/other.dll", FileOne),
            ("lib/app/valid.txt", FileTwo)
        });
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => new VelopackPayloadInventoryBuilder().BuildUnsignedAsync(
                    path, Release(path), CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task BuildUnsignedAsync_RejectsSymlinkArchiveEntry()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "TurkuazInstaller-ZipInventory-" + Guid.NewGuid().ToString("N") +
            "-full.nupkg");
        try
        {
            using (var stream = File.Create(path))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var link = zip.CreateEntry("lib/app/link.txt");
                link.ExternalAttributes = unchecked((int)0xA0000000);
                using var writer = new StreamWriter(link.Open());
                writer.Write("../outside.txt");
            }
            await Assert.ThrowsAsync<InvalidDataException>(
                () => new VelopackPayloadInventoryBuilder().BuildUnsignedAsync(
                    path, Release(path), CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task BuildUnsignedAsync_RejectsEmptyPayloadInsteadOfSigningMetadataOnly()
    {
        var path = BuildFixture(new[]
        {
            ("fixture.nuspec", Encoding.UTF8.GetBytes("metadata"))
        });
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => new VelopackPayloadInventoryBuilder().BuildUnsignedAsync(
                    path, Release(path), CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task BuildUnsignedAsync_RejectsWrongTrustedReleaseArtifactSize()
    {
        var path = BuildFixture(new[] { ("lib/app/version.txt", FileOne) });
        try
        {
            var correct = Release(path);
            var wrong = new PackageRelease(
                correct.PackageId, correct.Version, correct.Channel,
                new ArtifactDescriptor(
                    correct.Artifact.Uri, correct.Artifact.Digest,
                    correct.Artifact.SizeBytes + 1));
            await Assert.ThrowsAsync<InvalidDataException>(
                () => new VelopackPayloadInventoryBuilder().BuildUnsignedAsync(
                    path, wrong, CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string BuildFixture(
        IEnumerable<(string Member, byte[] Bytes)> files)
    {
        var path = Path.Combine(Path.GetTempPath(),
            "TurkuazInstaller-ZipInventory-" + Guid.NewGuid().ToString("N") +
            "-full.nupkg");
        using (var stream = File.Create(path))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            foreach (var (name, bytes) in files)
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(bytes);
            }
        }
        return path;
    }

    private static PackageRelease Release(string path)
    {
        using var stream = File.OpenRead(path);
        var uri = new UriBuilder(Uri.UriSchemeFile, string.Empty)
        {
            Path = path
        }.Uri;
        return new PackageRelease(
            Package, SemanticVersion.Parse("2.0.0"), ReleaseChannel.Stable,
            new ArtifactDescriptor(
                uri, ArtifactDigest.ParseSha256(
                    Convert.ToHexString(SHA256.HashData(stream))),
                stream.Length));
    }

    private static string Sha256(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static SignedPayloadInventoryReader Reader(X509Certificate2 certificate)
        => new(new CmsManifestSignatureVerifier(
            new FakeManifestTrustPolicyRepository(
                new ManifestTrustPolicy(
                    certificate.Subject,
                    certificate.GetCertHashString(HashAlgorithmName.SHA256)
                        .ToLowerInvariant()))));

    private static X509Certificate2 CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=TurkuazInstaller Package Inventory LAB",
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
