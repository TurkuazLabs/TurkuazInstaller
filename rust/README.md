<!--
Dosya Yolu: REPO_ROOT/rust/README.md
Amac: TurkuazInstaller Rust gecisinin kapsam ve dogrulama kapilarini aciklamak
Modul - Markdown
Version: 2.3.19
Aciklama: Aktif Community Rust kaynak kodu ve kabul sinirlari
Bagimli Oldugu Katman: Documentation
-->

# Rust Migration

Bu workspace `rewrite/rust-community` dalinin aktif uygulama kodudur.
Rust kaynak kodu ve testleri tek workspace altinda gelistirilir.
Fonksiyonel testler production GUI ve imzali kurulum kabulunun yerine gecmez.

## Kanitlanmis Rust CI siniri

`rust/Cargo.lock` kaynak denetimindedir ve Linux/Windows CI cargo
check/build/test/Clippy kapilari `--locked` ile calisir. Sabit `203192d1` revizyonundaki
[CI 41342571609](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/41342571609)
iki platformda **PASS**. Bu gelistirme kanitidir; gercek temiz Windows
masaustunde GUI tiklama veya production imza kabulu degildir. Belge
navigasyonu icin [dokumantasyon dizinine](../docs/README.md) bakin.

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
- `setup-authoring-gui`: Windows Setup Studio opens a guided project wizard for package identity, payload directory, optional entry point, target folder, and end-user setup appearance. It saves and reopens bounded schema-versioned `.tzproj` documents, previews the selected setup theme, and produces an atomic output folder containing `.tzpkg`, a validated unsigned manifest draft, and theme JSON. A signed manifest and external publisher trust policy remain required for a distributable setup executable; see [SETUP_BUILDER.md](SETUP_BUILDER.md).
- `setup-bundle`: Signed manifest, detached signature, and the verified `.tzpkg` are stored in a bounded offline `.tzsetup` archive. The CLI can create it and install, update, repair, or policy-authorized rollback from it; the external signer trust policy is checked at install time and never embedded. A command-line self-extracting `.exe` can also be built from a CLI stub and bundle; extraction supports a final Authenticode certificate table appended after its payload footer. `build-distribution-pair` stages the primary Turkuaz setup and mandatory NSIS backup together from that bundle; the NSIS fallback offers install, update, repair, and policy-authorized rollback choices. A Windows `setup-gui` build opens a four-step Turkish desktop wizard for operation, package and publisher trust, install/data paths, and final review. The signed HTTPS online source can now be selected in the wizard alongside the original embedded offline bundle; online install/update/repair/rollback use the existing verified CLI workflow and require explicit HTTPS manifest/signature URLs plus the external publisher trust policy. Setup Studio can also generate an unsigned, preconfigured one-file WebSetup EXE carrying bounded, hash-checked HTTPS URLs, theme and public publisher trust policy (without the app payload). The GUI uses the existing verified HTTPS CLI; Windows CI validates config read-back and tamper rejection. Production Authenticode signing and clean-VM end-to-end online download acceptance are not yet complete. Operation choices appear as selectable cards; package IDs and required trust files are validated before continuing, and folders can be picked in native dialogs. Uninstall hides the trust-policy input and requires a separate confirmation on the review step. Every operation runs the existing CLI engine as a child process with an honest indeterminate working state; canceling stops the backend and attempts journal recovery. The default CLI artifact remains console based. Real signed-package VM acceptance, production Authenticode signing, and automated publishing remain pending.
- `deployment-activation`: Staged payload'i immutable `versions/<version>` dizinine kopyalar; `integrity/<deployment-key>.json` envanterinde dosya boyutu ve SHA-256 kaydeder; `active.json` isaretcisini atomik degistirir ve etkin/onceki surum entrypoint'lerini saklar. Onarimda mevcut surume yeni bir immutable klasor hazirlayip etkinlestirir. `verify_current` tum dosyalari kayitli envanterle karsilastirir. Envanter yerel tutarlilik kontroludur, kriptografik guven koku degildir. Ayrintilar: [DEPLOYMENT_LAYOUT.md](DEPLOYMENT_LAYOUT.md).
- `install-workflow`: Package lock altinda `VerifiedArtifact` eslesmesini kontrol eder, ilk install veya ileri SemVer update icin paketi stage eder, etkin deployment'i acar ve installed state'i yazar. Imzali ayni surum artifact'iyle onarim yeni immutable deployment hazirlayip etkin isaretciyi atomik gunceller. Ayni servis package lock ve journal altinda yalnizca bu paketin deployment klasorunu kaldiran recoverable uninstall akisini da saglar. State yazimi basarisizsa etkin surumu telafi eder; current signed release policy izin verirse bir onceki etkin surume rollback yapar. Sinirlar: [INSTALL_WORKFLOW.md](INSTALL_WORKFLOW.md).
- `installer-cli`: HTTPS manifest/signature okuma, signer policy dogrulamasi, release artifact download/hash/imza kontrolu, install/update/repair/rollback, kurulu paket listesi, envanter dogrulamasi, explicit interrupted-operation recovery ve kullanicinin verdigi pakette beyan edilen entrypoint ile uygulama baslatma komutlarini saglar; eski v1 paketlerde acik exe yolu alabilir. Yeni signed release/artifact dogrulanmis online veya offline bundle islemi baslamadan once bekleyen journal varsa ayni konservatif recovery akisini otomatik cagirir. Ayrica bilinen paket ID/hedef yolu oldugunda `launch`, `verify`, `uninstall` ve `list` oncesinde pending journal kontrol edilir ve guvenli recovery denenir. Argumansiz CLI/GUI acilisinda varsayilan data root icindeki kanonik journal dosyalari taranir ve konservatif recovery denenir; bozuk veya belirsiz kayit CLI'yi durdurur ya da GUI'yi bloklar. Ozel data root, secilen paket islemi oncesinde per-package preflight ile denetlenir; belirsiz first-install durumlari fail-closed birakilir. `.tzpkg` source builder'i ve paketin tam boyut/SHA-256 degerlerinden unsigned release manifest taslagi ureten `describe-package` komutu da CLI'dedir. Taslak imzalama anahtari tasimaz ve imzasizdir; detached CMS release imzalama ve dis trust policy akisi [RELEASE_SIGNING.md](RELEASE_SIGNING.md) icinde belgelenmistir. v2 paketleri entrypoint tasir; v1 paketleri geriye uyumluluk icin acik exe yolu ile calisir. Komut ve klasor sozlesmesi: [CLI.md](CLI.md). Her yeni degisiklik CI'dan gecmeden production setup sayilmaz.
- `operation-lock`: package ID basina OS advisory file lock alir; ayni paketin ikinci process'teki operation'ini fail-fast reddeder, farkli paketlerin islemleri birbirinden bagimsizdir. Process ID ve UTC unix timestamp'i kilit dosyasinda tanilama icin tutulur. Lease dusurulunce lock serbest kalir; dosya inode race'ini onlemek icin bos dosya olarak birakilir.
- `operation-journal`: typed operation/phase checkpoint'lerini package basina JSON dosyasinda saklar. 1 MiB okuma siniri ve package ID / enum deger dogrulamasi uygular; guncellemeyi ayni dizindeki gecici dosyayi sync edip rename ederek yazar. Yayimlanmis PascalCase alan adlari ve numeric enum sozlesmesi korunur.
- `install-state-store`: kurulu paket versiyon/channel/target state'ini package basina JSON'da tutar; bounded okuma, deterministik listeleme, typed deger dogrulamasi ve atomik replace uygular. Yayimlanmis PascalCase property ve numeric ReleaseChannel sozlesmesi korunur.
- `operation-context`: operation start/resume, phase degisimi, reboot checkpoint arm/await/clear, completion, cancel ve fail gecislerini journal'a yazar. Resume icin package/op/version/target/prerequisite eslesmesini ve reboot-safe phase'i zorunlu tutar. Ilk install ve ileri update akisi bu context'i kullanir. Journal kurtarma, CLI'nin ayri recover komutunda uygulanir; operation-context kendi basina recovery runner saglamaz.
- `authenticode-verification`: Windows WinTrust ile embedded Authenticode imzasini, whole-chain revocation'i (root haric), publisher subject'i ve varsa certificate SHA-256 pinini dogrular; signer sertifikasi CryptoAPI ile imzali dosyadan alinir.
- `manifest-verification`: Windows CryptoAPI ile detached CMS/PKCS#7 imzasini dogrulama; tek signer, sertifika gecerlilik zamani, trust policy subject eslesmesi ve sertifika SHA-256 pini kontrolu. Sertifika zinciri dogrulamasi yapilmaz; mevcut signature-only guven modelini izler.
- `manifest-trust-store`: dis YAML trust store okumasini 1 MiB ile sinirlar, yalniz regular file kabul eder, okuma sirasinda boyut degisikligini reddeder; schema version 1, package id, tekrar eden kayit ve package signer policy denetimi yapar; olmayan dosyada policy bulunamadi sonucu.
- `manifest-pipeline`: manifest ve imza stream'lerini 1 MiB / 256 KiB sinirinda okur, requested package trust policy'sini bulur, CMS imzasini dogrular ve yalnizca strict UTF-8 decode sonrasinda YAML parserini calistirir; sonuc typed `PackageRelease` domain modelidir.
- `manifest-parser`: schema v1 YAML alanlarini domain invariant'lariyla eslestirir; bilinmeyen alanlari yok sayar ve taninmayan channel, install mode, shortcut location veya signature algorithm degerlerini reddeder.
- Community Rust CI format, Clippy (uyarilar hata sayilir), workspace testleri, Linux/Windows compile, NSIS validation ve surumlenmis Windows x64 CLI/GUI setup stub/Setup Studio artifact'larini dogrular; artifact'lar yedi gun saklanir. CLI surumunu `--version` veya `-V` ile, GUI surumunu pencere basliginda gosterir. Bunlar development artifact'laridir; dagitilabilir Turkuaz setup ya da production release degildir.

