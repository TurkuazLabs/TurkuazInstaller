<!--
Dosya Yolu: REPO_ROOT/rust/SMOKE_TEST.md
Amac: Community Rust gelistirme ciktisi icin Windows kabul adimlarini ve basari kanitlarini tanimlamak
Modul - Markdown
Version: 0.3.0
Aciklama: Derleme kontrolunden gercek Windows kurulum kabulune gecis rehberi
Bagimli Oldugu Katman: QA | Documentation
-->

# Windows smoke test ve kabul akisi

Bu rehber, Community Rust gelistirme ciktisini kontrollu bir Windows makinesinde denemek icindir. GUI setup stub artifact'i yalniz arayuz kabugudur; release bundle eklenmeden kurulum yapmaz. Gercek imzali release paketi ve yayinlayici tarafindan saglanan guven politikasi olmadan lifecycle kabul testi yapilamaz.

## On kosullar

- [GitHub Actions](https://github.com/TurkuazLabs/TurkuazInstaller/actions) uzerindeki en son **basarili** Rust CI calismasindan `turkuaz-setup-builder-v<surum>-windows-x64` ve gereken CLI/GUI test artifact'larini indirin. Artifact'lar gecicidir; kullanmadan once run'in commit SHA'sini, sonucunu ve uygulama surumunu dogrulayin. Referans: [#41913467340](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/41913467340) (4 Kasim 2026, `ed272460`).
- Testi tercihen geri alinabilir bir Windows sanal makinesinde ve yonetici olmayan kullanici ile yapin.
- Test uygulamasi icin ayri, gercekten imzalanmis `.tzpkg`, bu paketi gosteren imzali manifest ve detached CMS imzasi kullanin.
- Ayni paket kimligi icin yayinlayici tarafindan saglanmis gercek trust policy dosyasini ayri tutun. Ornek YAML icindeki sifir pin ve ornek sertifika kimligi guvenilir degildir; uretimde kullanilamaz.
- Her komutta ayni paket kimligi, target ve data-root degerlerini kullanin. Asagidaki degerleri kendi paketinizle degistirin.

## Studio, EULA ve Windows uygulama kaydi kabul senaryosu

Bu bolumu geri alinabilir temiz Windows 10/11 test hesabinda,
gercek uygulama verilerinden ayri bir paketle calistirin.
`Turkuaz Setup Studio` artifact'ini acin:

1. Tam Kurulum projesi olusturun; uygulama kimligi ve ana EXE
   alanini doldurun. **03 Kurulum tasarimi** bolumune gecin.
2. Lisans (EULA) sayfasini acin. Metinler bosken paketin EXE
   olusmasinin engellendigini dogrulayin. Yayinciya ait gercek
   Turkce ve English test metinlerini girin.
3. Hos geldiniz ve EULA sayfasinin sirasini degistirin; kaydedip
   projeyi yeniden acin. Metin ve ac/kapat durumlari kalmali.
   Zorunlu Son kontrol/Kuruluyor/Tamamlandi sayfalari
   gizlenememeli ve yanlis siraya tasinamamali.
4. Ilk paketi ve Standart Kurulum EXE'sini olusturun.
   Sonra sihirbaz metnini degistirip EXE'yi yeniden uretin:
   taninan eski `standard/TurkuazSetup.exe` yedeklenmeli,
   taninmayan klasor veya dosya silinmemeli.
5. Bos hedefe yeni EXE'yi acin. EULA gorunmeli,
   **Kabul ediyorum** isaretlenmeden **Ileri** pasif olmali.
   Dili degistirdiginizde onay sifirlanmali.
6. Tek kullanici kapsaminda kurun; kurulum yolu kullanici
   profilinde olmali. Secilen kisa yollar ve Windows
   **Yuklu Uygulamalar** kaydi kontrol edilmeli.
7. Kaldirici penceresini acin: acik onay istenmeli.
   Kurulum sonrasinda elle olusturulan kullanici dosyasi
   silinmemeli. Degistirilmis paket dosyasi varsa kaldirma
   fail-closed davranip veriyi korumali.
8. Ayrica admin haklariyla **Tum kullanicilar** senaryosunu
   test edin: UAC onayi reddedildiginde kurulum olmamali;
   onaylandiginda Program Files, ortak kisa yol ve HKLM
   butunlesmesi dogrulanmali. Yeni yukseltilmis GUI surecinde
   EULA kabulunun yeniden istenmesini kontrol edin.

Her senaryoda Windows build/surum, paket kimligi, cikti
SHA-256, ekran goruntusu, islem sonucu ve hata mesaji
kaydedin. Gercek dosya, private key, token, musterinin
gizli uygulama verisi veya imzalama sertifikasini loglara
eklemeyin.

**Not:** Otomatik CI'nin PASS olmasi bu insan tarafindan
yapilan GUI testlerinin PASS oldugu anlamina gelmez.
Imzasiz yerel setup, publisher imzasini dogrulamaz.

## Cevrimici GUI modu (gelistirme kabulu)

Windows setup-gui stub uygulamasini acin ve Kur isleminden sonra Paket ve guven adimina gecin. Kurulum kaynagi olarak Cevrimici (HTTPS indirme) secin; gercek imzali release manifestinin HTTPS URL'sini, detached CMS imzasinin HTTPS URL'sini ve sadece yayincidan gelen trust policy dosyasini girin. Hedef ve veri dizinlerini test klasorleri olarak secin. Son kontrol ekraninda kaynak ve iki HTTPS URL gorunmelidir. Kurulum baslatildiginda ayni executable, CLI `install` komutunu child process olarak cagirir; URL ve guven hatalarinda kurulum durmalidir. Cevrimdisi secenek ise mevcut `install-self` offline bundle yolunu kullanmaya devam eder.

Olumsuz kontroller: `http:`, `file:`, bos adres, kullanici adi/parola iceren URL ve fragment reddedilmelidir. Test URL'leri ornektir; `example.test` uzerinde gercek release bulunmaz. Bu akis tek-EXE web installer export'u, gercek download progress gosterimi veya temiz Windows VM kabulunun tamamlandigini kanitlamaz.

## Tek dosya WebSetup kabul plani

Studio'da 1 Paket hazirla islemi sonrasinda 04 Cevrimici yayin bolumune gercek imzali HTTPS manifest ve detached CMS signature URL'lerini girin. Gercek yayinci trust YAML ve Windows setup GUI stub secin. 2 Cevrimici yukleyici olustur dugmesi proje klasorundeki web/TurkuazWebSetup.exe dosyasini uretmelidir. Bu EXE Authenticode imzalanip temiz Windows VM'de baslatildiginda yayin URL'leri ve paket kimligi otomatik dolmali, trust store dosyasi secimi gerekmemelidir. Sihirbaz, manifest ve artifact icin ayni signed-HTTPS verification motorunu kullanmalidir.

Negatif testler: manifest/signature icin HTTP ve credential URL'lerini reddet; imzayi, digest'i, policy eslesmesini, kesintili indirmeyi ve hatali interneti reddet; kurulum sonrasi launch/verify/uninstall ve iptal kurtarma durumunu kontrol et. CI'nin uretecegi imzasiz WebSetup EXE yalniz marker, payload-size ve SHA-256 config testidir, gercek CDN ya da production sertifikasi testi degildir. Private key hicbir ornege konulmaz.

## CI: Authenticode imzali WebSetup geri okuma

Windows signed bundle smoke script'i, gecici CI sertifikasi ve gercek yayinci pin'ine dayali test guven dosyasi ile bir `TurkuazWebSetup-signed-test.exe` hazirlar. Bu EXE, **Authenticode** ile imzalanir ve `web-setup-info` ile paket kimligi + manifest HTTPS adresinin imza eklendikten sonra da dogru okunabildigi kontrol edilir. Kullanilan `example.test` yayin URL'leri calisan CDN degildir ve bu smoke adimi ağa baglanmaz, kurulum baslatmaz veya test WebSetup EXE dosyasini kullaniciya dagitmaz. Signed WebSetup PE overlay + certificate table kabulunu test eder.

Bu kabul, gercek HTTPS yayindan CMS imzasi dogrulama / artifact GET/SHA-256 / temiz Windows ortaminda son kullanici kurulumunun yerini almaz. Bu gercek sunucu adimlari hala aciktir.

## Pozitif akisi

CLI artifact'i ile komut yardimini gorun. GUI artifact'ini acip ust cubuktaki Gorunum secicisinden Sistem, Acik ve Koyu seceneklerini deneyin; her secim uygulamanin acik/koyu gorunumunu hemen degistirmelidir. Trust policy ve dizin secicilerini inceleyin. Bu ciplak stub'a bundle eklenmedigi icin kurulum baslatmayin. Tam kurulum akisini asagidaki dagitim cifti testinde kontrol edin:

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

Gercek release bundle'i olustururken `build-setup-bundle --theme ocean` ile hazir paletlerden birini secin. Ozel marka/renk denemesi icin `rust/examples/setup-theme.example.json` dosyasini kullanip `--theme-config rust/examples/setup-theme.example.json` verin; iki secenegi ayni komutta vermeyin. Olusan `.tzsetup` paketinden `build-distribution-pair` komutunu calistirin. Komut NSIS yedegi de basariyla olusmadan cikti klasorunu yayinlamamalidir. Olusan iki EXE'yi Authenticode ile imzalayin; sonra imza dogrulamasini kontrol edin. Birincil Turkuaz setup ve NSIS yedegini temiz Windows sanal makinelerinde ayri ayri baslatip ayni paketin ayni surumunu kurdugunu dogrulayin. Birincil Turkuaz setup `setup-gui` stub'ini kullanir; kurulum penceresinde temadaki marka adi, altyazi ve renkler; ayrica uygulama acik/koyu gorunum secicisi, islem secimi, belirsiz ilerleme durumu ve iptal secenegi gorunmelidir.

## Basari kanitlari ve hata raporu

Her akista su bilgileri kaydedin: Windows surumu/architecture, artifact workflow run linki ve SHA-256, paket kimligi/surumu, calistirilan komut, exit code, tam hata metni ve ilgili log dosyalari. Trust-store dosyasini, private key'i veya imzali release icerigini herkese acik issue'ya eklemeyin.

Su durumlarda kabul basarisiz sayilir: gecersiz imza veya digest'in reddedilmemesi; target/data-root disina beklenmeyen dosya yazilmasi; kurulum/guncelleme kesintisinde tutarsiz aktif surum; list/verify/launch'in kurulum durumuyla celismesi; rollback'in imzali rollback politikasini asmasi; uninstall'in yonetilmeyen dosyalari silmesi; primary/backup'un ayni signed release'i kurmamasi.

## Release oncesi kalan bloklar

Windows CI smoke betigi her dis process icin 300 saniyelik sure siniri uygular, `signtool.exe` dosyasini Windows SDK'nin surumlu x64 klasorlerinde arar ve tum smoke adimini 15 dakika ile sinirlar. Test sertifikasi guven magazasina eklenmez. Authenticode durumu `Valid`, `NotTrusted` veya yalnızca guvenilmeyen self-signed root mesajini veren `UnknownError` olabilir; signer thumbprint'i eslesmeli, hash uyusmazligi ve diger hata mesajlari reddedilir. Sertifika hazirligi, guven magazasi ve paket imzasi ayri asama ile loglanir.

Bu rehber production kabul kaniti degildir; CI bunun bir kismini otomatik calistirir. Windows CI gecici test sertifikasi ile detached CMS imzali offline bundle olusturur; install, update, launch, tamper rejection, repair, rollback ve uninstall adimlarini calistirir. Ayrica paired distribution'i derler; primary setup ve NSIS backup'u test sertifikasi ile Authenticode imzalar, ikisinin digest ve signer kimligini dogrular, sonra imzali primary setup ile install-self akisini calistirir. Test gercek publisher sertifikasini veya temiz son kullanici VM kabulunu kapsamaz. Rust workspace, strict Clippy, Windows compile ve NSIS kontrolleri de CI'da surer. Argumansiz CLI/GUI acilisi varsayilan data root journal'larini tarayip kurtarmayi dener; bozuk veya belirsiz journal CLI'yi durdurur ya da GUI'de yeni kurulumu bloke eder. Son kullaniciya hazir dagitim icin gercek Authenticode imza dogrulamasi, Windows adapter'lari, production signing ve temiz VM kabul sonucu ayrica gereklidir. GUI iptali backend process'ini sonlandirip recovery'yi otomatik dener; recovery belirsiz durum bildirirse manuel adimlari izleyin.

## Kucuk imzali ornek test kurulumu

Rust Migration CI basarili tamamlandiginda `turkuaz-example-test-setup-v<surum>-windows-x64` adli ayri bir test artifact'i yuklenir. ZIP; `TurkuazSetup.exe`, NSIS yedegi, test icin pinlenmis `manifest-trust.yaml`, CLI ve kucuk bir Windows ornek uygulamasi icerir. Uygulama baslatilinca gorunur bir test mesaji acilir.

ZIP'i ayri bir klasore cikartip yalnizca deneme icin kullanin. Sihirbazda `Kur` islemini secip `İleri`'ye basin. Paket kimligini `ci.smoke` birakin; guven dosyasinin yanindaki test YAML dosyasi oldugunu kontrol edin; kurulum dizinini `%LOCALAPPDATA%\TurkuazInstaller-Test\App`, veri dizinini `%LOCALAPPDATA%\TurkuazInstaller-Test\Data` secin; son kontrol ekraninda `Kur ve doğrula`'ya basin. Ardindan `README-TR.txt` icindeki CLI komutuyla ornek uygulamayi acin. Kaldirmayi GUI'den denemek icin setup'i yeniden acin, `Kaldır` islemini secin, ayni paket ve yollari kullanin, son ekrandaki onay kutusunu isaretleyip `Paketi kaldır`'a basin. Self-signed CI sertifikasi nedeniyle Windows yayinci uyarisi gosterebilir; bu imza production yayincisini dogrulamaz ve sertifika Trusted Root'a eklenmemelidir. Artifact yedi gun tutulur ve production'da kullanilmaz.

CI artifact'inin olusmasi kod dogrulamasidir; temiz son kullanici makinesinde elle GUI testi yerine gecmez.
