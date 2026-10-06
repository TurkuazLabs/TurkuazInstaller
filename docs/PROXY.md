# 📄 Dosya Yolu: /docs/PROXY.md
# 📌 Amac: TurkuazInstaller system, direct ve custom HTTP proxy konfigurasyonunu tanimlamak
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: network.json yolu, modlar, Windows default credentials, custom proxy sinirlari ve runtime kapsamini aciklar
# Bagimli Oldugu Katman: Config | Tool | Controller

# Proxy Support

TurkuazInstaller tum Windows HTTP istemcileri icin tek proxy policy kullanir.

Runtime config yolu:

`%LOCALAPPDATA%/TurkuazInstaller/config/network.json`

Dosya yoksa varsayilan mod `system` olur.

Bu davranis onceki `new HttpClient()` davranisini korur: Windows/system proxy ayarlari kullanilir.

## System

Ornek:

```json
{
  "schema_version": 1,
  "mode": "system",
  "use_default_credentials": false
}
```

System modunda explicit proxy URI verilmez.

Windows/system proxy kullanilir.

Kurumsal proxy mevcut Windows kullanici kimligini istiyorsa:

```json
{
  "schema_version": 1,
  "mode": "system",
  "use_default_credentials": true
}
```

Bu ayar proxy authentication icin Windows default credentials kullanir.

## Direct

Proxyyi tamamen kapatmak icin:

```json
{
  "schema_version": 1,
  "mode": "direct"
}
```

Direct modda:

- custom_proxy kullanilamaz
- use_default_credentials=true kullanilamaz

## Custom

Explicit HTTP proxy:

```json
{
  "schema_version": 1,
  "mode": "custom",
  "custom_proxy": "http://proxy.example.com:8080/",
  "bypass_local": true,
  "use_default_credentials": false
}
```

Windows default credentials ile custom proxy:

```json
{
  "schema_version": 1,
  "mode": "custom",
  "custom_proxy": "http://proxy.example.com:8080/",
  "bypass_local": false,
  "use_default_credentials": true
}
```

Custom proxy URI:

- absolute olmalidir
- bu tranche'ta scheme yalniz http olabilir
- userinfo tasiyamaz
- path tasiyamaz
- query tasiyamaz
- fragment tasiyamaz

Bu nedenle asagidaki format reddedilir:

`http://user:password@proxy.example.com:8080/`

Proxy username/password/token network.json icinde saklanmaz.

## Runtime Kapsami

Ayni proxy policy su HTTP yollarina uygulanir:

- bootstrap GitHub self-update discovery
- bootstrap replacement download
- WinUI signed manifest download
- WinUI detached .p7s download
- WinUI package/prerequisite artifact download
- CLI signed manifest download
- CLI detached .p7s download
- CLI package/prerequisite artifact download

GitHub/Gitea provider adapterlari caller tarafindan verilen HttpClient'i kullanmaya devam eder. Runtime composition WindowsHttpClientFactory kullandiginda ayni proxy policy otomatik uygulanir.

## Private Provider Credentials

Proxy authentication ile GitHub/Gitea repository credentiallari farkli guvenlik sinirlaridir.

Private provider tokenlari Windows Credential Manager'da kalir.

Proxy config:

- provider token tasimaz
- proxy password tasimaz
- origin Authorization headerini degistirmez

Detay: docs/PRIVATE_PROVIDER_CREDENTIALS.md

## Fail-Closed Config

network.json mevcut fakat malformed ise runtime sessizce system proxy'ye donmez.

Asagidaki durumlar config hatasidir:

- desteklenmeyen schema_version
- bilinmeyen property
- bilinmeyen mode
- custom modda eksik custom_proxy
- system/direct modda custom_proxy
- direct modda default credentials
- URI icinde userinfo/path/query/fragment
- desteklenmeyen proxy scheme

Bu davranis yanlis proxy konfigurasyonunun fark edilmeden atlanmasini engeller.