Rust gecisi halen migration foundation'dir; production veya end-user setup hazirliginda degildir ve production kabulunu kanitlamaz. CLI signed online install/update/repair/rollback, kurulu state listeleme/dogrulama/baslatma/uninstall, signed offline bundle ve self-extracting setup uretim akislarini icerir; no-argument CLI/GUI acilisi varsayilan data root journal'larini tarayip konservatif recovery dener. Ozel data root secilen paket isleminde denetlenir. Prerequisite ve Windows integration adapter'lari, production Authenticode signing/publishing ve gercek imzali paketlerle temiz Windows VM lifecycle kabul testi acik kalir. Windows CI gecici self-signed test sertifikasi ile detached CMS dogrulamasini, offline install/update/repair/rollback/launch/uninstall akisini ve Authenticode sertifikasi eklenmis self-extracting primary setup testini calistirir. Production yayinlayici sertifikasi ve temiz son kullanici VM kabul testi halen gereklidir.

## Windows smoke test

Gelistirme artifact'ini gercek imzali release girdileriyle temiz bir Windows VM'de denemek icin [SMOKE_TEST.md](SMOKE_TEST.md) adimlarini izleyin. Bu kabul akisi henuz calistirilmis degildir; CI derleme sonucu VM kurulum kabulunun yerine gecmez.

