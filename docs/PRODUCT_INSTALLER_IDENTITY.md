<!--
Dosya Yolu: REPO_ROOT/docs/PRODUCT_INSTALLER_IDENTITY.md
Amac: Urun gorunen adi ile teknik istemci/packet/installer kimliklerini ayirmak
Modul - Documentation Markdown
Version: 1.0.0
Aciklama: Platform bagimsiz urun build ve QA/release kimlik kontrati
Bagimli Oldugu Katman: Config | View | Packaging | Security
-->

# Product Installer Identity Contract v1

TurkuazInstaller Community is **product neutral**. A product-specific
installer must not rename the Community engine or change its manifest
signature/trust semantics. Product branding is provided using reviewed
`ui-profile.yml`, product metadata, and a pinned graphic asset.

## Five distinct identities

| Field | Visibility | Definition |
| --- | --- | --- |
| `display_name` | Player | Human-friendly product name, e.g. `Turkuaz Ragnarok Online` |
| `edition_label` | Optional player text | Classic/Preview/Renewal; never inferred from a build year |
| `client_profile_id` | Internal | Stable identity such as `classic-2015`, `renewal-2022`, `preview-qa` |
| `client_build` and `packetver` | Internal | Exact validated executable build and compatible packet revision; null until proven |
| `installer_version` | Installer/about UI | SemVer of the actual installer; NOT client year, packetver or game version |

Each build additionally records `package_id` (immutable installed-product
identity), `patch_channel` (dev/qa/beta/live), `source_commit` (full Git SHA),
target platform/architecture and a verifiable SHA-256 for every distributed
binary and any accompanying artwork.

Changing a package ID, install directory, main EXE or shortcut name is
a **migration**, not a cosmetic rename. Preserving existing game-client data,
update and uninstall must be separately tested.

## Naming conventions

Player-facing setup file: `Turkuaz-Ragnarok-Online-Setup.exe` (stable).
A channel- or edition-specific public filename is optional only with an
explicit user-facing product decision.

Internal examples:
- `tro-classic-2015-live-<validated-client-build>-<source-sha12>`
- `tro-renewal-2022-qa-<validated-client-build>-<source-sha12>`
- `tro-preview-qa-0.1.0-<source-sha12>-test-only`

Do not put unverified game-client versions into a filename. Use an
artifact manifest with explicit `client_year: null` and `packetver: null`
rather than inventing them. QA artifacts and signed releases are separate.
A source SHA, ZIP checksum, signed CMS manifest and publisher Authenticode
all serve *different* purposes.

## Product example: Ragnar preview (no gameplay claim)

- `LevelUpGT/beam-patcher`, draft PR #11 owns the preview setup UI, real
  Velopack executable, and private profile inventory.
- `b1glord/ragnarokclientpatch`, draft PR #1 owns the public read-only CI
  artifact; existing `2015` patch aliases and `2022 ready:false` are
  not changed by setup UI naming.
- Preview uses existing `levelupgt.ragnar-classic-preview` package ID,
  `RagnarClassicPreview.exe` main EXE, and existing QA install root.
  These values are *not* globally renamed.
- `classic-2015` and `renewal-2022` are reserved profile names only.
  Neither becomes a real 2015/2022 install until exact authorized
  game-client binary, client build, packetver, GRF/content provenance,
  patch feed readiness, update/rollback and Windows smoke are verified.
- Selected Concept C artwork is a pinned optional product-specific
  **binary resource**, not a theme downloaded/executed at runtime.
  Preserve the signed resource and SHA-256 contract when bundling.

## Non-negotiable gates

- Profile, year, packetver, product package ID and installer SemVer must be
  read from one reviewed, version-controlled build metadata source.
- Reject missing or unverified fields for an actual production client.
- Refuse `qa_install_enabled` or `production_publish_allowed` upgrades
  based only on a UI configuration edit.
- Signed release gates remain separate: validated legal publisher,
  certificate profile, timestamp, revocation, installer and nested-payload
  hashes, independent review and Windows 10/11 real install/upgrade/rollback.
- No arbitrary remote XAML, runtime patch-policy mutations or automatic
  release on a branding-only commit.
