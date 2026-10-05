// 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/Properties/AssemblyInfo.cs
// 📌 Amac: Bootstrap internal Service ve Tool sinirlarini Windows test assembly'sine gorunur yapar
// 📌 Modul - Config CSharp
// Version: 1.0.0
// Aciklama: Production public API yuzeyini genisletmeden bootstrap orchestration unit testlerini destekler
//
// Bagimli Oldugu Katman: Config | Tool

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(
    "TurkuazInstaller.Bootstrapper.Tests")]
