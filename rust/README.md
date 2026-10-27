<!--
Dosya Yolu: REPO_ROOT/rust/README.md
Amac: TurkuazInstaller Rust gecisinin kapsam ve dogrulama kapilarini aciklamak
Modul - Markdown
Version: 2.1.0
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

- `domain`: package id, SemVer, SHA-256 digest, release channel, artifact/signature, prerequisite, Windows integration, release policy, install/update/repair/rollback/uninstall plan modelleri, kurulu package state ve package-scoped manifest signer trust policy.
- `metadata`: manifest/signature icin 1 MiB / 256 KiB limitler, Content-Length kontrolu ve stream sirasinda bounded read.
- `artifact-verification`: staged artifact stream'i icin imzali descriptor'daki tam byte boyutu ve SHA-256 kontrolu; asiri uzun stream'de erken kesme.
- `artifact-transfer`: HTTPS ve `file:` artifactlerini benzersiz staging dizinine stream eder; HTTPS redirect'lerinde HTTPS disina cikmaz, imzali boyut ust sinirini erkenden uygular, exact size kontroluyle `.partial` dosyayi basarili aktarim sonunda atomik adlandirir. Bu crate artifact'i apply etmez.
- `artifact-validation`: download → tam boyut/SHA-256 → metadata varsa Authenticode siralamasini uygular. Yalniz tum uygulanabilir kapilar basariliysa opaque `VerifiedArtifact` dondurur; dogrulama hatasinda indirilmis dosyayi ve bos staging klasorlerini temizler. Install workflow bu token'i dogrulanmis release icin staging, versioned activation ve kurulu-state kaydina kadar tuketiyor; headless CLI install/update/rollback komutlari bagli, runtime launch ve UI henuz yok.
- `package-engine`: `.tzpkg` v1 archive sozlesmesi, `turkuaz-package.json` release identity/version baglamasi, payload builder'i ve verified-artifact staging'i saglar. ZIP-slip, Windows path kurallari, buyuk/kucuk harf path cakismasi, duplicate entry, symlink/ozel dosya ve acilmis boyut/sayi sinirlari kontrol edilir. Ayrintilar: [PACKAGE_FORMAT.md](PACKAGE_FORMAT.md).
- `deployment-activation`: Staged payload'i immutable `versions/<version>` dizinine kopyalar; dosyalar sync edildikten sonra `active.json` isaretcisini ayni volume icinde atomik degistirir. Onceki surumu geri alma adayi olarak kaydeder ve mevcut surumleri ezmez. Ayrintilar: [DEPLOYMENT_LAYOUT.md](DEPLOYMENT_LAYOUT.md).
- `install-workflow`: Package lock altinda `VerifiedArtifact` eslesmesini kontrol eder, ilk install veya ileri SemVer update icin paketi stage eder, etkin deployment'i acar ve installed state'i yazar. State yazimi basarisizsa etkin surumu telafi eder; current signed release policy izin verirse bir onceki etkin surume rollback yapar. Sinirlar: [INSTALL_WORKFLOW.md](INSTALL_WORKFLOW.md).
- `installer-cli`: HTTPS manifest/signature okuma, signer policy dogrulamasi, release artifact download/hash/imza kontrolu, install/update workflow'u, signed policy ile rollback ve `.tzpkg` source builder'i icin ilk komut satiri giris noktasidir. Signed release manifest olusturma/imzalama henuz yoktur. Komut ve klasor sozlesmesi: [CLI.md](CLI.md). Development CLI henuz compile CI'dan gecmedi ve production setup degildir.
- `operation-lock`: package ID basina OS advisory file lock alir; ayni paketin ikinci process'teki operation'ini fail-fast reddeder, farkli paketlerin islemleri birbirinden bagimsizdir. Process ID ve UTC unix timestamp'i kilit dosyasinda tanilama icin tutulur. Lease dusurulunce lock serbest kalir; dosya inode race'ini onlemek icin bos dosya olarak birakilir.
- `operation-journal`: typed operation/phase checkpoint'lerini package basina JSON dosyasinda saklar. 1 MiB okuma siniri ve package ID / enum deger dogrulamasi uygular; guncellemeyi ayni dizindeki gecici dosyayi sync edip rename ederek yazar. .NET journal'daki PascalCase alan adlari ve numeric enum sozlesmesi korunur.
- `install-state-store`: kurulu paket versiyon/channel/target state'ini package basina JSON'da tutar; bounded okuma, deterministik listeleme, typed deger dogrulamasi ve atomik replace uygular. .NET PascalCase property ve numeric ReleaseChannel sozlesmesi korunur.
- `operation-context`: operation start/resume, phase degisimi, reboot checkpoint arm/await/clear, completion, cancel ve fail gecislerini journal'a yazar. Resume icin package/op/version/target/prerequisite eslesmesini ve reboot-safe phase'i zorunlu tutar. Ilk install ve ileri update akisi bu context'i kullanir; recovery runner henuz yoktur.
- `authenticode-verification`: Windows WinTrust ile embedded Authenticode imzasini, whole-chain revocation'i (root haric), publisher subject'i ve varsa certificate SHA-256 pinini dogrular; signer sertifikasi CryptoAPI ile imzali dosyadan alinir.
- `manifest-verification`: Windows CryptoAPI ile detached CMS/PKCS#7 imzasini dogrulama; tek signer, sertifika gecerlilik zamani, trust policy subject eslesmesi ve sertifika SHA-256 pini kontrolu. Sertifika zinciri dogrulamasi yapilmaz; mevcut .NET sozlesmesiyle ayni signature-only guven modelini izler.
- `manifest-trust-store`: dis YAML trust store'da schema version 1, package id, tekrar eden kayit ve package signer policy denetimi; olmayan dosyada policy bulunamadi sonucu.
- `manifest-pipeline`: manifest ve imza stream'lerini 1 MiB / 256 KiB sinirinda okur, requested package trust policy'sini bulur, CMS imzasini dogrular ve yalnizca strict UTF-8 decode sonrasinda YAML parserini calistirir; sonuc typed `PackageRelease` domain modelidir.
- `manifest-parser`: schema v1 YAML alanlarini domain invariant'lariyla eslestirir; bilinmeyen alanlari yok sayar ve taninmayan channel, install mode, shortcut location veya signature algorithm degerlerini reddeder.
- Community Rust CI Linux format/workspace compile ve Windows workspace compile kapilarini calistirir; Windows compile sonrasi yedi gun saklanan development CLI binary artifact'i yukler. Bu binary Turkuaz setup veya production release degildir. .NET CI mevcut Community davranis referansini dogrulamaya devam eder.

