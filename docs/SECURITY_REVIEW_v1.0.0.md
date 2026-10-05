# 📄 Dosya Yolu: /docs/SECURITY_REVIEW_v1.0.0.md
# 📌 Amac: TurkuazInstaller 1.0.0 Stable Community guvenlik incelemesi kapsam, bulgu ve release kapilarini kaydetmek
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: Transport, integrity, process, path, state, rollback, signing ve secret boundary incelemesini release oncesi dokumante eder
# Bagimli Oldugu Katman: Service | Repo | Tool | View | Config

# Security Review - v1.0.0

## Kapsam

Inceleme su sinirlari kapsar:

- release provider transportu
- artifact download ve SHA-256 verification
- atomic staging
- Velopack process invocation
- install state commit
- rollback state davranisi
- Windows bootstrap self-update
- UAC elevation siniri
- WinUI input siniri
- release signing
- CI secret siniri

## Sonuc

Kod seviyesinde release engelleyici P1/P2 bulgu bulunmadi.

Stable tag yayinlamadan once Azure Artifact Signing production identity baglantisi zorunludur.

## Dogrulanan Invariantlar

### Transport

- remote manifest ve artifact icin plain HTTP reddedilir
- HTTPS veya local file kaynagi kabul edilir
- credential manifest icine yazilmaz

### Integrity

- artifact size apply oncesi kontrol edilir
- SHA-256 digest apply oncesi kontrol edilir
- verification failure sonrasi stage, apply ve state save baslamaz
- release asset checksum manifesti signing sonrasinda uretilir

### State

- state apply basarili olmadan commit edilmez
- apply failure state'i degistirmez
- rollback failure mevcut state'i korur
- rollback success onceki version state'ini commit eder
- JSON state write temp dosya ve atomic move kullanir

### Process

- Velopack processleri shell command stringi ile calistirilmaz
- argumentlar ProcessStartInfo.ArgumentList ile ayrilir
- non-zero exit code basarili sayilmaz
- UAC varsayilan degildir; yalniz explicit elevation portu kullanir

### Path

- package staging generated path root disina cikamaz
- preserve path install root disina cikamaz
- Velopack current altinda preserve path reddedilir
- manifest dosya adi Path.GetFileName ile normalize edilir

### Release Signing

Production release workflow:

- GitHub OIDC kullanir
- Azure Artifact Signing private key'i build runnera tasimaz
- SHA-256 file digest kullanir
- RFC3161 SHA-256 timestamp kullanir
- release oncesi SignTool /pa ile Authenticode verification yapar
- signing config eksikse release fail olur; unsigned fallback yoktur

## Kabul Edilen Sinirlar

- Generic Community paketlerinde artifact imzasi product policy'ye baglidir; SHA-256 her zaman zorunludur.
- Stable TurkuazInstaller binary release'i release pipeline tarafinda Authenticode signed olmak zorundadir.
- Local file kaynaklari kullanicinin guven siniri icindedir.
- Self-contained WinUI output Microsoft ve .NET runtime dosyalarini da tasir; yalniz TurkuazInstaller isimli binary'ler TurkuazLabs signing identity ile imzalanir.

## Release Blocker

Asagidaki Azure ve GitHub konfigurasyonu olmadan v1.0.0 tag'i yayinlanmamalidir:

- AZURE_CLIENT_ID
- AZURE_TENANT_ID
- AZURE_SUBSCRIPTION_ID
- ARTIFACT_SIGNING_ENDPOINT
- ARTIFACT_SIGNING_ACCOUNT_NAME
- ARTIFACT_SIGNING_CERT_PROFILE_NAME
- ARTIFACT_SIGNING_TIMESTAMP_URL
