<!--
Dosya Yolu: docs/THEME_KEY_ISSUER.md
Amac: TPE dis anahtar tedariki icin sunucu/istemci guvenlik kontratini tanimlamak
Modul - Markdown
Version: 0.1.0-qa
Aciklama: OAuth benzeri guvenilir session, scope, TLS ve anahtar sunucu sorumluluklari
Bagimli Oldugu Katman: Security | Infrastructure | API
-->

# Theme Key Issuer v1 — private service contract (not deployed)

**Status: client adapter and isolated tests only. NO production key issuer
has been deployed and NO identity-provider session token is configured.**
The general TurkuazInstaller WinUI currently consumes signed ZIP-readable
\`theme.tpk\` only; \`theme.tpe\` must not be advertised as available in the UI.

## Responsibilities

\`HttpsThemeDecryptionKeyProvider\` is a narrowly bounded HTTP client.
It does **not** authenticate end users or decide whether a caller can
open a licensed/encrypted theme. The remote server MUST make those
decisions independently, based on an authenticated server-side identity
and the requested \`package_id\` and \`key_id\`.

The caller must inject a trusted absolute HTTPS URI **compiled from a
reviewed product configuration** and an \`IThemeAccessTokenSource\` that
obtains a short-lived scoped token through a real authenticated login
flow (for example, an audited authorization code + PKCE implementation).
User-writable \`ui-profile.yml\`, \`.tpe\` header, HTTP redirect, arbitrary
download data or unsigned settings must NEVER choose the key server.
No master key, signing private key, password or reusable bearer token
belongs in the EXE or this repository.

### Request

\`\`\`http
POST /v1/theme-key HTTP/1.1
Host: [trusted publisher domain]
Authorization: Bearer <short-lived authorization token>
Content-Type: application/json
Cache-Control: no-store

{"package_id":"levelupgt.ragnar-classic-preview","key_id":"key-v1"}
\`\`\`

The client requires HTTPS, disallows credentials/query strings/fragments
in the endpoint URL, disables automatic redirects and cookies, and limits
response bodies to 1,024 bytes, including when Content-Length is missing.

### Authorized response

\`\`\`http
HTTP/1.1 200 OK
Content-Type: application/json
Cache-Control: no-store

{
  "package_id": "levelupgt.ragnar-classic-preview",
  "key_id": "key-v1",
  "key_base64": "<base64 encoding of exactly 32 bytes>"
}
\`\`\`

The examples here contain placeholders, not a usable key/token.
Client rejects extra/duplicate response fields, different package/key,
incorrect encoding, missing \`no-store\`, oversized bodies and non-200
responses. Non-200 responses never fall back to plaintext theme files.
The returned key byte buffer belongs to \`EncryptedSignedThemePackReader\`
and is zeroed after use; outer GCM integrity and independently pinned
inner CMS signature must BOTH pass.

### The server MUST enforce all of the following

1. Valid access token signature, issuer, audience, expiry and
   product-specific scope; no client-claimed role or self-issued token.
2. Server-side entitlement tied to the authenticated account and exact
   product, channel, key ID and current release; deny unknown/revoked keys.
3. Rate limits, replay/revocation strategy and abuse telemetry without
   logging token/key bytes or the raw response body.
4. TLS with controlled origin; no redirect to third-party hosts.
   \`Cache-Control: no-store\` and no CDN/browser/proxy key caching.
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
      product/key binding, \`no-store\` response and exact 256-bit key.
- [x] Missing token refuses without contacting server.
- [x] Redirect, unauthorized status, malformed/oversized responses,
      duplicate JSON fields, wrong identity and HTTP endpoints rejected.
- [x] End-to-end local \`.tpe\` -> fake authorized HTTPS -> AES-GCM ->
      pinned CMS -> PNG success.
- [ ] Real authenticated identity provider and server implementation.
- [ ] Real certificate-pinned, publisher-managed native distribution.
- [ ] Real Windows 10/11 GUI screenshot/screen transitions at multiple DPI.
- [ ] External security review of key issuer identity, authorization,
      replay/rotation, exposure and local extraction limits.

**No external server or real secrets should be provisioned by these tests.**
