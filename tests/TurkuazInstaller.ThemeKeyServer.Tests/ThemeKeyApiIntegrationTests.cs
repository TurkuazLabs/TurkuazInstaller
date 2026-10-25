// Dosya Yolu: tests/TurkuazInstaller.ThemeKeyServer.Tests/ThemeKeyApiIntegrationTests.cs
// Amac: Dogrulanmis JWT, HTTPS ve entitlement/vault kontrollu HTTP testi
// Modul - CSharp xUnit WebApplicationFactory Tests
// Version: 0.1.0-qa
// Aciklama: Gecici RSA JWT testleridir; token/public issuer/anahtar gercek degildir
// Bagimli Oldugu Katman: ThemeKeyServer | Authentication | Authorization

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Infrastructure.Branding;
using Xunit;

namespace TurkuazInstaller.ThemeKeyServer.Tests;

public sealed class ThemeKeyApiIntegrationTests
{
    private const string Product = "levelupgt.ragnar-classic-preview";
    private const string KeyId = "key-v1";
    private const string Issuer = "https://identity.test.invalid";
    private const string Audience = "turkuaz.theme.test";

    [Fact]
    public async Task AuthenticatedEntitledUserReceivesOneScopedNonCacheableKey()
    {
        using var app = new AppFixture(entitled: true);
        using var client = app.CreateHttpsClient();
        using var req = ValidRequest(app.Token());

        using var reply = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
        Assert.True(reply.Headers.CacheControl?.NoStore);
        Assert.Equal("application/json", reply.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(await reply.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(3, root.EnumerateObject().Count());
        Assert.Equal(Product, root.GetProperty("package_id").GetString());
        Assert.Equal(KeyId, root.GetProperty("key_id").GetString());
        Assert.Equal(32, Convert.FromBase64String(
            root.GetProperty("key_base64").GetString()!).Length);
        Assert.Equal(1, app.Entitlements.Calls);
        Assert.Equal(1, app.Vault.Calls);
        Assert.All(app.Vault.LastIssuedKey!, b => Assert.Equal((byte)0, b));
    }

    [Fact]
    public async Task MissingJwtIs401AndNeverQueriesEntitlementStore()
    {
        using var app = new AppFixture(entitled: true);
        using var client = app.CreateHttpsClient();
        using var request = ValidRequest(token: null);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(0, app.Entitlements.Calls);
        Assert.Equal(0, app.Vault.Calls);
    }

    [Theory]
    [InlineData("bad-audience")]
    [InlineData("expired")]
    [InlineData("no-scope")]
    [InlineData("wrong-signature")]
    [InlineData("wrong-token-type")]
    public async Task WrongJwtOrScopeCannotIssueKey(string failure)
    {
        using var app = new AppFixture(entitled: true);
        using var client = app.CreateHttpsClient();
        var token = failure switch
        {
            "bad-audience" => app.Token(audience: "wrong.audience"),
            "expired" => app.Token(expired: true),
            "no-scope" => app.Token(scope: "other.permission"),
            "wrong-signature" => app.Token(badSignature: true),
            "wrong-token-type" => app.Token(tokenType: "JWT"),
            _ => throw new InvalidOperationException()
        };
        using var request = ValidRequest(token);
        using var response = await client.SendAsync(request);
        Assert.Equal(failure == "no-scope"
            ? HttpStatusCode.Forbidden
            : HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, app.Entitlements.Calls);
        Assert.Equal(0, app.Vault.Calls);
    }

    [Fact]
    public async Task RevokedEntitlementBlocksKeyVault()
    {
        using var app = new AppFixture(entitled: false);
        using var client = app.CreateHttpsClient();
        using var req = ValidRequest(app.Token());
        using var reply = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, reply.StatusCode);
        Assert.Equal(1, app.Entitlements.Calls);
        Assert.Equal(0, app.Vault.Calls);
    }

    [Theory]
    [InlineData("{\"package_id\":\"levelupgt.ragnar-classic-preview\",\"key_id\":\"key-v1\",\"key_id\":\"key-v1\"}")]
    [InlineData("{\"package_id\":\"../invalid\",\"key_id\":\"key-v1\"}")]
    [InlineData("{\"package_id\":\"levelupgt.ragnar-classic-preview\",\"key_id\":\"key-v1\",\"grant\":true}")]
    [InlineData("{\"package_id\":\"levelupgt.ragnar-classic-preview\",}")]
    public async Task MalformedOrDuplicateJsonIsRejectedBeforeVault(string body)
    {
        using var app = new AppFixture(entitled: true);
        using var client = app.CreateHttpsClient();
        using var req = ValidRequest(app.Token());
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var reply = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, reply.StatusCode);
        Assert.Equal(0, app.Vault.Calls);
    }

