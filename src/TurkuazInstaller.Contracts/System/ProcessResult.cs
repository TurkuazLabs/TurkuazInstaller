// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/System/ProcessResult.cs
// 📌 Amac: Tool process calistirma sonucunu typed olarak Application ve adapter katmanlarina tasir
// 📌 Modul - Port CSharp
// Version: 0.5.0
// Aciklama: Exit code, stdout ve stderr degerlerini ortak process result modeliyle temsil eder
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Contracts.System;

public sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
