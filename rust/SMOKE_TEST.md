<!--
Dosya Yolu: REPO_ROOT/rust/SMOKE_TEST.md
Amac: Community Rust gelistirme ciktisi icin Windows kabul adimlarini ve basari kanitlarini tanimlamak
Modul - Markdown
Version: 0.3.3
Aciklama: Derleme kontrolunden gercek Windows kurulum kabulune gecis rehberi
Bagimli Oldugu Katman: QA | Documentation
-->

# Windows smoke test ve kabul akisi

Bu rehber, Community Rust gelistirme ciktisini kontrollu bir Windows makinesinde denemek icindir. GUI setup stub artifact'i yalniz arayuz kabugudur; release bundle eklenmeden kurulum yapmaz. Gercek imzali release paketi ve yayinlayici tarafindan saglanan guven politikasi olmadan lifecycle kabul testi yapilamaz.

## Yerel klasor degistirme ve Geri Donusum Kutusu kabulu

Bu testleri yalnizca bos/dedike Windows 10/11 VM kullanarak yapin:
1. Test uygulamasini `qa.recycle` klasorune yukleyin ve farkli
   bir `user-data.txt` dosyasi ekleyin. Standart kurulum EXE'sini
   tekrar acin. Kullanici onay kutusu isaretlenmeden Kurulumu baslat
   **basarisiz olmali**, eski icerik dokunulmadan kalmalidir.
2. Son kontrol ekraninda tam hedef yol ve tum dosyalarin
   Geri Donusum Kutusu'na tasinacagi uyarisini okuyup
   checkbox'i isaretleyin. Yeni paket hazirlandiktan sonra
   onceki klasorun Geri Donusum Kutusu'nda bulundugunu
   dogrulayin; yeni klasorde guncel paket ve yeni receipt olmali.
3. UAC ile tum kullanicilar seceneginde yeniden acilan pencerede
   checkbox isaretsiz olmali, yeniden acik onay gerekmeli.
4. Recycle Bin devre disi, disk dolu veya Windows Shell islemi
   iptal edilmisken eski dosyalarin kalici silinmedigini ve
   temiz kurulumun baslatilmadigini dogrulayin.
5. Farkli `package-id` ile adlandirilmis klasor, UNC paylasimi,
   junction/symlink ve disaridan gelen degisiklik senaryolarinin
   reddedildigini gozlemleyin. Gercek Windows recycle davranisi,
   yalnızca headless/UI unit testleriyle dogrulanmis sayilmaz.

`install-local-self` CLI komutu onaysiz oldugu icin
mevcut klasore kurulum yapmayi reddetmeye devam eder.
Eski klasorun geri alinmasi otomatik degildir.

## Chrome tarafindan engellenen test ZIP'i ve dosya guvenligi

`turkuaz-example-test-setup-v0.2.20-windows-x64.zip` bir **CI test
artifact'idir**; production imzasi ve Google Safe Browsing itibari yoktur.
Chrome `Bu dosya tehlikeli` diyorsa korumayi kapatmayin veya dosyayi
zorla tutup calistirmayin. UAC/UAT testlerini gecmek, Chrome indirme
uyarisini kaldirmaz.

Test paketinin CI tarafinda olusturulan yeni surumunde
`SHA256SUMS.txt` ve `SECURITY-AUDIT.json` dosyalari bulunur.
GitHub Actions run sayfasindaki `Windows example test artifact audit`
ozetinde beklenen **ic dosya** SHA-256'lari, commit SHA'si ve Defender
durumu gorunur. Bu hash'ler ZIP'in kendisine degil icindeki dosyalara
aittir. Rapor GitHub CI sonucuyla eslestirilir; tek basina guvenilir
yayinci veya zararsizlik kaniti sayilmaz.

Dosya kurumunuzun politikasiyla indirmeye uygun bulunmus ve izole
test VM'ine normal yoldan elde edilmisse, cikardiktan sonra dosyalari
**calistirmadan** PowerShell ile karsilastirin:

```powershell
$root = 'C:\Test\TurkuazInstaller-Example'
Get-Content -LiteralPath (Join-Path $root 'SHA256SUMS.txt') | ForEach-Object {
    $parts = $_ -split ' \*', 2
    if ($parts.Count -ne 2) { throw 'Gecersiz SHA256SUMS satiri' }
    $file = Join-Path $root $parts[1]
    $actual = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
    if ($actual -ne $parts[0]) { throw "SHA256 eslesmedi: $($parts[1])" }
}
Get-AuthenticodeSignature -LiteralPath (Join-Path $root 'TurkuazSetup.exe') |
    Select-Object Status, StatusMessage, SignerCertificate
```

CI EXE'leri gecici self-signed test sertifikasiyla imzalanir;
`NotTrusted` olmasi beklenebilir. `manifest-trust.yaml` Windows
Authenticode yayinci sertifikasinin yerine gecmez. Test sertifikasini
Trusted Root/Trusted Publishers'a eklemeyin.