## Guncel Rust kabul sirasi

Asagidaki adimlar yapilmis Rust kodunu yeniden port etme plani degildir;
gercek production kabulune kalmis islerdir. Durum/kanit
[RUST_READINESS.md](RUST_READINESS.md) icindedir.

1. Windows prereq, shortcut/protocol, preserve_paths ve data migration
   adapter'larini Rust runtime'a yaz ve security/contract testlerini ekle.
2. Setup Studio, wizard, kurulum/onarma/kaldirma icin gercek Windows
   pencere, tus/tiklama ve ayrintili kullanici kabulunu yap.
3. Windows 10/11 temiz VM, yanlis signature/hash, yol kacisi, ag
   kesintisi, kesinti recovery ve upgrade/rollback E2E'yi kanitla.
4. Gercek publisher signing, timestamp, trust chain/revocation,
   artifact provenance ve antivirus/SmartScreen olaylarini dogrula.
5. Commit edilmis Cargo.lock ve Linux/Windows --locked CI kapilarini koru;
   degisen her commit'in sonucunu ayri dogrula, TEST artifact'larini
   production release'den ayir.
6. NSIS yedek yukleyicisinin yerine guvenli Rust recovery/fallback
   alternatifi hazirlamadan tum dagitimin %100 Rust oldugunu iddia etme.
7. Pro runtime entitlement, credentials ve signed rollout baglantisini
   Community install guven sinirini bozmadan bagla.

Bu gelistirme dali signed production release kabulunu henuz tamamlamamistir.
