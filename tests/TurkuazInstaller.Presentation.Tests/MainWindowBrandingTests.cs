// 📄 Dosya Yolu: /tests/TurkuazInstaller.Presentation.Tests/MainWindowBrandingTests.cs
// 📌 Amac: MainWindowViewModel branding/localization override ve fallback davranisini test eder
// 📌 Modul - Test CSharp
// Version: 1.5.0
// Aciklama: Branding, channel, placeholder ve runtime initial state override davranisini built-in fallback ile birlikte dogrular
//
// Bagimli Oldugu Katman: View | Language | Config

using TurkuazInstaller.Contracts.Branding;
using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Presentation.Language;
using TurkuazInstaller.Presentation.ViewModels;
using Xunit;

namespace TurkuazInstaller.Presentation.Tests;

public sealed class MainWindowBrandingTests
{
    [Fact]
    public void Promotion_UsesSharedDefaultWithoutPerProductUi()
    {
        var model = new MainWindowViewModel(InstallerUiProfile.Empty);
        Assert.True(model.IsPromotionVisible);
        Assert.Equal("TurkuazLabs Community", model.PromotionProvider);
    }

    [Fact]
    public void Promotion_CanBeOverriddenAndDisabled()
    {
        var model = new MainWindowViewModel(
            new InstallerUiProfile(
                null,
                InstallerBrandingProfile.Empty,
                new Dictionary<InstallerUiLabelKey, string>(),
                new InstallerPromotionProfile(false, "BRAND", "Pro", "Test", "Company")));
        Assert.False(model.IsPromotionVisible);
        Assert.Equal("Pro", model.PromotionTitle);
    }

    [Fact]
    public void Wizard_DefaultsToQuickPerUser()
    {
        var model = new MainWindowViewModel(InstallerUiProfile.Empty);

        Assert.Equal(InstallerInstallMode.Quick, model.SelectedInstallMode);
        Assert.Equal(InstallerInstallScope.PerUser, model.SelectedInstallScope);
        Assert.False(model.IsCustomInstall);
    }

    [Fact]
    public void Wizard_CustomSelectionTogglesTargetField()
    {
        var model = new MainWindowViewModel(InstallerUiProfile.Empty);
        model.SelectedInstallModeIndex = (int)InstallerInstallMode.Custom;

        Assert.True(model.IsCustomInstall);
        Assert.Equal(InstallerInstallMode.Custom, model.SelectedInstallMode);
        Assert.Equal(InstallerUiLabels.CustomTargetHeader, model.CustomTargetHeader);
    }

    [Fact]
    public void Wizard_UnknownSelectionDoesNotActivateNewScope()
    {
        var model = new MainWindowViewModel(InstallerUiProfile.Empty);
        model.SelectedInstallScopeIndex = -1;

        Assert.Equal(InstallerInstallScope.PerUser, model.SelectedInstallScope);
    }

    [Fact]
    public void Constructor_AppliesOverridesAndPreservesFallbacks()
    {
        var profile =
            new InstallerUiProfile(
                "en-US",
                new InstallerBrandingProfile(
                    "Nova Installer",
                    "Nova Setup",
                    null,
                    "Nova Community"),
                new Dictionary<InstallerUiLabelKey, string>
                {
                    [InstallerUiLabelKey.Install] =
                        "Install",
                    [InstallerUiLabelKey.Update] =
                        "Update",
                    [InstallerUiLabelKey.Ready] =
                        "Ready",
                    [InstallerUiLabelKey.Stable] =
                        "Stable EN",
                    [InstallerUiLabelKey.Beta] =
                        "Beta EN",
                    [InstallerUiLabelKey.ManifestPlaceholder] =
                        "https://example.test/manifest.yml",
                    [InstallerUiLabelKey.InstalledAppsCountPrefix] =
                        "Installed packages:"
                });

        var viewModel =
            new MainWindowViewModel(
                profile);

        Assert.Equal(
            "Nova Installer",
            viewModel.WindowTitle);
        Assert.Equal(
            "Nova Setup",
            viewModel.HeaderTitle);
        Assert.Equal(
            InstallerUiLabels.HeaderSubtitle,
            viewModel.HeaderSubtitle);
        Assert.Equal(
            "Nova Community",
            viewModel.FooterLabel);
        Assert.Equal(
            "Install",
            viewModel.InstallLabel);
        Assert.Equal(
            "Update",
            viewModel.UpdateLabel);
        Assert.Equal(
            InstallerUiLabels.Repair,
            viewModel.RepairLabel);

        Assert.Equal(
            "Ready",
            viewModel.StatusMessage);

        Assert.Equal(
            new[]
            {
                "Stable EN",
                "Beta EN"
            },
            viewModel.ChannelOptions);

        Assert.Equal(
            "https://example.test/manifest.yml",
            viewModel.ManifestPlaceholder);

        Assert.Equal(
            "Installed packages: 0",
            viewModel.InstalledAppsCountText);
    }

