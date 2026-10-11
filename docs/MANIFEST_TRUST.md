# 📄 Dosya Yolu: /docs/MANIFEST_TRUST.md
# 📌 Amac: TurkuazInstaller detached manifest signature ve package bazli trust store modelini aciklar
# 📌 Modul - Markdown
# Version: 1.4.0
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

## Gercek CMS Local File CI Entegrasyon Testi

tests/TurkuazInstaller.Infrastructure.Tests/SignedFileManifestIntegrationTests.cs
her testte gecici ve production disi RSA sertifikasi olusturur. Raw manifest
byte'larini detached CMS .p7s olarak imzalar, bagimsiz YAML trust policy
dosyasina o sertifikanin SHA-256 pinini yazar ve gercek FileReleaseProvider
ile release okur.

CI kabul/reddetme kontrolleri:

- dogru sertifika ve external pin: manifest parse basarili
- signature imzalandiktan sonra gecerli YAML'in degistirilmesi: reddedilir
- yanlis external SHA-256 pin: reddedilir
- ayni subject ile farkli sertifikanin imzasi: reddedilir
- eksik detached signature: reddedilir
- eksik independent trust store: reddedilir

Gecici private key repository'ye, release artifactina veya CI secrets'a
yazilmaz. Bu bir metadata/trust pipeline entegrasyon testidir;
InstallerWorkflowService uzerinden kurulum, dosya indirme, artifact
Authenticode veya gercek Windows VM urun migration testinin yerine gecmez.

## Signed Manifest -> Artifact SHA -> Workflow CI Bridge

SignedFileManifestIntegrationTests icindeki ek teori testi, ephemeral CMS
ile imzalanan gercek local-file manifesti external SHA-256 pinned trust
store'dan gecirir. Sonra gercek DefaultArtifactDownloader ve
Sha256ArtifactVerifier ile signed manifestin tanimladigi fixture artifacti
kontrol edilir.

- dogru signed metadata + dosya byte'lari: staging/apply portuna ulasilir,
  state commit portu cagrilir
- dogru imzali metadata ama yanlis artifact SHA-256: stage/apply ve
  install state kaydi yapilmadan operasyon fail-closed biter

Guvenli test siniri: uygulama gercekten kurulmaz; Package Engine ve state
repository test double'dir. Kaynak fixture byte'lari executable degildir.
Artifact Authenticode ve production signer zinciri de bu testin konusu
degildir; gercek signed product VM kabul testi #41 halen zorunludur.

## Windows Authenticode Real-Trust CI Gate

WindowsAuthenticodeArtifactSignatureVerifierTests, Windows hosted CI'da
Windows veya PowerShell'in mevcut guvenilir bir signer tarafindan
imzalanmis gercek EXE dosyasini secerek native WinVerifyTrust kodunu
dogrudan cagirir. Test:

- Windows trust chain + gercek publisher subject + SHA-256 signer pin
  dogrulamasi basariliysa positive kabul eder.
- Yanlis publisher subject veya yanlis signer SHA-256 pinini reddeder.
- Gercek imzali dosyanin gecici bir kopyasindaki PE byte degisikligini
  reddeder. Sistem dosyasini degistirmez.

Bu bir gercek Windows Authenticode *verification* testidir. Test
sertifikasi olusturmaz, Windows trust store'a CA eklemez ve antivirus
istisnasi olusturmaz. GitHub-hosted Windows'un guvenilen sistem
sertifikasini test fixture'i olarak kullanir.

Bu test, uygulamamizin kendi Velopack artifactinin Authenticode ile
imzalanmis oldugunu veya end-user Windows VM'de kurulabildigini
kanitlamaz. Production Artifact Signing ve #41 manuel kabul kapilari
ayri kalir.
