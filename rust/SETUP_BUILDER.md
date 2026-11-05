<!--
Dosya Yolu: REPO_ROOT/rust/SETUP_BUILDER.md
Amac: Windows Setup Studio tam boy proje panellerini ve derleme akisini anlatmak
Modul - Markdown
Version: 0.9.5
Aciklama: Birlesik Studio gorunumu, istege bagli bilgi paneli ve alan durumu gostergeleri
Bagimli Oldugu Katman: View | Service | Documentation
-->

# Turkuaz Setup Studio

Turkuaz Setup Studio, uygulama dosyalarindan Turkuaz .tzpkg paketi, setup tema ayarlari ve imzali yayin girdilerinden dagitim ciktilari hazirlayan Windows masaustu aracidir. Acilista IDE benzeri bir karsilama ekrani gorunur. Yeni Tam Kurulum veya Cevrimici Kurulum projesi secildiginde dort adimli modal proje sihirbazi acilir. Secim .tzproj icerisinde saklanir. Eski projeler varsayilan Tam Kurulum turuyle uyumlu acilir. Calisma alaninda solda bolum menusu, merkezde proje ozeti ve sagda son kullanici setup onizlemesi bulunur. Uygulama gorunumu secenegi yalnizca Setup Studio penceresini etkiler; setup tema preset'i, marka adi, alt baslik ve vurgu rengi son kullanici kurulum sihirbazina uygulanir. Sag kurulum onizlemesindeki devam dugmesi yalnizca goruntudur ve kurulum baslatmaz.

## Kullaniciya gorunen adlandirma

Studio karsilama ekraninda proje turleri **Tam Kurulum** ve **Cevrimici Kurulum** olarak gorunur. Teknik literaturdeki *Full Installer* ve *Web Installer* ifadeleri UI ana basligi olarak kullanilmaz. Proje secimi kaydedilen `.tzproj` dosyasinda aynen `offline` ve `web` teknik kimlikleriyle korunur; dosya bicimi veya CLI komutlari yeniden adlandirilmaz.

Sihirbazda **Kurulum tasarimi** (eski Setup tasarimi), **Kurulum motoru (EXE)** (eski GUI stub), **Yayinci guven dosyasi** (eski trust store), **Imzali yayin dosyasi (YAML)** (eski signed manifest), **Yayin imza dosyasi** (eski detached signature) ve **Uygulama paketi** (eski payload paketi) adlari tercih edilir. `application.tzpkg`, `TurkuazWebSetup.exe`, `manifest-trust.yaml` gibi gercek dosya adlari ve WebSetup CLI arayuzu degismez. NSIS icin kullanici etiketi **Yedek yukleyici araci (NSIS)** olur.

## Windows GUI derlemesi

Rust workspace kokunden calistirin:

    cargo build --locked --manifest-path rust/Cargo.toml -p turkuaz-installer-cli --release --features setup-authoring-gui

Olusan dosya:

    rust/target/release/turkuaz-installer.exe

Argumansiz acildiginda Setup Studio penceresini gosterir. Windows CI, ayni dosyanin surumlu turkuaz-setup-builder-vX.Y.Z-windows-x64.exe gelistirme artifact'ini de uretir.

## Tum proje bolumlerinde tam boy Studio (Windows)

**01 Urun bilgileri**, **02 Uygulama dosyalari** ve **04 Dagitim/Yayin**
bolumleri de 03 tasarimindaki gibi ana calisma alaninda acilir;
kucuk modal editorler kullanilmaz. Solda proje gezintisi, ortada
bolumun gercek duzenleme alanlari ve ilerleme gostergesi, sagda
baglamsal proje ozeti, kurulum tasarimina kisayol ve guvenli
son kullanici setup onizlemesi vardir. Uzun AV ve imzali yayin
formlari bagimsiz dikey kaydirma kullanir.

**Genel bakis** sayfasi genis ekranlarda iki sutun, dar ekranlarda
tek sutun proje kartlari sunar. Bolum sayfalarinin altinda **Genel
bakis**, **Onceki bolum**, **Sonraki bolum** ve son bolumde mevcut
kurulum olusturma eylemi bulunur. Onceki girdi alanlari, adim
dogrulamasi, paket olusturma, .tzproj kaydi ve guvenlik davranislari
degistirilmez. Tasarim panelindeki onizleme butonlari kurulum
motorunu calistirmaz.

Gorsel CI derlemesi gercek ekran goruntusu testi yerine gecmez.
Windows kabul testinde 01/02/03/04, Genel bakis, kaydirma,
Turkce metinler, yeniden boyutlandirma ve acik/koyu tema
ayrica karsilastirilir. Production release otomatik olusturulmaz.

## Gelistirilmis Studio gezintisi ve bilgi paneli

Rakip kurulum IDE'lerinden (Advanced Installer ve install4j) alinan
bolum hiyerarsisi ilkesine gore 01, 02 ve 04 ana editorlerinde
**Bilgi panelini gizle/goster** kontrolu vardir. Sag panel kapatildiginda
ana form daha genis bir calisma yuzeyi kazanir. 03 tasariminda sayfa
ozellikleri paneli bagimsizdir ve bu dugmeden etkilenmez.

