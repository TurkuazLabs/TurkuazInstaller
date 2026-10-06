# 📄 Dosya Yolu: /docs/BOOTSTRAP_SELF_UPDATE.md
# 📌 Amac: TurkuazInstaller bootstrap self-update discovery, download, trust ve handoff modelini tanimlamak
# 📌 Modul - Markdown
# Version: 1.2.0
# Aciklama: Automatic update hatalarinda current launch fallback ile explicit self-update fail-closed trust sinirini sabitler
# Bagimli Oldugu Katman: Service | Tool | Config

# Bootstrap Self Update

Bootstrap self-update mevcut two-process handoff mekanizmasini kullanir.

Yeni katman handoff oncesinde update'i otomatik kesfeder, indirir ve dogrular.

## Discovery

Normal bootstrap startup sirasinda prerequisite kontrolu basarili olduktan sonra:

1. current bootstrap executable yolu resolve edilir
2. current bootstrap Authenticode trust kontrolunden gecemiyorsa auto-update atlanir
3. GitHub latest stable release endpointi sorgulanir
4. release tag SemVer olarak parse edilir
5. yalniz current versiondan daha yeni stable release kabul edilir
6. exact TurkuazInstaller.Bootstrapper.exe asseti aranir
7. asset state uploaded olmali
8. browser download URL https://github.com/... olmali
9. asset size pozitif olmali
10. GitHub release asset digest alani sha256: formatinda zorunludur

Automatic self-update availability ozelligidir; normal uygulama startup'inin guven zinciri degildir.

Asagidaki automatic self-update hatalari current trusted bootstrap ile desktop launch'i engellemez:

- network/GitHub availability hatasi
- malformed newer-release metadata
- download/size/hash failure
- untrusted replacement
- automatic handoff failure

Bu durumlarda ilgili update denemesi reddedilir; dogrulanmamis replacement asla calistirilmaz ve mevcut surumle startup devam eder.

Caller cancellation yutulmaz ve yukariya tasinir.

Explicit replacement ve complete-self-update akislari availability fallback degildir; trust failure durumunda fail-closed kalir.

## Download Integrity

Replacement staging alani:

%LOCALAPPDATA%/TurkuazInstaller/bootstrap-update

Downloader:

- response Content-Length varsa release size ile eslestirir
- stream expected size degerini asarsa aninda durur
- final byte count exact size ile eslesmelidir
- SHA-256 GitHub asset digest ile birebir eslesmelidir
- verification tamamlanmadan final replacement path dondurulmez

## Authenticode Trust

Self-update icin ayrica degistirilebilir signer config kullanilmaz.

Trust anchor calisan bootstrap executable'in kendi Authenticode signer kimligidir.

Current bootstrap:

- Windows WinVerifyTrust kontrolunden gecmelidir
- signer certificate okunabilir olmalidir

Replacement bootstrap:

- Windows WinVerifyTrust kontrolunden gecmelidir
- publisher subject current bootstrap ile ayni olmalidir
- signer certificate SHA-256 current bootstrap ile birebir ayni olmalidir

Bu nedenle unsigned development bootstrap auto-update yapmaz.

Certificate rotation planli bir release gecisi gerektirir; strict certificate identity otomatik olarak gevsetilmez.

## Handoff

Integrity ve Authenticode trust basarili olduktan sonra mevcut iki-process self-update handoff baslar.

1. downloaded replacement process complete-self-update modunda baslatilir
2. parent bootstrap kapanir
3. replacement current bootstrap dosyasini retry policy ile overwrite eder
4. yeni target bootstrap ayni application/resume argumentlariyla baslatilir
5. staged source cleanup argumenti ile temizlenir

Reboot resume argumentlari self-update boyunca korunur.

## Explicit Replacement

Eski internal/debug --begin-self-update <path> yolu korunur.

Bu yol artik trust bypass degildir.

Explicit replacement da current bootstrap ile ayni Authenticode publisher subject ve certificate SHA-256 kimligini tasimadan handoff baslatamaz.

Complete-self-update source executable da target bootstrap'a karsi ayni signer verification zincirinden gecmeden overwrite yapamaz.

## Release Asset

Signed release su standalone asseti de yayinlar:

TurkuazInstaller.Bootstrapper.exe

Asset combined distribution icindeki imzalanmis root bootstrap dosyasindan kopyalanir.

GitHub Release upload sonrasi asset digest discovery tarafindan kullanilir.

Standalone bootstrap EXE ayrica provenance attestation alir.