Windows Defender taramasi destekleniyorsa CI onu ayrica denemeye
calisir. Ortam izin vermezse rapor **not_run_unavailable** seklinde
kalir; asla `temiz` olarak yazilmaz. Yerel Defender taramasinda
tehdit bulunmamasi da Chrome'un alarmindan bagimsiz bir guvenlik
garantisi degildir. Gercek Windows UAT yalnizca ayri bir test VM'inde
ve izinli/dogrulanmis pakette uygulanmalidir.

## On kosullar

- [GitHub Actions](https://github.com/TurkuazLabs/TurkuazInstaller/actions) uzerindeki en son **basarili** Rust CI calismasindan `turkuaz-setup-builder-v<surum>-windows-x64` ve gereken CLI/GUI test artifact'larini indirin. Artifact'lar gecicidir; kullanmadan once run'in commit SHA'sini, sonucunu ve uygulama surumunu dogrulayin. Referans: [#41913467340](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/41913467340) (4 Kasim 2026, `ed272460`).
- Testi tercihen geri alinabilir bir Windows sanal makinesinde ve yonetici olmayan kullanici ile yapin.
- Test uygulamasi icin ayri, gercekten imzalanmis `.tzpkg`, bu paketi gosteren imzali manifest ve detached CMS imzasi kullanin.
- Ayni paket kimligi icin yayinlayici tarafindan saglanmis gercek trust policy dosyasini ayri tutun. Ornek YAML icindeki sifir pin ve ornek sertifika kimligi guvenilir degildir; uretimde kullanilamaz.
- Her komutta ayni paket kimligi, target ve data-root degerlerini kullanin. Asagidaki degerleri kendi paketinizle degistirin.

## Windows UAC yetki ve UAT kabul matrisi

UAC (User Account Control), Windows'un korumali klasor veya HKLM
islemleri icin yonetici onayi istemesidir. UAT (User Acceptance Testing)
ise gercek Windows kullanici arayuzunun ve kurulum davranisinin
kontrollu kabuludur; CI kod testinin aynisi degildir.

Otomatik Windows CI, `windows_elevation::tests` ve
`windows-uac-resume-smoke.ps1` ile yetki istemeden su olumsuz
senaryolari dogrular: bilinmeyen flag, ek EULA/recycle onay flag'i,
bozuk boolean, eksik/degismis arguman sirasi, goreli/traversal
hedefi ve desteklenmeyen dil. Testler gercek UAC prompt'u acmaz,
admin izni almaz, dosya tasimaz ya da klasor silmez.

Gercek UAT'yi geri alinabilir Windows 10/11 sanal makinede,
test icin uretilmis dosyalarla ve farkli iki kullanici turunde yapin:

1. Standart kullanici / Yalnizca bu kullanici: Windows yonetici
   onayi istenmemeli; kurulum kullanici veri koklerinde kalmali.
2. Standart kullanici / Tum kullanicilar: Windows `runas`
   ekrani cikmali. Iptal edilirse eski klasor, HKLM,
   Program Files ve kisayollar degismemeli.
3. UAC onaylanirsa ikinci bir kurulum penceresi acilmali.
   Hedef Program Files altinda ve paket kimligi ayni olmali.
   Yeni pencerede `Kurulumu baslat` tiklanmadan dosya
   kopyalamasi / kayit degisikligi yapilmamali.
4. Lisans/EULA sayfasi varsa yeni yukseltilmis surecte kabul
   tekrar istenmeli. Komut satirindan `--accept-license`
   veya `--recycle-approved` kabul edilmemeli.
5. Mevcut kurulum klasoru durumunda (PR #71 ile birlikte)
   Recycle Bin onayi UAC sonrasinda **yeniden istenmeli**.
   Onaysizsa hedef korunmali. Geri donusum islemi hata
   verirse asla kalici silme/ustune yazma fallback'i olmamali.
6. Admin kullanici ile Tum kullanicilar senaryosu: zaten
   elevated token varsa gereksiz ikinci UAC istenmemeli,
   fakat review/ozel klasor kabulu yine zorunlu olmali.
7. UAT raporuna Windows yapisi, UAC sonucu, kullanici haklari,
   test commit SHA'si, EXE'nin Authenticode signer sonucu,
   uygulama surumu, hedef kapsam/yolu, beklenen ve
   gercek davranis ile gizli bilgi icermeyen ekran
   goruntusunu kaydedin.

Windows UAC dialogu, kod imzasi veya yayinci orijinalligi
garantisi saglamaz. Imzasiz test EXE'si Windows'ta
bilinmeyen yayinci gorunebilir; production dagitimi icin
gercek yayinci sertifikasi ile Authenticode dogrulamasi,
imzali CMS release ve guven politikasi ayri kabul kapilaridir.
Islem gercek dosyalar uzerinde degil VM fixture'larinda
yapilmalidir.

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
