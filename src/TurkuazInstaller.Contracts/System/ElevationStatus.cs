// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/System/ElevationStatus.cs
// 📌 Amac: Explicit UAC elevation sonucunu typed durum olarak tanimlar
// 📌 Modul - Port CSharp
// Version: 0.6.0
// Aciklama: Basarili elevated process ve kullanici tarafindan iptal edilen UAC durumlarini ayirir
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Contracts.System;

public enum ElevationStatus
{
    Completed = 0,
    Cancelled = 1
}
