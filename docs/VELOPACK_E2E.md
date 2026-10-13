# 📄 Dosya Yolu: /docs/VELOPACK_E2E.md
# 📌 Amac: TurkuazInstaller gercek Velopack E2E kalite kapisinin kapsamini ve failure anlamini dokumante etmek
# 📌 Modul - Markdown
# Version: 1.6.0
# Aciklama: vpk ile gercek Setup/full package uretimini ve Package Engine install-update-repair-rollback-uninstall test zincirini tanimlar
# Bagimli Oldugu Katman: Tool | Config

# Real Velopack E2E

Bu kalite kapisi FakeProcessRunner kullanmaz.

Windows hosted runner icinde pinlenmis vpk toolu ile iki gercek Velopack release uretilir.

Akis:

1. minimal Windows fixture publish edilir
2. v1 Setup.exe ve full nupkg uretilir
3. v2 full nupkg uretilir
4. TurkuazInstaller VelopackPackageEngine v1 Setup.exe ile install yapar
5. disk marker v1 olarak dogrulanir
6. current disindaki UserData klasorunde benzersiz veri marker'i olusturulur
7. v2 full nupkg ile update uygulanir; disk marker v2 ve UserData byte'lari dogrulanir
8. v2 full nupkg ile repair uygulanir; disk marker v2 ve UserData byte'lari dogrulanir
9. v1 full nupkg ile rollback uygulanir; disk marker v1 ve UserData byte'lari dogrulanir
10. gercek Update.exe uninstall calistirilir
11. active install dosyalarinin kaldirildigi dogrulanir

UserData fixture klasor/marker adlari config/velopack-e2e.psd1'dedir.
Bu test gercek Velopack package engine'i kapsar, signed manifest/workflow
oncesindeki trust verification zincirini calistirmaz. Uninstall sonrasi
user data muhafazasini da kanitlamaz; bu ayri urun-policy VM kabuludur.
Unsigned fixture hicbir zaman production release ya da guvenilir artifact
olarak sunulmaz.

vpk surumu config/velopack-e2e.psd1 icinde pinlenir.

Bu workflow yesil olmadan Stable Community runtime gercek Velopack entegrasyonu tamamlanmis kabul edilmez.

## Ephemeral CMS ile Signed Workflow Windows E2E

Ayni hosted Windows test isinde ikinci bir izole kurulum root'u kullanilir.
SignedWorkflowAcceptanceRunner gercek vpk Setup/full.nupkg fixture dosyalari
icin iki surumun manifestlerini olusturur. Manifest byte'lari runtime'da
uretilen ephemeral RSA/X.509 sertifikasi ile CMS detached .p7s olarak
imzalanir; external YAML trust store ayri bir klasorde certificate
SHA-256 pini ile olusturulur.

Gercek adapter zinciri:

- FileReleaseProvider + CmsManifestSignatureVerifier +
  YamlManifestTrustPolicyRepository
- InstallerWorkflowService + DefaultArtifactDownloader +
  Sha256ArtifactVerifier
- VelopackPackageEngine + SystemProcessRunner
- JsonInstallStateRepository (kalici, ayri test state root)

Olumlu akis: v1 Setup ile install; v2 full ile update ve repair;
v1 full ile rollback; uninstall. Her adimda gerçek current surum
marker'i ve kalici install state dogrulanir. UserData marker'i
update/repair/rollback boyunca byte olarak korunmalidir.

Olumsuz akis: imzali manifest byte'lari degistirilirse veya external
certificate pini yanlissa release okunmamalidir; gecerli imzali
manifest yanlis payload SHA-256 tasiyorsa UpdateAsync gercek
package mutation ve state commit oncesinde fail-closed bitmelidir.

Ek olumsuz akislar:
- CMS manifest imzasi ve artifact SHA-256 eslesse dahi `nupkg`
  arsivi bozuksa gercek Velopack updater paketi reddetmeli; V1
  install state, aktif surum ve UserData korunmalidir.
