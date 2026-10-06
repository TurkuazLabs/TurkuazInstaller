# 📄 Dosya Yolu: /docs/INSTALLED_APP_CATALOG.md
# 📌 Amac: TurkuazInstaller kurulu uygulama katalog/list UI veri kaynagi, refresh ve hata sinirlarini tanimlamak
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: Committed state repository -> Presentation catalog service -> WinUI list akisini ve read-only sinirini aciklar
# Bagimli Oldugu Katman: Repo | Service | View | Language

# Installed App Catalog

Kurulu uygulama katalogu TurkuazInstaller'in kendi committed install state kayitlarini listeler.

View dosya sistemini dogrudan okumaz.

Akis:

`IInstallStateRepository.ListAsync -> InstalledAppCatalogService -> MainWindowViewModel -> WinUI ListView`

## Gosterilen Alanlar

Her satir:

- package id
- installed version
- stable/beta channel
- target path

bilgisini gosterir.

Manifest URL katalog state'inden tahmin edilmez veya uydurulmaz.

## Siralama

Repository committed JSON state dosyalarini okur.

Presentation katmani package id alanina gore ascending deterministic siralama uygular.

## Refresh

Katalog:

- normal application startup'ta
- kullanici Yenile butonuna bastiginda
- basarili install/update/repair/rollback/uninstall sonrasinda
- basarili reboot-resume sonrasinda

yenilenir.

Installer mutation aktifken manuel katalog refresh baslatilmaz.

## Error Boundary

Katalog yukleme hatasi ile installer operation hatasi ayri UI state alanlaridir.

Bozuk veya gecersiz state dosyasi varsa:

- katalog Warning InfoBar gosterir
- mevcut installer operation sonucu basarisiz olarak isaretlenmez
- state dosyasi sessizce atlanmaz

Bu davranis bozuk committed state kaydinin fark edilmesini saglar.

## Read-only Sinir

Catalog UI state yazmaz veya silmez.

State mutation yalniz mevcut install/update/repair/rollback/uninstall Application workflow'lari tarafindan yapilir.

Katalog View:

- repository bypass yapamaz
- state dosyasi silemez
- manifest source tahmin edemez
- package mutation baslatmaz

Bu tranche yalniz katalog/list gorunumu ve refresh davranisini kapsar.
