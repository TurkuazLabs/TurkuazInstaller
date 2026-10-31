<!--
Dosya Yolu: REPO_ROOT/rust/SETUP_BUILDER.md
Amac: Windows Setup Studio proje panelini, modal editorlerini ve derleme akisini anlatmak
Modul - Markdown
Version: 0.6.0
Aciklama: Setup Studio panelini, modal editorleri ve dagitim akisini belgeler
Bagimli Oldugu Katman: View | Service | Documentation
-->

# Turkuaz Setup Studio

Turkuaz Setup Studio, uygulama dosyalarindan Turkuaz .tzpkg paketi, setup tema ayarlari ve imzali yayin girdilerinden dagitim ciktilari hazirlayan Windows masaustu aracidir. Acilista IDE benzeri bir karsilama ekrani gorunur. Yeni Tam Kurulum veya Cevrimici Kurulum projesi secildiginde dort adimli modal proje sihirbazi acilir. Secim .tzproj icerisinde saklanir. Eski projeler varsayilan Tam Kurulum turuyle uyumlu acilir. Calisma alaninda solda bolum menusu, merkezde proje ozeti ve sagda son kullanici setup onizlemesi bulunur. Uygulama gorunumu secenegi yalnizca Setup Studio penceresini etkiler; setup tema preset'i, marka adi, alt baslik ve vurgu rengi son kullanici kurulum sihirbazina uygulanir.

## Kullaniciya gorunen adlandirma

Studio karsilama ekraninda proje turleri **Tam Kurulum** ve **Cevrimici Kurulum** olarak gorunur. Teknik literaturdeki *Full Installer* ve *Web Installer* ifadeleri UI ana basligi olarak kullanilmaz. Proje secimi kaydedilen `.tzproj` dosyasinda aynen `offline` ve `web` teknik kimlikleriyle korunur; dosya bicimi veya CLI komutlari yeniden adlandirilmaz.

Sihirbazda **Kurulum tasarimi** (eski Setup tasarimi), **Kurulum motoru (EXE)** (eski GUI stub), **Yayinci guven dosyasi** (eski trust store), **Imzali yayin dosyasi (YAML)** (eski signed manifest), **Yayin imza dosyasi** (eski detached signature) ve **Uygulama paketi** (eski payload paketi) adlari tercih edilir. `application.tzpkg`, `TurkuazWebSetup.exe`, `manifest-trust.yaml` gibi gercek dosya adlari ve WebSetup CLI arayuzu degismez. NSIS icin kullanici etiketi **Yedek yukleyici araci (NSIS)** olur.

## Windows GUI derlemesi

Rust workspace kokunden calistirin:

    cargo build --manifest-path rust/Cargo.toml -p turkuaz-installer-cli --release --features setup-authoring-gui

Olusan dosya:

    rust/target/release/turkuaz-installer.exe

Argumansiz acildiginda Setup Studio penceresini gosterir. Windows CI, ayni dosyanin surumlu turkuaz-setup-builder-vX.Y.Z-windows-x64.exe gelistirme artifact'ini de uretir.

## Proje calisma alani

Ilk acilista iki proje sablonu yer alir: Tam Kurulum (tum dosyalari iceren offline paket) veya Cevrimici Kurulum (HTTPS uzerinden indiren kucuk EXE). Secince modal sihirbaz acilir. Eski .tzproj dosyasi Proje ac ile yuklenebilir. Yeni proje secimi mevcut calisma verisini degistirecekse onay modal'i acilir; baslangica donuldugunde acik projeye geri donulebilir. Proje calisma alaninda solda numarali adimlar, ortada tek sutunlu duzenleme kartlari, sagda son kullanici setup onizlemesi bulunur. Modal sihirbaz urun bilgileri, uygulama dosyalari, setup tasarimi ve yalnizca secilen tipe ait yayin/cikti alanlarini gosterir. Alt kisimda her iki tur icin 1 Uygulama paketini hazirla ve yalnizca secilen tipe ait 2 Tam Kurulum olustur veya 2 Cevrimici yukleyici olustur dugmeleri yer alir. Studio arayuzu Sistem/Acik/Koyu ayariyla kendi paletini degistirir; sagdaki onizlemenin Acik/Koyu secimi ise olusturulacak setup temasinin onizlemesini etkiler.

- Urun bilgileri: uygulama adi, paket kimligi, SemVer surumu ve stable/beta kanali.
- Uygulama dosyalari: kaynak klasoru ve istege bagli goreli EXE yolu.
- Setup tasarimi: varsayilan kurulum yolu, hazir tema preset'i, marka adi, alt baslik ve istege bagli HEX vurgu rengi.
- Cevrimdisi dagitim (Tam Kurulum): cikti klasoru, imzali manifest, detached imza, yayinci trust store'u, setup GUI stub'i ve istege bagli makensis.exe yolu.
- Cevrimici yayin (Cevrimici Kurulum): cikti klasoru, HTTPS manifest/imza adresleri, yayinci trust store'u ve setup GUI stub'i.

Uygulama paketi ve imzali dagitim cifti ana ekrandaki eylemlerden baslatilir. Gerekli bilgiler eksikse ilgili modal editor otomatik acilir.

