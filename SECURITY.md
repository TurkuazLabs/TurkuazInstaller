<!--
Dosya Yolu: REPO_ROOT/SECURITY.md
Amac: Rust Community guvenlik bildirim ve destek sinirlarini tanimlamak
Modul - Markdown
Version: 1.1.0
Aciklama: Gizli guvenlik raporu, aktif Rust migration ve production olmayan artifact politikasi
Bagimli Oldugu Katman: Documentation | Security
-->

# Security Policy

TurkuazInstaller guvenlik problemlerini public issue uzerinden paylasmayin.
GitHub deposunun **Security** bolumunden private vulnerability report /
Security Advisory kullanin. Varsa etkilenen commit/tag, tekrar uretme
adimlari, etki, maskelenmis log ve test kanitini ekleyin.
Secret, token, private key veya gercek musteri verisi eklemeyin.

## Destek ve surum durumu

Rust Community uygulamasi `rewrite/rust-community` dalinda,
[PR #63](https://github.com/TurkuazLabs/TurkuazInstaller/pull/63)
uzerinden Draft olarak gelistirilmektedir. Rust migration
artifact'lari gecici TEST ciktilaridir; production release
veya resmi desteklenen 1.x Rust surumu degildir.
`main` dalindaki eski dagitim/guvenlik modeli ile Rust
gelistirme dalini birbirine karistirmayin. Guncel Rust
kabul durumu icin [Rust readiness](rust/RUST_READINESS.md) belgesine bakin.

Guvenlik aciklari kritikse yayin, signing veya installer
production kabulune kadar onay verilmemelidir.
