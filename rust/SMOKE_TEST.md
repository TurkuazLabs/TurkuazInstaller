<!--
Dosya Yolu: REPO_ROOT/rust/SMOKE_TEST.md
Amac: Community Rust gelistirme ciktisi icin Windows kabul adimlarini ve basari kanitlarini tanimlamak
Modul - Markdown
Version: 0.2.3
Aciklama: Derleme kontrolunden gercek Windows kurulum kabulune gecis rehberi
Bagimli Oldugu Katman: QA | Documentation
-->

# Windows smoke test ve kabul akisi

Bu rehber, Community Rust gelistirme ciktisini kontrollu bir Windows makinesinde denemek icindir. GUI setup stub artifact'i yalniz arayuz kabugudur; release bundle eklenmeden kurulum yapmaz. Gercek imzali release paketi ve yayinlayici tarafindan saglanan guven politikasi olmadan lifecycle kabul testi yapilamaz.

## On kosullar

- Guncel PR #63 Rust Migration CI calismasindan `turkuaz-installer-cli-v<surum>-windows-x64` ve `turkuaz-setup-gui-stub-v<surum>-windows-x64` artifact'larini indirin. Ikisi de yedi gun sonra silinir; kullanmadan once Actions tarihini ve CLI'nin `--version` ciktisini kontrol edin.
- Testi tercihen geri alinabilir bir Windows sanal makinesinde ve yonetici olmayan kullanici ile yapin.
- Test uygulamasi icin ayri, gercekten imzalanmis `.tzpkg`, bu paketi gosteren imzali manifest ve detached CMS imzasi kullanin.
- Ayni paket kimligi icin yayinlayici tarafindan saglanmis gercek trust policy dosyasini ayri tutun. Ornek YAML icindeki sifir pin ve ornek sertifika kimligi guvenilir degildir; uretimde kullanilamaz.
- Her komutta ayni paket kimligi, target ve data-root degerlerini kullanin. Asagidaki degerleri kendi paketinizle degistirin.

## Pozitif akisi

CLI artifact'i ile komut yardimini gorun. GUI artifact'ini acarak kurulum penceresinin acildigini ve trust policy/dizin secicilerini inceleyin; bu ciplak stub'a bundle eklenmedigi icin kurulum baslatmayin. Tam kurulum akisini asagidaki dagitim cifti testinde kontrol edin:

```powershell
.\turkuaz-installer.exe --version
.\turkuaz-installer.exe --help
```

Imzali manifestten ilk kurulumu yapin:

```powershell
.\turkuaz-installer.exe install --package-id example.product --manifest-url https://updates.example.test/channel.yaml --signature-url https://updates.example.test/channel.yaml.p7s --trust-store .\manifest-trust.yaml --target .\test-install --data-root .\test-data
```

Cikti satirindaki active package directory altinda, manifestteki entry point'in calistigini dogrulayin:

```powershell
.\turkuaz-installer.exe launch --package-id example.product --target .\test-install --data-root .\test-data
```

Kurulum envanterini dogrulayin ve kayitli kurulumu listeleyin:

```powershell
.\turkuaz-installer.exe verify --package-id example.product --target .\test-install --data-root .\test-data
.\turkuaz-installer.exe list --data-root .\test-data
```

Ayni test paketi icin yayinlayici tarafindan imzalanmis daha yuksek bir SemVer release hazirsa, guncelleme, rollback politikasinin izin verdigi onceki surume donus, repair ve uninstall akisini sirayla deneyin. Rollback icin kurulu surumun imzali manifesti/signature'i gerekir. Uninstall komutu yalnizca yonetilen deployment'i siler ve `--confirm-uninstall YES` ister. Test verisini korumak icin uninstall'i en son calistirin.

## Dagitim cifti kabul akisi

Gercek release bundle'i ile `build-distribution-pair` komutunu calistirin. Komut NSIS yedegi de basariyla olusmadan cikti klasorunu yayinlamamalidir. Olusan iki EXE'yi Authenticode ile imzalayin; sonra imza dogrulamasini kontrol edin. Birincil Turkuaz setup ve NSIS yedegini temiz Windows sanal makinelerinde ayri ayri baslatip ayni paketin ayni surumunu kurdugunu dogrulayin. Birincil Turkuaz setup `setup-gui` stub'ini kullanir; kurulum penceresi, islem secimi, indeterminate ilerleme durumu ve iptal secenegi gorunmelidir.

