# 📄 Dosya Yolu: /docs/LOCAL_TEST_PACKAGE.md
# 📌 Amac: Community'nin production signing tamamlanmadan sinirli Windows x64 manuel test paketini tarif etmek
# 📌 Modul - Markdown
# Version: 2.7.0
# Aciklama: Kisa sureli unsigned GitHub Actions artifacti ile gercek imzali release'i guvenlik acisindan ayirir
# Bagimli Oldugu Katman: Tool | Repo | Config

# Community Local Test Package (Unsigned)

## Hedef

Mevcut Community Core, WinUI, bootstrap ve CLI kodunu **izole Windows x64
gelistirme VM** ortaminda manuel kabul testi icin indirebilir hale getirmek.

`Community Unsigned Local Test Package` workflow'u production signing
**atlamaz**: Production release workflow'una dokunmaz ve imzasiz artifact'i
asla signed GitHub Release olarak yayinlamaz. Ayrica production Azure/GitHub
identity/secrets konfiguru gerekmez.

## Paket nasil uretilir?

1. `main` dalinda **Actions → Community Unsigned Local Test Package →
   Run workflow**.
2. CI'nin testleri ve combined x64 publish/archive kontrolunun
   `success` oldugunu dogrula.
3. Run'un `Artifacts` bolumundeki
   `TURKUAZINSTALLER-TEST-ONLY-UNSIGNED-win-x64` dosyasini al.
4. Artifact ZIP icindeki `SHA256SUMS.txt` ile test ZIP'inin SHA-256
   degerini karsilastir.
5. Icindeki `TurkuazInstaller-UNSIGNED-LOCAL-TEST-win-x64.zip` arsivini
   **disposable Windows x64 VM** uzerinde ayri bir klasore ac.
6. Koruma katmanlarini devre disi birakmadan mevcut
   `TurkuazInstaller.Bootstrapper.exe` ile uygulama arayuzunu ac.
7. Guvenilir, ayrica imzali bir ornek urun manifesti,
   `.p7s` sidecar'i ve **harici** manifest trust policy'si ile
   install/update/repair/rollback/uninstall kabul senaryolarini uygula.

Paket yalnizca **3 gun** GitHub Actions artifact olarak saklanir.
Tag, GitHub Release, production sertifikasi veya update feed uretilmez.

## Windows Desktop CI artifact ayrimi

Normal `Windows Desktop CI` x64 ve ARM64 combined distribution
artifactlari **imzasiz** oldugu icin yeni adlar
`TurkuazInstaller-TEST-ONLY-UNSIGNED-DISTRIBUTION-win-x64` ve
`TurkuazInstaller-TEST-ONLY-UNSIGNED-DISTRIBUTION-win-arm64`
olarak belirlenmistir. Uc gun sonra sona ererler. Bunlar onayli
`Signed Release` workflow'unun yayinladigi
`TurkuazInstaller-win-x64.zip` ve `TurkuazInstaller-win-arm64.zip`
assetleriyle ayni sey degildir. Eski Actions calistirmalarinda genel
isimli (`TurkuazInstaller-win-x64` gibi) artefaktlar bulunabilir;
bunlar da **imzasiz test ciktilari** olarak kabul edilir.

Test cihazlari icin README, checksum ve acik unsigned uyarisi iceren
`Community Unsigned Local Test Package` paketi tercih edilir. Kullanicilara
production imzasi olmadan diger Windows Desktop CI artifactlari
dagitilmaz; antiviruse istisna eklenmez.

## Guvenlik / limitler

- Binary'ler production Authenticode imzasi almamistir. End-user'a
  dagitilmaz, musteri makinesinde calistirilmaz. Windows guvenlik
  korumalarini bypass etmek icin yonerge verilmez.
- Unsigned local test build *release* degildir; production release icin
  `docs/RELEASE_CHECKLIST.md` ve Azure Artifact Signing preflight
  zorunludur.
- Test ZIP'i **urun manifesti signer yetkisi** vermez, package trust
  ve diger SHA-256/AuthentiCode enforcement aynen korunur.
- CI unit/build, reproducible ZIP, gercek WinUI pencere handle'i ve
  ayni prosesin MainWindowReady asamasini kontrol eder. Bu test hosted
  Windows CI ortamindadir; tum Windows 10 surumlerini kanitlamaz.
  Gercek urun icin imzali manifest, temiz kurulum ve geri alma manuel
  kabulunu tamamlamak hala ayri gerekir.
- Kaspersky gibi urunlerde 'Dusuk Kisitlamali' uygulama grubu olayi tek
  basina Trojan tespiti degildir. Ayrica zararli yazilim tespiti varsa
  tehdit adi, tespit edilen dosya ve eylem bagimsiz incelenir; antivirus
  istisnasi veya kapatmasi bu testin bir parcasi degildir.
- ARM64 production readiness CI korunur; bu lokal test artifact'i
  bilerek yalnizca x64 hedefler.
