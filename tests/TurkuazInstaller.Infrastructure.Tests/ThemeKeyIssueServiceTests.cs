// Dosya Yolu: tests/TurkuazInstaller.Infrastructure.Tests/ThemeKeyIssueServiceTests.cs
// Amac: Server-side dogrulanmis principal ve entitlement temelli key issuance red kurallari
// Modul - CSharp xUnit Tests
// Version: 0.1.0-qa
// Aciklama: Anonim, scope yanlisi, duplicate subject, iptal edilen hak, vault kusurlari
// Bagimli Oldugu Katman: Identity Middleware Contract | Entitlement | Vault

using System.Security.Claims;
using System.Security.Cryptography;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Infrastructure.Branding;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class ThemeKeyIssueServiceTests
{
    private const string Product = "levelupgt.ragnar-classic-preview";
    private const string OtherProduct = "unrelated.product";
    private const string KeyId = "key-v1";
    private const string Subject = "user-fixture-01";

    [Fact]
    public async Task ValidatedScopedEntitledUserReceivesOnlyRequestedKey()
    {
        var vault = new CountingVault((byte[])RandomNumberGenerator.GetBytes(32).Clone());
        var entitlements = new CountingEntitlements(true);
        var service = new ThemeKeyIssueService(entitlements, vault);
        var result = await service.IssueAsync(Identity(
            authenticated: true, scope: "openid turkuaz.theme.read"),
            PackageId.Parse(Product), KeyId);

        Assert.NotNull(result);
        Assert.Equal(Product, result.PackageId);
        Assert.Equal(KeyId, result.KeyId);
        Assert.Equal(32, result.Key.Length);
        Assert.Equal(1, entitlements.Calls);
        Assert.Equal(1, vault.Calls);
        Assert.Equal(Subject, entitlements.LastSubject);
        CryptographicOperations.ZeroMemory(result.Key);
    }

    [Theory]
    [InlineData(false, "turkuaz.theme.read", KeyId)]
    [InlineData(true, "", KeyId)]
    [InlineData(true, "turkuaz.theme.read.extra", KeyId)]
    [InlineData(true, "anythingelse", KeyId)]
    [InlineData(true, "turkuaz.theme.read", "../bad")]
    [InlineData(true, "turkuaz.theme.read", "UPPERCASE")]
    public async Task InvalidAuthScopeOrKeyDoesNotContactEntitlementsOrVault(
        bool authenticated, string scope, string keyId)
    {
        var entitlements = new CountingEntitlements(true);
        var vault = new CountingVault(RandomNumberGenerator.GetBytes(32));
        var service = new ThemeKeyIssueService(entitlements, vault);
        var result = await service.IssueAsync(
            Identity(authenticated, scope), PackageId.Parse(Product), keyId);
        Assert.Null(result);
        Assert.Equal(0, entitlements.Calls);
        Assert.Equal(0, vault.Calls);
    }

    [Fact]
    public async Task DuplicateOrMissingSubjectIsRejectedWithoutDatabaseCalls()
    {
        var entitlements = new CountingEntitlements(true);
        var vault = new CountingVault(RandomNumberGenerator.GetBytes(32));
        var service = new ThemeKeyIssueService(entitlements, vault);

        var duplicate = Identity(true, "turkuaz.theme.read");
        ((ClaimsIdentity)duplicate.Identity!).AddClaim(new Claim("sub", "another-person"));
        Assert.Null(await service.IssueAsync(duplicate, PackageId.Parse(Product), KeyId));

        var missing = Identity(true, "turkuaz.theme.read", includeSubject: false);
        Assert.Null(await service.IssueAsync(missing, PackageId.Parse(Product), KeyId));
        Assert.Equal(0, entitlements.Calls);
        Assert.Equal(0, vault.Calls);
    }

    [Fact]
    public async Task EntitlementRevocationRejectsBeforeSecretVaultCall()
    {
        var entitlements = new CountingEntitlements(false);
        var vault = new CountingVault(RandomNumberGenerator.GetBytes(32));
        var service = new ThemeKeyIssueService(entitlements, vault);
        var result = await service.IssueAsync(
            Identity(true, "turkuaz.theme.read"),
            PackageId.Parse(Product), KeyId);
        Assert.Null(result);
        Assert.Equal(1, entitlements.Calls);
        Assert.Equal(0, vault.Calls);
    }

    [Fact]
    public async Task EntitlementMustBeForExactProductAndKeyId()
    {
        var entitlements = new CountingEntitlements(true, expectedProduct: Product,
            expectedKeyId: KeyId);
        var vault = new CountingVault(RandomNumberGenerator.GetBytes(32));
        var service = new ThemeKeyIssueService(entitlements, vault);
        var wrongProduct = await service.IssueAsync(
            Identity(true, "turkuaz.theme.read"),
            PackageId.Parse(OtherProduct), KeyId);
        var wrongKey = await service.IssueAsync(
            Identity(true, "turkuaz.theme.read"),
            PackageId.Parse(Product), "other-key");
        Assert.Null(wrongProduct);
        Assert.Null(wrongKey);
        Assert.Equal(0, vault.Calls);
    }

    [Fact]
    public async Task MissingVaultKeyReturnsNoSecretAndBadLengthIsZeroed()
    {
        var entitlements = new CountingEntitlements(true);
        var missingVault = new CountingVault(null);
        Assert.Null(await new ThemeKeyIssueService(entitlements, missingVault).IssueAsync(
            Identity(true, "turkuaz.theme.read"),
            PackageId.Parse(Product), KeyId));

        var invalid = new byte[16];
        RandomNumberGenerator.Fill(invalid);
        var invalidVault = new CountingVault(invalid);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ThemeKeyIssueService(entitlements, invalidVault).IssueAsync(
                Identity(true, "turkuaz.theme.read"),
                PackageId.Parse(Product), KeyId));
        Assert.All(invalid, v => Assert.Equal((byte)0, v));
    }

    [Fact]
    public async Task MissingAuthenticationIsNotImplicitlyGrantedByCallerClaims()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("sub", Subject), new Claim("scope", "turkuaz.theme.read"),
            new Claim("role", "owner"), new Claim("entitled", "true")
        }); // No authentication type => NOT authenticated.
        var principal = new ClaimsPrincipal(identity);
        var entitlement = new CountingEntitlements(true);
        var vault = new CountingVault(RandomNumberGenerator.GetBytes(32));
        var result = await new ThemeKeyIssueService(entitlement, vault).IssueAsync(
            principal, PackageId.Parse(Product), KeyId);
        Assert.Null(result);
        Assert.Equal(0, entitlement.Calls);
        Assert.Equal(0, vault.Calls);
    }

    private static ClaimsPrincipal Identity(
        bool authenticated, string scope, bool includeSubject = true)
    {
        var claims = new List<Claim> { new("scope", scope) };
        if (includeSubject) claims.Add(new Claim("sub", Subject));
        return new ClaimsPrincipal(new ClaimsIdentity(
            claims, authenticated ? "validated-jwt-fixture" : null));
    }

    private sealed class CountingEntitlements(
        bool grant, string expectedProduct = Product, string expectedKeyId = KeyId)
        : IThemeKeyEntitlementStore
    {
        public int Calls { get; private set; }
        public string? LastSubject { get; private set; }
        public Task<bool> IsAuthorizedAsync(
            string authenticatedSubject, PackageId packageId, string keyId,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastSubject = authenticatedSubject;
            return Task.FromResult(grant &&
                authenticatedSubject == Subject &&
                packageId.Value == expectedProduct && keyId == expectedKeyId);
        }
    }

    private sealed class CountingVault(byte[]? key) : IThemeKeySecretStore
    {
        public int Calls { get; private set; }
        public ValueTask<byte[]?> RetrieveOwnedKeyAsync(
            PackageId packageId, string keyId, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(key);
        }
    }
}
