# 📄 Dosya Yolu: /docs/MANIFEST_TRUST.md
# 📌 Amac: TurkuazInstaller detached manifest signature ve package bazli trust store modelini aciklar
# 📌 Modul - Markdown
# Version: 1.2.0
# Aciklama: CMS/PKCS#7 trust zincirine manifest/signature byte limitleri, external pinning, provisioning ve rotation kurallarini ekler
# Bagimli Oldugu Katman: Service | Repo | Tool | Config

# Manifest Trust

TurkuazInstaller v1.1 manifesti YAML olarak parse etmeden once detached CMS/PKCS#7 signature dogrulamasi yapar.

Bu kontrol artifact Authenticode kontrolunden ayridir.

Manifest, installer'in hangi artifacti indirecegini ve hangi policy'leri uygulayacagini belirledigi icin manifest trust birinci guven siniridir.

## Dosya Kontrati

Manifest:

```text
installer-manifest.yml
```

Detached signature:

```text
installer-manifest.yml.p7s
```

Remote providerlarda `.p7s` dosyasi manifest URI'sinin ayni path'ine suffix olarak eklenir.

Local file provider ayni klasorde ayni isim + `.p7s` bekler.

Signature eksikse manifest parse edilmez.

## Kaynak Boyut Sinirlari

Parser veya CMS verification oncesinde metadata bounded okunur:

- manifest maksimum: 1 MiB (1048576 byte)
- detached `.p7s` maksimum: 256 KiB (262144 byte)
- remote Content-Length bu sinirlari asiyorsa body okunmadan reddedilir
- Content-Length yoksa stream limit asildigi anda kesilir
- local file boyutu okunmadan once ayni limitlerle kontrol edilir

Bu sinirlar bozuk veya kotu niyetli provider'in RAM tuketimini sinirsiz buyutmesini engeller.

## Imza Formati

- CMS/PKCS#7 detached signature
- binary `.p7s`
- raw manifest byte'lari imzalanir
- signer certificate CMS icinde bulunur
- tam olarak bir signer kabul edilir
- manifest strict UTF-8 olarak parse edilir

Whitespace veya satir sonu degisikligi dahil manifest byte'larindaki degisiklik signature verification'i bozar.

## External Trust Store

Trust policy manifestin icinde bulunmaz.

Varsayilan desktop yolu:

```text
%LOCALAPPDATA%/TurkuazInstaller/config/manifest-trust.yml
```

Ornek:

```yaml
schema_version: 1

packages:
  - id: example-app
    publisher_subject: "CN=Example Software"
    certificate_sha256: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
```

Gercek certificate SHA-256 degeri release signer sertifikasindan alinmalidir.

Package icin trust entry yoksa operation fail-closed biter.

## Neden Manifest Disinda

Manifest kendi beklenen signer kimligini tanimlasaydi attacker hem manifesti hem signer alanini degistirebilirdi.

TurkuazInstaller bunun yerine package id ile dis trust store entry'sini secer.

Dogru CMS signature tek basina yeterli degildir.

Signer certificate:

1. gecerli tarih araliginda olmali
2. configured publisher subject ile eslesmeli
3. configured certificate SHA-256 pin ile birebir eslesmeli

Certificate SHA-256 pini manifest trust icin zorunludur.

## Provisioning

Bir proje TurkuazInstaller'a gecmeden once:

1. production manifest signer sertifikasi belirlenir
2. publisher subject kaydedilir
3. sertifikanin SHA-256 hash'i kaydedilir
4. package id icin trust store entry provision edilir
5. release pipeline manifesti uretir
6. manifest raw byte'lari CMS detached olarak imzalanir
7. manifest ve `.p7s` birlikte publish edilir
8. clean install/update/repair/rollback testleri signed manifest ile calistirilir

Trust store release kaynagindan indirilmemelidir.

Aksi durumda manifest ile trust anchor ayni compromise alanina girmis olur.

## Certificate Rotation

Yeni signer sertifikasina gecmeden once trust store yeni certificate SHA-256 pini ile dagitilmalidir.

Eski pin kaldirilmadan yeni sertifika ile manifest publish edilmemelidir.

Rotation sirasinda trust config guncellemesi manifest release'inden once tamamlanir.

## Failure Davranisi

Asagidaki durumlarda manifest parse edilmez:

- `.p7s` yok
- CMS bozuk
- manifest byte'lari degismis
- signer sayisi bir degil
- package trust entry yok
- publisher subject farkli
- certificate SHA-256 farkli
- signer certificate su anki tarih icin gecersiz
- manifest 1 MiB limitini asiyor
- detached signature 256 KiB limitini asiyor

Artifact download ve package apply bu asamadan sonra baslar.
