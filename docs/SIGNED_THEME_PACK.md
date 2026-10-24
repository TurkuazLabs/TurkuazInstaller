<!--
Dosya Yolu: docs/SIGNED_THEME_PACK.md
Amac: Imzali harici tema paketinin v1 bicimini, sinirlarini ve QA kullanimini tanimlar
Modul - Markdown
Version: 0.1.0-qa
Aciklama: Fail-closed signature, hash, package identity ve teslim riski
Bagimli Oldugu Katman: Infrastructure | Manifest Trust | Build
-->

# Signed Theme Pack v1 — `theme.tpk`

This is a **cross-product infrastructure primitive**, not a Ragnar-specific
binary patcher. It does **not** replace Velopack nor change its package
operation contracts. It can load approved theme PNGs for an installer or
launcher only after an independent trust policy verifies the package.

## Security boundary

A `.tpk` file is a bounded ZIP archive with exactly:

- `manifest.json`: strict UTF-8 JSON, signed as the **raw, exact bytes**;
- `manifest.p7s`: **detached CMS/PKCS#7** signature on `manifest.json`;
- `assets/<asset-id>.png`: 1–8 PNG assets enumerated by the signed manifest.

`SignedThemePackReader` delegates CMS verification to the **existing**
`CmsManifestSignatureVerifier`. The caller passes the intended `PackageId`.
The signer's publisher subject and certificate SHA-256 come from the
**installer's existing trusted policy repository**. The package cannot
supply, override or enroll its own signing key or publisher identity.

Once CMS verification succeeds, the reader rejects any mismatched product,
schema, ID, SemVer, field, screen name, asset hash or size. Unknown, duplicate,
absolute, case-changed, traversal, symlink and unlisted ZIP entries are
rejected. Each asset is restricted to PNG with signed dimensions, under
16 MiB and within a 16,777,216-pixel upper bound. Archive size is at most
80 MiB, and the in-memory reader never extracts ZIP members to disk.
Output bytes are returned only after **the entire pack** verifies.

A checksum alone is **not authentication**. Do not put a certificate
from the pack in the trusted publisher store, do not allow a user-writable
policy to add signing authorities, and do not rely on unsigned sidecars.

### Confidentiality is NOT part of v1

This `.tpk` is readable with normal ZIP tools. Its signature makes
unauthorized modification **detectable and rejectable**, not impossible;
an attacker may still replace files, but the real installer must refuse
them. Art already rendered locally can be copied via memory or a screenshot.
Merely renaming ZIP to `.bin` or `.tpk` is **not encryption** or DRM.

For server-confidential assets in a future v2, use authenticated
AES-256-GCM encryption with a genuinely external, authorized key-delivery
mechanism. Never ship the decryption key inside the EXE, ZIP or repository.
Offline public installers cannot offer absolute protection against asset
extraction. Product/game integrity and anti-cheat are separate concerns.

## Manifest v1

Sample (hash/length values below are placeholders, **not signable release
data**):

```json
{
  "schema_version": 1,
  "format": "turkuaz.theme",
  "package_id": "levelupgt.ragnar-classic-preview",
  "theme_id": "turkuaz-fantasy",
  "theme_version": "0.1.0",
  "assets": [
    {
      "id": "concept-c",
      "sha256": "<lowercase 64-character digest>",
      "size_bytes": 1,
      "width": 1672,
      "height": 941
    }
  ],
  "screens": {
    "welcome": "concept-c"
  }
}
```

`assets` links each asset ID to its exact `assets/<asset-id>.png`
ZIP member. `screens` maps logical GUI stages to verified IDs.
For Ragnar QA use `welcome=concept-c`, `options/complete=sunrise`,
`installing/failed=twilight`. Another product may use its own screen names.

## Offline packaging (trusted development machine)

```bash
python3 tools/theme-pack/build_theme_pack.py \
  --package-id levelupgt.ragnar-classic-preview \
  --theme-id turkuaz-fantasy --version 0.1.0 \
  --asset concept-c=/trusted/concept-c-background.png \
  --asset sunrise=/trusted/sunrise-background.png \
  --asset twilight=/trusted/twilight-background.png \
  --screen welcome=concept-c --screen options=sunrise \
  --screen installing=twilight --screen complete=sunrise \
  --screen failed=twilight \
  --certificate /private/signing-cert.pem \
  --private-key /private/signing-key.pem \
  --output /safe/theme.tpk
```