Projeler .tzproj uzantisiyla kaydedilip Setup Studio icinde tekrar acilabilir. Her uygulama paketi derlemesi, 04 Yayin ve cikti bolumunde secilen cikti ust klasoru altinda yeni bir {package-id}-{version} klasoru olusturur; var olan klasorun ustune yazmaz. Uygulama dosyalari bolumundeki kaynak klasoru cikti konumu degildir. Ana ekrandaki Olusturulan dosyalar paneli hedef klasoru ve gercekten mevcut application.tzpkg ile distribution/TurkuazSetup.exe dosyalarinin durumunu ayri gosterir. Cikti klasorunu ac dugmesi Windows Dosya Gezgini ile mevcut klasoru acar; EXE dosyasini calistirmaz. Ilk adim yalnizca .tzpkg dosyasi olusturur, kurulum EXE'si degildir. Imzali dagitim ciftini hazirlama adimi ayni klasordeki package ve theme dosyalarini kullanir ve yeni bir distribution alt klasoru olusturur.

## WebSetup EXE cikarmak

Yeni **Cevrimici Kurulum** projesi secilir. Modal sihirbazin **04 Cevrimici yayin** bolumunde **Imzali yayin manifesti HTTPS adresi** ve **Detached imza HTTPS adresi** girilir. Yayinci trust store (public policy) ve Windows setup GUI stub'i secilmis olmalidir. Once **1 Uygulama paketini hazirla**, sonra **2 Cevrimici yukleyici olustur** kullanilir. Studio, ayni versiyonlu proje klasorunun `web/TurkuazWebSetup.exe` dosyasina bir Windows WebSetup stub'i uretir.

WebSetup dosyasi .tzpkg uygulama verisini tasimaz. Icinde paket kimligi, imzali manifest ve imza HTTPS adresleri, hedef kurulum klasoru, secili setup temasi ve **public** yayinci trust store yer alir. Yayin private key'i, imzalama parolasi, kimlik dogrulama bilgisi veya paket verisi EXE icine eklenmez. Bu nedenle EXE genellikle full installer'dan kucuktur. Tek dosyadir; kullanicidan manuel manifest URL'si veya ayri trust-store dosyasi istemez.

Kurulum sihirbazi, gomulu yayini kullanip mevcut CLI HTTPS indirme ve detached CMS, SHA-256 ve istege bagli Authenticode dogrulamasindan gecirir. URL'ler HTTPS olmali; HTTP, credentials, fragment reddedilir. Gomulu ayar ve theme bounded JSON ile SHA-256 footer kullanir; bu digest yayinci imzasi **degildir**. **WebSetup.exe dagitimdan once Authenticode ile imzalanmalidir.** EXE ve imzali release ayni yayinciya ait olmali; guven zinciri VM testinde kontrol edilir.

Windows CI, imzasiz bir WebSetup test EXE'si olusturup config'i geri okur ve byte degistirilmis config'i reddeder. CI ornegi `.test` URL'si tasir, bu nedenle gercek bir sunucudan indirme yapmaz. Manuel temiz Windows VM'de gercek imzali HTTPS yayin test edilmedikce production hazir sayilmaz.

Komut satiri ayni output'u `build-web-setup --stub GUI.exe --package-id ID --manifest-url HTTPS_URL --signature-url HTTPS_URL --trust-store trust.yaml --target DIR --theme-config setup-theme.json --output WebSetup.exe` ile uretebilir. `web-setup-info --file WebSetup.exe` config'i ozetler (trust policy'yi yazdirmaz).

## Derleme ciktisi

- application.tzpkg: kaynak klasorundeki payload'in Turkuaz paket bicimi.
- release-manifest.draft.yaml: paket boyutu ve SHA-256 ozeti hesaplanmis, sema denetiminden gecmis imzasiz manifest taslagi. Taslaktaki artifact URI yerel file: URI'sidir.
- setup-theme.json: secilen preset ve varsa marka/renk ozellestirmesi.
- project.tzproj: proje alanlarinin yeniden acilabilir kopyasi.
- distribution/TurkuazSetup.exe: imzali release bundle iceren birincil setup.
- distribution/TurkuazInstallerBackup.exe: ayni release bundle icin zorunlu NSIS yedegi.
- distribution/manifest-trust.yaml: imzali manifesti dogrulayan yayin politikasi; setup ve NSIS EXE dosyalariyla birlikte tutulmalidir.

Manifest taslagi bir yayina guven kazandirmaz. Yerel file: URI'si yalnizca paketleme/yerel deneme icindir; uzak dagitim icin artifact HTTPS adresine yuklenmeli ve manifest bu adresle tekrar uretilmelidir. Dagitim cifti butonu, secilen yayin anahtariyla imzali manifesti dis trust policy ile dogrular, package ve setup temasiyla gecici .tzsetup bundle uretir ve bunu setup GUI stub ile birlestirir. NSIS yedegi de ayni bundle'dan olusur. Uretilen iki EXE dagitilmadan once Authenticode ile imzalanmalidir. manifest-trust.yaml genel dogrulama politikasidir ve dagitim klasorunde setup dosyalarinin yaninda kalmalidir. Signing key ve trust policy Setup Studio projesine veya .tzpkg dosyasina gomulmez; imza/trust dosyalari .tzproj icinde yalnizca dosya yolu olarak saklanir. Eski .tzproj dosyalari yeni alanlar bos birakilarak acilir. RELEASE_SIGNING.md, CLI.md ve SMOKE_TEST.md imzalama akisinin ayrintilarini aciklar.
