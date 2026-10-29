<!--
Dosya Yolu: REPO_ROOT/rust/README.md
Amac: TurkuazInstaller Rust gecisinin kapsam ve dogrulama kapilarini aciklamak
Modul - Markdown
Version: 2.3.4
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
- `artifact-validation`: download → tam boyut/SHA-256 → metadata varsa Authenticode siralamasini uygular. Yalniz tum uygulanabilir kapilar basariliysa opaque `VerifiedArtifact` dondurur; dogrulama hatasinda indirilmis dosyayi ve bos staging klasorlerini temizler. Install workflow bu token'i dogrulanmis release icin staging, versioned activation ve kurulu-state kaydina kadar tuketiyor; headless CLI install/update/rollback komutlari bagli, optional Windows setup GUI ayni CLI engine'i child process olarak cagiriyor.
- `package-engine`: `.tzpkg` v1/v2 archive sozlesmesi, `turkuaz-package.json` release identity/version baglamasi, opsiyonel paket entrypoint'i, payload builder'i ve verified-artifact staging'i saglar. ZIP-slip, Windows path kurallari, buyuk/kucuk harf path cakismasi, duplicate entry, symlink/ozel dosya ve acilmis boyut/sayi sinirlari kontrol edilir. Ayrintilar: [PACKAGE_FORMAT.md](PACKAGE_FORMAT.md).
- `setup-bundle`: Signed manifest, detached signature, and the verified `.tzpkg` are stored in a bounded offline `.tzsetup` archive. The CLI can create it and install, update, repair, or policy-authorized rollback from it; the external signer trust policy is checked at install time and never embedded. A command-line self-extracting `.exe` can also be built from a CLI stub and bundle; extraction supports a final Authenticode certificate table appended after its payload footer. `build-distribution-pair` stages the primary Turkuaz setup and mandatory NSIS backup together from that bundle; the NSIS fallback offers install, update, repair, and policy-authorized rollback choices. A Windows `setup-gui` build opens a desktop wizard for package identity, publisher trust policy, install/data paths, and install/update/repair/rollback selection; it runs the existing CLI engine as a child process, shows an indeterminate working state, and can cancel that process, then attempts journal recovery. The default CLI artifact remains console based. Real signed-package VM acceptance, production Authenticode signing, and automated publishing remain pending.
- `deployment-activation`: Staged payload'i immutable `versions/<version>` dizinine kopyalar; `integrity/<deployment-key>.json` envanterinde dosya boyutu ve SHA-256 kaydeder; `active.json` isaretcisini atomik degistirir ve etkin/onceki surum entrypoint'lerini saklar. Onarimda mevcut surume yeni bir immutable klasor hazirlayip etkinlestirir. `verify_current` tum dosyalari kayitli envanterle karsilastirir. Envanter yerel tutarlilik kontroludur, kriptografik guven koku degildir. Ayrintilar: [DEPLOYMENT_LAYOUT.md](DEPLOYMENT_LAYOUT.md).
- `install-workflow`: Package lock altinda `VerifiedArtifact` eslesmesini kontrol eder, ilk install veya ileri SemVer update icin paketi stage eder, etkin deployment'i acar ve installed state'i yazar. Imzali ayni surum artifact'iyle onarim yeni immutable deployment hazirlayip etkin isaretciyi atomik gunceller. Ayni servis package lock ve journal altinda yalnizca bu paketin deployment klasorunu kaldiran recoverable uninstall akisini da saglar. State yazimi basarisizsa etkin surumu telafi eder; current signed release policy izin verirse bir onceki etkin surume rollback yapar. Sinirlar: [INSTALL_WORKFLOW.md](INSTALL_WORKFLOW.md).
- `installer-cli`: HTTPS manifest/signature okuma, signer policy dogrulamasi, release artifact download/hash/imza kontrolu, install/update/repair/rollback, kurulu paket listesi, envanter dogrulamasi, explicit interrupted-operation recovery ve kullanicinin verdigi pakette beyan edilen entrypoint ile uygulama baslatma komutlarini saglar; eski v1 paketlerde acik exe yolu alabilir. Yeni signed release/artifact dogrulanmis online veya offline bundle islemi baslamadan once bekleyen journal varsa ayni konservatif recovery akisini otomatik cagirir. Ayrica bilinen paket ID/hedef yolu oldugunda `launch`, `verify`, `uninstall` ve `list` oncesinde pending journal kontrol edilir ve guvenli recovery denenir. Yalniz GUI/CLI acilisinda tum journal dosyalarini taramaz; belirsiz first-install durumlari fail-closed birakilir. `.tzpkg` source builder'i ve paketin tam boyut/SHA-256 degerlerinden unsigned release manifest taslagi ureten `describe-package` komutu da CLI'dedir. Taslak imzalama anahtari tasimaz ve imzasizdir; detached CMS release imzalama ve dis trust policy akisi [RELEASE_SIGNING.md](RELEASE_SIGNING.md) icinde belgelenmistir. v2 paketleri entrypoint tasir; v1 paketleri geriye uyumluluk icin acik exe yolu ile calisir. Komut ve klasor sozlesmesi: [CLI.md](CLI.md). Her yeni degisiklik CI'dan gecmeden production setup sayilmaz.
- `operation-lock`: package ID basina OS advisory file lock alir; ayni paketin ikinci process'teki operation'ini fail-fast reddeder, farkli paketlerin islemleri birbirinden bagimsizdir. Process ID ve UTC unix timestamp'i kilit dosyasinda tanilama icin tutulur. Lease dusurulunce lock serbest kalir; dosya inode race'ini onlemek icin bos dosya olarak birakilir.
- `operation-journal`: typed operation/phase checkpoint'lerini package basina JSON dosyasinda saklar. 1 MiB okuma siniri ve package ID / enum deger dogrulamasi uygular; guncellemeyi ayni dizindeki gecici dosyayi sync edip rename ederek yazar. .NET journal'daki PascalCase alan adlari ve numeric enum sozlesmesi korunur.
- `install-state-store`: kurulu paket versiyon/channel/target state'ini package basina JSON'da tutar; bounded okuma, deterministik listeleme, typed deger dogrulamasi ve atomik replace uygular. .NET PascalCase property ve numeric ReleaseChannel sozlesmesi korunur.
- `operation-context`: operation start/resume, phase degisimi, reboot checkpoint arm/await/clear, completion, cancel ve fail gecislerini journal'a yazar. Resume icin package/op/version/target/prerequisite eslesmesini ve reboot-safe phase'i zorunlu tutar. Ilk install ve ileri update akisi bu context'i kullanir; recovery runner henuz yoktur.
- `authenticode-verification`: Windows WinTrust ile embedded Authenticode imzasini, whole-chain revocation'i (root haric), publisher subject'i ve varsa certificate SHA-256 pinini dogrular; signer sertifikasi CryptoAPI ile imzali dosyadan alinir.
- `manifest-verification`: Windows CryptoAPI ile detached CMS/PKCS#7 imzasini dogrulama; tek signer, sertifika gecerlilik zamani, trust policy subject eslesmesi ve sertifika SHA-256 pini kontrolu. Sertifika zinciri dogrulamasi yapilmaz; mevcut .NET sozlesmesiyle ayni signature-only guven modelini izler.
- `manifest-trust-store`: dis YAML trust store okumasini 1 MiB ile sinirlar, yalniz regular file kabul eder, okuma sirasinda boyut degisikligini reddeder; schema version 1, package id, tekrar eden kayit ve package signer policy denetimi yapar; olmayan dosyada policy bulunamadi sonucu.
- `manifest-pipeline`: manifest ve imza stream'lerini 1 MiB / 256 KiB sinirinda okur, requested package trust policy'sini bulur, CMS imzasini dogrular ve yalnizca strict UTF-8 decode sonrasinda YAML parserini calistirir; sonuc typed `PackageRelease` domain modelidir.
- `manifest-parser`: schema v1 YAML alanlarini domain invariant'lariyla eslestirir; bilinmeyen alanlari yok sayar ve taninmayan channel, install mode, shortcut location veya signature algorithm degerlerini reddeder.
- Community Rust CI Linux format/workspace compile ve Windows workspace compile kapilarini calistirir; Windows'ta development CLI ile setup-gui stub'ini ayri ayri derleyip yedi gun saklanan artifact'lar yukler. Bu binary Turkuaz setup veya production release degildir. .NET CI mevcut Community davranis referansini dogrulamaya devam eder.

