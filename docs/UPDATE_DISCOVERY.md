# 📄 Dosya Yolu: /docs/UPDATE_DISCOVERY.md
# 📌 Amac: Masaustu read-only update discovery UX veri akisini, sonuc durumlarini ve mutation sinirlarini tanimlamak
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: Signed manifest latest release ile committed installed state karsilastirmasini ve WinUI gorunumunu aciklar
# Bagimli Oldugu Katman: Service | Repo | Tool | View | Language

# Update Discovery UX

TurkuazInstaller masaustu arayuzu package id, channel ve signed manifest source kullanarak read-only guncelleme kontrolu yapar.

Bu akis install/update mutation baslatmaz.

## Veri Akisi

`WinUI -> MainWindowController -> InstallerDesktopService -> IInstallerRuntimeService.CheckUpdateAsync -> UpdateCheckService`

Runtime signed manifest providerini ayni trust zinciriyle resolve eder.

UpdateCheckService:

1. committed install state kaydini okur
2. signed provider latest release bilgisini resolve eder
3. installed version ile latest version degerini karsilastirir
4. typed availability sonucu dondurur

## Sonuc Durumlari

Release bulunamazsa:

`release_not_found`

Latest surum kurulu surumden yeniyse:

`available`

Latest surum kurulu surumle ayni veya daha eskiyse:

`current`

Paket kurulu degil fakat signed release varsa Application sonucu teknik olarak available olabilir; Presentation bunu kullaniciya `Paket kurulu degil` olarak gosterir ve update-available UI state'ini acmaz.

## Gosterilen Bilgi

WinUI:

- kurulu surum
- son signed release surumu
- guncelleme durumu
- discovery error

bilgisini ayri kartta gosterir.

Package id, channel veya manifest source degisirse onceki discovery sonucu temizlenir.

## Trust

Update discovery yeni bir trust modeli acmaz.

Manifest:

1. raw byte olarak alinir
2. detached .p7s signature zorunlu tutulur
3. external package trust policy resolve edilir
4. signer subject + certificate SHA-256 pinleri dogrulanir
5. ancak sonra parse edilir

Unsigned veya trust policy'yi gecemeyen manifest discovery sonucu uretemez.

## Read-only Sinir

Discovery sirasinda:

- package artifact indirilmez
- staging yapilmaz
- package apply edilmez
- install state yazilmaz
- operation journal yazilmaz
- reboot resume request yazilmaz
- background update baslatilmaz

Update butonu mevcut explicit mutation akisidir; discovery sonucu kendi basina update baslatamaz.

## Concurrency

Update discovery calisirken installer mutation, retry ve catalog refresh baslatilmaz.

Installer mutation calisirken update discovery baslatilmaz.

Bu ayrim ayni UI state uzerinde concurrent provider/state islemlerini engeller.

## Hata Siniri

Discovery error ile installer operation error ayri tutulur.

Provider/network/trust hatasi:

- update discovery error InfoBar'inda gosterilir
- ana installer operation `HasError` state'ini acmaz
- eski availability sonucu update-available olarak korunmaz

## Background Policy

Bu tranche yalniz manual read-only discovery UX'tir.

Background update policy ve version skip/pinning roadmap'in ayri maddeleridir.
