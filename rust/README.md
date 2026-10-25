<!--
Dosya Yolu: REPO_ROOT/rust/README.md
Amac: TurkuazInstaller Rust gecisinin kapsam ve dogrulama kapilarini aciklamak
Modul - Markdown
Version: 0.1.0
Aciklama: Mevcut .NET davranisini koruyarak asamali Rust portu icin mimari sinir cizer
Bagimli Oldugu Katman: Documentation
-->

# Rust Migration

Bu workspace TurkuazInstaller'in paralel Rust uygulamasidir. Her Rust ozelligi karsilik gelen contract ve Windows kabul kontrollerinden gecene kadar mevcut .NET uygulamasi davranis referansi olarak kalir. Production release akislari mevcut imzali release kapilarini kullanmaya devam eder.

## Boundary

- Rust Community kodu private TurkuazInstaller-Pro koduna baglanamaz veya onu paketleyemez.
- Manifest guveni, artifact dogrulamasi, kurulu state, islem kilidi, kurtarma ve paket uygulama guvenligi Community sorumlulugunda kalir.
- Pro typed staged-rollout karari verebilir; paket uygulama veya rollback yetkisi veremez.
- Mevcut Pro entegrasyonu production'a bagli degildir. Dilden bagimsiz sinir tasarlanirken bu durum acik tutulur.
- Kullanici dagitiminda Turkuaz Installer birincil setup, NSIS zorunlu yedek olur ve ikisi birlikte uretilir. Paylasimda Turkuaz setup verilir.

## Migration order

1. Saf Domain degerlerini port edip serialized contract'i koru.
2. Platform API'lerinden bagimsiz Contracts ve Application akislarini port et.
3. Metadata parse, imza, sinirli indirme, state, journal ve process adapterlarini port et.
4. x64 ve ARM64 davranisini koruyarak Windows API'lerini port et.
5. CLI ve NativeAOT bootstrap davranisini port et.
6. WinUI akislarini Rust-native Windows UI adapteriyla karsila.
7. .NET uygulamasini kaldirmadan once install, update, repair, rollback, uninstall, recovery, signing ve paketli startup'i kanitla.

Ilk crate su anda `PackageId`, `SemanticVersion`, `ArtifactDigest` ve `ReleaseChannel` tiplerini port eder. Bu temel, release veya runtime degisikligi anlamina gelmez.