    [Fact]
    public void EmptyCatalog_HasGuidance_AndDisablesOperations()
    {
        var model = new MainWindowViewModel(InstallerUiProfile.Empty);

        Assert.True(model.IsCatalogEmpty);
        Assert.False(model.HasInstalledApps);
        Assert.Equal(InstallerUiLabels.InstalledAppsEmpty, model.InstalledAppsEmptyMessage);
        Assert.Equal(InstallerUiLabels.ActionGuidePackageId, model.ActionGuidanceMessage);
        Assert.True(model.CanRun); // Refresh remains usable with no installed packages.
        Assert.False(model.CanInstall);
        Assert.False(model.CanUpdate);
        Assert.False(model.CanRepair);
        Assert.False(model.CanRollback);
        Assert.False(model.CanUninstall);
        Assert.False(model.CanCheckUpdate);

        model.HasCatalogError = true;
        Assert.False(model.IsCatalogEmpty);
        model.HasCatalogError = false;
        Assert.True(model.IsCatalogEmpty);
    }

    [Fact]
    public void Actions_RequireCorrectSignedSourceAndMatchingInstalledPackage()
    {
        var model = new MainWindowViewModel(InstallerUiProfile.Empty);

        model.PackageIdText = "turkuazlabs.office";
        Assert.Equal(InstallerUiLabels.ActionGuideManifest, model.ActionGuidanceMessage);
        Assert.False(model.CanInstall);

        model.ManifestSource = "C:\\lab\\office-manifest.yml";
        Assert.True(model.CanInstall);
        Assert.True(model.CanCheckUpdate);
        Assert.Equal(InstallerUiLabels.ActionGuideNotInstalled, model.ActionGuidanceMessage);
        Assert.False(model.CanUninstall);

        model.SetInstalledApps(new[]
        {
            new InstalledAppListItemViewModel(
                "turkuazlabs.OFFICE", "0.3.1", "beta", "C:\\lab\\installed")
        });
        Assert.True(model.HasInstalledApps);
        Assert.False(model.IsCatalogEmpty);
        Assert.False(model.CanInstall); // Installed package must not be installed twice.
        Assert.True(model.CanUpdate);
        Assert.True(model.CanRepair);
        Assert.False(model.CanRollback);
        Assert.True(model.CanUninstall);

        model.RollbackManifestSource = "C:\\lab\\rollback.yml";
        Assert.True(model.CanRollback);
        Assert.Equal(InstallerUiLabels.ActionGuideReady, model.ActionGuidanceMessage);

        model.ManifestSource = "  ";
        Assert.False(model.CanUpdate);
        Assert.False(model.CanRepair);
        Assert.False(model.CanRollback);
        Assert.False(model.CanCheckUpdate);
        Assert.True(model.CanUninstall); // Uninstall requires no manifest.

        model.PackageIdText = "some.other.package";
        Assert.True(model.IsCatalogEmpty == false); // Other installed package still exists.
        Assert.False(model.CanUninstall);
        Assert.False(model.CanUpdate);
    }

    [Fact]
    public void ActionAvailability_NotifiesBindings_AndHonorsBusyState()
    {
        var model = new MainWindowViewModel(InstallerUiProfile.Empty);
        var changed = new List<string>();
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
            {
                changed.Add(e.PropertyName);
            }
        };

        model.PackageIdText = "turkuazlabs.office";
        model.ManifestSource = "C:\\lab\\signed-manifest.yml";

        Assert.Contains(nameof(model.CanInstall), changed);
        Assert.Contains(nameof(model.CanCheckUpdate), changed);
        Assert.Contains(nameof(model.ActionGuidanceMessage), changed);

        changed.Clear();
        model.IsBusy = true;
        Assert.False(model.CanRun);
        Assert.False(model.CanInstall);
        Assert.False(model.CanCheckUpdate);
        Assert.Contains(nameof(model.CanInstall), changed);

