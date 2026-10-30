<!--
Dosya Yolu: REPO_ROOT/rust/SETUP_BUILDER.md
Amac: Windows Setup Studio ile paket projesi olusturma ve derleme akisini anlatmak
Modul - Markdown
Version: 0.1.0
Aciklama: Setup Builder sihirbazinin girdilerini, proje dosyasini ve imzalama sinirini belgeler
Bagimli Oldugu Katman: View | Service | Documentation
-->

# Turkuaz Setup Studio

Turkuaz Setup Studio, uygulama dosyalarindan Turkuaz .tzpkg paketi ve setup proje girdileri hazirlayan Windows masaustu aracidir. Baslangic akisi InstallShield Project Assistant benzeri yonlendirmeli adimlardan olusur; son kullaniciya gosterilecek setup icin canli tema onizlemesi sunar.

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
4. Cikti ust klasorunu secip paket hazirlamayi baslatin.

Projeler .tzproj uzantisiyla kaydedilip Setup Studio icinde tekrar acilabilir. Her proje derlemesi yeni bir {package-id}-{version} cikti klasoru olusturur; var olan klasorun ustune yazmaz. Islem staging klasorunda yapilir ve tamamlaninca tek klasor olarak yayinlanir.

## Derleme ciktisi

- application.tzpkg: kaynak klasorundeki payload'in Turkuaz paket bicimi.
- release-manifest.draft.yaml: paket boyutu ve SHA-256 ozeti hesaplanmis, sema denetiminden gecmis imzasiz manifest taslagi. Taslaktaki artifact URI yerel file: URI'sidir.
- setup-theme.json: secilen preset ve varsa marka/renk ozellestirmesi.
- project.tzproj: proje alanlarinin yeniden acilabilir kopyasi.

Manifest taslagi bir yayina guven kazandirmaz. Yerel file: URI'si yalnizca paketleme/yerel deneme icindir; uzak dagitim icin artifact HTTPS adresine yuklenmeli ve manifest bu adresle tekrar uretilmelidir. Kurulabilir self-extracting setup icin release manifestinin yayin anahtariyla imzalanmasi, imzanin dis trust policy ile dogrulanmasi ve ayni setup temasiyla .tzsetup bundle uretilmesi gerekir. Bu akis RELEASE_SIGNING.md, CLI.md ve SMOKE_TEST.md icinde aciklanir. Signing key ve trust policy Setup Studio projesine veya .tzpkg dosyasina gomulmez.