## Basari kanitlari ve hata raporu

Her akista su bilgileri kaydedin: Windows surumu/architecture, artifact workflow run linki ve SHA-256, paket kimligi/surumu, calistirilan komut, exit code, tam hata metni ve ilgili log dosyalari. Trust-store dosyasini, private key'i veya imzali release icerigini herkese acik issue'ya eklemeyin.

Su durumlarda kabul basarisiz sayilir: gecersiz imza veya digest'in reddedilmemesi; target/data-root disina beklenmeyen dosya yazilmasi; kurulum/guncelleme kesintisinde tutarsiz aktif surum; list/verify/launch'in kurulum durumuyla celismesi; rollback'in imzali rollback politikasini asmasi; uninstall'in yonetilmeyen dosyalari silmesi; primary/backup'un ayni signed release'i kurmamasi.

## Release oncesi kalan bloklar

Windows CI smoke betigi her dis process icin 300 saniyelik sure siniri uygular, `signtool.exe` dosyasini Windows SDK'nin surumlu x64 klasorlerinde arar ve tum smoke adimini 15 dakika ile sinirlar. Test sertifikasi guven magazasina eklenmez. Authenticode durumu `Valid`, `NotTrusted` veya yalnızca guvenilmeyen self-signed root mesajini veren `UnknownError` olabilir; signer thumbprint'i eslesmeli, hash uyusmazligi ve diger hata mesajlari reddedilir. Sertifika hazirligi, guven magazasi ve paket imzasi ayri asama ile loglanir.

Bu rehber production kabul kaniti degildir; CI bunun bir kismini otomatik calistirir. Windows CI gecici test sertifikasi ile detached CMS imzali offline bundle olusturur; install, update, launch, tamper rejection, repair, rollback ve uninstall adimlarini calistirir. Ayrica paired distribution'i derler; primary setup ve NSIS backup'u test sertifikasi ile Authenticode imzalar, ikisinin digest ve signer kimligini dogrular, sonra imzali primary setup ile install-self akisini calistirir. Test gercek publisher sertifikasini veya temiz son kullanici VM kabulunu kapsamaz. Rust workspace, strict Clippy, Windows compile ve NSIS kontrolleri de CI'da surer. Argumansiz CLI/GUI acilisi varsayilan data root journal'larini tarayip kurtarmayi dener; bozuk veya belirsiz journal CLI'yi durdurur ya da GUI'de yeni kurulumu bloke eder. Son kullaniciya hazir dagitim icin gercek Authenticode imza dogrulamasi, Windows adapter'lari, production signing ve temiz VM kabul sonucu ayrica gereklidir. GUI iptali backend process'ini sonlandirip recovery'yi otomatik dener; recovery belirsiz durum bildirirse manuel adimlari izleyin.

## PR #63 kucuk test kurulumu

Rust Migration CI basarili tamamlandiginda `turkuaz-example-test-setup-v<surum>-windows-x64` adli ayri bir test artifact'i yuklenir. ZIP; `TurkuazSetup.exe`, NSIS yedegi, test icin pinlenmis `manifest-trust.yaml`, CLI ve kucuk bir Windows ornek uygulamasi icerir. Uygulama baslatilinca gorunur bir test mesaji acilir.

ZIP'i ayri bir klasore cikartip yalnizca deneme icin kullanin. Kurulum sihirbazinda paket kimligi `ci.smoke`, kurulum dizini `%LOCALAPPDATA%\TurkuazInstaller-Test\App`, veri dizini `%LOCALAPPDATA%\TurkuazInstaller-Test\Data` olsun. `README-TR.txt` icindeki adimlarla uygulamayi CLI'dan acip ayni setup sihirbazindan onayli kaldirma yapabilirsiniz. Self-signed CI sertifikasi nedeniyle Windows yayinci uyarisi gosterebilir; bu imza production yayincisini dogrulamaz ve sertifika Trusted Root'a eklenmemelidir. Artifact yedi gun tutulur ve production'da kullanilmaz.

CI artifact'inin olusmasi kod dogrulamasidir; temiz son kullanici makinesinde elle GUI testi yerine gecmez.
