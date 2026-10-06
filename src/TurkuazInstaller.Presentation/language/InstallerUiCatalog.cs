// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/language/InstallerUiCatalog.cs
// 📌 Amac: Varsayilan Language metinleri ile typed UI profile override degerlerini tek resolverda birlestirir
// 📌 Modul - Language CSharp
// Version: 1.1.0
// Aciklama: Branding ve tum desteklenen kullanici metinlerini tek typed localization katalogundan fallback-guvenli sunar
//
// Bagimli Oldugu Katman: Language | View | Service | Config

using TurkuazInstaller.Contracts.Branding;

namespace TurkuazInstaller.Presentation.Language;

public sealed class InstallerUiCatalog
{
    private readonly InstallerUiProfile _profile;

    public InstallerUiCatalog(
        InstallerUiProfile profile)
    {
        ArgumentNullException.ThrowIfNull(
            profile);

        _profile = profile;
    }

    public string? Culture =>
        _profile.Culture;

    public string WindowTitle =>
        ResolveBranding(
            _profile.Branding.WindowTitle,
            InstallerUiLabels.WindowTitle);

    public string HeaderTitle =>
        ResolveBranding(
            _profile.Branding.HeaderTitle,
            InstallerUiLabels.HeaderTitle);

    public string HeaderSubtitle =>
        ResolveBranding(
            _profile.Branding.HeaderSubtitle,
            InstallerUiLabels.HeaderSubtitle);

    public string Footer =>
        ResolveBranding(
            _profile.Branding.Footer,
            InstallerUiLabels.Footer);

