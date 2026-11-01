<!--
Dosya Yolu: REPO_ROOT/README.md
Amac: TurkuazInstaller Community ana sayfasinda main ve Rust gelistirme durumunu ayirmak
Modul - Markdown
Version: 1.9.0
Aciklama: Legacy .NET main kaynagi, Rust migration dali, dokumantasyon ve release kabul sinirlari
Bagimli Oldugu Katman: Documentation
-->

# TurkuazInstaller

TurkuazInstaller, masaustu uygulamalari icin ortak kurulum,
guncelleme, onarma, geri alma (rollback) ve kaldirma altyapisi
gelistiren bir projedir.

**Onemli:** Bu depoda iki farkli teknoloji asamasi vardir.
`main` ile Rust gecis dali ayni uygulama olarak sunulmamalidir.
Kaynak kod, belgeler, CI ve kabul kanitlari her dal icin ayridir.

## Hangi dal hangi uygulama?

| Dal | Teknoloji | Guncel durum |
| --- | --- | --- |
| [`main`](https://github.com/TurkuazLabs/TurkuazInstaller/tree/main) | .NET / WinUI 3, NativeAOT, Velopack | Onceki Community uygulamasi ve tarihsel teknik belgeler burada korunur. Kod ve CI calismalari production yayin onayi degildir. |
| [`rewrite/rust-community`](https://github.com/TurkuazLabs/TurkuazInstaller/tree/rewrite/rust-community) | Rust CLI, Setup Studio, setup wizard, signed `.tzpkg` / `.tzsetup` | Yeni Community gelistirme dali; [PR #63](https://github.com/TurkuazLabs/TurkuazInstaller/pull/63) **Draft**. `main`'e birlestirilmedi. |

Rust'in private Pro modulleri ayri depoda gelistirilir.
Public Community, Pro kaynak koduna baglanmaz.

## Yeni Rust gelistirmesini incelemek icin

Rust dalinda su belgeler bulunur:

- [Rust README ve gelistirici komutlari](https://github.com/TurkuazLabs/TurkuazInstaller/blob/rewrite/rust-community/README.md)
- [Rust belge dizini](https://github.com/TurkuazLabs/TurkuazInstaller/blob/rewrite/rust-community/docs/README.md)
- [Setup Studio / Cevrimici Kurulum](https://github.com/TurkuazLabs/TurkuazInstaller/blob/rewrite/rust-community/rust/SETUP_BUILDER.md)
- [Runtime ve production kabul durumu](https://github.com/TurkuazLabs/TurkuazInstaller/blob/rewrite/rust-community/rust/RUST_READINESS.md)
- [Windows manuel kabul rehberi](https://github.com/TurkuazLabs/TurkuazInstaller/blob/rewrite/rust-community/rust/SMOKE_TEST.md)

Rust gelistirme dalinin `203192d1` revizyonunda
[Linux ve Windows CI 41342571609](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/41342571609)
**PASS** (1 Kasim 2026). Bu basari build/test ve Windows
gelistirme artifact'lari icindir; gercek kullanici GUI tiklamasi,
production publisher signing, temiz Windows VM kurulumu ve
production release onayi **degildir**.

Rust dalinda prereq/shortcut/protocol/preserved-data adapter'lari,
gercek publisher guveni ve son kullanici kabul kapilari eksiktir.
Ana installer motoru Rust olsa da NSIS fallback kullanilir; tum
dagitim icin `%100 Rust` iddiasi henuz gecerli degildir.

## Mevcut `main` (.NET / WinUI) kaynagi

`main` dalinda su yapilar bulunur:

- `src/`: Domain/Application, Windows platformu, WinUI, CLI
  ve NativeAOT bootstrapper uygulamalari.
- `TurkuazInstaller.slnx` ve `TurkuazInstaller.Windows.slnx`:
  .NET cozumleri.
- `docs/`: Velopack, manifest guveni, prerequisite, Windows
  entegrasyonu, yayinlama ve sistem mimarisinin onceki gelistirme
  asamasina ait belgeleri.
- `examples/`: mevcut .NET kaynak agacina ait ornekler.

[Main dokumantasyon dizini](docs/README.md) ilgili kaynaklari
listeler. Bu belgeleri yeni Rust runtime'inin tamamlanmis
islevleri olarak kullanmayin.

Onceki `main` kaynak revizyonu `1048931b` icin Core, Windows
Desktop, Contract Validation, Stable Readiness ve Velopack E2E
CI isleri **PASS** (14 Ekim 2026). Bu tarihsel CI sonucu, Rust
derlemesi veya yeni signed production yayin anlamina gelmez.

[Onceki main README durum kaydi](https://github.com/TurkuazLabs/TurkuazInstaller/blob/1048931bf7498ab4d4440414c4463e6ee5e7421e/README.md)
Git gecmisinde saklanmaktadir.

## Yayinlama ve guvenlik durumu

**1 Kasim 2026 kontrolu:** GitHub Releases listesinde yayimlanmis
bir production release bulunmadi. Kod/CI basarisi ile production
imzasi, gercek dagitim ve kullanici kabulunu ayri degerlendirin.

- [Mevcut main release checklist](docs/RELEASE_CHECKLIST.md)
- [Tarihsel main roadmap](docs/ROADMAP.md)
- [Main guvenlik modeli](docs/SECURITY_MODEL.md)
- [Guvenlik bildirimi](SECURITY.md)

Production imzalama, gerekli sertifika/hesap yapilandirmalari
ve son kullanici kabul kanitlari tamamlanmadan unsigned test
ciktilarini stable production dagitimi olarak yayinlamayin.
Gecis PR'i taslaktir; main'i Rust'a otomatik tasimaz.
