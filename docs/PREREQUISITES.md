# 📄 Dosya Yolu: /docs/PREREQUISITES.md
# 📌 Amac: TurkuazInstaller prerequisite detection ve guvenli auto-install mimarisini tanimlamak
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: Detector registry, signed installer verification, explicit elevation ve post-install re-probe kurallarini sabitler
# Bagimli Oldugu Katman: Service | Tool | Config

# Prerequisite Engine

TurkuazInstaller prerequisite katmani iki ayri sorumluluga ayrilir:

1. Application katmanindaki generic detector registry
2. Platform Tool katmanindaki detector ve installer adapterlari

Application kodu belirli bir Windows prerequisite kimligini switch ile bilmez.

## Built-in Detectorlar

Community v1.1 built-in prerequisite id degerleri:

- windows-build
- architecture
- dotnet-desktop-runtime

Her detector tek bir id sahibi olur.

Ayni id ikinci kez register edilirse composition hatasi olusur.

Bilinmeyen id fail-closed olarak false doner.

## Auto-install Contract

Bir prerequisite eksikse auto-install yalniz manifestte explicit install policy varsa kullanilir.

Auto-install artifacti su alanlari tasir:

- HTTPS veya file URI
- SHA-256
- size_bytes
- Authenticode signature policy
- publisher_subject
- optional certificate_sha256
- shell-free argument listesi
- explicit requires_elevation

Auto-install policy signed manifestin parcasi oldugu icin manifest trust zinciri tarafindan korunur.

## Guven Zinciri

Prerequisite installer executable calistirilmadan once:

1. signed manifest dogrulanir
2. prerequisite detector kosar
3. eksik prerequisite icin installer artifacti indirilir
4. size ve SHA-256 dogrulanir
5. Authenticode WinVerifyTrust kosar
6. publisher subject birebir kontrol edilir
7. certificate_sha256 tanimliysa certificate pin kontrol edilir
8. installer shell kullanmadan argument listesi ile calistirilir
9. requires_elevation=true ise yalniz bu process icin UAC istenir
10. process exit code 0 olmadan basarili kabul edilmez
11. ayni detector yeniden kosar
12. prerequisite hala saglanmiyorsa ana paket stage/apply baslamaz

Bu zincirde hash veya signature dogrulamasi atlanamaz.

## Reboot Siniri

Windows installer exit code 1641 veya 3010 donerse mevcut v1.1 runtime islemi basarili saymaz.

Reboot/resume orchestration ayri P1 maddesidir.

Bu ozellik tamamlanana kadar reboot isteyen prerequisite auto-install fail-closed durur ve ana paket apply edilmez.

## Manifest Ornegi

```yaml
install:
  prerequisites:
    - id: dotnet-desktop-runtime
      version: ">=10.0.0"
      install:
        artifact:
          uri: https://downloads.example.invalid/windowsdesktop-runtime.exe
          sha256: fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210
          size_bytes: 12345678
          signature:
            algorithm: authenticode
            publisher_subject: "CN=Microsoft Corporation"
        arguments:
          - "/install"
          - "/quiet"
          - "/norestart"
        requires_elevation: true
```

Production projeleri gercek vendor URI, hash, size ve publisher degerlerini release pipeline icinde uretmelidir.

## Katman Siniri

- Domain: Prerequisite, expression ve install policy
- Service: detector registry ve orchestration
- Repo: prerequisite icin persistent storage yok
- Tool: Windows detectorlari, download/verification ve process adapterlari
- View: operation progress
- Config: signed manifest policy

Proje-ozel detection logic Core Application veya Controller icine eklenmemelidir.
