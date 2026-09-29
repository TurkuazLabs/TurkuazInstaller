# 📄 Dosya Yolu: /docs/SECURITY_MODEL.md
# 📌 Amac: TurkuazInstaller Community install/update/repair/rollback guvenlik invariantlarini tanimlamak
# 📌 Modul - Markdown
# Version: 0.2.0
# Aciklama: Hash, imza, path, download, rollback ve secret sinirlarini Community Core icin sabitler
# Bagimli Oldugu Katman: Domain | Application | Port | Adapter | Config

# Security Model

## Community'de Zorunlu Guvenlik

Asagidaki davranislar Pro ozelligi degildir:

- SHA-256 artifact dogrulamasi
- dijital imza dogrulama kontrati
- path traversal / archive escape engelleme
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

## Rollback

Rollback:

- onceki version metadata'sini korur
- yarim uygulanmis yeni paketi active state olarak isaretlemez
- veri klasorlerini product preserve policy disinda silmez
- rollback artifact'i icin de integrity verification uygular

## Secret Boundary

Manifest veya public config su degerleri tasimaz:

- private signing key
- access token
- private feed credential
- entitlement credential
- production certificate private key

Authentication secret'i Adapter tarafinda environment/OS credential store/secret provider uzerinden gelir.
