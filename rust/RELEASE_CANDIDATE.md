<!--
Dosya Yolu: REPO_ROOT/rust/RELEASE_CANDIDATE.md
Amac: Community Rust surum adayi icin kanit, artifact ve yayin kapilarini tek yerde izlemek
Modul - Markdown
Version: 0.1.1
Aciklama: v0.2.24 hash envanteri, CI ve production kabul sinirlari
Bagimli Oldugu Katman: Documentation | QA | Release
-->

# TurkuazInstaller v0.2.24 - surum adayi hazirligi

**Durum (2026-11-17):** TEST / RC DEGERLENDIRMESI. Bu dosya GitHub
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

Windows hosted runner `windows-2025` ortamindadir. Bu kanit,
kullanicinin Windows 10/11 masaustunde elle tiklayarak yaptigi test
veya production Authenticode imza/itibar kaniti **degildir**.

## Sadece test icin indirilebilir ciktilar

CI #43966467156 `rust-windows` isinden uretildi. Dosya adlarindaki
`v0.2.24`, Cargo CLI surumudur; imzali RC tag'i veya onayli
production yayin adi degildir.

| CI artifact adi | Kullanim | Sinir |
| --- | --- | --- |
| `turkuaz-setup-builder-v0.2.24-windows-x64` | Setup Studio gelistirme build'i | TEST-ONLY |
| `turkuaz-installer-cli-v0.2.24-windows-x64` | CLI gelistirme build'i | TEST-ONLY |
| `turkuaz-setup-gui-stub-v0.2.24-windows-x64` | Native GUI test stub'i | TEST-ONLY |
| `turkuaz-web-setup-test-v0.2.24-windows-x64` | Ornek URL'li imzasiz WebSetup | TEST-ONLY |
| `turkuaz-example-test-setup-v0.2.24-windows-x64` | Yerel kurulum smoke ornegi | TEST-ONLY |
| `turkuaz-test-artifact-inventory-v0.2.24-windows-x64` | 8 dosyanin boyut/SHA-256 ve commit envanteri | TEST-ONLY |

Bu artifact'lar 7 gunluk CI saklama politikasina sahiptir ve
GitHub kayitlarina gore **23 Kasim 2026** tarihinde sona erer.
Indirme baglantisi icin yukaridaki CI sayfasini kullanin;
artifact'lar GitHub Release asset'i degildir.
CI artifact'lari icindeki dosyalar icin SHA-256 envanteri **KAYDEDILDI**
ve GitHub Actions ayni paketleri yeniden indirip dogruladi.
Bu envanterin imzali bagimsiz yayinci onayi/attestation oldugu
iddia edilmez. Gercek yayinci Authenticode imzasi ve production
yayin envanteri **HENUZ KAYDEDILMEDI**. Paketleri public stable
feed'e tasimayin.

## RC kabul matrisi

- [x] Kaynak commit, PR ve test artifact'lari ayri kimliklerle belirlendi.
- [x] Linux hosted fmt, Clippy, build ve testler basarili.
- [x] Windows hosted GUI/Studio build, kayit ve local uninstall
      negatif senaryolari basarili.
- [x] Windows hosted native setup ve signed offline smoke basarili.
- [x] CI tarafinda 5 test artifact'i / 8 dosya icin SHA-256
      envanteri, GitHub'dan indirme-sonrasi dogrulama ve
      ayni boyutta bozulma reddi basarili (CI #43966467156).
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
