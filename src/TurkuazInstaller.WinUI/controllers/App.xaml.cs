// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/controllers/App.xaml.cs
// 📌 Amac: WinUI launch requestini alir ve desktop composition root tarafindan uretilen ana pencereyi acar
// 📌 Modul - Controller CSharp
// Version: 1.2.0
// Aciklama: XAML initialize eder, process startup argumanlarini request olarak alir ve composition/View activation akisini baslatir
//
// Bagimli Oldugu Katman: Controller | Config | View

using Microsoft.UI.Xaml;
using TurkuazInstaller.WinUI.Config;
using TurkuazInstaller.WinUI.Tools;

namespace TurkuazInstaller.WinUI;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;

    public App()
    {
        WinUiStartupDiagnostics.Record("AppConstructorEntered");
        UnhandledException += (_, args) =>
            WinUiStartupDiagnostics.Record("XamlUnhandledException", args.Exception);
        InitializeComponent();
        WinUiStartupDiagnostics.Record("AppXamlInitialized");
    }

    protected override void OnLaunched(
        LaunchActivatedEventArgs args)
    {
        WinUiStartupDiagnostics.Record("OnLaunchedEntered");
        try
        {
            var commandLineArguments =
                Environment.GetCommandLineArgs();

            IReadOnlyList<string> startupArguments =
                commandLineArguments.Length <= 1
                    ? Array.Empty<string>()
                    : commandLineArguments[1..];

            WinUiStartupDiagnostics.Record("CompositionStarting");
            _window = DesktopCompositionRoot
                .CreateMainWindow(
                    startupArguments);
            WinUiStartupDiagnostics.Record("MainWindowCreated");
            _window.Activate();
            WinUiStartupDiagnostics.Record("MainWindowActivated");
        }
        catch (Exception error)
        {
            WinUiStartupDiagnostics.Record("OnLaunchedFailed", error);
            throw;
        }
    }
}
