# 📄 Dosya Yolu: /docs/PRIVATE_PROVIDER_CREDENTIALS.md
# 📌 Amac: Private GitHub/Gitea release provider credential saklama, resolve ve request authorization modelini tanimlamak
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: Windows Credential Manager target contracti, host allow-list, header scheme ve secret boundary kurallarini sabitler
# Bagimli Oldugu Katman: Port | Tool | Config

# Private Provider Credentials

Private provider credential destegi GitHubReleaseProvider ve GiteaReleaseProvider icin optionaldir.

Credential yoksa provider mevcut public/anonymous davranisla devam eder.

Generic HTTPS provider bu dilimde private credential adapteri kullanmaz.

## Secret Store

Token manifest, YAML config, command line veya log icinde tutulmaz.

Windows Credential Manager icinde Generic Credential olarak saklanir.

Target format:

`TurkuazInstaller/provider/{provider}/{authority}`

Ornek GitHub:

`TurkuazInstaller/provider/github/api.github.com`

Ornek Gitea:

`TurkuazInstaller/provider/gitea/gitea.example.com`

Explicit port varsa authority icinde korunur:

`TurkuazInstaller/provider/gitea/gitea.example.com:3000`

Windows Credential Manager UI kullanilirken:

- credential type: Generic Credential
- Internet or network address: yukaridaki target
- User name: operator etiketi olabilir; runtime kullanmaz
- Password: provider access token

Token icin yalniz gerekli repository/release okuma yetkileri verilmelidir.

## Resolver

IProviderCredentialResolver provider turu + HTTPS authority ile token arar.

WindowsProviderCredentialResolver deterministic target adini uretir ve WindowsCredentialManagerReader uzerinden secret blobunu okur.

Credential bulunamazsa null doner.

ProviderAccessToken ToString secret degeri dondurmez; [REDACTED] dondurur.

## GitHub

GitHub API requestlerinde:

`Authorization: Bearer <token>`

kullanilir.

Public github.com icin credential allow-list varsayilan olarak:

- api.github.com
- github.com

authority degerlerini kapsar.

GitHub Enterprise veya ayrik asset origin kullanan kurulumlarda ek credential originleri explicit provider option ile tanimlanmalidir.

Release metadata icinden gelen arbitrary host otomatik olarak credential allow-listine eklenmez.

## Gitea

Gitea API token requestlerinde:

`Authorization: token <token>`

kullanilir.

Varsayilan credential scope Gitea API base URI authority degeridir.

Manifest veya detached .p7s sidecar farkli bir HTTPS origin uzerindeyse o origin explicit allow-list icinde tanimlanmadan token alamaz.

## Manifest Trust

Credential yalniz transport authentication saglar.

Private kaynaktan indirilen manifest icin mevcut trust zinciri degismez:

1. manifest byte'lari alinir
2. detached .p7s sidecar alinir
3. CMS/PKCS#7 signature dogrulanir
4. package-scoped publisher subject pini dogrulanir
5. certificate SHA-256 pini dogrulanir
6. ancak sonra manifest parse edilir

Private token signed manifest trust yerine gecmez.

## Host Leak Protection

Authorization header yalniz:

- HTTPS request
- explicit allow-list authority

kosullari birlikte saglanirsa eklenir.

Release metadata attacker-controlled veya yanlis configure edilmis bir asset URL tasisa bile token keyfi hosta gonderilmez.

URI userinfo formu credential origin/authority olarak reddedilir.

## Logging

Token:

- event loga yazilmaz
- exception mesajina eklenmez
- manifest icine serialize edilmez
- credential target adinin parcasi degildir

Credential target yalniz provider kimligi ve authority tasir.
