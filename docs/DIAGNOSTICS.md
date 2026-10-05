# 📄 Dosya Yolu: /docs/DIAGNOSTICS.md
# 📌 Amac: TurkuazInstaller v1.1 operation lock, crash journal ve structured log storage davranisini aciklar
# 📌 Modul - Markdown
# Version: 1.1.0
# Aciklama: LocalApplicationData altindaki lock/journal/log dosyalarinin amacini ve support inceleme akislarini dokumante eder
# Bagimli Oldugu Katman: Service | Repo | Tool | Config

# Diagnostics

TurkuazInstaller v1.1 her package mutasyonunu correlation id ile izler.

Varsayilan kullanici storage rootu:

```text
%LOCALAPPDATA%/TurkuazInstaller/
  state/
  staging/
  locks/
  journal/
  logs/
```

## Operation Lock

`locks/<package-id>.lock`

dosyasi active install/update/repair/rollback/uninstall boyunca exclusive handle ile acik tutulur.

Ayni package icin ikinci process lock alamaz ve operation baslatilmaz.

## Crash Journal

`journal/<package-id>.json`

son kalici checkpointi tasir.

Basarili operation tamamlaninca journal silinir.

Journal kalmissa:

- process crash etmis olabilir
- kullanici operation'i iptal etmis olabilir
- package engine veya verification fail etmis olabilir

`Phase` ve `Failure` alanlari son bilinen durumu gosterir.

Journal otomatik olarak installed state yerine gecmez.

## Structured Log

`logs/<package-id>.jsonl`

her satirda tek JSON event tutar.

Event alanlari:

- TimestampUtc
- OperationId
- PackageId
- Operation
- Phase
- Level
- EventName
- Message
- ErrorType

Ayni OperationId ile bir operation'in tum checkpointleri korele edilebilir.

Log kaydi diagnostics amaclidir; secret, token veya signing private key yazilmamalidir.

## Support Bundle

Support bundle exportu sonraki v1.1 adiminda eklenecektir.

Bundle olusturulurken credential ve secret alanlari dahil edilmeyecektir.
