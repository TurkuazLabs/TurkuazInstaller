<!--
Dosya Yolu: REPO_ROOT/README.md
Amac: Community Rust kurulum sistemi icin guncel kaynak ve test kapsamini anlatmak
Modul - Markdown
Version: 2.2.1
Aciklama: Rust Studio, installer CLI ve release guveni
Bagimli Oldugu Katman: Documentation
-->

# TurkuazInstaller Community — Rust

Community installer, Setup Studio, setup wizard ve CLI Rust ile gelistirilir.
Paket motoru, imzali manifest dogrulamasi, artifact kontrolu,
kurulum/guncelleme/onarma/rollback ve kurtarma Rust crate'lerini kullanir.

- [Belge dizini ve gelistirme kilavuzu](docs/README.md)
- [Rust mimari](rust/README.md)
- [CLI](rust/CLI.md)
- [Setup Studio](rust/SETUP_BUILDER.md)
- [Kurulum islemleri](rust/INSTALL_WORKFLOW.md)
- [Paket formati](rust/PACKAGE_FORMAT.md)
- [Windows kabul](rust/SMOKE_TEST.md)

Gelistirme artifact'lari production release degildir. Gercek Windows
GUI smoke, production Authenticode imzalama ve temiz kurulum kabul
testleri ayri kapilardir. Community, private Pro koduna bagli degildir.

## Rust durumu ve destek siniri

Aktif kod Rust crate'leri altindadir. Koken `contracts/` YAML
dosyalari Rust runtime'a otomatik yuklenmez; bir bolumu onceki
.NET/WinUI uygulamasinin **tasarim referansidir**. Gercek imzali
manifest parser'i `rust/crates/manifest-parser` icindedir.
Ornekler `examples/` klasorunde placeholder tasir ve production
paketi degildir.

- [Rust kabul ve eksik isler](rust/RUST_READINESS.md)
- [Guncel Windows test rehberi](rust/SMOKE_TEST.md)
- [Imzali paket yayinlama](rust/RELEASE_SIGNING.md)

**Sinir:** Rust kodu ve Windows CI gecmis olsa da gercek
signed publisher, temiz VM GUI smoke ve Windows prereq/shortcut/
protocol/preserved-data adapter'lari henüz production kabulunu gecmedi.
NSIS backup script'i halen Rust disi zorunlu fallback'tir;
`%100 Rust dagitim araci` hedefi icin bu ayri giderilmelidir.

## Gelistirici dogrulama (repo kokunden)

```powershell
cargo fmt --manifest-path rust/Cargo.toml --all -- --check
cargo clippy --locked --manifest-path rust/Cargo.toml --workspace --all-targets -- -D warnings
cargo test --locked --manifest-path rust/Cargo.toml --workspace --all-targets
```

`rust/Cargo.lock` commit edilmistir. Kaynak revizyonu `7b0c2613`
icin [Linux ve Windows CI 41339318653](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/41339318653)
**PASS**; bu kanit yalniz o commit'e aittir. Rust Windows gelistirme
artifact'lari (CLI, Setup Studio, GUI stub, WebSetup ve ornek TEST kurulum)
Actions ciktilaridir, release degildir. Gercek Windows masaustu ve
temiz VM kullanici kabulunu kanitlamaz.

Aktif dal `rewrite/rust-community`, PR #63 Draft.
Ana dal ve eski commit gecmisi bu calismada degistirilmedi.
