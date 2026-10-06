// 📄 Dosya Yolu: /tests/TurkuazInstaller.Presentation.Tests/MainWindowBrandingTests.cs
// 📌 Amac: MainWindowViewModel branding/localization override ve fallback davranisini test eder
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Typed UI profile ile branding ve label override uygulanirken tanimsiz alanlarin built-in Language metnine dondugunu dogrular
//
// Bagimli Oldugu Katman: View | Language | Config

using TurkuazInstaller.Contracts.Branding;
using TurkuazInstaller.Presentation.Language;
using TurkuazInstaller.Presentation.ViewModels;
using Xunit;

namespace TurkuazInstaller.Presentation.Tests;

public sealed class MainWindowBrandingTests
{
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
                        "Update"
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
    }
}