Sol gezinme ve Genel bakis kartlari yalnizca gerekli alanlara deger
girilip girilmedigini yazar; onay/validasyon sonucu olduklarini
iddia etmez. Sag paneldeki **Bolum kontrol listesi** secili bolume
hizli gecis saglar. Mevcut proje verisi, .tzproj serilestirmesi,
guvenlik dogrulamalari, dosya sistemi ve imzali dagitim mantigi
degistirilmez. Bu gostergeler paket olusturuldu, imzalandi, guvenli
veya Windows'ta denendi anlamina gelmez.

CI'da form gorunumu/veri degismezligi ve girdi durumu helper testleri
ayri dogrulanir. Gercek Windows GUI boyutlandirma ve kaydirma
kontrolu manuel gorsel kabul adimidir.

## Tam boy kurulum tasarimi (Windows Studio)

Kullanici **03 Kurulum tasarimi** bolumunu actiginda artik kucuk
bir modal form yerine, Studio'nun ana calisma alaninda uc sutunlu
tasarim ekrani gorur:

- **Sol:** proje gezintisi ve mevcut bolumler.
- **Orta:** genis, klasik Windows penceresine benzeyen **canli
  kurulum onizlemesi**. Secilen adimin gercek TR/EN basligi,
  aciklamasi ve EULA metni gosterilir. Ornek Geri/Ileri/Iptal
  dugmeleri yalnizca onizleme sayfasi degistirir, diske yazmaz.
- **Sag:** dikey **Kurulum Adimlari** listesi. Istege bagli
  sayfalari ekleme/gizleme, yukari/asagi tasima, tek sayfa
  ozelliklerini duzenleme ve secilen dilin EULA metnini girme.
  Kurulum ozeti, ilerleme ve sonuc adimlari kilitlidir.

Studio varsayilan olarak koyu tema acilir, Windows kurulum
onizlemesi her zaman acik renkli gorunur. Sistem/Acik/Koyu
tercihi yine kullanilabilir. Tema, marka, varsayilan kurulum
klasoru ve HEX vurgu rengi sagdaki katlanabilir bolumde
duzenlenebilir. Onizleme bir **tasarim simulasyonu**dur:
Windows icin hazirlanan EXE'nin tum piksel detaylarini veya
gercek UAC, dosya yazma ve kaldirma davranisini canli olarak
calistirmaz. Gercek EXE'nin gorunumu Windows kabul testinde
ayrica dogrulanir.

Bu tasarim degisikligi `.tzproj` alanlarini veya paket
motorunun kurulum/kaldirma/guvenlik islemlerini degistirmez.

## UI seciciler ve proje kaydi

