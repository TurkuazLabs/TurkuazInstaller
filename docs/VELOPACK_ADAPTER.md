# 📄 Dosya Yolu: /docs/VELOPACK_ADAPTER.md
# 📌 Amac: Merkezi TurkuazInstaller ile Velopack arasindaki adapter sinirini ve runtime kontratini tanimlamak
# 📌 Modul - Markdown
# Version: 1.1.0
# Aciklama: Setup/full/delta staging, safe delta reconstruction/fallback ve install/update/repair/rollback/uninstall CLI sinirini dokumante eder
# Bagimli Oldugu Katman: Service | Tool | Config

# Velopack Adapter

TurkuazInstaller merkezi bir installer oldugu icin baska uygulamalari kurar, gunceller, onarir, geri alir ve kaldirir.

Install modeli full kalir. v1.2 sonrasi delta yalniz optional transfer optimizasyonudur; apply her zaman dogrulanmis full package ile yapilir.

## Initial Install

```text
Setup.exe --silent --installto <target>
```

## Optional Delta Optimization

Update release signed manifest icinde optional delta metadata tasiyabilir.

Delta yalniz su kosullarda secilir:

- from_version kurulu surumle birebir eslesir
- delta size_bytes full artifacttan kucuktur
- delta artifact HTTPS/file + exact size + SHA-256 + optional Authenticode zincirinden gecer
- kurulu Velopack packages cache icinde exact base full nupkg vardir

Package Engine reconstruction:

```text
Update.exe --silent patch --old <base-full.nupkg> --delta <target-delta.nupkg> --output <target-full.nupkg>
```

Reconstructed full nupkg, target release full artifact SHA-256 ve varsa Authenticode policy ile yeniden dogrulanmadan apply baslamaz.

Delta download, verify, base lookup, patch veya reconstructed-full verify adimlarindan biri basarisizsa mutation baslamadan full artifact yoluna geri donulur.

Repair ve rollback delta kullanmaz; full package kontratini korur.

## Update / Repair / Rollback

Full nupkg once install root packages klasorune atomik olarak kopyalanir.

```text
Update.exe --silent --rootDir <target> --packageDir <target>/packages apply --norestart --package <full.nupkg>
```

Repair ayni surum full package ile calisir.

Rollback previous release full package ile calisir.

## Uninstall

Resmi Velopack uninstall kontrati kullanilir:

```text
Update.exe --silent --rootDir <target> uninstall
```

Package Engine basarisizsa install state silinmez.

## Staging

Package Engine yalniz verification katmanindan gecmis artifacti stage eder.

- benzersiz operation directory
- .partial copy
- atomic rename
- package id/version ile typed PackageStage
- staging root containment

kurallari uygulanir.

## Preserve Paths

Manifest preserve_paths degerleri install/update planina gercek olarak tasinir.

Policy:

- path install rootuna gore relative olmalidir
- path traversal yasaktir
- current altindaki preserve path reddedilir

## Process Boundary

Tum Velopack cagrilari IProcessRunner uzerinden shell kullanmadan ProcessStartInfo.ArgumentList ile calistirilir.

Non-zero exit code typed PackageEngineException olarak yukari tasinir.

Unit testler CLI argument contractini FakeProcessRunner ile dogrular.

Gercek Velopack E2E testi Stable v1 final kalite kapisinda ayrica calistirilacaktir.
