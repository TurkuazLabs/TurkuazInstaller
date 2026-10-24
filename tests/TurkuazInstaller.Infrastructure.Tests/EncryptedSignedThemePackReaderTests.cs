// Dosya Yolu: tests/TurkuazInstaller.Infrastructure.Tests/EncryptedSignedThemePackReaderTests.cs
// Amac: Dis anahtarli AES-GCM zarfini ve zorunlu ic CMS imzasini dogrulamak
// Modul - CSharp xUnit Tests
// Version: 0.1.0-qa
// Aciklama: Gercek GCM, CMS ve ephemeral fixture; hatali anahtar, tamper ve baska urun red testleri
// Bagimli Oldugu Katman: Security | Infrastructure | Trust

using System.Buffers.Binary;
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

public sealed class EncryptedSignedThemePackReaderTests
{
    private const string Product = "levelupgt.ragnar-classic-preview";
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+XGmYAAAAASUVORK5CYII=");

    [Fact]
    public async Task DecryptsAndChecksSeparatelySignedThemeWithoutWritingPlaintext()
    {
        using var cert = Certificate();
        var plaintext = BuildSignedTheme(cert);
        var secret = RandomNumberGenerator.GetBytes(32);
        var envelope = Encrypt(plaintext, secret);
        var provider = new EphemeralProvider((byte[])secret.Clone());
        var reader = Reader(cert, provider);

        using var stream = new MemoryStream(envelope);
        var result = await reader.ReadAsync(stream, PackageId.Parse(Product));

        Assert.Equal("qa-landscape", result.ThemeId);
        Assert.Equal(Png, result.ForScreen("welcome").ToArray());
        Assert.DoesNotContain("manifest.json", Encoding.Latin1.GetString(envelope));
        Assert.True(provider.Key!.All(value => value == 0));
    }

