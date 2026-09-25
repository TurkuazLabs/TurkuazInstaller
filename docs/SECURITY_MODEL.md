# 📄 Dosya Yolu: /docs/SECURITY_MODEL.md
# 📌 Amac: Installer ve updater guvenlik zincirini tanimlar
# 📌 Modul - Markdown
# Version: 0.1.0
# Aciklama: Paket kaynagindan transaction commit asamasina kadar dogrulama modelini sabitler

Bagimli Oldugu Katman: Tool

# Security Model

```text
HTTPS
  -> Manifest validation
  -> Release policy validation
  -> Package staging download
  -> SHA-256 verification
  -> Authenticode verification
  -> Transactional install
  -> Health check
  -> Commit
```

Health check basarisiz olursa onceki calisan surume rollback uygulanir.

## Ilkeler

- Hash dogrulamasi tum editionlarda zorunludur.
- Imza kontrolu provider bagimsiz port olmalidir.
- Paket dogrudan aktif kurulum dizinine indirilmez.
- Private key repository icinde tutulmaz.
- Secrets config dosyalarina yazilmaz.
- Production update source HTTPS disinda kabul edilmez.
