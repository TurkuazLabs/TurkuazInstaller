// Dosya Yolu: tests/TurkuazInstaller.Infrastructure.Tests/EncryptedSignedThemePackReaderTests.cs
// Amac: Dis anahtarli AES-GCM zarfini ve zorunlu ic CMS imzasini dogrulamak
// Modul - CSharp xUnit Tests
// Version: 0.5.0-qa
// Aciklama: Gercek GCM, CMS ve ephemeral fixture; hatali anahtar, tamper ve baska urun red testleri
// Bagimli Oldugu Katman: Security | Infrastructure | Trust

using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
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
    public void SourceFactoryDoesNotGuessTransportOrPathFromAnUntrustedFile()
    {
        using var cert = Certificate();
        var signed = TrustedSignedReader(cert);
        var normal = VerifiedThemePackSourceFactory.Create(
            ThemePackTransportMode.SignedTpk, signed);
        Assert.Equal("theme.tpk", normal.FileName);
        Assert.Same(signed, normal.Reader);

        var encrypted = VerifiedThemePackSourceFactory.Create(
            ThemePackTransportMode.EncryptedTpe, signed);
        Assert.Equal("theme.tpe", encrypted.FileName);
        Assert.IsType<EncryptedSignedThemePackReader>(encrypted.Reader);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VerifiedThemePackSourceFactory.Create(
                (ThemePackTransportMode)999, signed));
    }

    [Fact]
    public async Task EncryptedModeWithoutKeyProviderNeverFallsBackToSignedZip()
    {
        using var cert = Certificate();
        var signedPack = BuildSignedTheme(cert);
        var secret = RandomNumberGenerator.GetBytes(32);
        var protectedPack = EncryptedSignedThemePackWriter.Encrypt(
            signedPack, secret, "key-v1");
        var source = VerifiedThemePackSourceFactory.Create(
            ThemePackTransportMode.EncryptedTpe, TrustedSignedReader(cert));

        // An encrypted distribution must refuse even if a properly signed
        // plaintext .tpk happens to sit next to the executable.
        using var encrypted = new MemoryStream(protectedPack);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            source.Reader.ReadAsync(encrypted, PackageId.Parse(Product)));

        using var plain = new MemoryStream(signedPack);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            source.Reader.ReadAsync(plain, PackageId.Parse(Product)));
    }

    [Fact]
    public async Task SelectedEncryptedSourceAcceptsAuthorizedKeyAndCms()
    {
        using var cert = Certificate();
        var key = RandomNumberGenerator.GetBytes(32);
        var tpe = EncryptedSignedThemePackWriter.Encrypt(
            BuildSignedTheme(cert), key, "key-v1");

        var source = VerifiedThemePackSourceFactory.Create(
            ThemePackTransportMode.EncryptedTpe,
            TrustedSignedReader(cert), new EphemeralProvider((byte[])key.Clone()));
        Assert.Equal("theme.tpe", source.FileName);

        using var stream = new MemoryStream(tpe);
        var verified = await source.Reader.ReadAsync(
            stream, PackageId.Parse(Product));
        Assert.Equal(Png, verified.ForScreen("welcome").ToArray());
    }

    [Fact]
    public async Task CompiledExactReleasePinAcceptsOnlyApprovedSignedTheme()
    {
        using var cert = Certificate();
        var pack = BuildSignedTheme(cert);
        var trusted = TrustedSignedReader(cert);
        var allowed = VerifiedThemePackSourceFactory.Create(
            ThemePackTransportMode.SignedTpk, trusted,
            releasePin: PinnedThemeRelease.Create("qa-landscape", "0.1.0"));

        using var valid = new MemoryStream(pack);
        var verified = await allowed.Reader.ReadAsync(
            valid, PackageId.Parse(Product));
        Assert.Equal("0.1.0", verified.Version);

        var oldRelease = VerifiedThemePackSourceFactory.Create(
            ThemePackTransportMode.SignedTpk, trusted,
            releasePin: PinnedThemeRelease.Create("qa-landscape", "0.2.0"));
        using var outdated = new MemoryStream(pack);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            oldRelease.Reader.ReadAsync(outdated, PackageId.Parse(Product)));

        var replacedTheme = VerifiedThemePackSourceFactory.Create(
            ThemePackTransportMode.SignedTpk, trusted,
            releasePin: PinnedThemeRelease.Create("other-theme", "0.1.0"));
        using var changed = new MemoryStream(pack);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            replacedTheme.Reader.ReadAsync(changed, PackageId.Parse(Product)));
    }

    [Fact]
    public async Task ExactReleasePinAlsoGuardsEncryptedThemeAfterValidGcmAndCms()
    {
        using var cert = Certificate();
        var key = RandomNumberGenerator.GetBytes(32);
        var encrypted = EncryptedSignedThemePackWriter.Encrypt(
            BuildSignedTheme(cert), key, "key-v1");
        var source = VerifiedThemePackSourceFactory.Create(
            ThemePackTransportMode.EncryptedTpe,
            TrustedSignedReader(cert), new EphemeralProvider((byte[])key.Clone()),
            PinnedThemeRelease.Create("qa-landscape", "0.2.0"));

        using var input = new MemoryStream(encrypted);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            source.Reader.ReadAsync(input, PackageId.Parse(Product)));
    }

    [Theory]
    [InlineData("../bad-theme", "0.1.0")]
    [InlineData("invalid theme", "0.1.0")]
    [InlineData("approved", "v0.1.0")]
    public void InvalidReleasePinCannotBeCreated(string id, string version)
    {
        Assert.ThrowsAny<Exception>(() => PinnedThemeRelease.Create(id, version));
    }

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
    public async Task TrustedOfflineWriterRoundTripsWithRuntimeReader()
    {
        using var cert = Certificate();
        var key = RandomNumberGenerator.GetBytes(32);
        var signedPack = BuildSignedTheme(cert);

        var a = EncryptedSignedThemePackWriter.Encrypt(signedPack, key, "key-v1");
        var b = EncryptedSignedThemePackWriter.Encrypt(signedPack, key, "key-v1");
        Assert.NotEqual(a, b); // Randomized nonce per encryption.
        using var stream = new MemoryStream(a);
        var verified = await Reader(cert, new EphemeralProvider((byte[])key.Clone()))
            .ReadAsync(stream, PackageId.Parse(Product));
        Assert.Equal(Png, verified.ForScreen("welcome").ToArray());
    }

    [Fact]
    public void OfflineWriterRejectsUnsafeKeyIdentifiersAndShortKeys()
    {
        var data = new byte[] { 1, 2, 3 };
        var key = RandomNumberGenerator.GetBytes(32);
        Assert.Throws<ArgumentException>(() =>
            EncryptedSignedThemePackWriter.Encrypt(data, key, "../attack"));
        Assert.Throws<ArgumentException>(() =>
            EncryptedSignedThemePackWriter.Encrypt(data, key, "SPACE KEY"));
        Assert.Throws<ArgumentException>(() =>
            EncryptedSignedThemePackWriter.Encrypt(data, new byte[16], "key-v1"));
    }

    [Fact]
    public async Task FullAuthorizedHttpsDeliveryUnlocksOnlyProperlySignedEncryptedTheme()
    {
        using var publisher = Certificate();
        var signedPack = BuildSignedTheme(publisher);
        var secret = RandomNumberGenerator.GetBytes(32);
        var encrypted = EncryptedSignedThemePackWriter.Encrypt(
            signedPack, secret, "key-v1");

        using var issuer = new HttpsThemeDecryptionKeyProvider(
            new Uri("https://issuer.example.invalid/api/v1/theme-key"),
            new TestThemeSession(),
            new FakeKeyTransport(secret));
        using var file = new MemoryStream(encrypted);

        var verified = await Reader(publisher, issuer).ReadAsync(
            file, PackageId.Parse(Product));
        Assert.Equal("qa-landscape", verified.ThemeId);
        Assert.Equal(Png, verified.ForScreen("welcome").ToArray());
    }

    private sealed class TestThemeSession : IThemeAccessTokenSource
    {
        public ValueTask<string?> AcquireTokenAsync(
            PackageId product, CancellationToken cancellationToken) =>
            ValueTask.FromResult<string?>(
                product.Value == Product ? "ci-test-ephemeral" : null);
    }

    private sealed class FakeKeyTransport(byte[] secret) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("ci-test-ephemeral", request.Headers.Authorization?.Parameter);
            using var body = JsonDocument.Parse(
                await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert.Equal(Product, body.RootElement.GetProperty("package_id").GetString());
            Assert.Equal("key-v1", body.RootElement.GetProperty("key_id").GetString());

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        package_id = Product,
                        key_id = "key-v1",
                        key_base64 = Convert.ToBase64String(secret)
                    }),
                    Encoding.UTF8, "application/json")
            };
            response.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
            return response;
        }
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

    private static SignedThemePackReader TrustedSignedReader(
        X509Certificate2 allowedSigner)
    {
        var pin = new ManifestTrustPolicy(
            allowedSigner.Subject,
            allowedSigner.GetCertHashString(HashAlgorithmName.SHA256).ToLowerInvariant());
        var verifier = new CmsManifestSignatureVerifier(
            new FakeManifestTrustPolicyRepository(pin));
        return new SignedThemePackReader(verifier);
    }

    private static EncryptedSignedThemePackReader Reader(
        X509Certificate2 allowedSigner, IThemeDecryptionKeyProvider provider)
    {
        return new EncryptedSignedThemePackReader(
            TrustedSignedReader(allowedSigner), provider);
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