- V2 basarili update/repair sonrasinda, V1 geri alma manifesti
  imzali olmasina ragmen paket SHA-256 degeri yanlissa rollback
  uygulanmamali; V2 state/disk marker ve UserData korunmalidir.
  Ardindan gecerli V1 manifestiyle normal geri alma basarmalidir.

Bu testler gecersiz paket/rollback girisinin fail-closed oldugunu
kanitlar; paket kopyalanmaya basladiktan sonra beklenmedik crash,
atik olmadan otomatik recovery ya da production Authenticode
imzasi/Windows 10 UAC davranisini kanitlamaz.

CI beklenen cikti:
- TURKUAZ_INSTALLER_SIGNED_WORKFLOW_FAIL_CLOSED_OK
- TURKUAZ_INSTALLER_SIGNED_BROKEN_ARTIFACT_FAIL_CLOSED_OK
- TURKUAZ_INSTALLER_SIGNED_ROLLBACK_BAD_HASH_FAIL_CLOSED_OK
- TURKUAZ_INSTALLER_SIGNED_REAL_VELOPACK_WORKFLOW_E2E_OK

Guvenlik ve kapsam sinirlari:
- Test signer'i production disi, kisa omurlu ve yalniz testte kullanilir.
- Gercek Velopack fixture Setup.exe ve full.nupkg Authenticode ile
  production signer olarak imzalanmis degildir. Signed *manifest*
  Authenticode-signed *artifact* ile ayni sey degildir.
- Windows CI testi, izole manuel Windows 10/11 VM kabulunun,
  kullaniciya ozel release artefaktlarinin veya antivirus verdict'inin
  yerine gecmez.
- Uninstall sonrasi UserData'nin korunmasi urun policy'sine gore
  ayri test edilir. Test production release yayimlamaz.

## Running updater kill: process-level recovery evidence

The crash journal E2E includes three isolated cases: before updater
launch, after a successful updater and before state commit, and while
a real `Update.exe` process is observable as running.

The third fixture publishes the live updater PID in an atomically
renamed signal file. The parent verifies that process, terminates
the workflow process tree, waits for the updater to exit, and then
requires that committed V1 state, the original `Applying` journal
and outside-`current` UserData are preserved. New update/uninstall
operations must remain blocked by the recovery hold.

The current on-disk version is intentionally **not** asserted in
the running-updater case: a process kill may leave a partial tree.
This gate is about rejecting further mutation and preserving user
data, not guaranteeing that the kill hit a specific file write or
recovering a partial installation. Production-safe reconciliation
still requires independently verified on-disk evidence.

## Observed payload digest after crash

Each of the three hosted-Windows crash fixture cases also invokes
`InstallerPayloadEvidenceService` against the **actual installed version
marker**. The test saves the V1 marker's SHA-256 before update and then
compares observed on-disk bytes with that fixture-only baseline:
- pre-updater kill: selected marker must match V1;
- post-updater success / pre-state-commit kill: marker must differ from V1;
- running-updater kill: marker may match, differ, or be absent; this
  deliberately does not assume a consistent disk after interruption.

Every scenario still requires journal preservation, committed V1 state,
unchanged UserData and a recovery hold. A successful marker comparison
does not validate all installed payload files, prove a trusted signed
per-file baseline, or allow unattended repair.

## Signed inventory + real installed Velopack marker

The hosted-Windows signed workflow now creates an independent,
ephemeral-CMS-signed per-file inventory for the expected V2 version
marker. Its SHA-256 is computed from known pre-packaging V2 bytes, **not**
from the installed target. The inventory is verified using the same
external test-certificate pin as the release manifest and is bound to
the already verified V2 package ID, channel, semantic version and
distribution artifact digest.

After the actual signed-release workflow updates the real Velopack
fixture, `InstallerPayloadEvidenceService` independently hashes the
installed marker; the selected file must match the verified inventory
entry. The separate UserData preservation assertion remains active.

This demonstrates a complete trust-and-observation **LAB path for a
selected marker**, not signed exhaustive payload inventory generation
for a production package. It does not repair, unlock journals, or permit
automatic file replacement.
