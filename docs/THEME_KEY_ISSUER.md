<!--
Dosya Yolu: docs/THEME_KEY_ISSUER.md
Amac: TPE dis anahtar tedariki icin sunucu/istemci guvenlik kontratini tanimlamak
Modul - Markdown
Version: 0.4.0-qa
Aciklama: OAuth benzeri guvenilir session, scope, TLS ve anahtar sunucu sorumluluklari
Bagimli Oldugu Katman: Security | Infrastructure | API
-->

# Theme Key Issuer v1 — private service contract (not deployed)

**Status: JWT HTTP host source and isolated tests only. NO production key issuer
has been deployed and NO identity-provider session token is configured.**
The general TurkuazInstaller WinUI currently consumes signed ZIP-readable
`theme.tpk` only; `theme.tpe` must not be advertised as available in the UI.

## Responsibilities

`HttpsThemeDecryptionKeyProvider` is a narrowly bounded HTTP client.
It does **not** authenticate end users or decide whether a caller can
open a licensed/encrypted theme. The remote server MUST make those
decisions independently, based on an authenticated server-side identity
and the requested `package_id` and `key_id`.

The caller must inject a trusted absolute HTTPS URI **compiled from a
reviewed product configuration** and an `IThemeAccessTokenSource` that
obtains a short-lived scoped token through a real authenticated login
flow (for example, an audited authorization code + PKCE implementation).
User-writable `ui-profile.yml`, `.tpe` header, HTTP redirect, arbitrary
download data or unsigned settings must NEVER choose the key server.
No master key, signing private key, password or reusable bearer token
belongs in the EXE or this repository.

### Request

```http
POST /v1/theme-key HTTP/1.1
Host: [trusted publisher domain]
Authorization: Bearer <short-lived authorization token>
Content-Type: application/json
Cache-Control: no-store

{"package_id":"levelupgt.ragnar-classic-preview","key_id":"key-v1"}
```

The client requires HTTPS, disallows credentials/query strings/fragments
in the endpoint URL, disables automatic redirects and cookies, and limits
response bodies to 1,024 bytes, including when Content-Length is missing.

### Authorized response

```http
HTTP/1.1 200 OK
Content-Type: application/json
Cache-Control: no-store

{
  "package_id": "levelupgt.ragnar-classic-preview",
  "key_id": "key-v1",
  "key_base64": "<base64 encoding of exactly 32 bytes>"
}
```

The examples here contain placeholders, not a usable key/token.
Client rejects extra/duplicate response fields, different package/key,
incorrect encoding, missing `no-store`, oversized bodies and non-200
responses. Non-200 responses never fall back to plaintext theme files.
The returned key byte buffer belongs to `EncryptedSignedThemePackReader`
and is zeroed after use; outer GCM integrity and independently pinned
inner CMS signature must BOTH pass.

### Server-side authorization core (source + isolated tests)

The new `ThemeKeyIssueService` provides an independent authorization decision
**inside a trusted API host**. Its input must be the `ClaimsPrincipal`
produced by the host's cryptographically validated JWT bearer middleware,
**not** an arbitrary object or `role` sent by the client. The host must
validate a configured issuer, audience, signature, lifetime and token type
BEFORE calling this service. Setting `ClaimsIdentity.IsAuthenticated`
without cryptographic JWT validation is not a security boundary.

The service checks exactly one nonempty `sub` claim, the exact scope
`turkuaz.theme.read`, safe key-ID grammar and a positive server-side
`IThemeKeyEntitlementStore.IsAuthorizedAsync(subject, package, keyId)`
decision. Until these checks pass it will **never** contact
`IThemeKeySecretStore`. Approved requests can retrieve only a 32-byte AES key
from the trusted vault; an invalid-length vault response is wiped and
rejected. The HTTP host must return only the documented three JSON fields,
set `Cache-Control: no-store`, and erase the owned key buffer after
serialization.

These are intentionally **interfaces for real private entitlement and
vault integrations**. No entitlement database, KMS implementation, HTTP
server deployment, JWT middleware configuration or client login session
has been supplied. CI creates temporary in-memory test doubles to verify
denial of anonymous and unscoped principals, duplicate subject claims,
revoked entitlements, unrelated products and unauthorized keys.

