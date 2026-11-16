<!--
Dosya Yolu: REPO_ROOT/rust/SMOKE_TEST.md
Amac: Community Rust gelistirme ciktisi icin Windows kabul adimlarini ve basari kanitlarini tanimlamak
Modul - Markdown
Version: 0.3.23
Aciklama: Windows DELETE preflight, envanter handle ve acik uygulama kabul matrisi
Bagimli Oldugu Katman: QA | Documentation
-->

# Windows smoke test ve kabul akisi

Windows CLI, GUI stub, Setup Studio ve self-extracting setup EXE'leri
embedded application manifest icinde `requestedExecutionLevel=asInvoker`
ve `uiAccess=false` tasir. UAC gerektiren all-users islemleri uygulama
manifestinin butun sureci administrator olarak baslatmasiyla degil,
ayri ve acik kullanici onayli elevation akisi ile yurutulur. CI,
Windows SDK `mt.exe` ile bu manifesti build artifact'larindan cikarip
degerleri dogrular. Bu manifest SmartScreen reputation/signature
uyarisini gidermez; signer guveni ve dosya itibari ayri yayin kapilaridir.

Bu rehber, Community Rust gelistirme ciktisini kontrollu bir Windows makinesinde denemek icindir. GUI setup stub artifact'i yalniz arayuz kabugudur; release bundle eklenmeden kurulum yapmaz. Gercek imzali release paketi ve yayinlayici tarafindan saglanan guven politikasi olmadan lifecycle kabul testi yapilamaz.

## Standart sihirbaz gorunurlugu (2026-11-13)

Standart kurulum penceresi varsayilan olarak 900x700 mantiksal boyutta
acilir. Baslik cubugu ve kenarlik dahil fiziksel pencere, bulundugu
monitorun Windows calisma alanina ortalanir; gorev cubugu ve monitor
koordinat baslangici hesaba katilir. Kucuk ekranlarda dis pencere
calisma alanina sigdirilir; sabit minimum boyut bunu engellemez.
Onceki pencere boyutu/konumu geri yuklenmez. Kullanici pencereyi
tasidiktan veya boyutlandirdiktan sonra tekrar zorla ortalanmaz.
Normal acilis ve UAC sonrasi devam ayni pencere baslatma yolunu kullanir.
Windows geometri sorgusu basarisizsa eframe'in ortalama davranisi
korunur; stderr'e `TI-GUI-001` yazilir (monitor/pencere yerlestirme hatasi).

