<!--
Dosya Yolu: REPO_ROOT/rust/SETUP_BUILDER.md
Amac: Windows Setup Studio ile paket projesi olusturma ve derleme akisini anlatmak
Modul - Markdown
Version: 0.1.0
Aciklama: Setup Builder sihirbazinin girdilerini, proje dosyasini ve imzalama sinirini belgeler
Bagimli Oldugu Katman: View | Service | Documentation
-->

# Turkuaz Setup Studio

Turkuaz Setup Studio, uygulama dosyalarindan Turkuaz .tzpkg paketi, setup tema ayarlari ve imzali yayin girdilerinden dagitim cifti hazirlayan Windows masaustu aracidir. Baslangic akisi InstallShield Project Assistant benzeri yonlendirmeli adimlardan olusur; son kullaniciya gosterilecek setup icin canli tema onizlemesi sunar. Uygulama gorunumu secenegi yalnizca Setup Studio penceresini etkiler; 3. adimdaki setup preset'i, marka adi, alt baslik ve vurgu rengi son kullanici kurulum sihirbazina uygulanir.

## Windows GUI derlemesi

Rust workspace kokunden calistirin:

    cargo build --manifest-path rust/Cargo.toml -p turkuaz-installer-cli --release --features setup-authoring-gui

Olusan dosya:

    rust/target/release/turkuaz-installer.exe

Argumansiz acildiginda Setup Studio penceresini gosterir. Windows CI, ayni dosyanin surumlu turkuaz-setup-builder-vX.Y.Z-windows-x64.exe gelistirme artifact'ini de uretir.

## Sihirbaz adimlari

1. Uygulama adini, paket kimligini, SemVer surumunu ve stable/beta kanalini girin.
2. Uygulama dosyalarinin klasorunu secin. Isterseniz kaynak klasoru icindeki goreli EXE yolunu belirleyin.
3. Varsayilan kurulum yolunu, setup tema preset'ini, marka adini, alt basligi ve istege bagli HEX vurgu rengini duzenleyin. Onizleme son kullanici setup penceresinin acik paletini gosterir.
4. Cikti ust klasorunu secip uygulama paketini olusturun. Yayin manifestini imzaladiktan sonra imzali manifesti, detached imzayi, yayinci trust store'u ve Windows setup GUI stub dosyasini secerek dagitim ciftini olusturun. NSIS PATH uzerinde degilse makensis.exe konumunu belirtin.

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
