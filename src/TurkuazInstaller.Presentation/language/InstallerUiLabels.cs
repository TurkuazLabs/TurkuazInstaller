// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/language/InstallerUiLabels.cs
// 📌 Amac: TurkuazInstaller masaustu arayuzundeki kullanici metinlerini merkezi Language katmaninda tanimlar
// 📌 Modul - Language CSharp
// Version: 1.1.0
// Aciklama: Install, update, repair, rollback, uninstall ve reboot resume UX metinlerini tek katalogda tutar
//
// Bagimli Oldugu Katman: Language

namespace TurkuazInstaller.Presentation.Language;

public static class InstallerUiLabels
{
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

    public const string Footer =
        "TurkuazLabs Community";
}