Alt panel ilk karede de dugme, ayirici ve bosluklar icin yeterli alani
ayirir; sonraki karelerde egui olculen icerik yuksekligini kullanir.
Alt eylem satiri yalnizca dugmelerin yuksekligini ayirir; kalan alani
kaplayip sayfa icerigini kirpmaz. Standart kurulum ve kaldirma govdesi
kucuk pencerelerde dikey kaydirilabilir. Kurulumda her sayfanin kaydirma
konumu ayridir. Paket motoru, lisans/UAC onayi ve veri koruma kurallari
degismez. Takip: [Windows kabul #41](https://github.com/TurkuazLabs/TurkuazInstaller/issues/41).

- Otomatik regresyon: `unsigned_local_gui::tests::destination_controls_remain_visible_beside_header_and_footer`
  gercek egui sayfasini 900x700, 860x550 ve 760x490 boyutta, acik/koyu
  temada, Turkce/English ve ard arda uc karede cizer. Kurulum kapsami,
  kullanici secimi, Gozat ve Ileri metinlerinin kirpma alaninda
  tamamen gorunur oldugunu denetler. 900x700 acilista hedef yolu,
  Gozat ve klasor koruma aciklamasi kaydirmadan gorunmelidir.
  Mevcut Windows CI wizard test adimina dahildir. `setup_window_layout`
  geometri testleri kucuk ekran, %100/%125/%150/%200 olcekli pencere,
  gorev cubugu ve negatif/ofset monitor koordinatlarini kapsar.
- Windows 10/11 manuel kabul **NOT RUN**: %100/%125/%150 olcekte
  hos geldiniz, lisans, hedef, kisayollar ve son kontrol sayfalarini
  gezin. Hedef yolu yazilabilmeli, Gozat gorunmeli; uzun aciklama ve
  hatalara kaydirarak ulasilabilmeli. Dil ve tema degisimini deneyin.
  1920x1080 ve 1366x768 ekranlarda ilk acilisin ortasini, baslik cubugu
  ve alt dugmelerin gorev cubugu disinda kalmasini kontrol edin.
  Yan/ust gorev cubugu ve ikinci monitoru da deneyin. Pencereyi
  tasiyip yeniden boyutlandirin: sayfa degisince yeri korunmali;
  kapatip acinca yeniden ortalanmali. UAC sonrasi da kontrol edin.
- Kaldirma penceresinde onay kutusu, uygulama/konum ve veri koruma
  aciklamalarini kontrol edin; onaysiz kaldirma baslamamali. Bu manuel
  kabul tamamlanmadan GUI sorunu production icin kapanmis sayilmaz.
- Studio'nun yeni derlemesini kullanarak standart EXE'yi yeniden
  olusturun. Eski gomulu EXE, Studio dosyasi degistirilince guncellenmez.

## Baslat menusu ve masaustu kisayolu kabul matrisi (NOT RUN)

Windows 10/11'de ayri, temiz bir test kullanicisi veya geri alinabilir
test VM'i kullanin. Bu kontrol otomatik CI'dan farklidir; sonuc almadan
PASS olarak kaydetmeyin.

1. Ana EXE'si ve guvenli `Turkce Uygulama 2.0` gibi bir adi olan
   test paketiyle kurun. Baslat menusunde uygulama ve
   `Turkce Uygulama 2.0 Kaldır.lnk` gorunmeli; masaustu secimi
   varsayilan isaretli olmali. Kaldirici pencere basligi da
   `Turkce Uygulama 2.0 Kaldır` olmali; tek basina
   `Kaldır.lnk` yeni kurulumlarda olusturulmamali.
2. Ayni uygulamanin farkli, temiz test kurulumu icin masaustu
   secimini kaldirin. Baslat menusu uygulama/kaldirma kisayollari
   kalmali, masaustu kisayolu olusturulmamali.
3. `CON`, `con.txt`, `COM1`, `lpt3.txt` ve basinda/sonunda
   bosluk olan test urun adlarini sirayla deneyin. Bu adlarla
   Windows kisayolu veya kaydi yayinlanmamali; varsa guvenli
   geri alma sonucu ve hata iletisi kaydedilmeli.
4. Var olan yabanci masaustu .lnk dosyasi veya dolu yabanci Baslat
   menusu klasoru ile cakisma olusturun. Kurulum mevcut kisayolu
   degistirmemeli; kismi dosya yayinini/geri almayi dogrulayin.
5. Kaldirma sonrasinda sadece envanterdeki degismemis kisayollarin
   silindigini, kullanicinin duzenledigi .lnk'nin korundugunu ve
   cleanup log sonucunu dogrulayin. Onceki surumun envanterine
   ait `Kaldır.lnk` dosyasinin sadece sahipligi dogrulandiginda
   temizlenebildigini, yabanci bir `Kaldır.lnk` dosyasinin
   korunmasini da test edin. Tek kullanici ve UAC onayli
   tum kullanicilar akislari ayri raporlansin.

CI'daki `windows_shortcuts::tests` ve
`windows_local_registration::tests` Windows COM, known-folder
ve sahiplik regresyonlaridir. Ozellikle
`shortcut_handle_delete_preserves_replacement` dosya kimligi
kontrolunden once ve dogrulanmis handle acildiktan sonra
dosya yolunun degistirilmesi halinde yabanci kisa yolun
korunmasini; `shortcut_handle_preserves_changed_legacy_and_hardlinks`
eski hash kayitlarinin degisen dosyayi, yeni kimlik kayitlarinin
coklu baglantili dosyayi korumasini ve ayni dosyada Shell metadata
degisimini kabul etmesini test eder.
Bu iki regresyon, izolasyonlu Windows otomatik testleridir;
gercek masaustunde dosya yolu yarisi ve antivirus etkisi
**NOT RUN**. Gercek masaustu, profil politikasi,
yeniden yonlendirme ve Windows 10/11 gorsel kabulu icin yeterli
degildir. Takip: [Windows manuel kabul #41](https://github.com/TurkuazLabs/TurkuazInstaller/issues/41).

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
6. Bir `package-id` icin normal kurulum yapip Windows Yuklu
   Uygulamalar kaydini ve kaldirici envanterini koruyun. Ayni
   hedefe temiz/recycle kurulum denemesi, onay isaretlense bile
   onceki klasoru tasimadan reddedilmeli. Windows uygulama
   kaydi, kisayollar ve kullanici dosyalari dokunulmadan kalmali.
   Once normal kaldirma akisini gerceklestirin; sonra ayni hedefe
   yeniden kurulum deneyin. Ayrica eski kaldirici envanterinin
   kalmis oldugu yetim-kayit senaryosunda fail-closed davranisi
   ayri inceleyin.
7. Test uygulamasinin eski Windows kaydi ve harici kaldiricisi
   yokken hedef dizinde `.turkuaz-local-install.json` yerel payload
   envanteri bulunsun. Recycle onayi verilse bile kurulum eski
   dosyayi ve klasoru tasimadan reddedilmeli. Envanter dosyasinin
   icerigini gecersiz yapan test verisiyle de fail-closed davranisi
   gozlemleyin. Bu test yalnizca izole test VM'inde yapilir.
8. Paket staging asamasinda baska bir test islemi ayni hedef
   icin Windows uninstall kaydi veya harici kaldirici EXE'si
   olusturursa, recycle callback'i kaydi tekrar fark edip islemi
   durdurmali; hedefteki eski klasor ve kullanici dosyalari
   degismemelidir. Test tek kullanimlik VM'de, test kimlikleriyle
   ve kontrollu staging/yarismaya izin veren QA duzenegiyle
   yapilmalidir. Gercek eszamanli kurulum testi henuz NOT RUN.

`install-local-self` CLI komutu onaysiz oldugu icin
mevcut klasore kurulum yapmayi reddetmeye devam eder.
Eski klasorun geri alinmasi otomatik degildir.

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

### Kaldirici yonetilen dizini - yol ve yabanci dosya korunumu

Otomatik Windows regresyonu:
`windows_local_registration::tests::managed_directory_rejects_unsafe_parent`
goreli yol ve `..` iceren yolu dizin olusturmadan reddetmeli; yabanci
ebeveyn/son klasor dosyasini oldugu gibi birakmali. Normal mevcut
`TurkuazInstaller/Uninstallers` dizinindeki kullanici dosyalari korunmali.
Windows 10/11 temiz VM'de, ozellikle all-users/UAC yolunda junction
ve eszamanli degisen ust dizin senaryolari ayrica elle denenmeli;
bu fiziksel kabul **NOT RUN**. Salt otomatik test, race-free yol
guvenligini kanitlamaz.

### Windows Uninstall kaydi UTF-16 sahiplik kontrolu

Windows CI'da `registry_sz_requires_exact_utf16_terminator` testi,
gecerli Turkce UTF-16 degerini kabul etmeli; eksik NUL, ara NUL,
cift NUL, tek bayt boy ve bozuk surrogate dizisini reddetmeli.
`hkcu_record_is_created_read_and_deleted_without_touching_other_keys`
testi izole HKCU anahtarinda kayitli `REG_SZ` degerini yerinde
degistirerek birden fazla metnin tek degerde saklanmasini reddetmeli;
sonra temiz kaydi yeniden okuyabilmeli. Bu regresyonlar
gercek kullanici kaydi veya mevcut kurulumu degistirmez.
Hosted CI testleri gercek Windows masaustu kabulunun yerine gecmez;
fiziksel kabul **NOT RUN**.

## Program acikken kaldirma kabul matrisi

Windows 10/11'de yalnizca izole test paketi ve geri alinabilir VM
kullanin. Programin kendi penceresini gercekten acin, sonra Windows
Yuklu Uygulamalar uzerinden kaldirmayi baslatin.

1. Program calisirken kaldirmayi onaylayin. Kaldirici uyarmali,
   uygulamayi zorla kapatmamalidir. Baslangicta kayitli ilk
   payload dosyasi, EXE, receipt, HKCU/HKLM kaydi ve kisayollar
   mevcut kalmali. Program verilerini, acik belgeleri kaydetmeli.
2. Programi normal bicimde kapatin. `Tekrar dene` tusuna basin;
   kaldirici yeniden onay almali, envanteri ve sahipligi tekrar
   kontrol etmeli, sonra normal kaldirmayi tamamlamali.
3. EXE acik degilken paket icindeki yuklenmis DLL/OCX dosyasi
   baska bir surecte kullanimdaysa kaldirma engellenmeli.
4. Restart Manager sorgusu hata verirse kaldirma varsayimla
   devam etmemeli. Hata ayrica loglanmali, kullanici verileri
   ve receipt korunmali. Hicbir durumda zorla sonlandirma,
   reboot veya sessiz kapatma kabul edilmez.
5. Testi hem standard-user HKCU hem de UAC ile onayli
   all-users HKLM senaryosunda tekrarlayin; sonuc, ekran
   goruntusu ve kurulum klasoru SHA-256 kaydedin.

Windows CI, `windows_uninstall_prioritizes_executable_resources`
regresyonunda kayitli dosyalar alfabetik olarak veri once olsa dahi
Windows EXE/DLL/COM/OCX siralamasinin once uygulandigini,
butun envanter girdilerinin bir kez ele alindigini dogrulamali.
Ayrica `windows_locked_later_file_blocks_before_any_deletion`
regresyonunda alfabetik olarak sonraki bir veri dosyasinin DELETE
paylasimi olmadan acik tutulmasi, ilk payload silinmeden kaldirmayi
reddetmeli; kilit kapandiktan sonra kurulum tamamen kaldirilmalidir.
`windows_receipt_handle_stays_pinned_until_deletion` testinde,
okunup dogrulanmis envanterin acik handle'i uzerinden rename
reddedilmeli ve ayni handle ile silme yalnizca envanteri
kaldirmalidir; payload korunmalidir.
`windows_running_application_blocks_removal_until_closed`
regresyonunda izole fixture EXE'sini gercek subprocess olarak
calistirir; program acikken silmeyi reddetmeli, ilk payload
dosyasini korumali, child durduktan sonra temiz kaldirmalidir.
Bu, acik GUI penceresi ve farkli kullanici oturumlari icin
fiziksel kabulun yerini **ALMAZ**. Eszamanli yeni program
baslatma yarisi ayrica **NOT RUN**.

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
   fail-closed davranip veriyi korumali. Izole test VM'inde
   kayitli kaldirici envanteri icin traversal/relative path,
   directory ve junction/reparse link durumlari da reddedilmeli.
   Bu olumsuz denemeler gercek musteri klasorlerinde yapilmaz.
   Harici kaldirici kayit envanterini yazma hakkiyla acik
   tutan test isleminde kaldirici baslamamali; yazici
   kapandiginda yeniden denenebilmeli. Ayrica uygulama
   hedefindeki `.turkuaz-local-install.json` **payload**
   envanteri yazilabilir handle ile acikken kaldirma payload
   dosyalarini silmeden reddedilmeli; handle kapandiginda
   normal kaldirma tekrar denenebilmeli. Bu iki ayri
   envanter icin testi izole Windows VM'de yapin.
   Windows dosya taniticisiyla silme regresyonunda SHA-256 ile
   dogrulanmis uygulama dosyasini baska ada tasiyin; ayni
   eski yola farkli kullanici dosyasi yerlestirin. Kaldirici
   yalnizca dogrulanmis dosya nesnesini silmeli ve yeni
   kullanici dosyasini korumali. Bu senaryoyu yalnizca
   izole QA fixture'i / test VM'inde calistirin.
   Test kurulumunun HKCU `UninstallString`, `DisplayName` veya
   `DisplayIcon` degerini tek seferde degistirerek kaldiriciyi
   acmayi deneyin: daha payload silme baslamadan reddedilmeli;
   orijinal dosyalar ve kisayollar korunmali. Degerleri geri
   getirdikten sonra normal kaldirma hala calismali.
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

Gercek release icin imzali `.tzsetup` paketini `build-self-extracting-setup --stub GUI.exe --bundle release.tzsetup --output TurkuazSetup.exe` ile tek Rust EXE dosyasina donusturun. Son dosyayi gercek yayinci sertifikasi ile Authenticode imzalayin ve CMS/publisher pinini dogrulayin. Tek kurulum sihirbazinin marka, renk, install/update/repair/rollback/uninstall, iptal ve kurtarma davranisini temiz Windows 10/11 makinelerinde ayri kabul edin.

## Basari kanitlari ve hata raporu

Her akista su bilgileri kaydedin: Windows surumu/architecture, artifact workflow run linki ve SHA-256, paket kimligi/surumu, calistirilan komut, exit code, tam hata metni ve ilgili log dosyalari. Trust-store dosyasini, private key'i veya imzali release icerigini herkese acik issue'ya eklemeyin.

Su durumlarda kabul basarisiz sayilir: gecersiz imza veya digest'in reddedilmemesi; target/data-root disina beklenmeyen dosya yazilmasi; kurulum/guncelleme kesintisinde tutarsiz aktif surum; list/verify/launch'in kurulum durumuyla celismesi; rollback'in imzali rollback politikasini asmasi; uninstall'in yonetilmeyen dosyalari silmesi; primary/backup'un ayni signed release'i kurmamasi.

## Release oncesi kalan bloklar

Windows CI smoke betigi her dis process icin 300 saniyelik sure siniri uygular, `signtool.exe` dosyasini Windows SDK'nin surumlu x64 klasorlerinde arar ve tum smoke adimini 15 dakika ile sinirlar. Test sertifikasi guven magazasina eklenmez. Authenticode durumu `Valid`, `NotTrusted` veya yalnızca guvenilmeyen self-signed root mesajini veren `UnknownError` olabilir; signer thumbprint'i eslesmeli, hash uyusmazligi ve diger hata mesajlari reddedilir. Sertifika hazirligi, guven magazasi ve paket imzasi ayri asama ile loglanir.

Bu rehber production kabul kaniti degildir. CI, gecici self-signed sertifikayla detached CMS imzali offline bundle, install/update/repair/rollback/uninstall, tamper reddi ve tek Rust `TurkuazSetup.exe` olusturma / Authenticode test signer dogrulamasini sinar. Windows 10/11 manuel GUI kabulunu, gercek yayinci sertifikasini, SmartScreen'i veya production imzali dagitim adapterlerini kanitlamaz. CLI/GUI kurtarma akislari ayrica dogrulanmalidir.

## Kucuk imzali ornek test kurulumu

Rust Migration CI basariliysa `turkuaz-example-test-setup-v<surum>-windows-x64` adli test ZIP'i olusur. ZIP yalnizca tek `TurkuazSetup.exe`, test icin pinli `manifest-trust.yaml`, Rust CLI ve kucuk bir Windows ornek uygulamasi icerir. Test imzasi production yayinci kimligi sayilmaz.

ZIP'i ayri bir klasore cikartip yalnizca deneme icin kullanin. Sihirbazda `Kur` islemini secip `İleri`'ye basin. Paket kimligini `ci.smoke` birakin; guven dosyasinin yanindaki test YAML dosyasi oldugunu kontrol edin; kurulum dizinini `%LOCALAPPDATA%\TurkuazInstaller-Test\App`, veri dizinini `%LOCALAPPDATA%\TurkuazInstaller-Test\Data` secin; son kontrol ekraninda `Kur ve doğrula`'ya basin. Ardindan `README-TR.txt` icindeki CLI komutuyla ornek uygulamayi acin. Kaldirmayi GUI'den denemek icin setup'i yeniden acin, `Kaldır` islemini secin, ayni paket ve yollari kullanin, son ekrandaki onay kutusunu isaretleyip `Paketi kaldır`'a basin. Self-signed CI sertifikasi nedeniyle Windows yayinci uyarisi gosterebilir; bu imza production yayincisini dogrulamaz ve sertifika Trusted Root'a eklenmemelidir. Artifact yedi gun tutulur ve production'da kullanilmaz.

CI artifact'inin olusmasi kod dogrulamasidir; temiz son kullanici makinesinde elle GUI testi yerine gecmez.

### Kaldirici EXE temizligi (Windows)

- Izole test VM'inde `TurkuazInstaller` managed ust klasorunu
  junction/reparse olarak olusturdugunuzda yeni `Uninstallers`
  alt klasoru acilmamali; baglantinin hedefindeki kullanici
  dosyalari degismemeli. Normal owned klasorlerde kurulum ve
  kaldirma calismali. Eszamanli path degistirme / gercek junction
  GUI kabul testi henuz **NOT RUN**; hosted CI bu testi
  dogrudan gerceklestirmiyor.
- Per-user kaldirici `MoveFileExW / DELAY_UNTIL_REBOOT` kullanmaz. Kucuk bir
  Rust yardimci sureci asil kaldiricinin process handle'inin sinyalini bekler.
  Surec bittikten sonra dogrulanmis EXE ve envanter handle'larini siler.
- SHA-256 ayni dosya taniticisinda kontrol edilir; ust dizinler sabitlenir.
  Reparse point, hardlink, degismis ve salt okunur dosyalar korunur.
  Yardimciya HANDLE_LIST ile yalniz DELETE, SYNCHRONIZE ve log yazma yetkisi
  aktarilir. Yardimci dosya yolu, PID aramasi veya shell komutu kabul etmez.
- Yardimci `build.rs` tarafindan Rust ile derlenir ve Studio icine gomulur.
  `Uninstallers/Cleanup/turkuaz-cleanup-<sha256>.exe` ortak onbellekte tutulur;
  tekrar kullanmadan once gomulu SHA-256 ile dogrulanir. All-users onbellegi
  yonetici korumali Program Files altindadir. Onbellek ve `cleanup-*.log`
  dosyalari uygulama kaldirmasinda bilerek korunur; tum klasor temizlendi denmez.
- Hazirlik/baslatma hatasi UI'da gorunur, EXE ve envanter korunur. Pencere
  kapandiktan sonraki sonuc `Cleanup/cleanup-*.log` dosyasina yazilir.
  Silme basarisizsa envanter tutulur. Shell, reboot veya ek UAC fallback yoktur.
- Hata kodlari: `TI-UNINSTALL-CLEANUP-001` kimlik/yol/icerik/sahiplik;
  `002` dosya silme erisimi; `003` kapanis sonrasi silme; `004` yardimci surec
  veya parent bekleme; `005` yardimci onbellek/log hazirligi. Basari: `TI-UNINSTALL-CLEANUP-OK`.
- Windows CI gercek Rust yardimci binary'siyle bekleme/silme, calisan EXE'nin
  surec sonuna kadar korunmasi, EXE+envanter temizligi, yabanci/degismis dosya
  ve degistirilmis yardimci onbelleginin korunmasini sinar. Ignore edilen
  child test ana test tarafindan ayri proses olarak calistirilir.
- Manuel kabul: Windows 10/11 standart kullanici oturumunda yeni Studio ile
  paketi yeniden olustur, kur, Windows uygulama listesinden kaldir. Sonuc
  acikken EXE ve envanter durmali; Kapat sonrasi ikisi kalkmali, log OK olmali,
  0x80070005 uyarisi olmamali. Manuel kontrol henuz CALISTIRILMADI.
- Eski uretilmis EXE ve onceden kurulmus kaldiricilar otomatik guncellenmez.
