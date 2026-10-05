# 📄 Dosya Yolu: /docs/REBOOT_RESUME.md
# 📌 Amac: TurkuazInstaller prerequisite reboot ve otomatik resume guvenilirlik modelini tanimlamak
# 📌 Modul - Markdown
# Version: 1.2.0
# Aciklama: Persisted request, pre-execution arm, HKCU RunOnce, post-reboot trust/re-probe ve scheduler cleanup zincirini sabitler
# Bagimli Oldugu Katman: Service | Repo | Tool | View | Config

# Reboot Resume

TurkuazInstaller prerequisite auto-install sirasinda Windows installer exit code 3010 veya 1641 donerse package apply adimina devam etmez.

Resume zinciri package-scoped ve fail-closed calisir.

## Kalici Veriler

Reboot oncesinde iki ayri kalici kayit vardir:

1. operation journal
2. installer resume request

Operation journal su karar verilerini tasir:

- operation id
- package id
- operation type
- expected version
- target path
- phase
- pending prerequisite id

Resume request su yeniden cozumleme verilerini tasir:

- package id
- operation type
- expected release version
- expected package artifact SHA-256
- release channel
- signed manifest source
- optional rollback manifest source

Bu iki kayit birbirinin yerine kullanilmaz.

Journal operasyon otoritesidir.

Resume request yalniz signed manifesti yeniden bulmak icin gereken request snapshotidir.

## Reboot Oncesi Akis

Eksik prerequisite icin:

1. signed manifest daha once dogrulanmis olmalidir
2. prerequisite installer indirilir
3. size ve SHA-256 dogrulanir
4. Authenticode publisher/certificate policy dogrulanir
5. resume request kalici repository icinde bulunur
6. HKCU RunOnce altinda package-scoped bootstrap relaunch kaydi olusturulur
7. journal RebootResumeArmed phase ve pending prerequisite id ile pre-execution checkpoint yazar
8. ancak bundan sonra prerequisite installer calistirilir

Installer normal basari donerse RunOnce kaydi silinir, post-install re-probe ayni process icinde yapilir ve armed checkpoint temizlenir.

Installer 3010 veya 1641 donerse:

- RunOnce kaydi korunur
- journal AwaitingReboot olur
- pending prerequisite id journal icinde kalir
- package staging/apply baslamaz
- operation Failed olarak isaretlenmez

## RunOnce Siniri

Community Windows runtime kullanici bazli su mekanizmayi kullanir:

HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\RunOnce

Value name ! ile baslar.

Bu secim Windows tarafinda RunOnce degerinin komut calisana kadar korunmasini saglar.

RunOnce komutu yalniz combined distribution root altindaki TurkuazInstaller.Bootstrapper.exe dosyasini ve package id degerini tasir.

Registry kaydi manifest URL, target path veya install komutu tasimaz.

RunOnce kaydinin tek basina package mutasyonu yapma yetkisi yoktur.

## Reboot Sonrasi Akis

Bootstrap normal prerequisite kontrolunu yaptiktan sonra internal resume package argumanini WinUI uygulamasina aktarir.

WinUI Service:

1. package-scoped resume request dosyasini okur
2. package-scoped operation journal dosyasini okur
3. journal phase degerinin RebootResumeArmed veya AwaitingReboot oldugunu dogrular
4. package id ve operation type eslesmesini dogrular
5. journal version ile expected resume version degerini dogrular
6. pending prerequisite id varligini dogrular
7. manifest kaynagini yeniden acar
8. detached CMS/PKCS#7 manifest signature trust kontrolunu yeniden yapar
9. resolved release version degerini persisted expected version ile karsilastirir
10. package artifact SHA-256 degerini persisted expected digest ile karsilastirir
11. ayni pending prerequisite detectorunu yeniden calistirir

Bu kontrollerden biri basarisizsa package staging/apply baslamaz.

## Reboot Dongusu Korumasi

Pending prerequisite reboot sonrasinda hala saglanmiyorsa ayni prerequisite installer tekrar calistirilmaz.

Runtime fail-closed durur.

Bu davranis bozuk prerequisite installer veya kalici reboot-required sonucunun sonsuz install/reboot dongusu olusturmasini engeller.

Pending prerequisite saglanmissa workflow ayni journal operation kimligi ile devam eder.

Sonraki prerequisite ayri bir reboot isterse yeni checkpoint ayni guven zinciri ile olusturulur.

## Manual Operation Siniri

Bir package journal kaydi AwaitingReboot durumundaysa normal GUI install, update, repair, rollback veya uninstall mutasyonu baslatilmaz.

Yalniz internal resume yolu bu checkpointi devam ettirebilir.

Bu kural yarim kalmis reboot state uzerine yeni operation yazilmasini engeller.

## Cleanup

Operation normal tamamlanirsa:

- operation journal silinir
- resume request best-effort temizlenir
- prerequisite installer reboot istemediyse RunOnce kaydi process icinde iptal edilir
- prerequisite installer hata verirse stale RunOnce kaydi temizlenir
- 3010/1641 donerse RunOnce kaydi reboot sonrasindaki tek seferlik relaunch icin korunur

Reboot bekleniyorsa journal ve resume request korunur.

## Windows Davranisi

Microsoft RunOnce belgelerine gore:

- HKCU RunOnce kullanici oturum actiginda tek seferlik komut calistirabilir
- value name basindaki ! silmeyi komut calisana kadar erteler
- RunOnce command line 260 karakter sinirina tabidir
- Safe Mode icin varsayilan davranis RunOnce calistirmamaktir

Community runtime Safe Mode zorlamasi icin * prefix kullanmaz.

Bu davranis normal Windows boot/login resume senaryosunu hedefler.
