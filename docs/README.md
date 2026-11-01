<!--
Dosya Yolu: REPO_ROOT/docs/README.md
Amac: Main dalindaki legacy .NET belgelerini yeni Rust kaynaklarindan net ayirmak
Modul - Markdown
Version: 1.0.1
Aciklama: Mevcut WinUI/Velopack belgelerine navigasyon ve Rust dali uyarisi
Bagimli Oldugu Katman: Documentation
-->

# Main dokumantasyon dizini (.NET / WinUI)

Bu klasordeki mevcut belgeler `main` dalinin .NET/WinUI/
NativeAOT/Velopack uygulamasina aittir. **Yeni Rust uygulamasinin
kullanici/kurulum talimatlari degildir.** Belgelerin v1.x veya
tamamlandi ifadeleri Rust runtime'a aktarilmis ozellik sayilmaz.

Rust gelistirme kaynaklari icin
[ayri Rust belge dizini](https://github.com/TurkuazLabs/TurkuazInstaller/blob/rewrite/rust-community/docs/README.md)
kullanilmalidir. Rust PR'i kapatilmistir; kaynak dali korunur ve
`main` ile henuz birlestirilmemistir.

## Main kaynak rehberleri

| Konu | Main belgesi |
| --- | --- |
| Uygulama mimarisi | [ARCHITECTURE.md](ARCHITECTURE.md) |
| CLI / otomasyon | [CLI.md](CLI.md) |
| Son kullanici arayuzu | [USER_GUIDE.md](USER_GUIDE.md) |
| WinUI masaustu | [WINUI_DESKTOP.md](WINUI_DESKTOP.md) |
| Velopack test kapsami | [VELOPACK_E2E.md](VELOPACK_E2E.md) |
| Proje entegrasyonu | [PROJECT_INTEGRATION.md](PROJECT_INTEGRATION.md) |
| Manifest guven modeli | [MANIFEST_TRUST.md](MANIFEST_TRUST.md) |
| Windows prerequisite | [PREREQUISITES.md](PREREQUISITES.md) |
| Windows entegrasyonu | [WINDOWS_INTEGRATION.md](WINDOWS_INTEGRATION.md) |
| Version/update politikasi | [VERSION_POLICY.md](VERSION_POLICY.md) |
| Guvenlik | [SECURITY_MODEL.md](SECURITY_MODEL.md) |
| Release imzalama | [RELEASE_SIGNING.md](RELEASE_SIGNING.md) |
| Yayin oncesi kontroller | [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md) |
| Onceki gelistirme plani | [ROADMAP.md](ROADMAP.md) |

Diger eski tasarim, kural ve CI belgeleri mevcut dosya yollariyla
korunur. Eski belgedeki ozelliklerin Rust'a aktarildigini,
gercek production signing tamamlandigini veya bir release
yayimlandigini varsaymayin.

## Teknik durum ayrimi

- `main`: mevcut .NET cozumu, legacy belgeler ve ona ait CI.
- `rewrite/rust-community`: Rust CLI/Studio/kurulum motoru,
  ayri `Cargo.lock`, test ve Windows gelistirme artifact'lari.
- Rust gelistirme CI **PASS**, production Authenticode,
  interaktif Windows GUI acceptance ve temiz VM E2E **NOT RUN/BLOCKED**
  olabilir; guncel durum icin Rust
  [readiness](https://github.com/TurkuazLabs/TurkuazInstaller/blob/rewrite/rust-community/rust/RUST_READINESS.md)
  dokumanini inceleyin.

[Main README'ye don](../README.md).