The command requires an existing OpenSSL binary. It must run only with a
properly managed certificate/key. Test-only ephemeral certificates are
permitted **solely** for isolated CI tests, never for an official installer
or real publisher pin. Private keys must not be logged, committed or
included in the `.tpk`. The tool refuses to overwrite a previous package.

## WinUI desktop integration (feature branch)

The WinUI desktop now supports an **explicit opt-in** binding in the usual
`ui-profile.yml` branding section:

```yaml
schema_version: 1
culture: tr-TR
branding:
  window_title: Turkuaz Ragnarok Online
  header_title: Turkuaz Ragnarok Online
  theme_package_id: levelupgt.ragnar-classic-preview
```

When `theme_package_id` is configured, the WinUI desktop reads
`theme.tpk` from **the same directory as `TurkuazInstaller.WinUI.exe`**,
not from the current working directory. The package must exist, carry the
configured exact product identity, pass the existing *external* pinned-CMS
policy, have matching PNG hashes, and successfully decode **all** images.
No image is extracted to the filesystem. The background picks the
`welcome/options/installing/complete/failed` screen mapping from the
verified package. If any verification/decode step fails, the UI displays
an error notice, prevents all installation actions, and does not execute
its startup controller. When `theme_package_id` is absent, the standard
WinUI UI remains unchanged.

**Theme trust is now separate from mutable update-manifest trust:**
`theme_package_id` is a display/product selector, NOT an authorization
source. WinUI creates a `CmsManifestSignatureVerifier` backed by
`CompiledThemePublisherPins` and `PinnedThemeTrustPolicyRepository`.
The latter snapshots reviewed pins in memory and never reads
`manifest-trust.yml` or the external `.tpk` for new trusted publishers.
The compile-time list is currently **empty**: when a user opts into a
theme, it is intentionally rejected until a real publisher identity and
certificate fingerprint have been reviewed and compiled into a trusted
distribution.
Product-specific signed builds can additionally set
`CompiledThemePublisherPins.RequiredThemePackageId` so that the approved
theme remains mandatory even if an attacker removes
`branding.theme_package_id` from a mutable `ui-profile.yml`. A conflict
between that compiled identity and user-configured identity fails before
the window is created. The generic Community distribution keeps the
compiled required ID `null` so products without a custom theme are
not affected; opt-in unpinned themes still fail closed. Unit tests cover valid CMS with no pin, wrong signer,
wrong product, and attempted caller mutation after snapshot.

The existing user-writable `manifest-trust.yml` for app/update manifests
remains unchanged; it does **not** authorize theme files. An attacker who
can patch the unsigned application binary or manipulate its runtime
cannot be defeated by a client-only check. Production requires
publisher-managed code signing, protected distribution/updates and
appropriate operating-system trust; no local-only DRM is claimed.

WinUI disables its content/action controls **from construction until**
the configured theme has passed complete CMS + PNG validation and image
decode. On failure no startup controller is invoked and the error notice
is left visible.

This integration targets the **TurkuazInstaller WinUI desktop**. It does
not automatically skin the separate Beam Tauri launcher or the existing
small native Ragnar setup EXE. For a real Ragnarok themed installer test,
build the WinUI distribution with approved original PNGs and a product
signing/trust identity, then run Windows 10/11 100/125/150% DPI image
acceptance. No screenshot validation is claimed by passing source tests.

## Integration and acceptance status

- **Implemented on this feature branch:** signed pack reader with
  existing policy verifier, offline `.tpk` builder, real-CMS test fixtures
  for valid, tampered and wrong-signer scenarios.
- **Opt-in WinUI wired in feature branch:** only after a product-bound,
  externally pinned theme verifies and every image decodes; invalid required
  packs disable install actions. Win32 Ragnar wizard and installed Tauri
  launcher remain separate and do not yet consume `SignedThemePackReader`.
- **Not yet established:** production publisher key/pin, signed release,
  Windows 10/11 screenshot smoke, confidential asset protection.

The other (native Ragnar) installer integration must load the adjacent `theme.tpk` from
the **trusted executable directory**, verify **before rendering**, and
show a clear error on invalid packages. Copy required launcher assets
into the installed application bundle, rather than relying on the
downloaded setup directory after installation. Production gates must
also exercise manifest pinning, no-downgrade policy, corrupt ZIP, and
theme fallback UX as separate cases.
