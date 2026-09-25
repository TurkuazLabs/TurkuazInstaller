// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Updates/UpdateAvailability.cs
// 📌 Amac: Update kontrol sonucunun durumunu type-safe olarak temsil eder
// 📌 Modul - Application CSharp
// Version: 0.1.0
// Aciklama: UI ve CLI tarafinin string durum kodu kullanmasini engeller
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Application.Updates;

public enum UpdateAvailability
{
    Unknown = 0,
    Current = 1,
    Available = 2,
    NotInstalled = 3
}
