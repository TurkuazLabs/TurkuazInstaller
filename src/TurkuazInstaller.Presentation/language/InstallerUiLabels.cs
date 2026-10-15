// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/language/InstallerUiLabels.cs
// 📌 Amac: TurkuazInstaller masaustu arayuzundeki kullanici metinlerini merkezi Language katmaninda tanimlar
// 📌 Modul - Language CSharp
// Version: 1.8.0
// Aciklama: Install/update/recovery metinlerine ek olarak katalog, discovery, background update ve version skip/pinning UX metinlerini tutar
//
// Bagimli Oldugu Katman: Language

namespace TurkuazInstaller.Presentation.Language;

public static class InstallerUiLabels
{
    public const string PromotionEyebrow = "TURKUAZ EKOSISTEMI";
    public const string PromotionTitle = "Daha fazlasini birlikte uretin";
    public const string PromotionDescription = "Turkuaz Office, UniZip ve topluluk araclarimizi kesfedin.";
    public const string PromotionProvider = "TurkuazLabs Community";

    public const string InstallOptionsTitle = "Kurulum Secenekleri";
    public const string InstallOptionsDescription =
        "Yuklemeden once kurulum seklini ve kimlerin kullanacagini secin.";
    public const string InstallModeHeader = "Kurulum Modu";
    public const string InstallScopeHeader = "Kullanim Kapsami";
    public const string QuickInstall = "Hizli Kurulum";
    public const string CustomInstall = "Ozellestirilmis Kurulum";
    public const string PerUserInstall = "Yalnizca benim icin (AppData)";
    public const string PerMachineUnavailable =
        "Tum kullanicilar (Program Files) - yakinda";
    public const string CustomTargetHeader =
        "AppData altinda ozel kurulum klasoru";
    public const string CustomTargetRequired =
        "Ozellestirilmis kurulumda AppData icinde kurulum klasoru girilmelidir.";

    public const string WindowTitle = "TurkuazInstaller";
    public const string HeaderTitle = "Turkuaz Installer";
    public const string HeaderSubtitle =
        "Kurulum, guncelleme, onarma, geri alma ve kaldirma merkezi";

    public const string PackageId = "Paket Kimligi";
    public const string Channel = "Kanal";
    public const string ManifestSource =
        "Manifest URL veya dosya yolu";
    public const string RollbackManifestSource =
        "Geri alma manifest URL veya dosya yolu";
    public const string TargetPath = "Kurulum Dizini";

    public const string Stable = "Stable";
    public const string Beta = "Beta";

    public const string Install = "Kur";
    public const string Update = "Guncelle";
    public const string Repair = "Onar";
    public const string Rollback = "Geri Al";
    public const string Uninstall = "Kaldir";
    public const string Retry = "Tekrar Dene";
    public const string Cancel = "Iptal";
    public const string RefreshInstalledApps = "Yenile";
    public const string CheckUpdates = "Guncellemeyi Kontrol Et";

    public const string Ready = "Hazir";
    public const string Preparing =
        "Release bilgisi hazirlaniyor";
    public const string Downloading =
        "Paket indiriliyor";
    public const string Verifying =
        "Butunluk ve imza dogrulaniyor";
    public const string Staging =
        "Paket staging alanina hazirlaniyor";
    public const string Applying =
        "Paket uygulaniyor";
    public const string Uninstalling =
        "Paket kaldiriliyor";
    public const string SavingState =
        "Kurulum durumu kaydediliyor";
    public const string RemovingState =
        "Kurulum kaydi temizleniyor";
    public const string Completed =
        "Islem basariyla tamamlandi";
    public const string Cancelled =
        "Islem iptal edildi";
    public const string RebootRequired =
        "Onkosul kurulumu tamamlandi; devam etmek icin Windows yeniden baslatilmali";

    public const string PackageIdRequired =
        "Paket kimligi zorunludur.";
    public const string ManifestRequired =
        "Bu islem icin manifest kaynagi zorunludur.";
    public const string RollbackManifestRequired =
        "Geri alma icin onceki surum manifesti zorunludur.";
    public const string TargetPathUnavailable =
        "Kurulum dizini girilmedi ve manifest install.target tanimlamiyor.";
    public const string OperationFailedPrefix =
        "Islem basarisiz:";

    public const string UpdateDiscoveryTitle =
        "Guncelleme Durumu";
    public const string InstalledVersion =
        "Kurulu surum";
    public const string LatestVersion =
        "Son surum";
    public const string UpdateNotChecked =
        "Kontrol edilmedi";
    public const string CheckingForUpdates =
        "Guncelleme kontrol ediliyor";
    public const string UpdateAvailable =
        "Yeni surum mevcut";
    public const string UpdateCurrent =
        "Paket guncel";
    public const string UpdateSkipped =
        "Bu surum version policy ile atlandi";
    public const string UpdatePinned =
        "Bu surum version pin ust sinirini asiyor";
    public const string UpdateNotInstalled =
        "Paket kurulu degil";
    public const string UpdateReleaseNotFound =
        "Release bulunamadi";
    public const string UpdateCheckFailedPrefix =
        "Guncelleme kontrolu basarisiz:";