- EXE secici ve kaynak klasoru secici ayni satir ve 112 x 40 piksel dugme olcusunu kullanir.
- Kurulum temasi listesinin uzerine gelince acik/koyu tema ornegi gorunur.
- Vurgu rengi HEX alani ve yanindaki RGB renk paletinden duzenlenebilir.
- "Projeyi kaydet" ilk kayitta Windows FOLDERID_Documents API ile kullanicinin
gercek Belgeler (OneDrive'a tasinmis/yeniden yonlendirilmis olabilir)
klasorunu bulur ve Belgeler/TurkuazInstaller/<urun adi>/project.tzproj
  yolunu olusturur. Gecersiz klasor karakterleri duzeltilir; urun adi
  proje icinde degismez. Var olan proje sessizce ezilmez.
- "Farkli kaydet" farkli konum secmeye izin verir. Var olan proje
  dosyasi kaydedildiginde ayni konum kullanilir.

## Proje calisma alani

Ilk acilista iki proje sablonu yer alir: Tam Kurulum (tum dosyalari iceren offline paket) veya Cevrimici Kurulum (HTTPS uzerinden indiren kucuk EXE). Ustteki Baslangic dugmesi yeni proje secimine doner; sol menudeki bolumler ve proje ayarlari kartlari mevcut projeyi duzenler. Tekrarlayan "Projeye basla" karti ve sol menudeki ikinci baslangic kisayolu kaldirildi. Secince ilgili tam boy bolum calisma alani acilir. Eski .tzproj dosyasi Proje ac ile yuklenebilir. Yeni proje secimi mevcut calisma verisini degistirecekse onay modal'i acilir; baslangica donuldugunde acik projeye geri donulebilir. Proje calisma alaninda solda numarali adimlar, ortada tam boy bolum editleri, sagda proje bilgileri ve son kullanici setup onizlemesi bulunur. Her bolum kendi alanlarini ana calisma yuzeyinde gosterir. Alt kisimda her iki tur icin 1 Uygulama paketini hazirla ve yalnizca secilen tipe ait 2 Tam Kurulum olustur veya 2 Cevrimici yukleyici olustur dugmeleri yer alir. Studio arayuzu Sistem/Acik/Koyu ayariyla kendi paletini degistirir; sagdaki onizlemenin Acik/Koyu secimi ise olusturulacak setup temasinin onizlemesini etkiler.

- Urun bilgileri: uygulama adi, paket kimligi, SemVer surumu ve stable/beta kanali.
- Uygulama dosyalari: kaynak klasoru ve istege bagli goreli EXE yolu.
- Setup tasarimi: varsayilan kurulum yolu, hazir tema preset'i, marka adi, alt baslik ve istege bagli HEX vurgu rengi.
- Cevrimdisi dagitim (Tam Kurulum): cikti klasoru, imzali manifest, detached imza, yayinci trust store'u, setup GUI stub'i ve istege bagli makensis.exe yolu.
- Cevrimici yayin (Cevrimici Kurulum): cikti klasoru, HTTPS manifest/imza adresleri, yayinci trust store'u ve setup GUI stub'i.

Uygulama paketi ve imzali dagitim cifti ana ekrandaki eylemlerden baslatilir. Gerekli bilgiler eksikse ilgili tam boy bolum otomatik acilir.

Yeni projede varsayilan cikti klasoru Windows `LOCALAPPDATA/TurkuazInstaller/Projects`,
uygulama kurulum hedefi ise `LOCALAPPDATA/Programs/Uygulamam` altindadir.
Bu ilk acilista yonetici yetkisi gerektiren Program Files hedefini dayatmaz;
Explorer'dan veya kisayoldan acilista process CWD'sine gore degismez.
`LOCALAPPDATA` yoksa OS gecici dizininde `TurkuazInstaller/Projects`
yedek yolu kullanilir; bu durumda kalici cikti icin Studio'dan
guvenli bir klasor secilmelidir. Kaydedilmis `.tzproj` dosyalarinin
kullanici tarafindan secilmis cikti yollari korunur.

Ilk paket olusturma isteginde, yeni projedeki varsayilan cikti ust klasoru
henuz yoksa Setup Studio eksik alt klasorleri olusturur. Bolum
dogrulamasi klasor olusturmaz; yalnizca paket olusturma asamasinda
olusturulur. Dosya, sembolik baglanti/junction veya `..` gecisi iceren
ust dizinler reddedilir. Yetki ve disk hatalari acikca raporlanir.
Kullanici tarafindan secilmis cikti konumu degistirilmez; var olan
`{package-id}-{version}` klasorunun ustune yazilmaz.

Projeler .tzproj uzantisiyla kaydedilip Setup Studio icinde tekrar acilabilir. Acik bir proje varken baska proje secmek icin onay istenir; iptal edilen dosya secimi proje verisini korur. Adim dogrulama hatalari ilgili tam boy bolumun icinde gorunur. Her uygulama paketi derlemesi, 04 Yayin ve cikti bolumunde secilen cikti ust klasoru altinda yeni bir {package-id}-{version} klasoru olusturur; var olan klasorun ustune yazmaz. Uygulama dosyalari bolumundeki kaynak klasoru cikti konumu degildir. Ana ekrandaki Olusturulan dosyalar paneli hedef klasoru ve gercekten mevcut application.tzpkg ile distribution/TurkuazSetup.exe dosyalarinin durumunu ayri gosterir. Birinci asama paketi hazir sayilmasi icin `project.tzproj` anlik goruntusu guncel kaynak, urun, surum, kurulum hedefi ve tema ayarlariyla eslesmelidir. Bu alanlar degistiyse eski paket hazir gosterilmez ve ikinci asama devre disi kalir; imza, trust ve yayin URL alanlarinin sonradan girilmesi paketi gecersiz kilmaz. Dis kaynak dosyalarinin icerigi proje dosyasindan otomatik takip edilmez. Degisik kaynakla yeniden paket icin yeni surum veya baska guvenli cikti konumu kullanilir. Cikti klasorunu ac dugmesi Windows Dosya Gezgini ile mevcut klasoru acar; EXE dosyasini calistirmaz. Ilk adim yalnizca .tzpkg dosyasi olusturur, kurulum EXE'si degildir. Imzali dagitim ciftini hazirlama adimi ayni klasordeki package ve theme dosyalarini kullanir ve yeni bir distribution alt klasoru olusturur.

## Cevrimici yayin kaynagi

Cevrimici Kurulum (Web Installer) projesinin 04 Cevrimici yayin adiminda **Yayin kaynagi** secilir:

- **GitHub Releases:** herkese acik `sahip/depo`, tam yayin etiketi (or. `v1.2.3`) ve imzali YAML manifest/imza asset dosya adlari girilir. Studio bu girdilerden `https://github.com/sahip/depo/releases/download/v1.2.3/<dosya>` seklinde iki HTTPS URL olusturur. Dosyalarin GitHub'a yuklenmesi mevcut kullanici sorumlulugundadir. `stable`/`beta` proje kanali yayin etiketini kendiliginden degistirmez. Bilincli olarak sabit tag secilir; `releases/latest` otomatik secilmez.
- **Ozel HTTPS sunucusu:** iki var olan HTTPS URL'si manuel girilir; CDN veya herhangi bir guvenli sunucu kullanilabilir.

Yayin ve imza baglantilarinda HTTP, sorgu parametresi, fragment ve URL icine kullanici adi/parola konulamaz. GitHub owner, repo, tag ve asset adlarinda path-traversal ve URL operator karakterleri reddedilir; ayri alanlar .tzproj icinde kalici saklanir. Eski .tzproj dosyalari `webReleaseSource` alani olmadiginda `https` secenegiyle acilir ve onceki adresler kullanilir. Secilen kaynak yalnizca adresleri belirler; imza ve SHA-256 dogrulama mevcut kurulum motorunda devam eder. GitHub private repository token'lari EXE'ye gomulmez; bu ilk asamada private repository indirimi desteklenmez.

Bu entegrasyon GitHub'a surum/asset yuklemez, bulut (S3/R2) API'si kurmaz, gercek CDN erisilebilirligini veya temiz Windows VM kurulumunu otomatik dogrulamaz.

## Baglantiyi Test Et (Studio)

**Cevrimici Kurulum (Web Installer) / 04 Cevrimici yayin** adimindaki **Baglantiyi Test Et** dugmesi, kaynak ayarlarini dogrulamak icin arka planda `check-release-source` CLI komutunu cagirir. GitHub Releases seciminde herkese acik release tag ve iki asset URL'si otomatik hesaplanir; Ozel HTTPS seciminde kullanicinin girdigi iki URL kullanilir. Yayinci guven dosyasi zorunludur. Islem arayuzu kilitlemez; kullanici kaynak ayarlarini degistirirse tamamlanmis eski sonuc gecerli kabul edilmez.

Komut: `turkuaz-installer check-release-source --package-id ID --manifest-url HTTPS_URL --signature-url HTTPS_URL --trust-store POLICY.yaml`. Bu komut:
1. HTTPS URL parametrelerini sinirlar; HTTP, URL icine gomulu kimlik bilgisi, query ve fragment kabul etmez.
2. Boyut sinirli manifest ve CMS detached imza dosyalarini indirir; mevcut `read_verified_release` ile imzayi, istenen paket kimligini ve yayinci politikasini dogrular.
3. Imzali manifestteki uygulama paketinin HTTPS olmasini ister ve paket icin yalnizca **HEAD** istegiyle erisilebilirligi ve sunucunun bildirdigi boyutun imzali boyutla uyumunu denetler.
4. **Kurulum yapmaz, uygulama paketini indirmez, cikti veya state dosyasi olusturmaz.** Sunucu HEAD desteklemiyorsa kontrol basarisiz raporlanabilir; bu, paketin GET ile kesin indirilemediginin kaniti degildir. HEAD basarisi da paketin SHA-256/hash veya Authenticode kontrolunu sagladigini kanitlamaz.

CLI birinci adimda gercek HTTPS sunucu baglantisi gerektirir. CI unit testleri URL validation, secili kaynak ve trust-file girdi kontratini sinar; public GitHub release'inden gercek CMS imzali manifest / package HEAD E2E testi bu asamada **yapilmadi**. Temiz Windows kabulunde gercek sertifika ve imzali release ile ayri dogrulama gereklidir.

## WebSetup EXE cikarmak

Yeni **Cevrimici Kurulum** projesi secilir. Tam boy Studio'nun **04 Cevrimici yayin** bolumunde **GitHub Releases** veya **Ozel HTTPS** secilir; buna gore release etiketi ve asset adlari ya da iki dogrudan HTTPS URL'si girilir. Yayinci trust store (public policy) ve Windows setup GUI stub'i secilmis olmalidir. Once **1 Uygulama paketini hazirla**, sonra **2 Cevrimici yukleyici olustur** kullanilir. Studio, ayni versiyonlu proje klasorunun `web/TurkuazWebSetup.exe` dosyasina bir Windows WebSetup stub'i uretir.

WebSetup dosyasi .tzpkg uygulama verisini tasimaz. Icinde paket kimligi, imzali manifest ve imza HTTPS adresleri, hedef kurulum klasoru, secili setup temasi ve **public** yayinci trust store yer alir. Yayin private key'i, imzalama parolasi, kimlik dogrulama bilgisi veya paket verisi EXE icine eklenmez. Bu nedenle EXE genellikle full installer'dan kucuktur. Tek dosyadir; kullanicidan manuel manifest URL'si veya ayri trust-store dosyasi istemez.

Kurulum sihirbazi, gomulu yayini kullanip mevcut CLI HTTPS indirme ve detached CMS, SHA-256 ve istege bagli Authenticode dogrulamasindan gecirir. URL'ler HTTPS olmali; HTTP, credentials, fragment reddedilir. Gomulu ayar ve theme bounded JSON ile SHA-256 footer kullanir; bu digest yayinci imzasi **degildir**. **WebSetup.exe dagitimdan once Authenticode ile imzalanmalidir.** EXE ve imzali release ayni yayinciya ait olmali; guven zinciri VM testinde kontrol edilir.

Windows CI, imzasiz bir WebSetup test EXE'si olusturup config'i geri okur ve byte degistirilmis config'i reddeder. CI ornegi `.test` URL'si tasir, bu nedenle gercek bir sunucudan indirme yapmaz. Manuel temiz Windows VM'de gercek imzali HTTPS yayin test edilmedikce production hazir sayilmaz.

Komut satiri ayni output'u `build-web-setup --stub GUI.exe --package-id ID --manifest-url HTTPS_URL --signature-url HTTPS_URL --trust-store trust.yaml --target DIR --theme-config setup-theme.json --output WebSetup.exe` ile uretebilir. `web-setup-info --file WebSetup.exe` config'i ozetler (trust policy'yi yazdirmaz).

## Rust Standart Kurulum (NSIS gerektirmez)

Tam Kurulum projesinin birinci adiminda `application.tzpkg` hazirlanir.
Ikinci adim olan "2 Standart Kurulum olustur", Studio'nun Rust EXE
motoruna paketi ekleyerek `standard/TurkuazSetup.exe` uretir.
Bir NSIS derleyicisi veya imza sertifikasi gerektirmez.

Bu EXE kendi baslatildiginda Rust yerel kurulum penceresini acar,
paketin kimligini/surumunu gosterir ve uygulama dosyalarini
`LocalAppData/Programs/<package-id>` altina kurabilir.
Kullanici hedef klasoru degistirebilir. Mevcut hedef asla uzerine
yazilmaz. Paket ZIP path, symlink, normal dosya ve boyut denetimleri
ile SHA-256 butunluk kontrolunden gecer.

### Windows standart kurulum sihirbazi (Rust, gelistirme)

Turkuaz Setup Studio tarafindan `standard/TurkuazSetup.exe` icine
gomulen `.tzpkg` paketinin yerel GUI'si altı asamadan olusur:
**Hos geldiniz -> Kurulum konumu -> Ek secenekler -> Son kontrol ->
Kuruluyor -> Kurulum sonucu**.

- Ilk dort sayfada `Geri`/`Ileri` bulunur; son kontrol onayi olmadan
  dosya kurulumu baslamaz.
- Kurulum konumu sayfasinda tek kullanici / tum kullanicilar secimi bulunur.
  Varsayilan hedef yolu secime gore Windows `UserProgramFiles` veya
  `ProgramFiles` known-folder API uzerinden otomatik degisir.
  Tum kullanicilar icin yalnizca `ProgramFiles` kokunun altina kurulum
  yapilir, tam yonetici tokeni gerekir. Bu gelistirme dalinda UAC onayi
  `ShellExecuteW(runas)` ile istenir; yeniden acilan yukseltilmis setup,
  paket kimligini ve hedef yolu tekrar dogrular ve son kontrol ekraninda
  ikinci acik kuruluma onay alir. UAC reddedilirse dosya yazilmaz.
  Sessizce current-user kurulumuna donulmez. Ortak masaustu/Start menu
  icin `PublicDesktop` ve `CommonPrograms` klasorleri kullanilir.
- Hedef klasor secimi sadece on denetim yapar. Gercek yol/reparse,
  dosya butunlugu ve mevcut klasor korumasi paket motorundadir.
  `Gozat` secilen ust klasore `package-id` adinda yeni hedef ekler.
- Ek seceneklerde, pakette guvenli ana `.exe` bulunursa kullanici
  `Baslat menusu` ve `Masaustu` kisayollarini ayri secer.
  Ana EXE yoksa gereksiz kisayol sayfasi atlanir; hazirlik adim sayisi
  4 yerine 3 olur.
- Alt navigasyon duğmeleri sagda konumlandirilir, kademeli
  Segoe UI sistem fontu (varsa) ve daha okunur tipografi kullanilir.
  Kurulum engelli ise mesaj kullaniciya acikca gosterilir.
- Son kontrolde secilen hedef ve kisayollar gorunur. Kurulum baslayinca
  navigasyon kapanir. Paket motoru dogrudan `LocalInstallPhase`
  olaylarini yayimlar: gomulu paket dogrulama, arsiv cikarma ve
  sahiplik envanteri hazirlama, son klasore yayinlama. Ardindan secili
  Windows kisayollari yapilir. Olaylar gercek motor adimlaridir,
  dosya bazli yuzde veya kesin sure vaadi degildir.
- Kurulum tamamlandiginda basari / kisayol uyarisi / hata ayrilir.
  Hatadan sonra yeni hedef secmek icin duzenleme sayfasina donulebilir.
- Kurulum sirasinda guvenli iptal destegi olmadigindan pencere
  kapatilma istegi ertelenir. `CancelClose` arayuz denetimidir;
  guc kesintisi veya zorla proses kapatma durumunda ayri recovery
  testleri gerekir.

Ekran akisi Inno Setup ve NSIS Modern UI'nin geleneksel sihirbaz
gorunumunden, WiX WixUI InstallDir ve Advanced Installer'in gercek
konfigurasyon sayfalarindan esinlenir. Microsoft Windows arayuz
rehberine gore gereksiz lisans/bilesen secim sayfalari sahte
secenek olarak eklenmedi; bu alanlar gercek proje gereksinimi olunca
ayri veri modeli ile gelistirilmelidir.

Kaynaklar:
- https://documentation.help/Inno-Setup/topic_wizardpages.htm
- https://nsis.sourceforge.io/Docs/Modern%20UI%202/Readme.html
- https://docs.firegiant.com/wix/tools/wixext/wixui/
- https://www.advancedinstaller.com/user-guide/user-interface-dialogs.html
- https://learn.microsoft.com/en-us/windows/win32/uxguide/exper-setup

### Yapilandirilabilir kurulum sayfalari ve EULA

`03 Kurulum tasarimi` sayfasindaki **Sihirbaz sayfalari** editoru,
sayfalari secme, acip kapama, yeniden siralama, Turkce/English baslik
ve aciklama belirleme imkani verir. Istege bagli asamalar: Karsilama,
Lisans sozlesmesi, Kurulum klasoru, Kisayol secimi. `Kuruluma hazir`,
`Kuruluyor` ve `Tamamlandi` sayfalari guvenlik akisi icin zorunludur;
kaldirilamaz ve siralari degistirilemez.

Lisans sozlesmesi (EULA) varsayilan olarak **kapali** gelir; mevcut
`.tzproj` projeleri `wizard` alani olmadan acilir. EULA acilinca
gercek urune ait Turkce ve Ingilizce sozlesme metinleri gerekir.
Metinler **proje sahibi tarafindan** girilir; ornek/hukuki sozlesme
uydurulmaz. Setup ekraninda metin kaydirilabilir; "Lisans kosullarini
kabul ediyorum" onay kutusu isaretlenmeden Ileri etkinlesmez.
Dil degistirilince onay sifirlanir. Tum-kullanicilar UAC ile
yeniden baslatmada EULA etkinse yeni surecte yeniden onay gerekir.

Proje `schemaVersion=1` korunur ve `wizard` alani istege baglidir.
Rust `TZLOCAL1` paket son ekinin dosya cikarma sozlesmesi korunur;
yeni EXE, `TZLOCAL2` son eki ile yapilandirilmis sihirbazi isaretler.
EULA/sayfa JSON'u paket baytlarindan hemen once ayri bir SHA-256
kontrollu, uzunlugu sinirli `TZWIZR01` blogunda tasinir. `TZLOCAL1`
EXE'ler varsayilan sayfalarla acilir; `TZLOCAL2` icinde eksik veya
bozuk sihirbaz blogu varsa EULA atlanmaz, dosya reddedilir. SHA-256
tek basina yayinci kimlik dogrulamasi degildir.
Degisen sihirbaz ayarlari ile kurulum EXE'si yeniden olusturulurken
eski taninabilir `standard/TurkuazSetup.exe` dosyasi silinmez,
`standard.backup-...` altina tasinir. Bilinmeyen cikti klasorune
dokunulmaz.

### Windows kisayollari (gelistirme, imzasiz standart kurulum)

`02 Uygulama dosyalari` bolumunde **ana uygulama EXE'si** (kaynak
dizinine gore goreli `entryPoint`) secilirse yeni Rust kurulum
penceresinde **Baslat menusunde kisayol olustur** varsayilan acik,
**Masaustunde kisayol olustur** ise varsayilan kapali olarak gorunur.
EXE tanimlanmazsa ya da paket ana uygulama yolu EXE degilse kisayol
olusturulmaz; tahmini EXE secimi yapilmaz.

Kisayollar Windows known-folder API ile mevcut kullaniciya ait gercek
Baslat Menusu/Programs ve yonlendirilmis Masaustu yolunda olusturulur.
`<package-id>/<package-id>.lnk` ve `<package-id>.lnk` adlandirmasi
kullanilir. Ucuncu taraf kisayollari ve mevcut Baslat menusu gruplari
asla ezilmez. Yalnizca kurulmus ve kurulum koku icinde dogrulanan EXE
hedeflenir. Kisayol hatasinda yuklenen dosyalar saklanir, son durum
eksik Windows butunlesmesi olarak acikca raporlanir.

Yeni yerel kurulumlar `.turkuaz-local-install.json` sahiplik kaydi ile
yayinlanir: kayit tam payload dosya yollari, SHA-256 ozetleri ve boyutlari
tasir. `turkuaz-installer uninstall-local --package-id ID --target DIR
--confirm-uninstall YES` sadece degistirilmemis kayitli dosyalari siler;
taninmayan/degistirilen klasor ve dosyalara dokunulmaz. Onceki
v0.2.19 kurulumlarinda kayit olmadigi icin otomatik kaldirma reddedilir.

Yeni kurulumda uygulama dosyalari yayinlandiktan sonra,
Windows `HKCU` (tek kullanici) veya `HKLM` (tum kullanicilar)
`Uninstall` kaydi olusturulur. Kalici kaldirici setup EXE'si kurulu
uygulama kokunun **disinda** `LocalAppData` / korumali `ProgramFiles` altinda
saklanir. Baslat menusu `Kaldir` kisayolu ve kayitli uygulama
kisayollari SHA-256 ile sahiplik envanterine alinir. Kaldirma ayri bir
onay ekrani ile baslatilir ve paket motorunun degistirilmemis dosyalar
kontrolu kullanilir. Degistirilmis veya yabanci kisayollar korunur.
Kaldirici EXE calisirken silinemeyebileceginden Windows'un yeniden
baslangicta silme mekanizmasi denenir; yetki yoksa kalici kaldirici
dosyasi kalabilir ve kullaniciya raporlanir.

Kurulum dili hos geldiniz ekraninda `Turkce` veya `English` olarak
secilir. Degistirilme yalnizca gorunum metinlerini etkiler; paket
kimligi, dosya yolu, guvenlik kararlari ve imzali yayim degismez.

**Kalan kabul:** Tum kullanicilar UAC baslatma ve HKLM kaydinin gercek
Windows 10/11 PC testleri, iptal/onarim parity, Studio'dan dili varsayilan
secme ve imzali installer kisayol parity'si. Yerel receipt kriptografik
imza degildir.
Bu kalan guvenlik ve kabul kapsami GitHub issue #64'te izlenir.
Kisayol birlestirme testleri Windows hosted CI'da calisir; manuel temiz
VM ve signed lifecycle kabul testleri ayridir.

**Guvenlik siniri:** Imzasiz standart paket kendi icindeki SHA-256'yi
kontrol eder fakat bu, yayinci kimligi/dijital imza dogrulamasi
degildir. Bu yol signed manifest trust/secure update/repair/rollback
motorunu kullanmaz. Imzali dagitim islemi Studio'da ayri gelismis
secenektir; ilerideki Pro otomasyonu Community'nin guvenligi asamaz.
Windows bilinmeyen yayinci uyarisi verebilir. Imzasiz EXE production
imzalama ve temiz Windows kabulunden gecmis sayilmaz.

Kurulum penceresini otomatik tiklamadan kurulum motoru testini Windows'ta
izole ornek uygulama ile dogrulamak icin:

```powershell
pwsh -NoProfile -File .\\rust\\scripts\\windows-native-setup-smoke.ps1 `
    -CliPath .\\rust\\target\\release\\turkuaz-installer-v0.2.19-windows-x64.exe `
    -StudioStubPath .\\rust\\target\\release\\turkuaz-setup-builder-v0.2.19-windows-x64.exe
```

Bu betik `NATIVE_RUST_SETUP_BUILD_PASS`, `NATIVE_RUST_SETUP_INSTALL_PASS`,
`NATIVE_RUST_SETUP_TAMPER_REJECTED` ve
`NATIVE_RUST_SETUP_OVERWRITE_REJECTED` adimlarini raporlar.
Gercek UI tiklama/kurulum deneyimi ayri Windows kabul testidir.

Eski NSIS yerel demo test betigi geriye donuk deneme icin dosya
gecmisinde bulunabilir; standart kurulumun bagimliligi degildir.
Gelistirmis imzali dagitimda NSIS yedek yukleyici ozelligi
istege bagli olarak ayrica bulunabilir.

## Istege bagli antivirüs taramasi

**02 Uygulama dosyalari** bolumunde "Paket olusturmadan once antivirüsle
tara" secenegi varsayilan olarak kapalidir. Acilirsa listeden TurkuazAV
(YARA-X) veya ClamAV secilebilir. Kullanici sectigi AV programinin
yolunu belirler. ClamAV icin `clamscan` kurulmus ve imza veritabani
guncel olmalidir. TurkuazAV icin imzali YARA-X kural dosyalari, motor,
ve guvenilir CLI/acik anahtar SHA-256 sabitleri gereklidir.

TurkuazAV'da kural eslesmesi yoksa **varsayilan olarak paketleme
durdurulur** (Unknown). Yalnizca kullanici kural kapsamini bilerek
onaylarsa no-match durumunda ilerlenebilir. ClamAV'da tarama hata
verirse, dosya boyutu limiti asilirsa veya virus bildirirse paket
olusturulmaz. Tarama acikken diger motora otomatik gecilmez.

Secim `.tzproj` dosyasina `avScan` altinda kaydedilir. Eski projeler
antivirus secenegi kapali olarak acilir; antivirüs programi kurmak
zorunda degildir. Secim degisirse daha once hazirlanmis paket "guncel"
sayilmaz ve yeniden hazirlanmalidir. Tarama her dosyayi kaynak
klasorunden degil, olusturulmus `.tzpkg` dosyasindan guvenli gecici
alana cikararak kontrol eder; kontrol tamamlanmadan paket paylasima
hazir hale getirilmez.

Bu asamada secim Studio'daki **paket olusturma** icindir; kullanicinin
elindeki kurulum EXE'si henüz son kullanici antivirüs secme arayuzu
saglamaz. AV taramasi tek basina virus yok garantisi vermez; yayinci
imzasi ve butunluk dogrulamasi ayri kontrollerdir.

## Cloudmersive ile çevrimiçi AV taraması (isteğe bağlı)

Antivirüs taraması seçiliyse açılır listeden **Cloudmersive (çevrimiçi)**
seçilebilir. Yerel EXE/ClamAV/YARA-X motoru istemez. Bulut taraması,
her bir uygulama dosyasını TLS üzerinden üçüncü tarafın
`POST https://api.cloudmersive.com/virus/scan/file` API'sine gönderir.
**Dosya içeriği karşı tarafa aktarılır.** Gizli veya telif hakkı nedeniyle
gönderme izni bulunmayan dosyalar için bu seçeneği kullanmayın.

Önce geçerli bir Cloudmersive hesabı ve API anahtarı edinin. Studio'yu
başlatan PowerShell oturumunda ortam değişkenini ayarlayın:

```powershell
$env:CLOUDMERSIVE_API_KEY = Read-Host 'Cloudmersive API anahtarı'
```

Bu değer `.tzproj` içine yazılmaz, program tarafından loglanmaz.
Studio'da **dosya gönderimini kabul ediyorum** onayı verilmeden tek bir
dosya bile gönderilmez. Her dosya en çok 32 MiB olabilir; daha büyük
dosya, kota, bağlantı ve belirsiz API yanıtı durumlarında paket oluşturma
reddedilir. Bulut taraması ücretli/kotalı olabilir; sağlayıcının veri
işleme şartlarını kullanıcı kontrol etmelidir. Temiz yanıt bilinmeyen
zararlı kod bulunmadığının kesin kanıtı değildir.

## Eski paket güncel değilse Standart Kurulum EXE oluşturma

"2 Standart Kurulum EXE oluştur" güncel paket varsa doğrudan EXE
oluşturur. Güncel paket yoksa önce **güncel proje ayarlarıyla** yeniden
paket hazırlar (antivirüs seçimi etkinse tarama uygular). Aynı
kimlik/sürümde daha önce Studio'nun oluşturduğu paket dizini varsa
önceki bütün çıktı klasörü, silinmeden
`<paket>-<surum>-backup-<pid>-<nonce>` biçiminde yedeklenir. Ancak
eski paket ve `project.tzproj` gerçek/okunabilir değilse veya başka
kimliğe aitse dizin korunur; farklı çıktı konumu ya da sürüm seçmek
gerekir. Yayın dosyaları sessizce üzerine yazılmaz.

Bu kolaylık sadece Community standart EXE akışındadır. İmzalı dağıtım
hala ayrı bir güvenilir imza/trust doğrulaması gerektirir.

## Derleme ciktisi

- application.tzpkg: kaynak klasorundeki payload'in Turkuaz paket bicimi.
- release-manifest.draft.yaml: paket boyutu ve SHA-256 ozeti hesaplanmis, sema denetiminden gecmis imzasiz manifest taslagi. Taslaktaki artifact URI yerel file: URI'sidir.
- setup-theme.json: secilen preset ve varsa marka/renk ozellestirmesi.
- project.tzproj: proje alanlarinin yeniden acilabilir kopyasi.
- distribution/TurkuazSetup.exe: imzali release bundle iceren birincil setup.
- distribution/TurkuazInstallerBackup.exe: ayni release bundle icin zorunlu NSIS yedegi.
- distribution/manifest-trust.yaml: imzali manifesti dogrulayan yayin politikasi; setup ve NSIS EXE dosyalariyla birlikte tutulmalidir.

Manifest taslagi bir yayina guven kazandirmaz. Yerel file: URI'si yalnizca paketleme/yerel deneme icindir; uzak dagitim icin artifact HTTPS adresine yuklenmeli ve manifest bu adresle tekrar uretilmelidir. Dagitim cifti butonu, secilen yayin anahtariyla imzali manifesti dis trust policy ile dogrular, package ve setup temasiyla gecici .tzsetup bundle uretir ve bunu setup GUI stub ile birlestirir. NSIS yedegi de ayni bundle'dan olusur. Uretilen iki EXE dagitilmadan once Authenticode ile imzalanmalidir. manifest-trust.yaml genel dogrulama politikasidir ve dagitim klasorunde setup dosyalarinin yaninda kalmalidir. Signing key ve trust policy Setup Studio projesine veya .tzpkg dosyasina gomulmez; imza/trust dosyalari .tzproj icinde yalnizca dosya yolu olarak saklanir. Eski .tzproj dosyalari yeni alanlar bos birakilarak acilir. RELEASE_SIGNING.md, CLI.md ve SMOKE_TEST.md imzalama akisinin ayrintilarini aciklar.
