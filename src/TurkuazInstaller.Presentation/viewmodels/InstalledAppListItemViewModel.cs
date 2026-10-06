// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/viewmodels/InstalledAppListItemViewModel.cs
// 📌 Amac: Kurulu uygulama katalog satirini WinUI framework bagimsiz bind edilebilir model olarak tasir
// 📌 Modul - ViewModel CSharp
// Version: 1.0.0
// Aciklama: Package id, version, channel ve target path bilgisini salt-okunur katalog satiri olarak tasir
//
// Bagimli Oldugu Katman: View | Language

namespace TurkuazInstaller.Presentation.ViewModels;

public sealed record InstalledAppListItemViewModel(
    string PackageId,
    string Version,
    string Channel,
    string TargetPath);
