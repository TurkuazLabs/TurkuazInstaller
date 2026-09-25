// 📄 Dosya Yolu: /src/TurkuazInstaller.UI/MainWindow.xaml.cs
// 📌 Amac: MainWindow ile MainViewModel arasindaki yalnizca UI baglantisini kurar
// 📌 Modul - View CSharp
// Version: 0.1.0
// Aciklama: Is kurali icermeyen WinUI code-behind sinifidir
//
// Bagimli Oldugu Katman: View

using Microsoft.UI.Xaml;
using TurkuazInstaller.UI.ViewModels;

namespace TurkuazInstaller.UI;

public sealed partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public MainViewModel ViewModel { get; }
}