    public const string BackgroundUpdatesTitle =
        "Arka Plan Guncelleme";
    public const string BackgroundUpdatesDisabled =
        "Arka plan guncelleme kontrolu kapali";
    public const string BackgroundUpdatesWaiting =
        "Arka plan guncelleme kontrolu bekliyor";
    public const string BackgroundUpdatesChecking =
        "Arka plan guncellemeleri kontrol ediliyor";
    public const string BackgroundUpdatesCompletedPrefix =
        "Arka plan kontrolu tamamlandi";
    public const string BackgroundUpdatesCheckedPrefix =
        "kontrol";
    public const string BackgroundUpdatesAvailablePrefix =
        "guncelleme";
    public const string BackgroundUpdatesFailurePrefix =
        "hata";
    public const string VersionUnavailable =
        "-";

    public const string InstalledAppsTitle =
        "Kurulu Uygulamalar";
    public const string InstalledAppsEmpty =
        "TurkuazInstaller ile yonetilen kayitli paket bulunamadi. "
        + "Baska bir kurucu ile yuklenen uygulamalar otomatik listelenmez.";
    public const string ActionGuidePackageId =
        "Baslamak icin paket kimligini girin. Kurulum sadece imzali bir manifest ile yapilir.";
    public const string ActionGuideManifest =
        "Imzali manifestin URL veya dosya yolunu girin. Dogrulama olmadan kurulum yapilmaz.";
    public const string ActionGuideNotInstalled =
        "Bu paket henuz yonetilen kurulum listesinde yok. Imzali manifest ile Kur kullanilabilir.";
    public const string ActionGuideReady =
        "Kayitli paket bulundu. Guncelleme/onarma icin imzali manifest, geri alma icin onceki surum manifesti gerekir.";
    public const string InstalledAppsCountPrefix =
        "Kurulu paket:";
    public const string CatalogLoadFailedPrefix =
        "Kurulu uygulama listesi yuklenemedi:";

    public const string StatusTitle = "Durum";
    public const string SourceTitle = "Paket Kaynagi";
    public const string OperationsTitle = "Islemler";
    public const string RecoveryTitle = "Kurtarma";

    public const string ManifestPlaceholder =
        "https://sunucu/installer-manifest.yml";
    public const string RollbackManifestPlaceholder =
        "C:\\paketler\\onceki\\installer-manifest.yml";
    public const string TargetPathPlaceholder =
        "Bos birakilirsa manifest install.target kullanilir";

    public const string DashboardEyebrow = "TURKUAZLABS  /  KURULUM MERKEZI";
    public const string DashboardTagline = "Uygulamalarinizi guvenli ve kontrollu yonetin. Paket secin, kaynagi belirtin, kurulumu adim adim tamamlayin.";
    public const string WorkflowSource = "Paket secimi";
    public const string WorkflowTrust = "Imza ve butunluk";
    public const string WorkflowInstall = "Kurulum ve ilerleme";
    public const string WorkflowMaintain = "Bakim ve kurtarma";
    public const string SourceAdvanced = "Gelismis secenekler (geri alma / kurulum yolu)";
    public const string SourceSecurityDescription = "Imza ve dosya butunlugu kontrolu kurulum motorunda zorunludur. Kaynak eklemek veya Kur butonunu etkinlestirmek guven onayi anlamina gelmez.";
    public const string ManagedCatalogDescription = "Yalnizca TurkuazInstaller ile kaydedilen kurulumlar burada gorunur.";
    public const string OperationsDescription = "Bir paket secin. Kurulum veya bakim islemleri yalnizca gerekli bilgiler saglandiginda etkinlesir.";
    public const string WizardReviewTitle = "Kurulumu Kontrol Et";
    public const string WizardReviewDescription = "Secimleri gozden gecirin. Imza ve paket SHA-256 dogrulamasi gercek islem basladiginda yapilir.";
    public const string WizardBack = "Geri";
    public const string WizardNext = "Devam Et";
    public const string WizardReviewTargetDefault = "Imzali manifestin varsayilan hedefi";
    public const string WizardTrustNotice = "Bu ekran imza dogrulamasi yapmaz. Kuruluma devam edilirse guven politikasi ve SHA-256 kontrolu zorunludur.";
    public const string SourceSectionTitle = "Yeni paket / kurulum kaynagi";
    public const string OverviewTitle = "Kurulum asamalari";

    public const string Footer =
        "TurkuazLabs Community";
}
