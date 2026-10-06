// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Branding/InstallerUiLabelKey.cs
// 📌 Amac: UI localization override anahtarlarini magic string kullanmadan typed contract olarak tanimlar
// 📌 Modul - Contract CSharp
// Version: 1.0.0
// Aciklama: Desteklenen masaustu label override alanlarini enum ile sabitler
//
// Bagimli Oldugu Katman: Service | Repo | Language

namespace TurkuazInstaller.Contracts.Branding;

public enum InstallerUiLabelKey
{
    PackageId,
    Channel,
    ManifestSource,
    RollbackManifestSource,
    TargetPath,
    Install,
    Update,
    Repair,
    Rollback,
    Uninstall,
    Retry,
    Cancel,
    RefreshInstalledApps,
    CheckUpdates,
    UpdateDiscoveryTitle,
    BackgroundUpdatesTitle,
    InstalledAppsTitle,
    StatusTitle,
    SourceTitle,
    OperationsTitle,
    RecoveryTitle
}
