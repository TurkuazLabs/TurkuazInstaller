// Dosya Yolu: src/TurkuazInstaller.Infrastructure/Branding/ThemeKeyIssueService.cs
// Amac: Server tarafinda tema anahtari vermeden once oturum kapsam ve hak kontrolu
// Modul - CSharp Service
// Version: 0.1.0-qa
// Aciklama: Yalniz trusted JWT middleware tarafindan dogrulanmis principal kabul edilmelidir
// Bagimli Oldugu Katman: Identity Middleware | Entitlement Store | Key Vault

using System.Security.Claims;
using System.Security.Cryptography;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Infrastructure.Branding;

/// <summary>
/// A server-side source of authorizations. Entitlements MUST come from
/// publisher-controlled state, never a claim submitted by the end user.
/// Revoked subscriptions, product access and key rotation are checked here.
/// </summary>
public interface IThemeKeyEntitlementStore
{
    Task<bool> IsAuthorizedAsync(
        string authenticatedSubject, PackageId packageId, string keyId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Server-side secure vault. Returned byte array must be an independently
/// owned, disposable copy, not a reference to a cached master key.
/// </summary>
public interface IThemeKeySecretStore
{
    ValueTask<byte[]?> RetrieveOwnedKeyAsync(
        PackageId packageId, string keyId, CancellationToken cancellationToken);
}

/// <summary>
/// Do not persist or log this result. HTTP host must serialize to the
/// documented JSON and clear Key before releasing the response buffer.
/// </summary>
public sealed record IssuedThemeKey(
    string PackageId, string KeyId, byte[] Key);

/// <summary>
/// SERVER-ONLY authorization logic. The surrounding HTTPS host MUST validate
/// a bearer JWT signature, audience, issuer, expiry and token type using
/// trusted middleware before calling this service. The service independently
/// checks the server-validated principal's auth flag, subject, required scope,
/// product-specific entitlement and the vault key. No client-supplied role,
/// publisher, principal or entitlement grant is accepted in a request.
/// </summary>
public sealed class ThemeKeyIssueService
{
    public const string RequiredScope = "turkuaz.theme.read";

    private readonly IThemeKeyEntitlementStore _entitlements;
    private readonly IThemeKeySecretStore _vault;

    public ThemeKeyIssueService(
        IThemeKeyEntitlementStore entitlements, IThemeKeySecretStore vault)
    {
        _entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
    }

    public async Task<IssuedThemeKey?> IssueAsync(
        ClaimsPrincipal validatedPrincipal,
        PackageId expectedProduct,
        string keyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validatedPrincipal);
        ArgumentNullException.ThrowIfNull(expectedProduct);
        cancellationToken.ThrowIfCancellationRequested();

        if (validatedPrincipal.Identity?.IsAuthenticated != true ||
            !IsSafeKeyId(keyId))
        {
            return null;
        }

        // No ambiguity across merged identities, duplicate JWT claims or
        // attacker-controlled names. The caller has already verified JWT
        // issuer/audience/signature/lifetime via authentication middleware.
        var subjects = validatedPrincipal.FindAll("sub")
            .Select(c => c.Value).ToArray();
        if (subjects.Length != 1 || string.IsNullOrWhiteSpace(subjects[0]) ||
            subjects[0].Length > 256)
        {
            return null;
        }

        // Standard OAuth scope can contain a whitespace-separated list.
        // Never use substring matching (e.g. turkuaz.theme.read.extra).
        var scopes = validatedPrincipal.FindAll("scope").SelectMany(
            c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (!scopes.Contains(RequiredScope, StringComparer.Ordinal))
        {
            return null;
        }

        if (!await _entitlements.IsAuthorizedAsync(
            subjects[0], expectedProduct, keyId, cancellationToken)
            .ConfigureAwait(false))
        {
            // Do not contact the vault for unentitled identities.
            return null;
        }

        var ownedKey = await _vault.RetrieveOwnedKeyAsync(
            expectedProduct, keyId, cancellationToken).ConfigureAwait(false);
        if (ownedKey is null)
        {
            return null;
        }

        if (ownedKey.Length != 32)
        {
            CryptographicOperations.ZeroMemory(ownedKey);
            throw new InvalidDataException(
                "Theme key vault returned an invalid AES-256 key length.");
        }

        // HTTP handler owns and must erase the returned 32-byte array.
        return new IssuedThemeKey(expectedProduct.Value, keyId, ownedKey);
    }

    private static bool IsSafeKeyId(string? keyId) =>
        keyId is { Length: >= 1 and <= 64 } &&
        keyId.All(c =>
            c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_');
}