        model.IsBusy = false;
        Assert.True(model.CanInstall);
        model.IsCheckingUpdate = true;
        Assert.False(model.CanInstall);
        model.IsCheckingUpdate = false;
        model.IsBackgroundUpdateCheckRunning = true;
        Assert.False(model.CanInstall);
        model.IsBackgroundUpdateCheckRunning = false;
        Assert.True(model.CanInstall);
    }

    [Fact]
    public void Dashboard_ResolvesSharedLabels_AndAllowsProductBrandingOverride()
    {
        var model = new MainWindowViewModel(InstallerUiProfile.Empty);
        Assert.Equal(InstallerUiLabels.DashboardEyebrow, model.DashboardEyebrowLabel);
        Assert.Equal(InstallerUiLabels.WorkflowTrust, model.WorkflowTrustLabel);
        Assert.Equal(InstallerUiLabels.SourceAdvanced, model.SourceAdvancedLabel);
        Assert.Equal(InstallerUiLabels.WizardTrustNotice, model.WizardTrustNoticeLabel);

        var branded = new MainWindowViewModel(
            new InstallerUiProfile(
                null,
                InstallerBrandingProfile.Empty,
                new Dictionary<InstallerUiLabelKey, string>
                {
                    [InstallerUiLabelKey.DashboardEyebrow] = "NOVA / SETUP",
                    [InstallerUiLabelKey.WizardReviewTitle] = "Review installation"
                },
                null));
        Assert.Equal("NOVA / SETUP", branded.DashboardEyebrowLabel);
        Assert.Equal("Review installation", branded.WizardReviewTitleLabel);
        Assert.Equal(InstallerUiLabels.WorkflowSource, branded.WorkflowSourceLabel);
    }

    [Fact]
    public void Wizard_RequiresReview_AndValidatesCustomTargetBeforeContinue()
    {
        var model = new MainWindowViewModel(InstallerUiProfile.Empty);
        Assert.False(model.CanContinueInstallWizard);
        model.PackageIdText = "turkuazlabs.lab";
        model.ManifestSource = "C:\\lab\\signed.yml";
        Assert.True(model.CanContinueInstallWizard);
        Assert.Equal(InstallerUiLabels.QuickInstall, model.WizardSelectedModeLabel);
        Assert.Equal(InstallerUiLabels.WizardReviewTargetDefault, model.WizardTargetPreview);

        model.SelectedInstallModeIndex = (int)InstallerInstallMode.Custom;
        Assert.False(model.CanContinueInstallWizard);
        Assert.Equal(InstallerUiLabels.CustomInstall, model.WizardSelectedModeLabel);
        model.TargetPath = "C:\\Users\\Lab\\AppData\\Local\\Programs\\Lab";
        Assert.True(model.CanContinueInstallWizard);
        Assert.Equal(model.TargetPath, model.WizardTargetPreview);
        model.SelectedInstallScopeIndex = (int)InstallerInstallScope.PerMachine;
        Assert.False(model.CanContinueInstallWizard);
        model.SelectedInstallScopeIndex = (int)InstallerInstallScope.PerUser;
        Assert.True(model.CanContinueInstallWizard);
        model.IsBusy = true;
        Assert.False(model.CanContinueInstallWizard);
        model.IsBusy = false;
        Assert.True(model.CanContinueInstallWizard);
        model.ManifestSource = string.Empty;
        Assert.False(model.CanContinueInstallWizard);
    }

    [Fact]
    public void Wizard_ReviewAvailability_RaisesPropertyChanged_OnEveryRelevantChoice()
    {
        var model = new MainWindowViewModel(InstallerUiProfile.Empty);
        var changed = new List<string>();
        model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null) changed.Add(args.PropertyName);
        };
        model.PackageIdText = "turkuazlabs.lab";
        model.ManifestSource = "C:\\lab\\signed.yml";
        Assert.Contains(nameof(model.CanContinueInstallWizard), changed);
        changed.Clear();
        model.SelectedInstallModeIndex = (int)InstallerInstallMode.Custom;
        Assert.Contains(nameof(model.CanContinueInstallWizard), changed);
        Assert.Contains(nameof(model.WizardTargetPreview), changed);
        Assert.Contains(nameof(model.WizardSelectedModeLabel), changed);
        changed.Clear();
        model.TargetPath = "C:\\Users\\Lab\\AppData\\Local\\Programs\\Lab";
        Assert.Contains(nameof(model.CanContinueInstallWizard), changed);
        Assert.Contains(nameof(model.WizardTargetPreview), changed);
        changed.Clear();
        model.SelectedInstallScopeIndex = (int)InstallerInstallScope.PerMachine;
        Assert.Contains(nameof(model.CanContinueInstallWizard), changed);
    }
}