Manifest intake typed Rust release modeline ulasir. Ozel paket v1 builder/staging ve versioned activation mevcut; ilk install/ileri update workflow'u bu katmanlari package lock, journal ve kurulu-state repository ile baglar. Release providerlari, artifacti manifestten secip indirme use-case'i, startup recovery, prerequisite/Windows integration, WinUI, repair/uninstall henuz tasinmadi; rollback service workflow'u ve imzali CLI komutu eklendi; yalnizca active kaydindaki bir onceki deployment'a doner. Development CLI source olarak eklendi; CI compile ve end-user acceptance henuz bekliyor. Rust portu tamamlanmis, release-ready veya mevcut .NET runtime'in yerine gecmis degildir.

## Migration order

1. Saf Domain degerlerini port edip serialized contract'i koru.
2. Manifest bounded input, detached CMS/PKCS#7 signature, external signer trust ve strict YAML parse zincirini port et.
3. Release providerlari ve artifact secim/indirme use-case'ini port et; mevcut exact-size transferi, SHA-256 ve Windows Authenticode dogrulamasini workflow'a bagla.
4. Mevcut package lock, journal, installed state repository ve operation context'i startup recovery orchestration'ina bagla.
5. `.tzpkg` v1 formatini signed release uretim akisina bagla; artifact size/hash ve manifest imzasini builder ciktisina gore olustur.
6. Ilk install/ileri update akisini repair/rollback/uninstall ve prerequisite workflow'leriyle tamamla; startup recovery ve crash-safe state reconciliation ekle.
7. x64 ve ARM64 davranisini koruyarak Windows API'lerini port et.
8. Mevcut development CLI'yi compile/acceptance kapilarindan gecirip secure bootstrap, portable data paths ve production command contract'ini tamamla.
9. WinUI akislarini Rust-native Windows UI adapteriyla karsila.
10. .NET uygulamasini kaldirmadan once signing, paired packaging, clean VM startup ve install/update/repair/rollback/uninstall parity'sini kanitla.

Rust portu tamamlanmis, release-ready veya mevcut .NET runtime'in yerine gecmis degildir.
