// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/YamlInstallerUiProfileRepositoryTests.cs
// 📌 Amac: YAML branding/localization profil parserinin typed ve fail-closed davranisini test eder
// 📌 Modul - Test CSharp
// Version: 1.1.0
// Aciklama: Missing file fallback, branding/label mapping, culture normalizasyonu, invalid culture ve unknown key reddini dogrular
//
// Bagimli Oldugu Katman: Repo | Tool | Language

using TurkuazInstaller.Contracts.Branding;
using TurkuazInstaller.Infrastructure.Branding;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class YamlInstallerUiProfileRepositoryTests
{
    [Fact]
    public void Read_WhenFileMissing_ReturnsEmptyProfile()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid().ToString("N"),
                "ui-profile.yml");

        var repository =
            new YamlInstallerUiProfileRepository(
                path);

        var profile =
            repository.Read();

        Assert.Null(
            profile.Culture);
        Assert.Null(
            profile.Branding.WindowTitle);
        Assert.Empty(
            profile.Labels);
    }

    [Fact]
    public void Read_MapsBrandingCultureAndLabels()
    {
        var path =
            CreateProfile(
                """
                schema_version: 1
                culture: tr-TR
                branding:
                  window_title: Nova Installer
                  header_title: Nova Setup
                  footer: Nova Community
                labels:
                  install: Yukle
                  update: Yenile
                """);

        try
        {
            var repository =
                new YamlInstallerUiProfileRepository(
                    path);

            var profile =
                repository.Read();

            Assert.Equal(
                "tr-TR",
                profile.Culture);
            Assert.Equal(
                "Nova Installer",
                profile.Branding.WindowTitle);
            Assert.Equal(
                "Nova Setup",
                profile.Branding.HeaderTitle);
            Assert.Equal(
                "Nova Community",
                profile.Branding.Footer);
            Assert.Equal(
                "Yukle",
                profile.Labels[InstallerUiLabelKey.Install]);
            Assert.Equal(
                "Yenile",
                profile.Labels[InstallerUiLabelKey.Update]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_RejectsInvalidCulture()
    {
        var path =
            CreateProfile(
                """
                schema_version: 1
                culture: definitely-not-a-real-culture
                """);

        try
        {
            var repository =
                new YamlInstallerUiProfileRepository(
                    path);

            Assert.Throws<FormatException>(
                repository.Read);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_RejectsUnknownLabelKey()
    {
        var path =
            CreateProfile(
                """
                schema_version: 1
                labels:
                  unsupported_label: value
                """);

        try
        {
            var repository =
                new YamlInstallerUiProfileRepository(
                    path);

            Assert.Throws<FormatException>(
                repository.Read);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateProfile(
        string content)
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                string.Concat(
                    "turkuaz-installer-ui-",
                    Guid.NewGuid().ToString("N"),
                    ".yml"));

        File.WriteAllText(
            path,
            content);

        return path;
    }
}