Manifest intake typed Rust release modeline ulasir. Ozel paket v1 builder/staging ve versioned activation mevcut; ilk install/ileri update workflow'u bu katmanlari package lock, journal ve kurulu-state repository ile baglar. Release providerlari, startup recovery, prerequisite/Windows integration ve WinUI henuz tasinmadi; rollback service workflow'u ve imzali CLI komutu eklendi; yalnizca active kaydindaki bir onceki deployment'a doner. Development CLI source olarak eklendi. Rust Migration CI #614 Linux/Windows format ve compile kontrollerini, Windows CLI build/artifact upload adimini gecti; gelistirme CLI artifact'i 2026-11-03'e kadar indirilebilir. End-user acceptance henuz yapilmadi. Rust portu tamamlanmis, release-ready veya mevcut .NET runtime'in yerine gecmis degildir.

## Windows smoke test

Gelistirme artifact'ini gercek imzali release girdileriyle temiz bir Windows VM'de denemek icin [SMOKE_TEST.md](SMOKE_TEST.md) adimlarini izleyin. Bu kabul akisi henuz calistirilmis degildir; CI derleme sonucu VM kurulum kabulunun yerine gecmez.

## Migration order

1. Saf Domain degerlerini port edip serialized contract'i koru.
2. Manifest bounded input, detached CMS/PKCS#7 signature, external signer trust ve strict YAML parse zincirini port et.
3. Release providerlari ve artifact secim/indirme use-case'ini port et; mevcut exact-size transferi, SHA-256 ve Windows Authenticode dogrulamasini workflow'a bagla.
4. Mevcut package lock, journal, installed state repository ve operation context'i startup recovery orchestration'ina bagla.
5. `.tzpkg` v1 formatini release uretim akisina bagla; artifact size/hash ve manifest imzasini builder ciktisina gore olustur.
6. Ilk install/ileri update akisini repair/rollback/uninstall ve prerequisite workflow'leriyle tamamla; startup recovery ve crash-safe state reconciliation ekle.
7. x64 ve ARM64 davranisini koruyarak Windows API'lerini port et.
8. Mevcut development CLI'yi compile/acceptance kapilarindan gecirip secure bootstrap, portable data paths ve production command contract'ini tamamla; paired setup output'unu signing akisina bagla.
9. WinUI akislarini Rust-native Windows UI adapteriyla karsila.
10. .NET uygulamasini kaldirmadan once signing, paired packaging, clean VM startup ve install/update/repair/rollback/uninstall parity'sini kanitla.

Rust portu tamamlanmis, release-ready veya mevcut .NET runtime'in yerine gecmis degildir.
