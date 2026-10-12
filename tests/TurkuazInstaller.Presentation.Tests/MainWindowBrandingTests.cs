// 📄 Dosya Yolu: /tests/TurkuazInstaller.Presentation.Tests/MainWindowBrandingTests.cs
// 📌 Amac: MainWindowViewModel branding/localization override ve fallback davranisini test eder
// 📌 Modul - Test CSharp
// Version: 1.3.0
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
}
