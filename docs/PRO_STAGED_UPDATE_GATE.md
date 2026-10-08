# 📄 Dosya Yolu: /docs/PRO_STAGED_UPDATE_GATE.md
# 📌 Amac: Community ve private Pro rollout integrasyonunun guvenlik sinirini tanimlamak
# 📌 Modul - Markdown
# Version: 2.5.0
# Aciklama: Optional staged update decision portunun pre-download ve pre-apply guvenlik denetimini belgeler
# Bagimli Oldugu Katman: Service | Tool | Model | Config

# Optional Pro Staged Update Gate

Community install, update, repair, rollback ve uninstall islemleri Pro kaynak koduna bagimli degildir.

## Entegrasyon

Pro runtime composition, `IStagedUpdatePolicyGate` portunu saglayabilir. Port, imzali ve trusted-context rollout decision service'in yalnizca typed verdict sonucunu Community'ye aktarir.

- `Eligible`: staged update iki kontrolu de gectikten sonra Community islemlerine devam edebilir.
- `Deferred` ve `Denied`: staged update reddedilir.
- `RollbackRequested`: update reddedilir; rollback otomatik tetiklenmez.
- Unknown enum veya provider exception: update reddedilir/istisna ile durdurulur.
- Provider, Community manifest ve artifact trust mekanizmasini degistiremez.

## Kontroller

`InstallerWorkflowService.UpdateAsync` signed release icin iki kontrol yapar:

1. Download, operation journal ve staging baslamadan once.
2. Staging bittikten sonra, package engine apply baslamadan hemen once.

Arada yeni bir signed kill switch veya daha yuksek policy revision gorulurse ikinci karar apply'i durdurur. Community artifact signature/hash/path/rollback policy mekanizmalari aynen gecerlidir.

Community-only composition `IStagedUpdatePolicyGate` vermiyorsa mevcut update yolu degismez. Bir Pro ticari composition bu portu zorunlu baglamalidir; bos bir port ile paid rollout guvenligi garanti edilemez.

## Bekleyen Production Baglantilari

- Pro authenticated opaque device/account binding ve atomic tamper-resistant revision backend: https://github.com/TurkuazSoft/TurkuazInstaller-Pro/issues/5
- Pro evaluator -> Community adapter, rollback coordination ve Windows E2E: https://github.com/TurkuazLabs/TurkuazInstaller/issues/38

Bir signed `RollbackRequired` projeksiyonu Community rollback izni sayilmaz. Community tekrar signed manifest, rollback policy, artifact hash, path ve operation lock dogrulamalarini uygulamalidir.
