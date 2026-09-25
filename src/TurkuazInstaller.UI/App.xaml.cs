// 📄 Dosya Yolu: /src/TurkuazInstaller.UI/App.xaml.cs
// 📌 Amac: WinUI composition root ve ana pencere yasam dongusunu yonetir
// 📌 Modul - View CSharp
// Version: 0.1.1
// Aciklama: Runtime config, language adapteri ve ViewModel baglantisini uygulama girisinde kurar
//
// Bagimli Oldugu Katman: View

using Microsoft.UI.Xaml;
using TurkuazInstaller.Infrastructure.Configuration;
using TurkuazInstaller.Infrastructure.Localization;
using TurkuazInstaller.UI.ViewModels;

namespace TurkuazInstaller.UI;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var configPath = Path.Combine(
            AppContext.BaseDirectory,
            InstallerFileLayout.ConfigDirectory,
            InstallerFileLayout.InstallerConfigurationFile);

        var configLoader = new YamlInstallerUiConfigurationLoader();
        var configuration = configLoader.Load(configPath);

        var languageFileName = string.Concat(
            configuration.DefaultLanguage,
            InstallerFileLayout.LanguageFileExtension);

        var languagePath = Path.Combine(
            AppContext.BaseDirectory,
            configuration.LanguageDirectory,
            languageFileName);

        var textCatalog = new YamlTextCatalog(languagePath);
        var viewModel = new MainViewModel(textCatalog);

        _window = new MainWindow(viewModel);
        _window.Activate();
    }
}
