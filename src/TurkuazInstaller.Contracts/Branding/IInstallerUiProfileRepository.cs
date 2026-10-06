// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Branding/IInstallerUiProfileRepository.cs
// 📌 Amac: UI branding/localization profilinin storage detayindan bagimsiz okunmasi icin Repo portunu tanimlar
// 📌 Modul - Repo Contract CSharp
// Version: 1.0.0
// Aciklama: Desktop composition rootun typed UI profilini dosya formatini bilmeden almasini saglar
//
// Bagimli Oldugu Katman: Repo | Service

namespace TurkuazInstaller.Contracts.Branding;

public interface IInstallerUiProfileRepository
{
    InstallerUiProfile Read();
}
