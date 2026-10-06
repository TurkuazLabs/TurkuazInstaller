# 📄 Dosya Yolu: /docs/VERSION_POLICY.md
# 📌 Amac: TurkuazInstaller version skip ve pinning policy konfigurasyonunu ve enforcement sinirini tanimlamak
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: Exact skipped version, maximum accepted version ceiling, discovery statusu ve shared mutation guard davranisini aciklar
# Bagimli Oldugu Katman: Service | Repo | Tool | Config | View

# Version Skip / Pinning

Version policy config yolu:

`%LOCALAPPDATA%/TurkuazInstaller/config/version-policy.json`

Dosya yoksa version policy uygulanmaz.

## Ornek

```json
{
  "schema_version": 1,
  "entries": [
    {
      "package_id": "example-app",
      "channel": "stable",
      "maximum_version": "2.5.0",
      "skipped_versions": [
        "2.4.1"
      ]
    }
  ]
}
```

Policy package id + channel bazlidir.

Ayni package/channel identity iki kez tanimlanamaz.

Bir entry en az bir kural tasimalidir:

- maximum_version
- skipped_versions

## skipped_versions

skipped_versions exact candidate release surumlerini engeller.

Ornek:

- installed: 2.4.0
- latest signed release: 2.4.1
- skipped_versions: 2.4.1

Discovery sonucu Skipped olur ve update mutation baslamaz.

Provider daha sonra 2.4.2 dondururse 2.4.2 ayri olarak degerlendirilir.

Skip eski bir release'i provider'dan secmez.

## maximum_version

maximum_version pinning icin maksimum kabul edilen surum ceiling degeridir.

Ornek:

- installed: 2.4.0
- maximum_version: 2.5.0
- latest: 2.5.0 -> update kabul edilebilir
- latest: 2.6.0 -> Pinned, update bloklanir

Bu model exact historic release fetch yapmaz.

Provider latest 2.6.0 donduruyorsa TurkuazInstaller 2.5.0 release'ini uydurmaz veya downgrade aramaz.

Pinned versiona gecmek icin signed manifest/release source ilgili surumu gercek ve dogrulanabilir olarak sunmalidir.

## Evaluation Sirasi

Installed package icin candidate latest release mevcut surumden yeniyse:

1. exact skipped_versions kontrol edilir
2. candidate maximum_version degerini asiyor mu kontrol edilir
3. policy engeli yoksa Available olur

Package kurulu degilse version update policy install kararini degistirmez.

## Mutation Enforcement

Policy yalniz UI etiketi degildir.

InstallerWorkflowService.UpdateAsync policy'yi:

- operation lock
- journal
- artifact download
- staging
- package apply

adimlarindan once kontrol eder.

Policy engelinde typed InstallerVersionPolicyException uretilir.

Ayni guard:

- WinUI update
- CLI update
- reboot-resume update

akislarina uygulanir.

Bu nedenle CLI veya resume yolu version policy'yi bypass edemez.

## Discovery

Manual ve background discovery:

- Skipped
- Pinned

sonuclarini ayri durum olarak gorur.

Latest signed version yine UI'da gosterilir.

Skipped/Pinned sonuc kendi basina mutation baslatmaz.

## Degisikliklerin Etkisi

Policy dosyasi runtime tarafindan her evaluation sirasinda yeniden okunur.

Bu nedenle operator policy dosyasini degistirdikten sonra uygulamayi yeniden kurmak gerekmez.

Reboot bekleyen bir update resume edilmeden once policy yeniden kontrol edilir.

Policy reboot sirasinda daha kisitli hale getirildiyse resume update guvenli sekilde bloklanir.

## Guvenlik

Version policy:

- unsigned manifest trust bypass yapmaz
- historic release fetch yapmaz
- automatic downgrade yapmaz
- pinned release uydurmaz
- artifact verification kurallarini gevsetmez
- install/repair/rollback semantigini degistirmez

Policy sadece update candidate acceptance siniridir.
