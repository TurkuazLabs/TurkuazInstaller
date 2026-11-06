<!--
Dosya Yolu: REPO_ROOT/docs/README.md
Amac: Aktif Community Rust gelistirmesine ait belgeler ve tarihsel kaynaklar icin dizin
Modul - Markdown
Version: 1.2.0
Aciklama: Studio, EULA, CLI, QA, release sinirlari ve tarihsel kaynaklar
Bagimli Oldugu Katman: Documentation
-->

# TurkuazInstaller Community belge dizini

Aktif gelistirme Rust workspace (`rust/`) altindadir. Bu belge,
hangi kaynagin gercek uygulamaya ait oldugunu ve hangi testin neyi
kanitladigini ayirir. Aktif kurulum uygulamasi, Studio ve CLI yalniz
Rust workspace kaynaklarindan olusur.

## Kurulum ve gelistirme

| Konu | Belge |
| --- | --- |
| Proje ozeti ve hizli kurulum | [Kok README](../README.md) |
| Rust crate'leri, bagimlilik ve gelistirme | [Rust README](../rust/README.md) |
| Setup Studio, yonetilebilir kurulum sayfalari ve EULA | [Setup Studio](../rust/SETUP_BUILDER.md) |
| Rust CLI komutlari | [CLI](../rust/CLI.md) |
| `.tzpkg` / native EXE ve sinirlar | [Paket formati](../rust/PACKAGE_FORMAT.md) |
| Install/update/repair/rollback/uninstall | [Install workflow](../rust/INSTALL_WORKFLOW.md) |
| Versioned deployment ve kurtarma | [Deployment layout](../rust/DEPLOYMENT_LAYOUT.md) |
| Publisher imzalama ve dagitim guveni | [Release signing](../rust/RELEASE_SIGNING.md) |
| Windows GUI manuel test ve olumsuz senaryolar | [Windows smoke](../rust/SMOKE_TEST.md) |
| Production'a gecis ve kalan kapilar | [Rust readiness](../rust/RUST_READINESS.md) |
| Guvenlik bildirimi | [SECURITY](../SECURITY.md) |

## Dogrulama kaniti

- [GitHub Actions #41913467340](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/41913467340):
  `ed272460` revizyonunda Linux ve Windows CI **SUCCESS** (4 Kasim 2026).
  Windows Studio GUI build, kisa yol, Rust installer ve test paketi
  otomatik kontrolleri calisti.
- Bu sonucun kapsaminda temiz Windows 10/11 masaustunde tam
  **elle kullanici kabul testi**, production Authenticode imzasi,
  gercek publisher kimligi ve ilk musteri uygulamasi pilotu bulunmaz.
- Belge guncellemesi veya `main` merge yeni kod/test basarisi
  sayilmaz. Yeni commit'in kendi CI calismasina bakin.

## Uyumluluk ve guvenlik

- `../contracts/` altindaki tarihsel YAML tasarimlari
  `legacy-design-only` durumundadir. Gercek manifest dogrulamasi
  `../rust/crates/manifest-parser` icindedir.
- Eski `.tzproj` projeleri lisans sayfasi kapali olarak acilir.
  EULA aciksa Turkce ve English metinler yayinci tarafindan girilir.
  Metin bosken EXE uretilmez; onay kutusu isaretlenmeden kurulum
  sonraki asamaya gecemez.
- Imzasiz yerel EXE SHA-256 butunluk denetimi saglar,
  publisher authentication saglamaz.
- Test ZIP/artifact'lari ve production Release ayri tutulur. Imzalama
  anahtari, parola veya gercek musteri verisi issue/CI artifact'ina
  eklenmemelidir.

Hangi dalda olursa olsun kodu production olarak dagitmadan once
[Windows manuel kabul rehberini](../rust/SMOKE_TEST.md) ve
[release kapilarini](../rust/RUST_READINESS.md) dogrulayin.
