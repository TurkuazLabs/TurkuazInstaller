// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Updates/UpdateAvailability.cs
// 📌 Amac: Update sorgusunun temel sonuc durumlarini tanimlar
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Release bulunamadi/current/available yaninda exact skip ve maximum-version pin policy bloklarini temsil eder
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Application.Updates;

public enum UpdateAvailability
{
    ReleaseNotFound = 0,
    Current = 1,
    Available = 2,
    Skipped = 3,
    Pinned = 4
}