    public string Resolve(
        InstallerUiLabelKey key)
    {
        if (
            _profile.Labels.TryGetValue(
                key,
                out var value))
        {
            return value;
        }

        return key switch
        {
            InstallerUiLabelKey.PackageId => InstallerUiLabels.PackageId,
            InstallerUiLabelKey.Channel => InstallerUiLabels.Channel,
            InstallerUiLabelKey.ManifestSource => InstallerUiLabels.ManifestSource,
            InstallerUiLabelKey.RollbackManifestSource => InstallerUiLabels.RollbackManifestSource,
            InstallerUiLabelKey.TargetPath => InstallerUiLabels.TargetPath,
            InstallerUiLabelKey.Stable => InstallerUiLabels.Stable,
            InstallerUiLabelKey.Beta => InstallerUiLabels.Beta,
            InstallerUiLabelKey.Install => InstallerUiLabels.Install,
            InstallerUiLabelKey.Update => InstallerUiLabels.Update,
            InstallerUiLabelKey.Repair => InstallerUiLabels.Repair,
            InstallerUiLabelKey.Rollback => InstallerUiLabels.Rollback,
            InstallerUiLabelKey.Uninstall => InstallerUiLabels.Uninstall,
            InstallerUiLabelKey.Retry => InstallerUiLabels.Retry,
            InstallerUiLabelKey.Cancel => InstallerUiLabels.Cancel,
            InstallerUiLabelKey.RefreshInstalledApps => InstallerUiLabels.RefreshInstalledApps,
            InstallerUiLabelKey.CheckUpdates => InstallerUiLabels.CheckUpdates,
            InstallerUiLabelKey.Ready => InstallerUiLabels.Ready,
            InstallerUiLabelKey.Preparing => InstallerUiLabels.Preparing,
            InstallerUiLabelKey.Downloading => InstallerUiLabels.Downloading,
            InstallerUiLabelKey.Verifying => InstallerUiLabels.Verifying,
            InstallerUiLabelKey.Staging => InstallerUiLabels.Staging,
            InstallerUiLabelKey.Applying => InstallerUiLabels.Applying,
            InstallerUiLabelKey.Uninstalling => InstallerUiLabels.Uninstalling,
            InstallerUiLabelKey.SavingState => InstallerUiLabels.SavingState,
            InstallerUiLabelKey.RemovingState => InstallerUiLabels.RemovingState,
            InstallerUiLabelKey.Completed => InstallerUiLabels.Completed,
            InstallerUiLabelKey.Cancelled => InstallerUiLabels.Cancelled,
            InstallerUiLabelKey.RebootRequired => InstallerUiLabels.RebootRequired,
            InstallerUiLabelKey.PackageIdRequired => InstallerUiLabels.PackageIdRequired,
            InstallerUiLabelKey.ManifestRequired => InstallerUiLabels.ManifestRequired,
            InstallerUiLabelKey.RollbackManifestRequired => InstallerUiLabels.RollbackManifestRequired,
            InstallerUiLabelKey.TargetPathUnavailable => InstallerUiLabels.TargetPathUnavailable,
            InstallerUiLabelKey.OperationFailedPrefix => InstallerUiLabels.OperationFailedPrefix,
            InstallerUiLabelKey.UpdateDiscoveryTitle => InstallerUiLabels.UpdateDiscoveryTitle,
            InstallerUiLabelKey.InstalledVersion => InstallerUiLabels.InstalledVersion,
            InstallerUiLabelKey.LatestVersion => InstallerUiLabels.LatestVersion,
            InstallerUiLabelKey.UpdateNotChecked => InstallerUiLabels.UpdateNotChecked,
            InstallerUiLabelKey.CheckingForUpdates => InstallerUiLabels.CheckingForUpdates,
            InstallerUiLabelKey.UpdateAvailable => InstallerUiLabels.UpdateAvailable,
            InstallerUiLabelKey.UpdateCurrent => InstallerUiLabels.UpdateCurrent,
            InstallerUiLabelKey.UpdateSkipped => InstallerUiLabels.UpdateSkipped,
            InstallerUiLabelKey.UpdatePinned => InstallerUiLabels.UpdatePinned,
            InstallerUiLabelKey.UpdateNotInstalled => InstallerUiLabels.UpdateNotInstalled,
            InstallerUiLabelKey.UpdateReleaseNotFound => InstallerUiLabels.UpdateReleaseNotFound,
            InstallerUiLabelKey.UpdateCheckFailedPrefix => InstallerUiLabels.UpdateCheckFailedPrefix,
            InstallerUiLabelKey.BackgroundUpdatesTitle => InstallerUiLabels.BackgroundUpdatesTitle,
            InstallerUiLabelKey.BackgroundUpdatesDisabled => InstallerUiLabels.BackgroundUpdatesDisabled,
            InstallerUiLabelKey.BackgroundUpdatesWaiting => InstallerUiLabels.BackgroundUpdatesWaiting,
            InstallerUiLabelKey.BackgroundUpdatesChecking => InstallerUiLabels.BackgroundUpdatesChecking,
            InstallerUiLabelKey.BackgroundUpdatesCompletedPrefix => InstallerUiLabels.BackgroundUpdatesCompletedPrefix,
            InstallerUiLabelKey.BackgroundUpdatesCheckedPrefix => InstallerUiLabels.BackgroundUpdatesCheckedPrefix,
            InstallerUiLabelKey.BackgroundUpdatesAvailablePrefix => InstallerUiLabels.BackgroundUpdatesAvailablePrefix,
            InstallerUiLabelKey.BackgroundUpdatesFailurePrefix => InstallerUiLabels.BackgroundUpdatesFailurePrefix,
            InstallerUiLabelKey.VersionUnavailable => InstallerUiLabels.VersionUnavailable,
            InstallerUiLabelKey.InstalledAppsTitle => InstallerUiLabels.InstalledAppsTitle,
            InstallerUiLabelKey.InstalledAppsCountPrefix => InstallerUiLabels.InstalledAppsCountPrefix,
            InstallerUiLabelKey.CatalogLoadFailedPrefix => InstallerUiLabels.CatalogLoadFailedPrefix,
            InstallerUiLabelKey.StatusTitle => InstallerUiLabels.StatusTitle,
            InstallerUiLabelKey.SourceTitle => InstallerUiLabels.SourceTitle,
            InstallerUiLabelKey.OperationsTitle => InstallerUiLabels.OperationsTitle,
            InstallerUiLabelKey.RecoveryTitle => InstallerUiLabels.RecoveryTitle,
            InstallerUiLabelKey.ManifestPlaceholder => InstallerUiLabels.ManifestPlaceholder,
            InstallerUiLabelKey.RollbackManifestPlaceholder => InstallerUiLabels.RollbackManifestPlaceholder,
            InstallerUiLabelKey.TargetPathPlaceholder => InstallerUiLabels.TargetPathPlaceholder,
            _ => throw new ArgumentOutOfRangeException(
                nameof(key),
                key,
                "Installer UI label key is not supported.")
        };
    }

    private static string ResolveBranding(
        string? value,
        string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value;
    }
}
