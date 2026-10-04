# 📄 Dosya Yolu: /docs/SECURITY_MODEL.md
# 📌 Amac: TurkuazInstaller Community install, update, repair ve rollback guvenlik invariantlarini tanimlamak
# 📌 Modul - Markdown
# Version: 0.5.0
# Aciklama: Hash, imza, path, staging, process, download ve rollback guvenlik kurallarini Community Core icin sabitler
# Bagimli Oldugu Katman: Service | Repo | Tool | Config

# Security Model

## Community'de Zorunlu Guvenlik

Asagidaki davranislar Pro ozelligi degildir:

- SHA-256 artifact dogrulamasi
- dijital imza dogrulama kontrati
- path traversal ve archive escape engelleme
- atomic staging
- install oncesi preflight
- repair integrity kontrolu
- rollback guvenligi
- HTTPS remote transport baseline
- secret degerlerin manifest icine yazilmamasi

## Download

Remote artifact varsayilan olarak HTTPS kullanir.

Local test ve air-gapped senaryo icin `file:` URI kullanilabilir.

Remote plain HTTP Community Core tarafinda reddedilir.

## Artifact Verification

Install veya update apply edilmeden once:

1. beklenen size policy kontrol edilir
2. SHA-256 hesaplanir
3. manifest SHA-256 ile esitlik dogrulanir
4. product policy imza istiyorsa imza dogrulanir
5. staging alani disina yazma girisimi reddedilir

Hash veya gerekli imza dogrulanamazsa apply baslamaz.

## Atomic Staging

Verified artifact dogrudan active install rootundan calistirilmaz.

Package Engine:

1. benzersiz staging operasyon klasoru olusturur
2. dosyayi `.partial` olarak kopyalar
3. ayni klasor icinde atomic rename yapar
4. package id ve version bilgisi ile typed `PackageStage` uretir

Apply, repair ve rollback yalniz staged artifact kabul eder.

## Process Boundary

Velopack executable cagirilari shell uzerinden string command olarak calistirilmaz.

Her argument process API `ArgumentList` alanina ayri deger olarak verilir.

Non-zero exit code basarili apply olarak kabul edilmez.

## Rollback

Rollback:

- onceki version metadata'sini korur
- yarim uygulanmis yeni paketi active state olarak isaretlemez
- veri klasorlerini product preserve policy disinda silmez
- rollback artifacti icin de integrity verification zorunludur
- staged artifact package id ve version degeri previous release ile eslesmelidir

## Preserve Data

Kalici application verisi Velopack `current` klasoru disinda tutulur.

Preserve path:

- install rootuna gore relative olmalidir
- target root disina cikamaz
- `current` altinda olamaz

Bu sinir executable payload ile kalici veriyi birbirinden ayirir.

## Secret Boundary

Manifest veya public config su degerleri tasimaz:

- private signing key
- access token
- private feed credential
- entitlement credential
- production certificate private key

Authentication secret Adapter tarafinda environment, OS credential store veya secret provider uzerinden gelir.