    [Fact]
    public async Task RejectsUnknownKeyAndNeverFallsBackToPlainTpk()
    {
        using var cert = Certificate();
        var encrypted = Encrypt(BuildSignedTheme(cert), RandomNumberGenerator.GetBytes(32));
        var provider = new EphemeralProvider(null);
        using var stream = new MemoryStream(encrypted);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            Reader(cert, provider).ReadAsync(stream, PackageId.Parse(Product)));
        Assert.Equal(1, provider.Calls);
    }

    [Theory]
    [InlineData("wrong-key")]
    [InlineData("ciphertext")]
    [InlineData("tag")]
    [InlineData("header-length")]
    [InlineData("header-id")]
    [InlineData("trailing-data")]
    public async Task FailsClosedOnCryptographicOrStructuralTamper(string change)
    {
        using var cert = Certificate();
        var plain = BuildSignedTheme(cert);
        var key = RandomNumberGenerator.GetBytes(32);
        var encrypted = Encrypt(plain, key);
        var wrong = (byte[])encrypted.Clone();
        var providerKey = (byte[])key.Clone();

        switch (change)
        {
            case "wrong-key": providerKey[0] ^= 1; break;
            case "ciphertext": wrong[wrong.Length / 2] ^= 1; break;
            case "tag": wrong[^1] ^= 1; break;
            case "header-length": wrong[9 + 7 + 12] ^= 1; break;
            case "header-id": wrong[9] = (byte)'X'; break;
            case "trailing-data": Array.Resize(ref wrong, wrong.Length + 1); break;
        }

        var provider = new EphemeralProvider(providerKey);
        using var stream = new MemoryStream(wrong);
        await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
            Reader(cert, provider).ReadAsync(stream, PackageId.Parse(Product)));
        if (provider.Calls != 0) Assert.True(provider.Key!.All(value => value == 0));
    }

    [Fact]
    public async Task CorrectDecryptionDoesNotBypassCmsPublisherPin()
    {
        using var original = Certificate();
        using var unrelated = Certificate();
        var key = RandomNumberGenerator.GetBytes(32);
        var encrypted = Encrypt(BuildSignedTheme(original), key);
        using var input = new MemoryStream(encrypted);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            Reader(unrelated, new EphemeralProvider((byte[])key.Clone()))
                .ReadAsync(input, PackageId.Parse(Product)));
    }

    [Fact]
    public async Task CorrectDecryptionDoesNotBypassProductBinding()
    {
        using var cert = Certificate();
        var key = RandomNumberGenerator.GetBytes(32);
        var encrypted = Encrypt(BuildSignedTheme(cert), key);
        using var input = new MemoryStream(encrypted);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            Reader(cert, new EphemeralProvider((byte[])key.Clone()))
                .ReadAsync(input, PackageId.Parse("another.product")));
    }

    [Fact]
    public async Task RealGcmIsInsufficientWhenInnerSignedPngHashWasChanged()
    {
        using var cert = Certificate();
        var signedPack = BuildSignedTheme(cert, modifyPng: true);
        var key = RandomNumberGenerator.GetBytes(32);
        var encrypted = Encrypt(signedPack, key);
        using var input = new MemoryStream(encrypted);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            Reader(cert, new EphemeralProvider((byte[])key.Clone()))
                .ReadAsync(input, PackageId.Parse(Product)));
    }

    private sealed class EphemeralProvider : IThemeDecryptionKeyProvider
    {
        public byte[]? Key { get; }
        public int Calls { get; private set; }

        public EphemeralProvider(byte[]? key) => Key = key;

        public ValueTask<byte[]?> ResolveKeyAsync(
            PackageId expectedProduct, string keyId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return ValueTask.FromResult<byte[]?>(keyId == "key-v1" &&
                expectedProduct.Value == Product ? Key : null);
        }
    }

    private static EncryptedSignedThemePackReader Reader(
        X509Certificate2 allowedSigner, IThemeDecryptionKeyProvider provider)
    {
        var pin = new ManifestTrustPolicy(
            allowedSigner.Subject,
            allowedSigner.GetCertHashString(HashAlgorithmName.SHA256).ToLowerInvariant());
        var verifier = new CmsManifestSignatureVerifier(
            new FakeManifestTrustPolicyRepository(pin));
        return new EncryptedSignedThemePackReader(
            new SignedThemePackReader(verifier), provider);
    }

    private static byte[] Encrypt(byte[] plaintext, byte[] key)
    {
        var id = "key-v1"u8.ToArray();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var header = new byte[9 + id.Length + nonce.Length + 4];
        "TURQTPK1"u8.CopyTo(header);
        header[8] = (byte)id.Length;
        id.CopyTo(header, 9);
        nonce.CopyTo(header, 9 + id.Length);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(header.Length - 4, 4),
            checked((uint)plaintext.Length));

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(key, 16))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag, header);
        }
        return header.Concat(ciphertext).Concat(tag).ToArray();
    }

    private static byte[] BuildSignedTheme(X509Certificate2 cert, bool modifyPng = false)
    {
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema_version = 1,
            format = "turkuaz.theme",
            package_id = Product,
            theme_id = "qa-landscape",
            theme_version = "0.1.0",
            assets = new[] {
                new {
                    id = "welcome-art",
                    sha256 = Convert.ToHexStringLower(SHA256.HashData(Png)),
                    size_bytes = Png.Length, width = 1, height = 1
                }
            },
            screens = new { welcome = "welcome-art" }
        });
        var signedCms = new SignedCms(new ContentInfo(manifest), detached: true);
        signedCms.ComputeSignature(new CmsSigner(
            SubjectIdentifierType.IssuerAndSerialNumber, cert)
        {
            IncludeOption = X509IncludeOption.EndCertOnly
        });

        using var bytes = new MemoryStream();
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "manifest.json", manifest);
            Add(zip, "manifest.p7s", signedCms.Encode());
            var art = (byte[])Png.Clone();
            if (modifyPng) art[^1] ^= 0x02;
            Add(zip, "assets/welcome-art.png", art);
        }
        return bytes.ToArray();
    }

    private static void Add(ZipArchive zip, string name, byte[] content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
        using var writer = entry.Open();
        writer.Write(content);
    }

    private static X509Certificate2 Certificate()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=Encrypted TPK CI Test Only", rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature, critical: true));
        return req.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-3), DateTimeOffset.UtcNow.AddDays(1));
    }
}
