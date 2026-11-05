<!--
Dosya Yolu: REPO_ROOT/README.md
Amac: TurkuazInstaller Community Rust projesi, kurulum ve dogrulama giris sayfasi
Modul - Markdown
Version: 2.3.0
Aciklama: Aktif Rust uygulamasi, test ciktisi, kaynak yapisi ve production sinirlari
Bagimli Oldugu Katman: Documentation
-->

# TurkuazInstaller Community

**TurkuazInstaller**, Windows uygulamalari icin Rust tabanli kurulum paketi ve
kurulum sihirbazi gelistirme projesidir. **Turkuaz Setup Studio** bir uygulama
klasorunden `.tzpkg` olusturur ve Windows icin tek dosyali
`TurkuazSetup.exe` uretebilir. Ayri, imzali yayin altyapisi
install / update / repair / rollback / uninstall islevlerini icerir.

Bu depoda aktif Community gelistirmesi **Rust** kodudur (`rust/`).
Eski .NET/WinUI3/Velopack uygulamasi ve belgeleri Git commit gecmisinde
korunur; Rust runtime'inin parcasi degildir. Community kodu private Pro
deposuna bagli degildir.

## Baslangic

| Islem | Kaynak |
| --- | --- |
| Kurulum Studio ve tek dosyali EXE | [Setup Studio](rust/SETUP_BUILDER.md) |
| Gercek Windows test adimlari | [Windows smoke test](rust/SMOKE_TEST.md) |
| Komut satiri ve paket olusturma | [Rust CLI](rust/CLI.md) |
| Paket formati ve guven sinirlari | [Paket formati](rust/PACKAGE_FORMAT.md) |
| Imzali yayin ve guven | [Release signing](rust/RELEASE_SIGNING.md) |
| Mimari ve workspace | [Rust README](rust/README.md) |
| Production hazirlik kontrolu | [Rust readiness](rust/RUST_READINESS.md) |
| Belge dizini | [docs/README.md](docs/README.md) |

### Studio'da standart kurulum hazirlama

1. Windows icin hazirlanmis **test artifact** ZIP'ini
   [GitHub Actions](https://github.com/TurkuazLabs/TurkuazInstaller/actions)
   sayfasindaki basarili `Rust Migration CI` calismasindan indirin.
2. Studio'da **Tam Kurulum** projesi acin; urun kimligini, surumu,
   uygulama dosyalarini ve varsa ana EXE'yi belirleyin.
3. **03 Kurulum tasarimi** bolumunde kuruluma ait sayfalari ve
   baslik/aciklamalari yonetin. EULA istege baglidir; aciksa gercek
   Turkce ve English lisans metinlerini girin.
4. `1 Uygulama paketini hazirla` ardindan
   `2 Standart Kurulum EXE olustur` komutunu calistirin.
   Taninan onceki standart EXE yedeklenerek korunur.
5. Olusan `standard/TurkuazSetup.exe` dosyasini bos, gecici
   hedef dizinde **test edin**. Kurulum konumu, kapsam
   (tek kullanici / tum kullanicilar), EULA kabul zorunlulugu,
   kisayollar, Windows Yuklu Uygulamalar kaydi ve kaldirmayi inceleyin.

**Onemli:** Imzasiz yerel setup, paket butunlugunu kontrol eder; yayinci
kimligini kriptografik olarak dogrulamaz. Guvenli guncelleme ve rollback
icin imzali manifest, dis yayinci guven politikasi ve release kontrolleri
gerekir. Test artifact'ini production installer olarak dagitmayin.

## Gelistirme (repo kokunden)

Rust toolchain ve Cargo kurulu olmali. Komutlar `rust/Cargo.lock`
dosyasini kullanir:

```powershell
cargo fmt --manifest-path rust/Cargo.toml --all -- --check
cargo clippy --locked --manifest-path rust/Cargo.toml --workspace --all-targets -- -D warnings
cargo test --locked --manifest-path rust/Cargo.toml --workspace --all-targets
```

Windows Studio'yu yerel olarak derlemek icin:

```powershell
cargo build --locked --manifest-path rust/Cargo.toml -p turkuaz-installer-cli --release --features setup-authoring-gui
.\rust\target\release\turkuaz-installer.exe
```

## Dogrulama: CI ile production kabulu ayni degil

**4 Kasim 2026 kod referansi:** `ed2724609e311f6d31aefbfab9a0ec497179ed81`
icin [Linux ve Windows CI #41913467340](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/41913467340)
**SUCCESS**. Bu calisma Rust fmt, build, Clippy, unit testleri, Windows
Studio derlemesini ve otomatik paketleme/kurulum testlerini tamamlamistir.
Bu kanit yalniz belirtilen commit icin gecerlidir; yeni commit veya
merge testlerini otomatik olarak kanitlamaz.

**Henuz production kabulune girmeyenler:** temiz Windows 10/11
masaustunde tam sihirbaz / kaldirma / tum kullanicilar UAC elle kabul,
gercek sertifikayla Authenticode ve publisher trust, ilk gercek urun
pilotu, Windows imzali dagitim adapter parity'si ve NSIS yedeginden
tam bagimsizlik. Acik kalemler
[Rust readiness](rust/RUST_READINESS.md) belgesinde izlenir.

`main` dalinda Rust kodunu birlestirmek ile GitHub **Release**
olusturmak ayri islemlerdir. Kaynak birlestirme production imzali
surum veya son kullanici kabulunun tamamlandigi anlamina gelmez.
