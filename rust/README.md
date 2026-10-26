<!--
Dosya Yolu: REPO_ROOT/rust/README.md
Amac: TurkuazInstaller Rust gecisinin kapsam ve dogrulama kapilarini aciklamak
Modul - Markdown
Version: 0.2.0
Aciklama: Mevcut .NET davranisini koruyarak asamali Rust portu icin mimari sinir ve gercek kapsam durumunu belirtir
Bagimli Oldugu Katman: Documentation
-->

# Rust Migration

Bu workspace TurkuazInstaller'in paralel Rust uygulamasidir. Rust ozellikleri .NET referansiyla contract ve Windows kabul kontrollerinden gecene kadar mevcut .NET uygulamasi davranis referansi olarak kalir. Production release akislari mevcut imzali release kapilarini kullanmaya devam eder.

## Boundary

- Rust Community kodu private TurkuazInstaller-Pro koduna baglanamaz veya onu paketleyemez.
- Manifest guveni, artifact dogrulamasi, kurulu state, islem kilidi, kurtarma ve paket uygulama guvenligi Community sorumlulugunda kalir.
- Pro typed staged-rollout karari verebilir; paket uygulama veya rollback yetkisi veremez.
- Mevcut Pro entegrasyonu production'a bagli degildir. Dilden bagimsiz sinir tasarlanirken bu durum acik tutulur.
- Kullanici dagitiminda Turkuaz Installer birincil setup, NSIS zorunlu yedek olur ve ikisi birlikte uretilir. Paylasimda Turkuaz setup verilir.

## Current implementation

- `domain`: package id, SemVer, SHA-256 digest, release channel, artifact/signature, prerequisite, Windows integration, release policy ve install/update/repair/rollback/uninstall plan modelleri.
- `metadata`: manifest/signature icin 1 MiB / 256 KiB limitler, Content-Length kontrolu ve stream sirasinda bounded read.
- Community Rust CI format ve workspace compile kapisidir. .NET CI mevcut Community davranis referansini dogrulamaya devam eder.

Bu parcalar tek basina install/update runtime'i veya imza guveni saglamaz. CMS dogrulamasi, external trust store, strict UTF-8/YAML parse, artifact transfer/hash/Authenticode, providerlar, state/journal, Windows adapterlari, workflow use-case'leri, CLI, WinUI ve paketleme henuz tasinmadi.

## Migration order

1. Saf Domain degerlerini port edip serialized contract'i koru.
2. Manifest bounded input, detached CMS/PKCS#7 signature, external signer trust ve strict YAML parse zincirini port et.
3. Release providerlari, artifact exact-size transferi, SHA-256 ve Windows Authenticode dogrulamasini port et.
4. Install state, package-scoped operation lock, crash journal, recovery ve process adapterlarini port et.
5. Install/update/repair/rollback/uninstall ve prerequisite workflow'lerini port et.
6. x64 ve ARM64 davranisini koruyarak Windows API'lerini port et.
7. CLI ve NativeAOT bootstrap davranisini port et.
8. WinUI akislarini Rust-native Windows UI adapteriyla karsila.
9. .NET uygulamasini kaldirmadan once signing, paired packaging, clean VM startup ve install/update/repair/rollback/uninstall parity'sini kanitla.

Rust portu tamamlanmis, release-ready veya mevcut .NET runtime'in yerine gecmis degildir.
