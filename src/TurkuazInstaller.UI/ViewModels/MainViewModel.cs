// 📄 Dosya Yolu: /src/TurkuazInstaller.UI/ViewModels/MainViewModel.cs
// 📌 Amac: Foundation UI ekraninin bind edilebilir metin durumunu yonetir
// 📌 Modul - ViewModel CSharp
// Version: 0.1.0
// Aciklama: Metinleri Language portundan alir ve is mantigi tasimaz
//
// Bagimli Oldugu Katman: View

using CommunityToolkit.Mvvm.ComponentModel;
using TurkuazInstaller.Contracts.Localization;

namespace TurkuazInstaller.UI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private static class Keys
    {
        public const string Title = "app_title";
        public const string Subtitle = "foundation_subtitle";
        public const string StatusTitle = "foundation_status_title";
        public const string StatusMessage = "foundation_status_message";
    }

    public MainViewModel(ITextCatalog textCatalog)
    {
        Title = textCatalog.Get(Keys.Title);
        Subtitle = textCatalog.Get(Keys.Subtitle);
        StatusTitle = textCatalog.Get(Keys.StatusTitle);
        StatusMessage = textCatalog.Get(Keys.StatusMessage);
    }

    public string Title { get; }

    public string Subtitle { get; }

    public string StatusTitle { get; }

    public string StatusMessage { get; }
}
