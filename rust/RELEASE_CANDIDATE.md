<!--
Dosya Yolu: REPO_ROOT/rust/RELEASE_CANDIDATE.md
Amac: Community Rust surum adayi icin kanit, artifact ve yayin kapilarini tek yerde izlemek
Modul - Markdown
Version: 0.1.7
Aciklama: RC indirme butunlugu ve manuel Windows UAT raporu kanitlari
Bagimli Oldugu Katman: Documentation | QA | Release
-->

# TurkuazInstaller v0.2.24 - surum adayi hazirligi

**Durum (2026-11-18):** TEST / RC DEGERLENDIRMESI. Bu dosya GitHub
Release, imzali son kullanici surumu veya yayin onayi degildir.
Kaynak gelistirme dali `fix/native-wizard-visible-content-20261111`,
[PR #76](https://github.com/TurkuazLabs/TurkuazInstaller/pull/76).
Dogrulanmis uygulama-kodu temeli:
`83a5418fa6f9ffe1231b85f8db20ac87b182d4cc`.
CLI paket surumu `0.2.24`. Uygulama kod temeli ayni kalirken
CI artifact dogrulama otomasyonu eklendi. Yeni kaynak veya belge
commit'inin kendi CI sonucu ayrica kontrol edilmelidir.
Surum tag'i yaratilmadi, `main` degismedi.

## Kanitlanmis hosted CI

[CI #43952156847](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/43952156847)
`83a5418f` icin Linux `rust-workspace` ve Windows `rust-windows`
**SUCCESS**. Linux Cargo fmt, locked check, Clippy ve workspace
testleri; Windows CLI/Studio/GUI build, kisayol/UAC/registry,
native setup, signed offline smoke ve local payload/receipt
regresyonlari calisti. Windows payload receipt testleri ozellikle
calisan EXE, ikili dosyayi once kaldirma, sonraki kilitli veri
dosyasinda islemi baslamadan reddetme ve dogrulanmis envanter
handle'inin korunmasi senaryolarini gecti.

[CI #43966467156](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/43966467156)
`2a8bbe679a3708491e9faf7298edbc729f2b2bc0` icin Linux ve Windows
**SUCCESS**. Ek Windows CI adimlari 5 test artifact'i icindeki 8
dosyaya ait SHA-256 ve byte uzunlugunu kaydeder, yuklenmis artifact
ZIP'lerini yeniden indirir ve acilmis 8 dosyayi kontrol eder.
Ayrica ayni boyuttaki degistirilmis dosya reddedilir.
Envanter PR head SHA ve gercek CI checkout SHA'yi ayri kaydeder.
Bu SHA-256 envanteri test kanitidir, imzali attestation degildir.

[CI #44186183796](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/44186183796)
`8037dccd793ae43c07d79fc31219c53a734849bd` icin Linux ve
Windows **SUCCESS**. Windows PowerShell 5.1 (Windows 10 varsayilan
kabuk) ile indirilen alti artifact'in Unicode karakter ve bosluk
tasiyan yeni klasorde, farkli CWD'den dogrulanmasi PASS.
Toplam 8 payload dosyasi ve 2 dogrulayici scripti kontrol edildi;
esit boyutta dosya degisikligi reddi de PASS. Bu hosted CI testi,
gercek Windows 10 masaustu GUI/UAC kabulu **degildir**.

Windows hosted runner `windows-2025` ortamindadir. Bu kanit,
kullanicinin Windows 10/11 masaustunde elle tiklayarak yaptigi test
veya production Authenticode imza/itibar kaniti **degildir**.

## Sadece test icin indirilebilir ciktilar

CI #44194993875 `rust-windows` isinden uretildi. Dosya adlarindaki
`v0.2.24`, Cargo CLI surumudur; imzali RC tag'i veya onayli
production yayin adi degildir.

| CI artifact adi | Kullanim | Sinir |
| --- | --- | --- |
| `turkuaz-setup-builder-v0.2.24-windows-x64` | Setup Studio gelistirme build'i | TEST-ONLY |
| `turkuaz-installer-cli-v0.2.24-windows-x64` | CLI gelistirme build'i | TEST-ONLY |
| `turkuaz-setup-gui-stub-v0.2.24-windows-x64` | Native GUI test stub'i | TEST-ONLY |
| `turkuaz-web-setup-test-v0.2.24-windows-x64` | Ornek URL'li imzasiz WebSetup | TEST-ONLY |
| `turkuaz-example-test-setup-v0.2.24-windows-x64` | Yerel kurulum smoke ornegi | TEST-ONLY |
| `turkuaz-test-artifact-inventory-v0.2.24-windows-x64` | 8 payload, 2 dogrulayici ve 1 UAT rapor scriptinin SHA-256 envanteri | TEST-ONLY |

Bu artifact'lar 7 gunluk CI saklama politikasina sahiptir.
Son tamamlanan [CI #44194993875](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/44194993875)
basarili; GitHub kayitlarinda bu calismanin 6 artifact'i
**24 Kasim 2026** tarihinde sona eriyor. Her yeni CI calismasinin
artifact saklama tarihi ayri oldugundan kesin tarihi GitHub'daki
ilgili Actions calismasindan kontrol edin.
Indirme baglantisi icin ilgili CI sayfasini kullanin;
artifact'lar GitHub Release asset'i degildir.
CI artifact'lari icindeki dosyalar icin SHA-256 envanteri **KAYDEDILDI**
ve GitHub Actions ayni paketleri yeniden indirip dogruladi.
Bu envanterin imzali bagimsiz yayinci onayi/attestation oldugu
iddia edilmez. Gercek yayinci Authenticode imzasi ve production
yayin envanteri **HENUZ KAYDEDILMEDI**. Paketleri public stable
feed'e tasimayin.

## Yerel Windows indirme butunlugu on kontrolu (TEST-ONLY)

Windows kabul testlerinden once **ayni basarili CI calismasindan**
alti artifact ZIP'ini indirin. Surumu `0.2.24` olan dosyalari
farkli CI calismalari arasinda karistirmayin. Dosyalari once izole,
bos bir klasore acin; test kurulumunu bu asamada calistirmayin.

```powershell
# Windows PowerShell 5.1 oturumunda; ornek CI: #44186183796
# Bu run icin PR source commit: 8037dccd793ae43c07d79fc31219c53a734849bd
$download = 'C:\Turkuaz-RC\downloads'
$unpacked = 'C:\Turkuaz-RC\unpacked'
if (Test-Path -LiteralPath $unpacked) {
    throw 'Fresh extraction directory required.'
}
$zipFiles = @(Get-ChildItem -LiteralPath $download -File -Filter 'turkuaz-*.zip')
if ($zipFiles.Count -ne 6) {
    throw 'Exactly six ZIP artifacts from a single successful run are required.'
}
New-Item -ItemType Directory -Path $unpacked -ErrorAction Stop | Out-Null
$zipFiles | ForEach-Object {
    Expand-Archive -LiteralPath $_.FullName `
        -DestinationPath (Join-Path $unpacked $_.BaseName) -ErrorAction Stop
}

# Altinci ZIP iki dogrulama ve bir UAT rapor scripti tasir.
# Once GitHub'daki
# kaynak dosyalarla karsilastirin; imzasiz script guven kokunuz degildir.
$verifier = Join-Path $unpacked 'turkuaz-test-artifact-inventory-v0.2.24-windows-x64\windows-rc-verify-download.ps1'
& $verifier -ArtifactRoot $unpacked `
    -Version '0.2.24' `
    -SourceCommit '8037dccd793ae43c07d79fc31219c53a734849bd'
```

Ornek, [CI #44186183796](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/44186183796)
icin sabitlenmistir. Baska CI calismasindan ZIP alirsaniz,
`-SourceCommit` degerini **o calismanin PR head SHA'si** ile
degistirin. Komut Windows PowerShell 5.1 icinde `& $verifier`
seklinde calisir; PowerShell 7 kurulumu gerektirmez.
Execution Policy engeline rastlanirsa dogrulamayi durdurun;
guvenlik politikasini sessizce devre disi birakmayin.

Komut, test envanterini, iki dogrulayici ve UAT rapor scriptinin
SHA-256 ve boyutunu, diger bes artifact klasorunu ve toplam 8 payload
dosyasinin adini, adetini, boyutunu ve SHA-256 degerini kontrol eder. Eksik/fazla/degistirilmis dosyada hata verir.
`-ExpectedCheckoutCommit` parametresi ancak **ayri ve guvenilir
bir kaynaktan** dogrulanan CI checkout SHA biliniyorsa eklenmelidir.
Aksi durumda o deger imzasiz envanterden okunur ve uyari verilir:
bu, CI kaynaginin kimligini **bagimsiz dogrulamak degildir**.
Indirdiginiz scriptleri calistirmadan once guvenilir GitHub
kaynaklariyla ve depo koduyla karsilastirin. Envanterle birlikte
scriptler ve diger dosyalar degistirildiyse hash eslesmesi
tek basina guven garantisi saglamaz. Bu kontrol, imzali Authenticode
ve gercek yayinci guven zincirinin yerini almaz.

Windows CI #44168436408, bu yerel komutun dogru checkout SHA
ile PASS, yanlis 40 haneli checkout SHA ile RED sonucunu
ve ayni uzunlukta degisen dosyanin SHA-256 reddini dogruladi.
Dogrulanan kod head'i `fe5a3838734a6f98a9ee3194c1e7437efe885cbd`.
Bu sonuclar hosted Windows 2025 CI ortamindan gelir; kullanicinin
gercek Windows 10/11 masaustunde acceptance testi **NOT RUN**.

[CI #44179175870](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/44179175870)
(`ab4a849af1e14c5ece29d4885c79bb14896d2dab`) Linux ve
Windows **SUCCESS**. Windows CI, altinci ZIP'deki iki
PowerShell scriptinin git checkout ile ayni SHA-256'ya sahip
oldugunu, indirilen test paketlerinin 8 dosyasini ve 2 scripti
kontrol etti. Yanlis checkout SHA, degistirilmis payload ve
ayrica degistirilmis script fail-closed reddedildi.
Gercek Windows 10/11 masaustu kabul testi **NOT RUN**;
production imzasi/attestation mevcut degil.

## Windows 10/11 manuel UAT kanit dosyasi (TEST-ONLY)

[CI #44194993875](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/44194993875)
`bff1729f5733a0e9cb29ef8cb9f3256979101032` icin Linux ve
Windows **SUCCESS**. Windows PowerShell 5.1 testi ile altinci ZIP'deki
`windows-rc-uat-report.ps1` calistirildi. 12 manuel kabul satirinin
`NOT_RUN` kaldigi, var olan raporun degistirilmedigi ve artifact
klasorune rapor yazilamadigi dogrulandi. CI bir manuel UAT yapmadi.

Ayni basarili CI'dan indirilen alti ZIP'i yukaridaki gibi ayri,
bos klasorlere cikartin. Scripti kaynak GitHub commit'iyle
karsilastirdiktan sonra izole Windows 10/11 VM icinde,
Windows PowerShell 5.1 oturumunda calistirin:

```powershell
$unpacked = 'C:\Turkuaz-RC\unpacked'
$kit = Join-Path $unpacked 'turkuaz-test-artifact-inventory-v0.2.24-windows-x64'
$uat = Join-Path $kit 'windows-rc-uat-report.ps1'
& $uat -ArtifactRoot $unpacked `
    -Version '0.2.24' `
    -SourceCommit 'bff1729f5733a0e9cb29ef8cb9f3256979101032' `
    -RunUrl 'https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/44194993875' `
    -OutputPath 'C:\Turkuaz-RC\rc-uat-evidence.json'
```

Bu SHA ve run URL'si sadece **CI #44194993875** icindir.
Farkli bir CI'dan artifact indirilirse ikisi de degistirilmelidir.
`-ExpectedCheckoutCommit` yalnizca bagimsiz dogrulanan GitHub
Actions checkout SHA biliniyorsa eklenir. Imzasiz envanter,
script ve kaynak baglantisi production yayinci kaniti degildir.
Execution Policy engelini sessizce devre disi birakmayin.

JSON; Windows surumu, kaynak SHA, envanter SHA-256 ve
12 ayri test satiri icerir. Her satir `NOT_RUN` baslar.
GUI, UAC, HKCU/HKLM, kurulum, kaldirma ve kullanici veri
koruma senaryolarini gercekte denedikten sonra PASS/FAIL,
reproduction ve maskelenmis ekran/log kanitlarini
[Windows kabul Issue #41](https://github.com/TurkuazLabs/TurkuazInstaller/issues/41)
icinde raporlayin. Kullanici verisi, secret veya private key
eklemeyin. Bu script kurulum calistirmaz, yetki yukseltmez,
Windows kabulunu otomatik PASS yapmaz.

## RC kabul matrisi

- [x] Kaynak commit, PR ve test artifact'lari ayri kimliklerle belirlendi.
- [x] Linux hosted fmt, Clippy, build ve testler basarili.
- [x] Windows hosted GUI/Studio build, kayit ve local uninstall
      negatif senaryolari basarili.
- [x] Windows hosted native setup ve signed offline smoke basarili.
- [x] CI tarafinda 5 test artifact'i / 8 payload dosyasi icin
      SHA-256 envanteri, indirme-sonrasi dogrulama ve bozulma
      reddi basarili (CI #43966467156).
- [x] Altinci artifact'ta iki dogrulayici scripti bulunur;
      indirilen script kaynaklari ve hash'i dogrulandi,
      script bozulmasi reddedildi (CI #44179175870).
- [x] Windows PowerShell 5.1 hosted CI'da Unicode/space klasor ve
      farkli CWD'den indirilen RC kitinin dogrulanmasi basarili
      (CI #44186183796). Gercek Windows 10 UAT yerine gecmez.
- [x] Windows PowerShell 5.1 ile UAT rapor scripti, 12 NOT_RUN
      senaryo, mevcut raporu ezmeme ve artifact dizinine yazmama
      testleri basarili (CI #44194993875). Manuel UAT degildir.
- [ ] PR #76 icin bagimsiz kod/guvenlik incelemesi onayi.
      Otomatik Codex review denemesi kullanim siniri nedeniyle
      tamamlanmadi; bu basarisiz kod veya temiz guvenlik raporu degildir.
- [ ] Temiz Windows 10 ve 11 x64 VM'lerde gercek masaustu GUI,
      yonetici UAC/HKLM, standard-user HKCU, EULA/dil/ESC,
      kisayol, Windows Yuklu Uygulamalar ve kaldirma kabulu.
- [ ] Uygulama acikken kaldirma, kilitli dosya, kaydedilmemis
      belge, kullanici dosyasi, tekrar dene ve restart yok senaryolari.
- [ ] CI disindaki temiz Windows 10/11 kabul ortaminda indirilmis
      artifact'in SHA-256'sini envanterle karsilastirma,
      uygulama surumu/PE metadata ve gercek dagitim kimligi kabul kaydi.
- [ ] Gercek yayinci Authenticode imzasi, zaman damgasi,
      sertifika guveni, zincir/revocation politikasi ve
      AV/SmartScreen kabul kaniti. Ornek signer/trust dosyalari
      gercek yayimda kullanilamaz.
- [ ] Signed Windows adapter parity: prerequisite/protocol,
      preserve_paths ve gercek install/update/repair/rollback
      kapsamlarinin kabul/istisna karari.
- [ ] Gercek uygulama pilotu ve son kullanici veri korunumu.
- [ ] Yayin kapsaminda son surum numarasi, RC tag,
      dagitim kanali ve yetkili yayin onayi.

## Izole Windows kabul proseduru

1. Basarili CI calismasindan test artifact'ini indirin;
   ZIP'i izole, bos bir test klasorunde acin. SHA-256 hesaplayip
   test kanitina ekleyin; uydurma/varsayilan hash kullanmayin.
2. **Temiz VM** icinde Setup Studio ile yalnizca ornek
   dosyalar iceren standart kurulumu uretin. EULA, dil,
   kullanici kapsami, varsayilan hedef, kisayol ve uninstall
   ekranlarini [SMOKE_TEST.md](SMOKE_TEST.md) adimlariyla deneyin.
3. Uygulamayi acik tutup kaldirmayi deneyin: kullanici uyarisini,
   zorla kapatmama ve dosyalarin korunmasini kaydedin.
   Uygulamayi kapatip `Tekrar dene` ile dogrulayin.
   Ayrica paylasim kilidi olan payload dosyasi ile deneyin.
4. Degistirilmis dosya, yabanci kisayol, kullanici belgeleri
   ve eski kurulum icin veri kaybi olmamasini dogrulayin.
   UAC iptali, hata ve kurtarma senaryolarini ayri kaydedin.
5. Pilot icin her senaryonun PASS/FAIL/NOT RUN sonucunu,
   Windows surumunu, paket SHA-256'sini ve
   `rust/SMOKE_TEST.md` kabul kanitini ekleyin.
   Basarisiz senaryoya production release izin vermeyin.

## Karar ve geri donus

Bu surum, mevcut delillerle **dahili RC/test degerlendirmesine
uygundur**. Gercek Windows kabul ve yayinci imzasi olmadan
**public stable / production yayinina hazir diye isaretlenmez**.
Test ZIP'leri mevcut urun kurulumlarini degistirmeden,
ayri bir VM'de denenir. Kaldirma atomik transaction degildir;
yaris sartlari ve parent junction/reparse denetimi manuel ve
bagimsiz guvenlik incelemesinde acik kalir.

Onay verilmedikce `main` merge, tag, GitHub Release,
Pro senkronizasyonu veya prod feed guncellemesi yapilmaz.

Kaynak takip: [Rust readiness](RUST_READINESS.md),
[Windows smoke](SMOKE_TEST.md), [Release signing](RELEASE_SIGNING.md),
[Windows kabul #41](https://github.com/TurkuazLabs/TurkuazInstaller/issues/41),
[imzali Windows adapter #64](https://github.com/TurkuazLabs/TurkuazInstaller/issues/64)
ve [pilot #42](https://github.com/TurkuazLabs/TurkuazInstaller/issues/42).
