# 📄 Dosya Yolu: /docs/BRANDING_LOCALIZATION.md
# 📌 Amac: TurkuazInstaller branding ve localization extension davranisini belgelemek
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: UI profile konumu, fallback davranisi, desteklenen alanlar ve fail-closed kurallari aciklar
# Bagimli Oldugu Katman: Config | Repo | View | Language

# Branding ve Localization

TurkuazInstaller masaustu uygulamasi kullaniciya ozel UI profilini asagidaki local config yolundan okur:

`%LOCALAPPDATA%\TurkuazInstaller\config\ui-profile.yml`

Dosya yoksa built-in TurkuazInstaller Community metinleri kullanilir. Profil, installer manifestinden ayridir ve paket tarafindan uzaktan degistirilemez.

## Contract

Contract kaynagi:

`contracts/ui-profile.yml`

Ornek profil:

`examples/ui-profile.yml`

Desteklenen branding alanlari:

- `window_title`
- `header_title`
- `header_subtitle`
- `footer`

Desteklenen localization label anahtarlari contract dosyasinda sabittir. Bilinmeyen anahtar fail-closed davranisiyla reddedilir. Bos label degeri reddedilir.

`culture` .NET culture adi olarak dogrulanir. Ornek: `tr-TR`, `en-US`.

## Guvenlik siniri

UI profile yalniz gorunen metinleri ve branding alanlarini etkiler. Paket kaynagi, artifact trust, signature, install target, prerequisite, Windows integration veya update policy kararlarini degistiremez.

Bu ayrim sayesinde branding/localization dosyasi guvenlik politikasina donusmez.
