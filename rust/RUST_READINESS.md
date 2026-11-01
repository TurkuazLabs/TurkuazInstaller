<!--
Dosya Yolu: REPO_ROOT/rust/RUST_READINESS.md
Amac: Community Rust gelistirme kaniti ve production kabul eksiklerini ayri kaydetmek
Modul - Markdown
Version: 1.0.3
Aciklama: Sabit commit/CI dogrulamasi, gercek runtime siniri ve kalici kabul kapilari
Bagimli Oldugu Katman: Documentation | QA | Release
-->

# Rust Installer - Dogrulanmis durum ve acik kabul kapilari

Bu belgedeki CI kaniti **2026-11-01** itibariyla incelenen sabit
Community revizyonu `7b0c2613e9d20717ff8db850db563ecba8f89609`
ve [taslak PR #63](https://github.com/TurkuazLabs/TurkuazInstaller/pull/63)
icin gecerlidir. Yeni docs commit'leri otomatik olarak test edilmis sayilmaz.
Pro icin [taslak PR #10](https://github.com/TurkuazSoft/TurkuazInstaller-Pro/pull/10)
ayri kaynak ve kabul surecidir.

## Aktif Rust gelistirme islevleri

- `rust/crates/installer-cli`: CLI; Windows Setup Studio, setup wizard
  ve onceden yapilandirilmis WebSetup gelistirme araclari.
- `rust/crates/package-engine`: `.tzpkg`, `.tzsetup` ve
  self-extracting setup olusturma/dogrulama.
- `manifest-pipeline`, `manifest-verification`, `manifest-trust-store`:
  detached CMS ve out-of-band publisher trust policy.
- `artifact-transfer`, `artifact-validation`,
  `authenticode-verification`: HTTPS/file staging, sinirli indirme,
  byte size/SHA-256 ve gerekli Windows Authenticode.
- `install-workflow`, `deployment-activation`, `operation-journal`,
  `install-state-store`: gelistirme install/update/repair/rollback/
  uninstall ve konservatif recovery.

Studio yeni projelerde per-user LOCALAPPDATA dizinini varsayar;
var olan `.tzproj` dosyalarinda secilmis yollar degistirilmez.
Bu kapsamin production kabulunu sagladigi iddia edilmez.

## Kanit: sabit revizyon icin otomatik CI

- [Community CI 41339318653](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/41339318653):
  **PASS** — Linux Rust format/check/Clippy/test ve Windows
  `--locked` workspace, NSIS script kontrolu, GUI build/test,
  imzali TEST fixture offline lifecycle smoke ve artifact upload.
- `rust/Cargo.lock` commit edilmistir; Linux/Windows cargo
  check/build/test/Clippy komutlari `--locked` kullanir.
- CI'nin urettigi CLI, GUI stub, Setup Studio, WebSetup ve ornek
  setup **TEST artifact'lari** production imzali dagitim degildir.
- Gercek kullanici masaustunde GUI pencere/tiklama: **NOT RUN**.
  Temiz Windows VM ve production certificate: **NOT RUN**.

## Production icin acik kabul kapilari

1. **BLOCKED — Publisher/provenance:** gercek yayin sertifikasi,
   Authenticode, timestamp, zincir/revocation, AV/SmartScreen ve
   signed HTTPS release trust kabulu.
2. **BLOCKED — Windows uygulama entegrasyonlari:** Rust prereq
   yukleme, kisayol, protocol kaydi, preserve_paths ve veri tasima
   adapter'lari gelistirme runtime'inda uygulanmadi.
3. **BLOCKED — Temiz Windows 10/11 VM:** GUI startup, tiklama/klavye,
   install/update/repair/rollback/uninstall, reboot, kesinti ve
   guvenli recovery kabul kanitlari.
4. **BLOCKED — Tam Rust dagitim:** birincil motor Rust olsa da
   `packaging/TurkuazInstallerBackup.nsi` zorunlu NSIS backup'tir.
   CI testleri icin PowerShell kullanilir. Tum dagitima `%100 Rust`
   denemez.
5. **BLOCKED — Ilk gercek urun pilotu ve dis yayin:** gercek uygulama
   verisi, dogrulanmis publisher trust ve son kullanici kabul kaniti.
6. **BLOCKED — Pro canli entegrasyonu:** lisans kaynagi, OS credential
   store, private feed arayuzu, authoritative rollout backend ve
   Pro/Community E2E private Pro deposunun sorumlulugundadir.

## Tarihsel YAML sozlesmesi / canli Rust kontrati

`contracts/` altindaki 11 YAML dosyasi
`# Rust-Runtime-Status: legacy-design-only` isaretlidir.
Bunlar onceki .NET planlarinin **tarihsel tasarim** kaydidir;
Rust bunlari yuklemez, belge veya schema olarak var olmalari
uygulama destegi anlamina gelmez.

Gecerli Rust signed manifest parser'i
`rust/crates/manifest-parser/src/lib.rs`,
release domain'i `rust/crates/domain/src/release.rs` dosyasindadir.
`examples/community-manifest.yml` TEST placeholder'lari icerir.
Uretim oncesi [RELEASE_SIGNING.md](RELEASE_SIGNING.md),
[SETUP_BUILDER.md](SETUP_BUILDER.md) ve
[Windows smoke rehberi](SMOKE_TEST.md) izlenmelidir.

## Takip ve kapanis kaniti

- [Windows manuel kabul #41](https://github.com/TurkuazLabs/TurkuazInstaller/issues/41)
- [Gercek uygulama pilotu #42](https://github.com/TurkuazLabs/TurkuazInstaller/issues/42)
- [Guvenlik #51](https://github.com/TurkuazLabs/TurkuazInstaller/issues/51)
- [AV / SmartScreen #58](https://github.com/TurkuazLabs/TurkuazInstaller/issues/58)

Community public crate'leri private Pro crate'lerine baglanmaz.
PR Draft, `main` merge ve production release yoktur.
