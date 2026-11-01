<!--
Dosya Yolu: REPO_ROOT/rust/RUST_READINESS.md
Amac: Community ve Pro Rust migration icin gercek islev ve release kabul sinirlarini kaydetmek
Modul - Markdown
Version: 1.0.1
Aciklama: Uygulanan, CI ile dogrulanan ve production icin eksik zorunlu kapilar
Bagimli Oldugu Katman: Documentation | QA | Release
-->

# Rust Installer - Dogrulanmis durum ve acik kabul kapilari

Denetim temel alinmis kaynak: Community `7acc8b7`,
[PR #63](https://github.com/TurkuazLabs/TurkuazInstaller/pull/63).
Bagli Pro kaynak: `d3e9238`,
[PR #10](https://github.com/TurkuazSoft/TurkuazInstaller-Pro/pull/10).
CI sonucu ile kullanicinin temiz Windows makinesinde gercek
calisma testi birbirinin yerine gecmez.

## Runtime - aktif Rust kodu

- `rust/crates/installer-cli`: Rust CLI, Windows Studio ve GUI wizard.
  Yeni Studio projeleri per-user LOCALAPPDATA proje/cikti/kurulum
  koklerini varsayilan olarak kullanir; kullanici kayitli .tzproj
  yollarinin anlami degistirilmez.
- `rust/crates/package-engine`: .tzpkg, .tzsetup, self-extracting setup.
- `manifest-pipeline`, `manifest-verification`, `manifest-trust-store`:
  dis guven politikasina bagli signed CMS manifest.
- `artifact-transfer`, `artifact-validation`, `authenticode-verification`:
  HTTPS/file staging, size/SHA-256, istege bagli Windows Authenticode.
- `install-workflow`, `deployment-activation`, `operation-journal`,
  `install-state-store`: full install, update, repair, policy rollback,
  uninstall ve konservatif recovery.

Kodun varligi her islevin production ortami icin kabulunu kanitlamaz.

## Dogrulanmis otomatik CI

- [Community run 41333284129](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/41333284129):
  Linux/Windows build, fmt, Clippy, Rust tests, Windows signed
  fixture ile lifecycle testleri ve TEST artifact'lari PASS.
- [Pro run 41335243809](https://github.com/TurkuazSoft/TurkuazInstaller-Pro/actions/runs/41335243809):
  Pro Rust workspace, CMS adapter, egui Studio, 3 GUI Windows
  TEST ZIP packaging PASS. Production release uretilmedi.

## Desteklenmeyen / bekleyen ozellikler

1. **Gercek publisher imzalama ve kaynak guveni:** Test sertifikasi
   production certificate, timestamp, provenance ve revocation kabulunu
   kanitlamaz.
2. **Windows entegrasyon adapter'lari:** Rust runtime onkosul
   yukleyici, kisayol, protocol registration, preserve_paths ve tam
   data migration davranisini henuz yerine getirmiyor. Kullanilmaz
   manifest alanlari destekleniyormus gibi ilan edilmemeli.
3. **Temiz Windows VM GUI/E2E:** GUI gercek pencere acilisi,
   klavye/tiklama, setup/update/repair/rollback/uninstall ve
   kesinti/kurtarma manuel/otonom clean-VM testleri bekliyor.
4. **Tam Rust dagitim hedefi:** Ana motor Rust; `packaging/
   TurkuazInstallerBackup.nsi` NSIS backup betigi ve Windows CI
   PowerShell smoke araclari Rust degil. NSIS fallback Rust temelli
   guvenli alternatife donusturulmeden `%100 Rust installer` iddiasi
   dogru olmaz.
5. **Tekrarlanabilir build:** Community ve Pro Rust workspace'lerinde
   `Cargo.lock` track edilmiyor; uygulama release'lerinde lockfile
   politikasi ve `--locked` CI kabul kapisi gerekir.
6. **Pro canli islevler:** Lisans provider, OS credential store,
   private feed UI baglantisi, authoritative rollout revision backend
   ve full production integration E2E tamamlanmadi.
7. **Dis yayin:** Gercek HTTPS signed release, kopuk ag,
   clean-VM install ve SmartScreen/antivirus kaniti bekliyor.

## Kontrat ve belge siniri

`contracts/` altindaki bazi YAML'ler eski .NET uygulamasinin
**gelecek/legacy tasarim kontratlari** olarak tutulur; Rust crate'leri
tarafindan yuklenmezler ve production ozelligi sayilmazlar.
Rust manifestinin uygulanan otoritesi
`rust/crates/manifest-parser/src/lib.rs` ve
`rust/crates/domain/src/release.rs` dosyalaridir.
Kullanilabilir minimal Rust YAML ornegi
`examples/community-manifest.yml`; placeholder'lar gercek
signed release ile degistirilmedikce bu da TEST ornegidir.
Gercek paket uretimi icin [RELEASE_SIGNING.md](RELEASE_SIGNING.md)
ve [SETUP_BUILDER.md](SETUP_BUILDER.md) gecerlidir.

## Kapanis icin gercek kanit

- Community: [manuel Windows kabul #41](https://github.com/TurkuazLabs/TurkuazInstaller/issues/41),
  [ilk urun pilotu #42](https://github.com/TurkuazLabs/TurkuazInstaller/issues/42),
  [security #51](https://github.com/TurkuazLabs/TurkuazInstaller/issues/51),
  [AV #58](https://github.com/TurkuazLabs/TurkuazInstaller/issues/58).
- Pro: [authoritative rollout #5](https://github.com/TurkuazSoft/TurkuazInstaller-Pro/issues/5)
  ve [Rust Pro PR #10](https://github.com/TurkuazSoft/TurkuazInstaller-Pro/pull/10).
- Public installer private Pro crate'lerine baglanamaz. Production
  imzali cikti ve main merge icin ayri onay gerekir.
