// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/controllers/App.xaml.cs
// 📌 Amac: WinUI launch requestini alir ve desktop composition root tarafindan uretilen ana pencereyi acar
// 📌 Modul - Controller CSharp
// Version: 0.7.2
// Aciklama: Application namespace cakismasini onleyerek XAML initialize, composition ve View activation akisini baslatir
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
        _window = DesktopCompositionRoot
            .CreateMainWindow();

        _window.Activate();
    }
}
