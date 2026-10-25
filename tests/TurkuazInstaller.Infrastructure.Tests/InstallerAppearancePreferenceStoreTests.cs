// Dosya Yolu: tests/TurkuazInstaller.Infrastructure.Tests/InstallerAppearancePreferenceStoreTests.cs
// Amac: Sistem/Acik/Koyu tercihinin guvenli ve kalici saklanmasini test etmek
// Modul - CSharp xUnit Tests
// Version: 0.1.0-qa
// Aciklama: Corrupt preference uygulama acilisini bloklamaz, imza politikasi degismez
// Bagimli Oldugu Katman: Infrastructure | Appearance

using TurkuazInstaller.Infrastructure.Branding;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class InstallerAppearancePreferenceStoreTests
{
    [Fact]
    public void MissingPreferenceDefaultsToSystem()
    {
        var path = Path.Combine(Path.GetTempPath(),
            Guid.NewGuid().ToString("N"), "appearance.json");
        Assert.Equal(InstallerAppearanceMode.System,
            new InstallerAppearancePreferenceStore(path).Read());
    }

    [Theory]
    [InlineData(InstallerAppearanceMode.System, "system")]
    [InlineData(InstallerAppearanceMode.Light, "light")]
    [InlineData(InstallerAppearanceMode.Dark, "dark")]
    public void AllModesSurviveProcessRestartWithoutPartialFile(
        InstallerAppearanceMode mode, string persisted)
    {
        var directory = Path.Combine(Path.GetTempPath(), "appearance-tests",
            Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "appearance.json");
        try
        {
            new InstallerAppearancePreferenceStore(path).Save(mode);
            Assert.Equal(mode, new InstallerAppearancePreferenceStore(path).Read());
            var json = File.ReadAllText(path);
            Assert.Contains("\"mode\":\"" + persisted + "\"", json);
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void SwitchingAppearanceOverwritesPreviousPreference()
    {
        var directory = Path.Combine(Path.GetTempPath(), "appearance-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "appearance.json");
        try
        {
            var prefs = new InstallerAppearancePreferenceStore(path);
            prefs.Save(InstallerAppearanceMode.Dark);
            prefs.Save(InstallerAppearanceMode.Light);
            Assert.Equal(InstallerAppearanceMode.Light,
                new InstallerAppearancePreferenceStore(path).Read());
            prefs.Save(InstallerAppearanceMode.System);
            Assert.Equal(InstallerAppearanceMode.System, prefs.Read());
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"schema_version\":2,\"mode\":\"dark\"}")]
    [InlineData("{\"schema_version\":1,\"mode\":\"DARK\"}")]
    [InlineData("{\"schema_version\":1,\"mode\":\"dark\",\"trusted_signer\":\"evil\"}")]
    [InlineData("{\"schema_version\":1,\"mode\":\"dark\",\"mode\":\"light\"}")]
    [InlineData("[]")]
    public void CorruptOrUnrecognizedPreferenceCannotBlockApp(string payload)
    {
        var directory = Path.Combine(Path.GetTempPath(), "appearance-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "appearance.json");
        try
        {
            File.WriteAllText(path, payload);
            Assert.Equal(InstallerAppearanceMode.System,
                new InstallerAppearancePreferenceStore(path).Read());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void OversizedPreferenceDefaultsToSystem()
    {
        var directory = Path.Combine(Path.GetTempPath(), "appearance-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "appearance.json");
        try
        {
            File.WriteAllText(path, new string(' ', 5000));
            Assert.Equal(InstallerAppearanceMode.System,
                new InstallerAppearancePreferenceStore(path).Read());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void UnexpectedValueIsNeverPersisted()
    {
        var directory = Path.Combine(Path.GetTempPath(), "appearance-tests",
            Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "appearance.json");
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new InstallerAppearancePreferenceStore(path).Save(
                (InstallerAppearanceMode)100));
        Assert.False(File.Exists(path));
    }
}
