<!--
Dosya Yolu: REPO_ROOT/rust/SETUP_BUILDER.md
Amac: Windows Setup Studio proje panelini, modal editorlerini ve derleme akisini anlatmak
Modul - Markdown
Version: 0.3.0
Aciklama: Setup Studio panelini, modal editorleri ve dagitim akisini belgeler
Bagimli Oldugu Katman: View | Service | Documentation
-->

# Turkuaz Setup Studio

Turkuaz Setup Studio, uygulama dosyalarindan Turkuaz .tzpkg paketi, setup tema ayarlari ve imzali yayin girdilerinden dagitim cifti hazirlayan Windows masaustu aracidir. Ana ekranda solda bolum menusu, merkezde proje ozeti ve sagda son kullanici setup onizlemesi bulunur. Bolum secimleri formu arka plani kilitleyen modal pencerede acar. Uygulama gorunumu secenegi yalnizca Setup Studio penceresini etkiler; setup tema preset'i, marka adi, alt baslik ve vurgu rengi son kullanici kurulum sihirbazina uygulanir.

## Windows GUI derlemesi

Rust workspace kokunden calistirin:

    cargo build --manifest-path rust/Cargo.toml -p turkuaz-installer-cli --release --features setup-authoring-gui

Olusan dosya:

    rust/target/release/turkuaz-installer.exe

Argumansiz acildiginda Setup Studio penceresini gosterir. Windows CI, ayni dosyanin surumlu turkuaz-setup-builder-vX.Y.Z-windows-x64.exe gelistirme artifact'ini de uretir.

## Proje calisma alani

Ana ekranda solda numarali proje adimlari, ortada tek sutunlu ve kaydirilabilir duzenleme bolumleri, sagda son kullanici setup onizlemesi yer alir. Ust kisimdaki Proje ac ve Kaydet eylemleri, alt kisimdaki once paket hazirla ve sonra imzali setup olustur eylemlerinden ayri tutulur. Ilk acilista Projeye basla veya Uygulama dosyalarini sec eylemleri yeni kullaniciyi yonlendirir. Bir bolum secildiginde arka plani kilitleyen modal editor acilir. Modal icindeki onceki/sonraki bolum dugmeleriyle ilerlenebilir. Studio arayuzu Sistem/Acik/Koyu ayariyla kendi paletini degistirir; sagdaki onizlemenin Acik/Koyu secimi ise yalnizca olusturulacak setup temasinin onizlemesini etkiler. Bu iki gorunum ayari birbirine karistirilmaz.

- Urun bilgileri: uygulama adi, paket kimligi, SemVer surumu ve stable/beta kanali.
- Uygulama dosyalari: kaynak klasoru ve istege bagli goreli EXE yolu.
- Setup tasarimi: varsayilan kurulum yolu, hazir tema preset'i, marka adi, alt baslik ve istege bagli HEX vurgu rengi.
- Yayin ve cikti: cikti klasoru, imzali manifest, detached imza, yayinci trust store'u, setup GUI stub'i ve istege bagli makensis.exe yolu.

Uygulama paketi ve imzali dagitim cifti ana ekrandaki eylemlerden baslatilir. Gerekli bilgiler eksikse ilgili modal editor otomatik acilir.

Projeler .tzproj uzantisiyla kaydedilip Setup Studio icinde tekrar acilabilir. Her uygulama paketi derlemesi yeni bir {package-id}-{version} cikti klasoru olusturur; var olan klasorun ustune yazmaz. Imzali dagitim ciftini hazirlama adimi ayni klasordeki package ve theme dosyalarini kullanir ve yeni bir distribution alt klasoru olusturur.

## Derleme ciktisi

- application.tzpkg: kaynak klasorundeki payload'in Turkuaz paket bicimi.
- release-manifest.draft.yaml: paket boyutu ve SHA-256 ozeti hesaplanmis, sema denetiminden gecmis imzasiz manifest taslagi. Taslaktaki artifact URI yerel file: URI'sidir.
- setup-theme.json: secilen preset ve varsa marka/renk ozellestirmesi.
- project.tzproj: proje alanlarinin yeniden acilabilir kopyasi.
- distribution/TurkuazSetup.exe: imzali release bundle iceren birincil setup.
- distribution/TurkuazInstallerBackup.exe: ayni release bundle icin zorunlu NSIS yedegi.
- distribution/manifest-trust.yaml: imzali manifesti dogrulayan yayin politikasi; setup ve NSIS EXE dosyalariyla birlikte tutulmalidir.

Manifest taslagi bir yayina guven kazandirmaz. Yerel file: URI'si yalnizca paketleme/yerel deneme icindir; uzak dagitim icin artifact HTTPS adresine yuklenmeli ve manifest bu adresle tekrar uretilmelidir. Dagitim cifti butonu, secilen yayin anahtariyla imzali manifesti dis trust policy ile dogrular, package ve setup temasiyla gecici .tzsetup bundle uretir ve bunu setup GUI stub ile birlestirir. NSIS yedegi de ayni bundle'dan olusur. Uretilen iki EXE dagitilmadan once Authenticode ile imzalanmalidir. manifest-trust.yaml genel dogrulama politikasidir ve dagitim klasorunde setup dosyalarinin yaninda kalmalidir. Signing key ve trust policy Setup Studio projesine veya .tzpkg dosyasina gomulmez; imza/trust dosyalari .tzproj icinde yalnizca dosya yolu olarak saklanir. Eski .tzproj dosyalari yeni alanlar bos birakilarak acilir. RELEASE_SIGNING.md, CLI.md ve SMOKE_TEST.md imzalama akisinin ayrintilarini aciklar.
