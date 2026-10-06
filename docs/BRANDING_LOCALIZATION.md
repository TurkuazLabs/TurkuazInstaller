# 📄 Dosya Yolu: /docs/BRANDING_LOCALIZATION.md
# 📌 Amac: TurkuazInstaller branding ve localization extension davranisini belgelemek
# 📌 Modul - Markdown
# Version: 1.1.0
# Aciklama: UI profile konumu, culture uygulamasi, tam kullanici metni override yuzeyi, fallback ve fail-closed kurallarini aciklar
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

`culture` .NET culture adi olarak dogrulanir ve WinUI process icin current/default thread culture ile UI culture'a uygulanir. Ornek: `tr-TR`, `en-US`.

Localization yalniz baslik ve butonlarla sinirli degildir. Contract icindeki desteklenen anahtarlar validation error, progress/status, update discovery, background update, channel, placeholder ve katalog metinlerini de kapsar. Profil bir anahtari override etmezse built-in Community metni kullanilir.

## Guvenlik siniri

UI profile yalniz gorunen metinleri ve branding alanlarini etkiler. Paket kaynagi, artifact trust, signature, install target, prerequisite, Windows integration veya update policy kararlarini degistiremez.

Bu ayrim sayesinde branding/localization dosyasi guvenlik politikasina donusmez.
