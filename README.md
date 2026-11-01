<!--
Dosya Yolu: REPO_ROOT/README.md
Amac: Community Rust donusum dalinin aktif kodunu ve dogrulama sinirlarini anlatmak
Modul - Markdown
Version: 2.0.0
Aciklama: .NET source ve CI'yi rust-community dalindan temizler, production durumu ayri
Bagimli Oldugu Katman: Documentation
-->

# TurkuazInstaller — Community Rust

Bu dal (`rewrite/rust-community`) **Rust tabanli Community** gelistirmesidir.
Studio proje hazirlama arayuzu, setup wizard, CLI, manifest/CMS ve artifact
dogrulama ile kurulum is akisi Rust crate'leriyle uygulanir.

- Rust workspace: [rust/Cargo.toml](rust/Cargo.toml)
- Rust mimari ve test kapsami: [rust/README.md](rust/README.md)
- Rust Studio: `rust/crates/installer-cli/src/setup_authoring_gui.rs`
- Rust setup sihirbazi: `rust/crates/installer-cli/src/setup_gui.rs`
- Kurulum motoru: `rust/crates/install-workflow`
- Windows CI: `.github/workflows/rust-migration.yml`

Bu gelistirme dalinda eski C# / .NET uygulama kaynaklari, projeleri ve
.NET build/test workflow'lari aktif agactan cikarilmistir. Bu degisiklik
**Git commit gecmisini geriye donuk silmez** ve `main` dalindaki eski
release mimarisini degistirmez. Eski .NET dokumanlari yalnizca gecmis
gereksinimlerin referansi olabilir; Rust API'si icin otorite degildir.

Rust uygulamasinin source/lint/test kabul kapilari, gercek GUI acilis smoke'u,
imzali production release ve temiz Windows kurulum dogrulamasi farkli
asamalardir. Production release veya `main` merge yapilmadi.

Pro ozel kodu bu public repoya alinmaz. Pro private repository'nin Rust
uygulamasi bu Community API'sini disardan, sabitlenmis commit uzerinden
kullanir.
