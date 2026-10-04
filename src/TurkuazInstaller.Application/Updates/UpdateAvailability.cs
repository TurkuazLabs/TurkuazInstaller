// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Updates/UpdateAvailability.cs
// 📌 Amac: Update sorgusunun temel sonuc durumlarini tanimlar
// 📌 Modul - Service CSharp
// Version: 0.3.0
// Aciklama: Release bulunamadi, current ve available durumlarini magic string olmadan temsil eder
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Application.Updates;

public enum UpdateAvailability
{
    ReleaseNotFound = 0,
    Current = 1,
    Available = 2
}
