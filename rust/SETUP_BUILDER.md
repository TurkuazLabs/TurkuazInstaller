<!--
Dosya Yolu: REPO_ROOT/rust/SETUP_BUILDER.md
Amac: Windows Setup Studio proje panelini, modal editorlerini ve derleme akisini anlatmak
Modul - Markdown
Version: 0.9.2
Aciklama: Setup Studio panelini, modal editorleri ve dagitim akisini belgeler
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

Ilk acilista iki proje sablonu yer alir: Tam Kurulum (tum dosyalari iceren offline paket) veya Cevrimici Kurulum (HTTPS uzerinden indiren kucuk EXE). Ustteki Baslangic dugmesi yeni proje secimine doner; sol menudeki bolumler ve proje ayarlari kartlari mevcut projeyi duzenler. Tekrarlayan "Projeye basla" karti ve sol menudeki ikinci baslangic kisayolu kaldirildi. Secince modal sihirbaz acilir. Eski .tzproj dosyasi Proje ac ile yuklenebilir. Yeni proje secimi mevcut calisma verisini degistirecekse onay modal'i acilir; baslangica donuldugunde acik projeye geri donulebilir. Proje calisma alaninda solda numarali adimlar, ortada tek sutunlu duzenleme kartlari, sagda son kullanici setup onizlemesi bulunur. Modal sihirbaz urun bilgileri, uygulama dosyalari, setup tasarimi ve yalnizca secilen tipe ait yayin/cikti alanlarini gosterir. Alt kisimda her iki tur icin 1 Uygulama paketini hazirla ve yalnizca secilen tipe ait 2 Tam Kurulum olustur veya 2 Cevrimici yukleyici olustur dugmeleri yer alir. Studio arayuzu Sistem/Acik/Koyu ayariyla kendi paletini degistirir; sagdaki onizlemenin Acik/Koyu secimi ise olusturulacak setup temasinin onizlemesini etkiler.

- Urun bilgileri: uygulama adi, paket kimligi, SemVer surumu ve stable/beta kanali.
- Uygulama dosyalari: kaynak klasoru ve istege bagli goreli EXE yolu.
- Setup tasarimi: varsayilan kurulum yolu, hazir tema preset'i, marka adi, alt baslik ve istege bagli HEX vurgu rengi.
- Cevrimdisi dagitim (Tam Kurulum): cikti klasoru, imzali manifest, detached imza, yayinci trust store'u, setup GUI stub'i ve istege bagli makensis.exe yolu.
- Cevrimici yayin (Cevrimici Kurulum): cikti klasoru, HTTPS manifest/imza adresleri, yayinci trust store'u ve setup GUI stub'i.

Uygulama paketi ve imzali dagitim cifti ana ekrandaki eylemlerden baslatilir. Gerekli bilgiler eksikse ilgili modal editor otomatik acilir.

Yeni projede varsayilan cikti klasoru Windows `LOCALAPPDATA/TurkuazInstaller/Projects`,
uygulama kurulum hedefi ise `LOCALAPPDATA/Programs/Uygulamam` altindadir.
Bu ilk acilista yonetici yetkisi gerektiren Program Files hedefini dayatmaz;
Explorer'dan veya kisayoldan acilista process CWD'sine gore degismez.
`LOCALAPPDATA` yoksa OS gecici dizininde `TurkuazInstaller/Projects`
yedek yolu kullanilir; bu durumda kalici cikti icin Studio'dan
guvenli bir klasor secilmelidir. Kaydedilmis `.tzproj` dosyalarinin
kullanici tarafindan secilmis cikti yollari korunur.

Ilk paket olusturma isteginde, yeni projedeki varsayilan cikti ust klasoru
henuz yoksa Setup Studio eksik alt klasorleri olusturur. Modal bolum
dogrulamasi klasor olusturmaz; yalnizca paket olusturma asamasinda
olusturulur. Dosya, sembolik baglanti/junction veya `..` gecisi iceren
ust dizinler reddedilir. Yetki ve disk hatalari acikca raporlanir.
Kullanici tarafindan secilmis cikti konumu degistirilmez; var olan
`{package-id}-{version}` klasorunun ustune yazilmaz.

Projeler .tzproj uzantisiyla kaydedilip Setup Studio icinde tekrar acilabilir. Acik bir proje varken baska proje secmek icin onay istenir; iptal edilen dosya secimi proje verisini korur. Modal sihirbazda dogrulama hatalari ilgili duzenleme penceresinin icinde gorunur. Her uygulama paketi derlemesi, 04 Yayin ve cikti bolumunde secilen cikti ust klasoru altinda yeni bir {package-id}-{version} klasoru olusturur; var olan klasorun ustune yazmaz. Uygulama dosyalari bolumundeki kaynak klasoru cikti konumu degildir. Ana ekrandaki Olusturulan dosyalar paneli hedef klasoru ve gercekten mevcut application.tzpkg ile distribution/TurkuazSetup.exe dosyalarinin durumunu ayri gosterir. Birinci asama paketi hazir sayilmasi icin `project.tzproj` anlik goruntusu guncel kaynak, urun, surum, kurulum hedefi ve tema ayarlariyla eslesmelidir. Bu alanlar degistiyse eski paket hazir gosterilmez ve ikinci asama devre disi kalir; imza, trust ve yayin URL alanlarinin sonradan girilmesi paketi gecersiz kilmaz. Dis kaynak dosyalarinin icerigi proje dosyasindan otomatik takip edilmez. Degisik kaynakla yeniden paket icin yeni surum veya baska guvenli cikti konumu kullanilir. Cikti klasorunu ac dugmesi Windows Dosya Gezgini ile mevcut klasoru acar; EXE dosyasini calistirmaz. Ilk adim yalnizca .tzpkg dosyasi olusturur, kurulum EXE'si degildir. Imzali dagitim ciftini hazirlama adimi ayni klasordeki package ve theme dosyalarini kullanir ve yeni bir distribution alt klasoru olusturur.

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

Yeni **Cevrimici Kurulum** projesi secilir. Modal sihirbazin **04 Cevrimici yayin** bolumunde **GitHub Releases** veya **Ozel HTTPS** secilir; buna gore release etiketi ve asset adlari ya da iki dogrudan HTTPS URL'si girilir. Yayinci trust store (public policy) ve Windows setup GUI stub'i secilmis olmalidir. Once **1 Uygulama paketini hazirla**, sonra **2 Cevrimici yukleyici olustur** kullanilir. Studio, ayni versiyonlu proje klasorunun `web/TurkuazWebSetup.exe` dosyasina bir Windows WebSetup stub'i uretir.

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

**Windows Kaldir kisayolu, Windows Uygulamalar kaydi, kisayollarin
sahiplik kaydina baglanmasi ve imzali installer kisayol parity'si henuz
uygulanmadi.** Yerel receipt kriptografik imza degildir; dis kullanici
verilerini otomatik silecek bir yol acilmaz.
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
