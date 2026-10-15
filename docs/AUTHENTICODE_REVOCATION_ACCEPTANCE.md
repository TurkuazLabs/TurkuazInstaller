<!--
Dosya Yolu: REPO_ROOT/docs/AUTHENTICODE_REVOCATION_ACCEPTANCE.md
Amac: Production oncesi Windows Authenticode revocation kanit ve kabul matrisi
Modul - Markdown
Version: 1.0.0
Aciklama: Gercek Windows VM senaryolari, guvenli kanit toplama ve release blokajlari
Bagimli Oldugu Katman: Tool | Testing | Security
-->

# Authenticode Revocation Acceptance (LAB only)

Status: **NOT RUN / NOT ACCEPTED**. This plan is not evidence that Windows revocation or offline scenarios have passed. Track [#51](https://github.com/TurkuazLabs/TurkuazInstaller/issues/51) and draft [#54](https://github.com/TurkuazLabs/TurkuazInstaller/pull/54).

## Verified implementation boundary

- `src/TurkuazInstaller.Platform.Windows/tools/WindowsAuthenticodeArtifactSignatureVerifier.cs` uses `WinVerifyTrust` / `WINTRUST_ACTION_GENERIC_VERIFY_V2`, `WTD_REVOKE_WHOLECHAIN`, `WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT` and `WTD_DISABLE_MD2_MD4`.
- A nonzero `WinVerifyTrust` status rejects the artifact. A zero result is followed by exact signer subject matching and, when configured, SHA-256 certificate pinning.
- `tests/TurkuazInstaller.Platform.Windows.Tests/WindowsAuthenticodeArtifactSignatureVerifierTests.cs` currently checks one real trusted embedded OS/PowerShell signer plus wrong subject, wrong certificate pin, and changed executable.
- Successful hosted CI is *not* proof of revoked, expired, offline, timestamp or altered Windows policy handling. `WTD_CACHE_ONLY_URL_RETRIEVAL` is not set; offline results can vary with CRL/OCSP cache and OS policy.

## Isolated test environment

Use disposable Windows 10 and 11 VMs, with snapshots before each case, non-production certificates and non-production signing keys. Build an isolated test PKI with an issuing CA, an HTTP CRL/OCSP endpoint and an embedded Authenticode-signed harmless fixture. Control certificate validity, revocation time, network reachability and timestamp configuration. Trust test roots **only inside the disposable VM**; remove with snapshot rollback. Never install ephemeral roots into production devices, modify production AV settings, or ship test-signed artifacts.

Record OS build, Windows trust policy, clock/timezone, fixture SHA-256, certificate thumbprint/chain (public only), URL cache state, endpoint/network state and exact `WinVerifyTrust` return status. Capture `VerifyAsync` result and whether stage/apply or bootstrap handoff occurred; redact personal and secret values. For genuinely reproducible offline tests, explicitly distinguish cold cache from warm cache. If a fixture lacks an embedded signature and only has a catalog signature, mark it **inapplicable**, not passed.

## Required cases

| ID | Scenario | Acceptance / expected evidence |
| --- | --- | --- |
| REV-01 | Valid embedded signature, trusted chain, responsive revocation endpoint | Trust succeeds, exact publisher and certificate pin match. |
| REV-02 | Issuing CA publishes revocation for signer, fresh CRL/OCSP is reachable | `WinVerifyTrust` nonzero; artifact rejected before stage/apply, including bootstrap replacement. |
| REV-03 | Revoked intermediate CA (root excluded), fresh revocation material | Artifact rejected; capture chain and native error. |
| REV-04 | No network, cold CRL/OCSP cache | Capture native status; **must not assert rejection without observing it**. If trusted despite unverified revocation, release policy must be explicitly reviewed and approved, otherwise BLOCK. |
| REV-05 | No network, warm cache with valid revocation material | Record acceptance/rejection and cache age; compare revoked and good certificate results. Do not infer REV-04 from this case. |
| REV-06 | Endpoint unreachable / timeouts / stale revocation data | Record exact status and elapsed time; reject any indeterminate path that violates the chosen production policy. Define maximum acceptable stall before release. |
| REV-07 | Expired signer without trusted timestamp | Reject or document platform-specific outcome and escalate any unexpected acceptance before release. |
| REV-08 | Expired signer with a valid trusted timestamp from signing validity period | Verify Windows trust result and publisher/pin, then independently review intended timestamp policy; do not presume approval. |
| REV-09 | Invalid/tampered signature and different signing certificate with same subject | Both rejected: tampering by `WinVerifyTrust`, cert substitution by pinned SHA-256 when pin is mandatory. |
| REV-10 | Valid digest but untrusted issuer or revoked signer in bootstrap self-update | Replacement never launched; existing trusted bootstrap remains usable when discovery fails. |
| REV-11 | Windows 10/11, standard-user and elevated account, clean VM / policy variants | Repeat critical REV-01/02/04/10 and document differences; no AV/trust-policy bypass. |

## Acceptance sequence

1. Pin the exact Installer and Office commit SHAs; note branch, runner/VM version, and trusted policy source *outside* the manifest.
2. Run existing Windows unit/integration/E2E tests unmodified; preserve their workflow URLs and conclusions.
3. Execute REV-01 through REV-11 with disposable fixtures. Save status/error output and *negative* stage/apply/handoff evidence. A setup failure or skipped case is **NOT RUN**, never success.
4. Perform signed manifest CMS verification separately, including external publisher subject and mandatory signer SHA-256 pin. Authenticode of an EXE does **not** replace manifest CMS verification.
5. For offline or unknown revocation results, decide and document whether a genuinely fail-closed explicit application policy is needed beyond the OS trust provider behavior. No code policy relaxation to obtain green tests.
6. Confirm production publisher certificate chain, timestamp provider, intended revocation timeout/cache behavior and Windows 10/11 user-acceptance evidence. Have the release owner approve the recorded evidence before any production promotion.

## Release gate

**BLOCK** production signing/promotion, NSIS removal and live Office/Start3POS migration until all required real-VM cases have recorded evidence and the security policy owner resolves any contradictory revocation outcomes. A passing hosted CI or unsigned LAB ZIP is insufficient. Keep PR #54 draft. No production certificate/private key is needed for this acceptance-plan document.
