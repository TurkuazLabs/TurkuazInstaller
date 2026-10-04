// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/System/ElevationResult.cs
// 📌 Amac: Explicit UAC elevation process sonucunu typed olarak tasir
// 📌 Modul - Port CSharp
// Version: 0.6.0
// Aciklama: UAC status ve varsa child process exit code degerini ortak kontratla dondurur
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Contracts.System;

public sealed record ElevationResult(
    ElevationStatus Status,
    int? ExitCode);
