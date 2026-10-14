<!--
Dosya Yolu: REPO_ROOT/AGENTS.md
Amac: TurkuazInstaller icin BURAK DEV STANDARD v3.0.0 uygulama kontrati
Modul - Documentation Markdown
Version: 3.0.0
Aciklama: Teknolojiye uyarlanmis, merkez standarda commit pinli agent kurallari
Bagimli Oldugu Katman: Documentation
-->

# TurkuazInstaller - Project Agent Contract

**Standart:** BURAK DEV STANDARD v3.0.0 (active).
**Commit:** `72f6f642128fa29ec9a68b18881e9aa225739c9d`.
**Kaynak:** https://github.com/TurkuazSoft/DevStandards/tree/72f6f642128fa29ec9a68b18881e9aa225739c9d
**Profil:** `platforms/dotnet.md`, .NET Windows installer.
**Kapsam ve roadmap:** Yerel README, docs, CONTRIBUTING, issue/PR ve mevcut test kontratlari.

## Zorunlu proje davranisi

- Once repo/branch, yerel proje dokumani, acik PR/issue ve CI'yi incele. "Devam et" son dogrulanmis asamadir.
- Calisan davranis, kullanici degisiklikleri ve hassas veriyi koru. Kapsam disi refactor, onaysiz live migration veya release yapma.
- Controller request/response; Service is kurallari; Repository/Model storage; Tool/Adapter dis servis; View arayuz; Language lokalizasyon. Native framework yapi/isimlendirmesini bozma.
- Format izin veriyorsa teknik dosya ustverisi: Dosya Yolu, Amac, Modul - FileType, Version, Aciklama, Bagimli Oldugu Katman. JSON/binary dosyaya yorum ekleme. Bilinmeyen yol REPO_ROOT/goreli-yol.
- Magic string, inline secrets, gizli token ve uydurma test sonucuna izin yok.
- Build, unit, contract, integration, UI, smoke testlerini degisiklik turune gore ayri raporla. Test artifacti bir release degildir.
- Guvenilmeyen fork PR kodu ozel self-hosted runner'da calistirilmaz. Baska repoya otomatik degisiklik yapilmaz.
- Private merkez repo dis katilimci tarafindan okunamayabilir; bu yerel temel kurallar bagimsiz uygulansin.

## Proje odagi

MSI/Velopack, Windows Desktop, package signing, upgrade/uninstall rollback tested independently.

Gelecekteki merkezi standart degisiklikleri yalniz kontrollu ayri PR ile alinir.
