// Dosya Yolu: src/TurkuazInstaller.Infrastructure/Branding/HttpsThemeDecryptionKeyProvider.cs
// Amac: Yetkili TLS servisi uzerinden paket bazli kisa omurlu AES anahtarini almak
// Modul - CSharp Tool/Adapter
// Version: 0.1.0-qa
// Aciklama: Token gelistirme/hosting ortamindan saglanir; URL/key/tokens EXE icine gomulmez
// Bagimli Oldugu Katman: Infrastructure | Network | Signing | Product Identity

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Infrastructure.Branding;

/// <summary>
/// Auth provider must acquire a short-lived, product-scoped session token from
/// an authenticated user flow. Do NOT use a literal bearer token, local file or
/// environment variable as an unattended production authentication substitute.
/// </summary>
public interface IThemeAccessTokenSource
{
    ValueTask<string?> AcquireTokenAsync(
        PackageId expectedProduct, CancellationToken cancellationToken);
}

/// <summary>
/// Client side of the optional remote theme-key contract. The SERVER (not
/// this client) must authenticate and authorize the current user, product and
/// keyId before releasing a 32-byte key. The endpoint is supplied by trusted
/// application composition, NEVER read from the encrypted theme envelope.
/// </summary>
public sealed class HttpsThemeDecryptionKeyProvider : IThemeDecryptionKeyProvider, IDisposable
{
    private const int MaxReplyBytes = 1024;
    private static readonly JsonSerializerOptions StrictJson =
        new() { PropertyNameCaseInsensitive = false, MaxDepth = 4 };
    private readonly Uri _endpoint;
    private readonly IThemeAccessTokenSource _tokens;
    private readonly HttpClient _http;

    public HttpsThemeDecryptionKeyProvider(
        Uri endpoint, IThemeAccessTokenSource tokens)
        : this(endpoint, tokens, new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = true
        })
    {
    }

    // A caller-supplied handler is intended for deterministic QA tests.
    // In production use the safe default handler constructor above.
    public HttpsThemeDecryptionKeyProvider(
        Uri endpoint, IThemeAccessTokenSource tokens,
        HttpMessageHandler transport)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(transport);

        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(endpoint.Host) ||
            !string.IsNullOrWhiteSpace(endpoint.UserInfo) ||
            !string.IsNullOrEmpty(endpoint.Query) ||
            !string.IsNullOrEmpty(endpoint.Fragment))
        {
            throw new ArgumentException(
                "A trusted absolute HTTPS key issuer URL without URL credentials or query is required.",
                nameof(endpoint));
        }

        _endpoint = endpoint;
        _tokens = tokens;
        _http = new HttpClient(transport, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public async ValueTask<byte[]?> ResolveKeyAsync(
        PackageId expectedPackageId, string keyId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expectedPackageId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsValidKeyId(keyId))
        {
            throw new InvalidDataException("Theme key identifier is invalid.");
        }

        var bearer = await _tokens.AcquireTokenAsync(
            expectedPackageId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(bearer))
        {
            return null;
        }
        if (bearer.Length > 8192 || bearer.Any(
            c => c <= 0x20 || c > 0x7e))
        {
            throw new InvalidDataException("Theme access token encoding is invalid.");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = JsonContent.Create(new
            {
                package_id = expectedPackageId.Value,
                key_id = keyId
            })
        };
        message.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", bearer);
        message.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };

        using var reply = await _http.SendAsync(
            message, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        // Reject redirects and all non-200 responses. No anonymous or cached
        // offline fallback; authentication/authorization failure is fail-closed.
        if (reply.StatusCode != HttpStatusCode.OK)
        {
            return null;
        }
        if (reply.Headers.CacheControl?.NoStore != true ||
            reply.Content.Headers.ContentType?.MediaType != "application/json" ||
            reply.Content.Headers.ContentLength > MaxReplyBytes)
        {
            throw new InvalidDataException("Theme issuer response security policy is invalid.");
        }

        await using var stream = await reply.Content.ReadAsStreamAsync(
            cancellationToken).ConfigureAwait(false);
        using var bytes = new MemoryStream();
        var buffer = new byte[256];
        int count;
        while ((count = await stream.ReadAsync(
            buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (bytes.Length + count > MaxReplyBytes)
            {
                throw new InvalidDataException("Theme issuer response exceeded its size limit.");
            }
            bytes.Write(buffer, 0, count);
        }

        try
        {
            using var json = JsonDocument.Parse(bytes.GetBuffer().AsMemory(0, (int)bytes.Length), new JsonDocumentOptions
            {
                MaxDepth = 4, CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                root.EnumerateObject().Count() != 3 ||
                root.GetProperty("package_id").GetString() != expectedPackageId.Value ||
                root.GetProperty("key_id").GetString() != keyId)
            {
                throw new InvalidDataException("Theme issuer reply identity is not authorized for this request.");
            }

            // Reject unknown or duplicate keys, including a self-reported
            // authorization override supplied by the key service.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var prop in root.EnumerateObject())
            {
                if (!seen.Add(prop.Name) ||
                    prop.Name is not ("package_id" or "key_id" or "key_base64"))
                {
                    throw new InvalidDataException("Theme issuer reply has unrecognized/duplicate fields.");
                }
            }

            var encoded = root.GetProperty("key_base64").GetString();
            if (encoded is null || encoded.Length != 44)
            {
                throw new InvalidDataException("Theme issuer did not return a 256-bit key.");
            }

            var key = Convert.FromBase64String(encoded);
            if (key.Length != 32)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
                throw new InvalidDataException("Theme issuer key length is invalid.");
            }
            // The caller owns this array and MUST zero it after decryption.
            return key;
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Theme issuer returned invalid JSON.", error);
        }
        catch (FormatException error)
        {
            throw new InvalidDataException("Theme issuer returned invalid key encoding.", error);
        }
        finally
        {
            Array.Clear(bytes.GetBuffer(), 0, (int)bytes.Length);
        }
    }

    private static bool IsValidKeyId(string? keyId)
    {
        return keyId is { Length: >= 1 and <= 64 } &&
               keyId.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_');
    }

    public void Dispose() => _http.Dispose();
}