    [Fact]
    public async Task OversizedBodyCannotReachVault()
    {
        using var app = new AppFixture(entitled: true);
        using var client = app.CreateHttpsClient();
        using var req = ValidRequest(app.Token());
        req.Content = new StringContent(new string('x', 2048),
            Encoding.UTF8, "application/json");
        using var reply = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, reply.StatusCode);
        Assert.Equal(0, app.Vault.Calls);
    }

    [Fact]
    public async Task PlainHttpIsDeniedEvenWithValidBearer()
    {
        using var app = new AppFixture(entitled: true);
        using var client = app.CreateHttpsClient();
        using var req = ValidRequest(app.Token());
        req.RequestUri = new Uri("http://localhost/v1/theme-key");
        using var reply = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, reply.StatusCode);
        Assert.Equal(0, app.Vault.Calls);
    }

    private static HttpRequestMessage ValidRequest(string? token) 
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/v1/theme-key")
        {
            Content = JsonContent.Create(new { package_id = Product, key_id = KeyId })
        };
        if (token is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    private sealed class AppFixture : WebApplicationFactory<Program>
    {
        private readonly RSA _rsa = RSA.Create(2048);
        private readonly bool _entitled;
        public SpyEntitlementStore Entitlements { get; }
        public SpyKeyVault Vault { get; }

        public AppFixture(bool entitled)
        {
            _entitled = entitled;
            Entitlements = new SpyEntitlementStore(entitled);
            Vault = new SpyKeyVault();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ThemeKeyIssuer:Authority"] = Issuer,
                    ["ThemeKeyIssuer:Audience"] = Audience
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IThemeKeyEntitlementStore>();
                services.RemoveAll<IThemeKeySecretStore>();
                services.AddSingleton<IThemeKeyEntitlementStore>(Entitlements);
                services.AddSingleton<IThemeKeySecretStore>(Vault);

                services.PostConfigure<JwtBearerOptions>(
                    JwtBearerDefaults.AuthenticationScheme, options =>
                    {
                        var rsaKey = new RsaSecurityKey(_rsa) { KeyId = "ephemeral-test-key" };
                        var provider = new OpenIdConnectConfiguration { Issuer = Issuer };
                        provider.SigningKeys.Add(rsaKey);
                        options.Authority = null; // isolated in-memory test, no OIDC discovery HTTP
                        options.MetadataAddress = null;
                        options.Configuration = provider;
                        options.ConfigurationManager =
                            new StaticConfigurationManager<OpenIdConnectConfiguration>(provider);
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = Issuer,
                            ValidateAudience = true,
                            ValidAudience = Audience,
                            ValidateLifetime = true,
                            RequireExpirationTime = true,
                            ValidateIssuerSigningKey = true,
                            RequireSignedTokens = true,
                            ClockSkew = TimeSpan.FromSeconds(30),
                            ValidTypes = ["at+jwt"],
                            IssuerSigningKey = rsaKey
                        };
                    });
            });
        }

        public HttpClient CreateHttpsClient() => CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        public string Token(
            string scope = "turkuaz.theme.read", string audience = Audience,
            bool expired = false, bool badSignature = false,
            string tokenType = "at+jwt")
        {
            var time = DateTime.UtcNow;
            using var foreign = badSignature ? RSA.Create(2048) : null;
            var rsa = foreign ?? _rsa;
            var creds = new SigningCredentials(
                new RsaSecurityKey(rsa) { KeyId = "ephemeral-test-key" },
                SecurityAlgorithms.RsaSha256);
            var header = new JwtHeader(creds) { ["typ"] = tokenType };
            var payload = new JwtPayload(
                issuer: Issuer, audience: audience,
                claims: [new Claim("sub", "qa-user-01"), new Claim("scope", scope)],
                notBefore: expired ? time.AddMinutes(-20) : time.AddSeconds(-10),
                expires: expired ? time.AddMinutes(-10) : time.AddMinutes(5));
            return new JwtSecurityTokenHandler().WriteToken(
                new JwtSecurityToken(header, payload));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) _rsa.Dispose();
        }
    }

    private sealed class SpyEntitlementStore(bool isGranted) : IThemeKeyEntitlementStore
    {
        public int Calls { get; private set; }
        public Task<bool> IsAuthorizedAsync(
            string authenticatedSubject, PackageId packageId,
            string keyId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(isGranted &&
                authenticatedSubject == "qa-user-01" &&
                packageId.Value == Product && keyId == KeyId);
        }
    }

    private sealed class SpyKeyVault : IThemeKeySecretStore
    {
        public int Calls { get; private set; }
        public byte[]? LastIssuedKey { get; private set; }

        public ValueTask<byte[]?> RetrieveOwnedKeyAsync(
            PackageId packageId, string keyId, CancellationToken cancellationToken)
        {
            Calls++;
            LastIssuedKey = RandomNumberGenerator.GetBytes(32);
            return ValueTask.FromResult<byte[]?>(LastIssuedKey);
        }
    }
}
