<!--
Dosya Yolu: REPO_ROOT/rust/README.md
Amac: TurkuazInstaller Rust gecisinin kapsam ve dogrulama kapilarini aciklamak
Modul - Markdown
Version: 1.0.0
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

- `domain`: package id, SemVer, SHA-256 digest, release channel, artifact/signature, prerequisite, Windows integration, release policy, install/update/repair/rollback/uninstall plan modelleri ve package-scoped manifest signer trust policy.
- `metadata`: manifest/signature icin 1 MiB / 256 KiB limitler, Content-Length kontrolu ve stream sirasinda bounded read.
- `artifact-verification`: staged artifact stream'i icin imzali descriptor'daki tam byte boyutu ve SHA-256 kontrolu; asiri uzun stream'de erken kesme.
- `artifact-transfer`: HTTPS ve `file:` artifactlerini benzersiz staging dizinine stream eder; HTTPS redirect'lerinde HTTPS disina cikmaz, imzali boyut ust sinirini erkenden uygular, exact size kontroluyle `.partial` dosyayi basarili aktarim sonunda atomik adlandirir. Bu crate artifact'i apply etmez.
- `authenticode-verification`: Windows WinTrust ile embedded Authenticode imzasini, whole-chain revocation'i (root haric), publisher subject'i ve varsa certificate SHA-256 pinini dogrular; signer sertifikasi CryptoAPI ile imzali dosyadan alinir. Uygulama akisi bu dogrulayiciye henuz baglanmadi.
- `manifest-verification`: Windows CryptoAPI ile detached CMS/PKCS#7 imzasini dogrulama; tek signer, sertifika gecerlilik zamani, trust policy subject eslesmesi ve sertifika SHA-256 pini kontrolu. Sertifika zinciri dogrulamasi yapilmaz; mevcut .NET sozlesmesiyle ayni signature-only guven modelini izler.
- `manifest-trust-store`: dis YAML trust store'da schema version 1, package id, tekrar eden kayit ve package signer policy denetimi; olmayan dosyada policy bulunamadi sonucu.
- `manifest-pipeline`: manifest ve imza stream'lerini 1 MiB / 256 KiB sinirinda okur, requested package trust policy'sini bulur, CMS imzasini dogrular ve yalnizca strict UTF-8 decode sonrasinda YAML parserini calistirir; sonuc typed `PackageRelease` domain modelidir.
- `manifest-parser`: schema v1 YAML alanlarini domain invariant'lariyla eslestirir; bilinmeyen alanlari yok sayar ve taninmayan channel, install mode, shortcut location veya signature algorithm degerlerini reddeder.
- Community Rust CI Linux format/workspace compile ve Windows workspace compile kapilarini calistirir. .NET CI mevcut Community davranis referansini dogrulamaya devam eder.

Artifact ancak SHA-256 ve Authenticode dogrulamalari basarili olduktan sonra apply edilebilir; bu iki verifier runtime akisina henuz baglanmadi. Release providerlari, state/journal, Windows adapterlari, workflow use-case'leri, CLI, WinUI ve paketleme henuz tasinmadi. Manifest trust policy domain modeli, dis YAML trust-store repository'si ve Windows CMS verifier mevcuttur. Strict UTF-8/YAML parse ve typed domain model donusumu da verified pipeline icindedir. Bu parcalar tek basina install/update runtime'i saglamaz.

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
