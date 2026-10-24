// Dosya Yolu: tests/TurkuazInstaller.Infrastructure.Tests/HttpsThemeDecryptionKeyProviderTests.cs
// Amac: HTTPS anahtar tesliminde urun/anahtar kapsam ve istemci red testleri
// Modul - CSharp xUnit Tests
// Version: 0.1.0-qa
// Aciklama: Dis istek yapmadan gercek HttpClient mesaj sinirlari
// Bagimli Oldugu Katman: Infrastructure | Security | Auth

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Infrastructure.Branding;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class HttpsThemeDecryptionKeyProviderTests
{
    private static readonly Uri Issuer = new("https://issuer.example.invalid/v1/theme-key");
    private const string Product = "levelupgt.ragnar-classic-preview";
    private const string KeyId = "key-v1";

    [Fact]
    public async Task ValidPinnedEndpointAndScopedTokenReturnsFreshKeyBytes()
    {
        var expected = RandomNumberGenerator.GetBytes(32);
        var calls = 0;
        using var provider = Provider(new StubHandler(async (request, ct) =>
        {
            calls++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(Issuer, request.RequestUri);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("ephemeral-access-token", request.Headers.Authorization?.Parameter);
            Assert.True(request.Headers.CacheControl?.NoStore);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal(Product, body.RootElement.GetProperty("package_id").GetString());
            Assert.Equal(KeyId, body.RootElement.GetProperty("key_id").GetString());
            return GoodReply(Product, KeyId, expected);
        }));
        var received = await provider.ResolveKeyAsync(PackageId.Parse(Product), KeyId,
            CancellationToken.None);
        Assert.NotNull(received);
        Assert.Equal(expected, received);
        Assert.NotSame(expected, received);
        Assert.Equal(1, calls);
        CryptographicOperations.ZeroMemory(received!);
    }

    [Theory]
    [InlineData("http://issuer.example.invalid/theme")]
    [InlineData("https://bad:token@issuer.example.invalid/key")]
    [InlineData("https://issuer.example.invalid/key?x=secret")]
    [InlineData("https://issuer.example.invalid/key#fragment")]
    public void RejectsUnsafeIssuerUrls(string endpoint)
    {
        Assert.Throws<ArgumentException>(() => new HttpsThemeDecryptionKeyProvider(
            new Uri(endpoint), new TokenSource("ephemeral-access-token")));
    }

    [Fact]
    public async Task NoSessionTokenDoesNotContactIssuer()
    {
        var calls = 0;
        using var provider = Provider(new StubHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }), token: null);
        Assert.Null(await provider.ResolveKeyAsync(
            PackageId.Parse(Product), KeyId, CancellationToken.None));
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task RejectsDeniedOrRedirectedKeyRequests(HttpStatusCode status)
    {
        using var provider = Provider(new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(status))));
        Assert.Null(await provider.ResolveKeyAsync(
            PackageId.Parse(Product), KeyId, CancellationToken.None));
    }

    [Fact]
    public async Task RejectsOtherProductAndOtherKeyInJsonBody()
    {
        foreach (var (product, keyId) in new[]
        {
            ("another.product", KeyId), (Product, "wrong-key")
        })
        {
            using var provider = Provider(new StubHandler((_, _) =>
                Task.FromResult(GoodReply(product, keyId,
                    RandomNumberGenerator.GetBytes(32)))));
            await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await provider.ResolveKeyAsync(PackageId.Parse(Product), KeyId,
                    CancellationToken.None));
        }
    }

    [Fact]
    public async Task RejectsMissingNoStore()
    {
        using var provider = Provider(new StubHandler((_, _) =>
        {
            var result = GoodReply(Product, KeyId, RandomNumberGenerator.GetBytes(32));
            result.Headers.CacheControl = null;
            return Task.FromResult(result);
        }));
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await provider.ResolveKeyAsync(PackageId.Parse(Product), KeyId,
                CancellationToken.None));
    }

    [Fact]
    public async Task RejectsHugeResponseEvenWithLyingContentLength()
    {
        using var provider = Provider(new StubHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(new string('x', 2000),
                    Encoding.UTF8, "application/json")
            };
            response.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
            response.Content.Headers.ContentLength = null;
            return Task.FromResult(response);
        }));
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await provider.ResolveKeyAsync(PackageId.Parse(Product), KeyId,
                CancellationToken.None));
    }

    [Fact]
    public async Task DuplicatePropertyOrUnknownPropertyIsRejected()
    {
        foreach (var json in new[]
        {
            "{\"package_id\":\"" + Product + "\",\"package_id\":\"" + Product +
            "\",\"key_id\":\"key-v1\",\"key_base64\":\"" +
            Convert.ToBase64String(new byte[32]) + "\"}",
            "{\"package_id\":\"" + Product + "\",\"key_id\":\"key-v1\"," +
            "\"key_base64\":\"" + Convert.ToBase64String(new byte[32]) +
            "\",\"publisher_override\":true}"
        })
        {
            using var provider = Provider(new StubHandler((_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                response.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
                return Task.FromResult(response);
            }));
            await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await provider.ResolveKeyAsync(PackageId.Parse(Product), KeyId,
                    CancellationToken.None));
        }
    }

    private static HttpResponseMessage GoodReply(
        string product, string keyId, byte[] key)
    {
        var json = JsonSerializer.Serialize(new
        {
            package_id = product,
            key_id = keyId,
            key_base64 = Convert.ToBase64String(key)
        });
        var reply = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        reply.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
        return reply;
    }

    private static HttpsThemeDecryptionKeyProvider Provider(
        HttpMessageHandler handler, string? token = "ephemeral-access-token") =>
        new(Issuer, new TokenSource(token), handler);

    private sealed class TokenSource(string? token) : IThemeAccessTokenSource
    {
        public ValueTask<string?> AcquireTokenAsync(
            PackageId expectedProduct, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(token);
        }
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
