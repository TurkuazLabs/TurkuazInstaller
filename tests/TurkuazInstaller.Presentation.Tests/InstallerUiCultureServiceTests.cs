// 📄 Dosya Yolu: /tests/TurkuazInstaller.Presentation.Tests/InstallerUiCultureServiceTests.cs
// 📌 Amac: UI profile culture degerinin Language service tarafindan dogru CultureInfo'ya cozulmesini test eder
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Culture tanimli ve tanimsiz profile davranisini global test process culture'ini degistirmeden dogrular
//
// Bagimli Oldugu Katman: Service | Language | Config

using TurkuazInstaller.Contracts.Branding;
using TurkuazInstaller.Presentation.Language;
using TurkuazInstaller.Presentation.Services;
using Xunit;

namespace TurkuazInstaller.Presentation.Tests;

public sealed class InstallerUiCultureServiceTests
{
    [Fact]
    public void Resolve_WithConfiguredCulture_ReturnsNormalizedCulture()
    {
        var catalog =
            new InstallerUiCatalog(
                new InstallerUiProfile(
                    "en-US",
                    InstallerBrandingProfile.Empty,
                    new Dictionary<InstallerUiLabelKey, string>()));

        var service =
            new InstallerUiCultureService();

        var culture =
            service.Resolve(
                catalog);

        Assert.NotNull(
            culture);

        Assert.Equal(
            "en-US",
            culture.Name);
    }

    [Fact]
    public void Resolve_WithoutConfiguredCulture_ReturnsNull()
    {
        var service =
            new InstallerUiCultureService();

        var culture =
            service.Resolve(
                new InstallerUiCatalog(
                    InstallerUiProfile.Empty));

        Assert.Null(
            culture);
    }
}