### ASP.NET Core JWT HTTP host (source added, NOT deployed)

A separate .NET 10 project now implements a deny-by-default HTTPS endpoint:

- Project: `src/TurkuazInstaller.ThemeKeyServer`.
- Route: `POST /v1/theme-key`.
- Startup requires a trusted HTTPS `ThemeKeyIssuer:Authority` and matching
  `ThemeKeyIssuer:Audience`; missing configuration refuses startup.
- Standard Microsoft JwtBearer verifies signed `at+jwt` tokens, known OIDC issuer,
  audience, signing key and lifetime with 30-second clock skew.
- Direct HTTPS is mandatory. Untrusted forwarded headers cannot bypass it.
- Per-validated-subject in-process fixed-window limit of 10 key requests
  per minute, no queue; the 11th gets HTTP 429 before entitlement or vault
  access. This is defense-in-depth, NOT a distributed/edge abuse-control
  solution. Real hosting requires independently managed IP and distributed
  rate limits across server instances.
- Strict JSON has exactly `package_id` and `key_id`, no duplicate/unknown grants,
  at most 1024 bytes; output and denials always set `Cache-Control: no-store`.
- 401 for missing/invalid bearer; 403 for missing entitlement; 400 for malformed
  JSON; 413 for oversized payload; no body, bearer or key logging.
- Default DI providers deny all access and return no key. No embedded master key,
  default account or production credentials exist.

`ThemeKeyApiIntegrationTests` exercises real cryptographically signed ephemeral
RSA JWTs with ASP.NET Core's in-memory TestServer: correct HTTP authorization,
wrong audience/type/signature, expiry, missing scope, revoked entitlements,
bad JSON, oversized bodies and plaintext HTTP denial. Tests use no public network.

Still required before deployment: authentic OIDC configuration and user login,
server-controlled entitlements, real KMS/HSM, TLS hosting, rate limiting,
revocation/rotation and independent security review. This is NOT a live service.
### The server MUST enforce all of the following

1. Valid access token signature, issuer, audience, expiry and
   product-specific scope; no client-claimed role or self-issued token.
2. Server-side entitlement tied to the authenticated account and exact
   product, channel, key ID and current release; deny unknown/revoked keys.
3. Rate limits, replay/revocation strategy and abuse telemetry without
   logging token/key bytes or the raw response body.
4. TLS with controlled origin; no redirect to third-party hosts.
   `Cache-Control: no-store` and no CDN/browser/proxy key caching.
5. Key material from a proper secret store/HSM/KMS, NEVER from source
   control, user-writable files or public release artifacts.
6. Reviewed client application publisher pin compiled into a signed and
   protected distribution. **Network authorization is not a replacement
   for validating signed theme content.**
7. Key rotation/version policy, recovery, downtime behavior and explicit
   rejection when the user's token has expired or when offline.

Every authorized client inevitably obtains a usable decryption key in
memory. A user controlling the endpoint can instrument the process,
capture the screen or dump decrypted assets. Encryption therefore
protects at-rest package inspection by casual users, **not** absolute
protection from local extraction. Avoid shipping valuable secrets to
untrusted devices in the first place.

## CI acceptance

- [x] Ephemeral token/HTTP fake: correct HTTPS POST, bearer header,
      product/key binding, `no-store` response and exact 256-bit key.
- [x] Missing token refuses without contacting server.
- [x] Redirect, unauthorized status, malformed/oversized responses,
      duplicate JSON fields, wrong identity and HTTP endpoints rejected.
- [x] End-to-end local `.tpe` -> fake authorized HTTPS -> AES-GCM ->
      pinned CMS -> PNG success.
- [x] Server-side authorization service interface and isolated scope/entitlement/vault-denial tests.
- [x] JWT-validating deny-by-default API host and isolated real RSA JWT/HTTP tests.
- [ ] Real entitlement database, KMS vault, hardened hosting and deployed server.
- [ ] Real authenticated identity-provider session flow in the Windows client.
- [ ] Real certificate-pinned, publisher-managed native distribution.
- [ ] Real Windows 10/11 GUI screenshot/screen transitions at multiple DPI.
- [ ] External security review of key issuer identity, authorization,
      replay/rotation, exposure and local extraction limits.

**No external server or real secrets should be provisioned by these tests.**
