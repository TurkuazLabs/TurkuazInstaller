# 📄 Dosya Yolu: /docs/VELOPACK_ADAPTER.md
# 📌 Amac: Merkezi TurkuazInstaller ile Velopack arasindaki adapter sinirini ve runtime kontratini tanimlamak
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: Setup.exe ve Update.exe install/update/repair/rollback/uninstall CLI entegrasyonunu ve staging guvenligini dokumante eder
# Bagimli Oldugu Katman: Service | Tool | Config

# Velopack Adapter

TurkuazInstaller merkezi bir installer oldugu icin baska uygulamalari kurar, gunceller, onarir, geri alir ve kaldirir.

Stable v1 manifest yalniz full payload modeli kullanir.

## Initial Install

```text
Setup.exe --silent --installto <target>
```

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
