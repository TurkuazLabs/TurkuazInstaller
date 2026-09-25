// 📄 Dosya Yolu: /src/TurkuazInstaller.UI/App.xaml.cs
// 📌 Amac: WinUI composition root ve ana pencere yasam dongusunu yonetir
// 📌 Modul - View CSharp
// Version: 0.1.0
// Aciklama: Infrastructure adapterlarini ViewModel ile uygulama girisinde birlestirir
//
// Bagimli Oldugu Katman: View

using Microsoft.UI.Xaml;
using TurkuazInstaller.Infrastructure.Localization;
using TurkuazInstaller.UI.ViewModels;

namespace TurkuazInstaller.UI;

public partial class App : Application
{
    private const string LanguageDirectory = "language";
    private const string DefaultLanguageFile = "tr.yml";

    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var languagePath = Path.Combine(
            AppContext.BaseDirectory,
            LanguageDirectory,
            DefaultLanguageFile);

        var textCatalog = new YamlTextCatalog(languagePath);
        var viewModel = new MainViewModel(textCatalog);

        _window = new MainWindow(viewModel);
        _window.Activate();
    }
}
