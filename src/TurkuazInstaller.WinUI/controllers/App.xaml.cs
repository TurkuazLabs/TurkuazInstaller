// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/controllers/App.xaml.cs
// 📌 Amac: WinUI launch requestini alir ve desktop composition root tarafindan uretilen ana pencereyi acar
// 📌 Modul - Controller CSharp
// Version: 1.1.0
// Aciklama: XAML initialize eder, process startup argumanlarini request olarak alir ve composition/View activation akisini baslatir
//
// Bagimli Oldugu Katman: Controller | Config | View

using Microsoft.UI.Xaml;
using TurkuazInstaller.WinUI.Config;

namespace TurkuazInstaller.WinUI;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(
        LaunchActivatedEventArgs args)
    {
        var commandLineArguments =
            Environment.GetCommandLineArgs();

        IReadOnlyList<string> startupArguments =
            commandLineArguments.Length <= 1
                ? Array.Empty<string>()
                : commandLineArguments[1..];

        _window = DesktopCompositionRoot
            .CreateMainWindow(
                startupArguments);

        _window.Activate();
    }
}
