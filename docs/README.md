<!--
Dosya Yolu: REPO_ROOT/docs/README.md
Amac: Community Rust installer icin mevcut dokumantasyonun giris ve kaynak dizini
Modul - Markdown
Version: 1.0.0
Aciklama: Gercek Rust kaynaklari, test kaniti, tarihsel kontratlar ve kabul sinirlari
Bagimli Oldugu Katman: Documentation
-->

# Community Rust belge dizini

Bu dizin `rewrite/rust-community` gelistirme dali icindir.
`main` ve onceki .NET/WinUI planlari burada tamamlanmis Rust
ozelligi olarak anlatilmaz. TEST artifact'i release degildir.

## Baslangic ve teknik kaynaklar

| Ihtiyac | Kaynak |
| --- | --- |
| Proje girisi ve hizli komutlar | [Kok README](../README.md) |
| Rust crate mimarisi | [Rust README](../rust/README.md) |
| CLI komutlari | [CLI](../rust/CLI.md) |
| Windows Setup Studio ve WebSetup | [Setup Studio](../rust/SETUP_BUILDER.md) |
| Paket formati (`.tzpkg`) | [Paket formati](../rust/PACKAGE_FORMAT.md) |
| Kurulum/update/repair/rollback | [Kurulum akisi](../rust/INSTALL_WORKFLOW.md) |
| Versioned deployment ve journal | [Deployment layout](../rust/DEPLOYMENT_LAYOUT.md) |
| Publisher imzalama | [Release signing](../rust/RELEASE_SIGNING.md) |
| Windows manuel kabul | [Smoke test](../rust/SMOKE_TEST.md) |
| Guncel kabul ve eksik isler | [Rust readiness](../rust/RUST_READINESS.md) |
| Guvenlik bildirimi | [Security policy](../SECURITY.md) |

## Dogrulanmis ile henuz onaylanmamis olanin farki

`rust/Cargo.lock` sabitlenmistir.
[Community 41339318653](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/41339318653)
sabit `7b0c2613` kaynaginda Linux ve Windows
`--locked` CI'yi **PASS** tamamladi. Sonraki commit'ler kendi CI
sonuclari gelmeden PASS sayilmaz.

Rust installer, Studio/wizard, signed offline TEST setup ve WebSetup
gelistirme artifact'lari uretilir. Gercek Windows GUI click,
production Authenticode, dis publisher/release, temiz Windows VM
installer lifecycle kabulu ve ilk gercek urun pilotu halen aciktir.

## Tarihsel dokumanlar ve uygulanan schema

`../contracts/` altindaki 11 YAML dosyasi
`legacy-design-only` durumundadir; Rust runtime tarafindan
otomatik yuklenmez. Gecerli signed manifest parser kodu
`../rust/crates/manifest-parser/src/lib.rs`,
domain kontrati `../rust/crates/domain/src/release.rs`
dosyalarindadir. `../examples/` placeholder'lari production
imzasi veya yayin guveni tasimaz.

## Gelistirme ve yayin ayrimi

Repo kokunden:
```powershell
cargo fmt --manifest-path rust/Cargo.toml --all -- --check
cargo clippy --locked --manifest-path rust/Cargo.toml --workspace --all-targets -- -D warnings
cargo test --locked --manifest-path rust/Cargo.toml --workspace --all-targets
```

Windows gercek testlerinde [kabul rehberini](../rust/SMOKE_TEST.md)
kullanin. Ozel sertifika/anahtar ve sifreleri issue veya artifact'a
eklemeyin. [PR #63](https://github.com/TurkuazLabs/TurkuazInstaller/pull/63)
Draft'tir; main merge ya da production release izni verilmemistir.
