<!--
Dosya Yolu: REPO_ROOT/rust/RUST_READINESS.md
Amac: Community Rust test kaniti ve production kabul eksiklerini ayri izlemek
Modul - Markdown
Version: 1.1.0
Aciklama: CI kaniti, yerel installer, signed workflow ve acik Windows kabul kapilari
Bagimli Oldugu Katman: Documentation | QA | Release
-->

# Rust Installer - Kanitlanmis durum ve production sinirlari

Bu belge, **4 Kasim 2026** tarihinde `ed2724609e311f6d31aefbfab9a0ec497179ed81`
revizyonunda [CI #41913467340](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/41913467340)
ile dogrulanan Rust Community kodunu anlatir. Belge commit'leri,
`main` merge islemi ve test artifact'lari yeni bir production kabul
kaniti olusturmaz.

## Aktif islevler

- `installer-cli`: signed online/offline install, update, repair,
  policy-allowed rollback, uninstall, launch, verify, journal recovery,
  Windows kurulum sihirbazlari ve Setup Studio.
- `package-engine`: `.tzpkg`, `.tzsetup`, imzasiz native setup
  paketi, yerel dosya envanteri ve guvenli silme kontrolleri.
- `manifest-pipeline`, `manifest-verification`,
  `manifest-trust-store`: detached CMS ve dis publisher policy.
- `artifact-transfer`, `artifact-validation`,
  `authenticode-verification`: HTTPS/file staging, boyut/SHA-256
  kontrolleri ve gerekli Windows Authenticode.
- `install-workflow`, `deployment-activation`,
  `operation-journal`: versioned deployment ve kurtarilabilir
  signed yasam dongusu.

## Yerel imzasiz Setup Studio (gelistirme)

- Tam Kurulum projesi standart `TurkuazSetup.exe` uretir;
  uygulama paketi EXE icine gomulur.
- `03 Kurulum tasarimi`: kurulum sayfalarini ac/kapat, sirala,
  Turkce/English baslik ve aciklama metinleri, EULA goruntuleme.
- EULA kapali varsayilir. Aciksa iki dilde yayinciya ait gercek
  metin gerekir; kullanıcı onay kutusu olmadan ilerleyemez.
  Eski `.tzproj` dosyalari varsayilan ayarla acilir.
- Tek kullanici / tum kullanicilar hedefi Windows known-folder
  API ile cozulur; all-users akisi UAC ister. Ortak veya kullaniciya
  ozel kisayol hedefleri ayridir.
- Windows Yuklu Uygulamalar kaydi ve ayri kaldirma sihirbazi
  yerel payload envanterini kullanir. Kullanici dosyalari, degisen
  uygulama dosyalari ve yabanci kisayollar korunur.
- Bu yerel akis **imzasiz** oldugundan SHA-256 kaydi publisher
  guven zinciri yerine gecmez; signed update/rollback yerine kullanilmaz.

## Otomatik test kaniti

[Linux ve Windows CI #41913467340](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/41913467340)
(4 Kasim 2026, `ed272460`): **SUCCESS**.

- Linux: Cargo fmt, locked workspace derleme, Clippy, Rust testleri.
- Windows: locked workspace, GUI/Studio EXE derleme,
  wizard/navigation, kisayol ve kapsam, HKCU kaydi, native setup
  smoke ile signed offline yasam dongusu testleri.
- Native EULA ayarlarinin EXE icine gomulup geri okunmasi, eski
  ayar uyumlulugu, bozulmus EULA'nin reddi ve sayfa kontrati
  Rust unit testlerinde dogrulandi.
- Test ZIP'leri gecici GitHub Actions artifact'idir; imzali
  production release degildir.

## Henuz acik kabul kapilari

1. **Windows 10/11 temiz makine:** EULA sayfasi, dil degisimi,
   geri/ileri/iptal, UAC ve HKLM butunlesmesi, kaldirma, upgrade,
   kisayol ve korunan dosyalarin manuel GUI kabul matrisi.
   Kullanici tarafinda daha once kurulumun temel dosya kopyalama ve
   sihirbaz ekranlari denendi; son EULA ve kaldirma degisikliklerinin
   tum matrisi henuz dogrulanmis degil.
2. **Publisher/provenance:** production sertifikasi, Authenticode,
   timestamp, chain/revocation, AV/SmartScreen ve gercek yayin
   manifesti ile dis trust policy kabulu.
3. **Signed installer Windows adapter parity:** prerequisites,
   protocol kaydi, preserve_paths ve Windows yasam dongusu
   entegrasyonlarinin tam fonksiyonel kabul testleri.
4. **Tam Rust dagitim:** `packaging/TurkuazInstallerBackup.nsi`
   fallback'i halen var. Uygulamanin tum dagitimi icin
   `%100 Rust` iddiasi yapilmaz.
5. **Gercek urun pilotu:** test verisinin disinda gercek yayinci
   imzali dagitim, kurtarma, veri korunumu ve Windows VM testi.
6. **Private Pro entegrasyonu:** lisans backend'i, staged rollout
   ve private feed ayri depodadir; Community public crate'lerine
   gizli kod veya anahtar eklenmez.

## Kaynaklar ve takip

- [Setup Studio / EULA](SETUP_BUILDER.md)
- [Windows kabul rehberi](SMOKE_TEST.md)
- [Release signing](RELEASE_SIGNING.md)
- [Windows manuel kabul #41](https://github.com/TurkuazLabs/TurkuazInstaller/issues/41)
- [Gercek uygulama pilotu #42](https://github.com/TurkuazLabs/TurkuazInstaller/issues/42)
- [Guvenlik #51](https://github.com/TurkuazLabs/TurkuazInstaller/issues/51)
- [AV / SmartScreen #58](https://github.com/TurkuazLabs/TurkuazInstaller/issues/58)

Kaynak birlestirme, test ZIP'i ve production release birbirinden
ayridir. Bu belgede olmayan adimlar PASS sayilmaz.
